using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit;
using GameDeveloperKit.Media;
using GameDeveloperKit.Resource;
using RenderHeads.Media.AVProVideo;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Playable
{
    public sealed class VideoPlayableHandle : PlayableHandle
    {
        private GameObject m_GameObject;
        private MediaPlayer m_Player;
        private AvProVideoPlayerInstance m_PlayerInstance;
        private readonly UniTaskCompletionSource m_Ready = new UniTaskCompletionSource();
        private bool m_Preloading;
        private bool m_FirstFrame;
        private bool m_Terminated;
        private bool m_Loop;
        private Transform m_Parent;
        private bool m_DontDestroyOnLoad;
        private bool m_SupportsAutoQuality;
        private VideoQualitySelection m_Quality;
        private IReadOnlyList<VideoQualityOption> m_QualityOptions = Array.Empty<VideoQualityOption>();
        private CancellationTokenSource m_QualityCancellation;
        private ResolveToRenderTexture m_TextureResolver;
        private RenderTexture m_StableOutputTexture;
        private bool m_HlsStartupLimitApplied;
        private CancellationTokenSource m_PreloadTimeoutCancellation;
        private int m_PreloadTargetHeight;
        private bool m_PreloadReady;
        private CancellationTokenSource m_HlsStartupUpgradeCancellation;
        private bool m_HlsStartupUpgradePending;
        private RawImage m_Surface;
        private string m_PreviewPath;
        private VideoDisplayMode m_DisplayMode = VideoDisplayMode.FitVertically;
        private Texture2D m_PreviewTexture;
        private bool m_PreviewDestroyTexture;
        private AssetHandle m_PreviewAssetHandle;
        private bool m_PreviewLoadStarted;
        private CancellationTokenSource m_PreviewCancellation;

        internal const float HlsStartupPeakBitRateMbps = 0.45f;
        internal static readonly Vector2Int HlsStartupMaximumResolution = new Vector2Int(426, 240);
        internal const double HlsPreloadTargetTimeoutSeconds = 12d;
        private const double QualitySwitchMaximumDriftSeconds = 0.1d;
        private const double QualitySwitchAlignmentTimeoutSeconds = 3d;

        internal VideoPlayableHandle(string path, VideoPlayableOptions options, bool preloading)
        {
            Path = path;
            RequestPath = path;
            m_Preloading = preloading;
            m_PlayerInstance = new AvProVideoPlayerInstance(
                preloading ? "VideoPlayablePreload" : "VideoPlayable",
                options?.Parent,
                options?.DontDestroyOnLoad != false,
                false);
            m_GameObject = m_PlayerInstance.GameObject;
            m_Player = m_PlayerInstance.Player;
            m_Player.AutoOpen = false;
            m_Player.AutoStart = true;
            m_Player.PlatformOptionsAndroid.allowUnsupportedVideoTrackVariants = true;
            m_Player.Events.AddListener(OnMediaEvent);
            ApplyOptions(options);
            PrepareNativeHlsOutput();
        }

        public string Path { get; private set; }

        /// <summary>
        /// The stable request address used to identify this playback across quality switches.
        /// </summary>
        public string RequestPath { get; }

        /// <summary>
        /// 绑定输出 surface（由 Playable 层在 PlayAsync 时调用）：Playable 统一管理
        /// 首帧前黑色/预览图显示、首帧后纹理绑定与颜色，调用方无需干预。
        /// </summary>
        internal void AttachSurface(RawImage output)
        {
            m_Surface = output;
            if (output == null)
            {
                return;
            }

            if (m_FirstFrame)
            {
                // 首帧已就绪（如预热复用）：立即绑定视频纹理。
                BindVideoSurface();
                return;
            }

            if (m_PreviewTexture != null)
            {
                // 预览图已在 Play 阶段加载完成：直接显示。
                ApplyPreviewToSurface();
                Debug.Log($"[VideoPlayableHandle] Preview shown on surface attach. path:{m_PreviewPath}");
            }
            else if (output.texture == null)
            {
                // 无预览图且 surface 无内容：首帧前保持黑色，避免白色 RawImage 泛白。
                output.color = Color.black;
            }

            // 预览图未就绪时继续等待异步加载结果（幂等）。
            StartPreviewLoadIfNeeded();
        }

        /// <summary>
        /// 首帧后绑定视频纹理到 surface（纹理变化/切档后由事件重新触发）。
        /// </summary>
        private void BindVideoSurface()
        {
            if (m_Surface == null || m_FirstFrame is false)
            {
                return;
            }

            VideoSurfaceBinder.Bind(m_Surface, Texture, RequiresVerticalFlip, m_DisplayMode);
        }

        /// <summary>
        /// 提前发起预览图加载（仅在冷启动播放时；预热复用场景首帧已就绪，无需预览图）。
        /// 与视频缓冲并行，首帧前显示的概率不受 SetSurface 时机影响。
        /// </summary>
        private void StartPreviewLoadIfNeeded()
        {
            if (m_PreviewLoadStarted ||
                m_Terminated ||
                m_FirstFrame ||
                string.IsNullOrWhiteSpace(m_PreviewPath))
            {
                return;
            }

            m_PreviewLoadStarted = true;
            LoadPreviewAsync().Forget(Debug.LogWarning);
        }

        private async UniTask LoadPreviewAsync()
        {
            try
            {
                var previewPath = m_PreviewPath;
                if (previewPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                    previewPath.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
                {
                    await LoadResourcePreviewAsync(previewPath);
                    return;
                }

                string url;
                if (previewPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    previewPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = previewPath;
                }
                else
                {
                    var settings = App.Config?.MediaDelivery;
                    url = settings != null
                        ? MediaUrlResolver.Resolve(new global::GameDeveloperKit.Media.MediaPath(previewPath), settings)
                        : "https://moviegame.wwhy.games/" + previewPath;
                }

                m_PreviewCancellation?.Cancel();
                m_PreviewCancellation = new CancellationTokenSource();
                var token = m_PreviewCancellation.Token;
                using var request = UnityWebRequestTexture.GetTexture(url);
                await request.SendWebRequest().WithCancellation(token);
                if (token.IsCancellationRequested || request.result != UnityWebRequest.Result.Success)
                {
                    // 预览图加载失败：保持黑色，等首帧后由 Bind 置白。
                    return;
                }

                var texture = DownloadHandlerTexture.GetContent(request);
                OnPreviewTextureReady(texture, bundleHandle: null, destroyOnRelease: true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[VideoPlayableHandle] Preview load failed: {exception.Message}");
            }
        }

        /// <summary>
        /// 从 bundle 或 Resources 加载预览图（Assets/ 或 Resources/ 路径）：句柄由 ReleasePreview 统一卸载。
        /// </summary>
        private async UniTask LoadResourcePreviewAsync(string previewPath)
        {
            if (previewPath.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
            {
                // Resources 内置资源：同步直读（不依赖 Resource 模块初始化，启动早期即可用）。
                // 纹理为共享资源，释放时仅断开引用，不销毁。
                var resourcesPath = previewPath.Substring("Resources/".Length);
                var dot = resourcesPath.LastIndexOf('.');
                var slash = resourcesPath.LastIndexOf('/');
                if (dot > slash)
                {
                    resourcesPath = resourcesPath.Substring(0, dot);
                }

                var texture = Resources.Load<Texture2D>(resourcesPath);
                if (texture == null)
                {
                    Debug.LogWarning($"[VideoPlayableHandle] Preview asset not found in Resources: {previewPath}");
                    return;
                }

                OnPreviewTextureReady(texture, bundleHandle: null, destroyOnRelease: false);
                return;
            }

            var handle = await App.Resource.LoadAssetAsync(previewPath);
            if (handle == null || handle.Status != ResourceStatus.Succeeded)
            {
                App.Resource.UnloadAsset(handle).Forget(Debug.LogException);
                Debug.LogWarning($"[VideoPlayableHandle] Preview asset load failed: {previewPath}");
                return;
            }

            var sprite = handle.GetAsset<Sprite>();
            var texture2d = sprite != null ? sprite.texture : handle.GetAsset<Texture2D>();
            if (texture2d == null)
            {
                App.Resource.UnloadAsset(handle).Forget(Debug.LogException);
                Debug.LogWarning($"[VideoPlayableHandle] Preview asset is not a texture: {previewPath}");
                return;
            }

            OnPreviewTextureReady(texture2d, handle, destroyOnRelease: false);
        }

        private void OnPreviewTextureReady(Texture2D texture, AssetHandle bundleHandle, bool destroyOnRelease)
        {
            if (m_Terminated || m_FirstFrame)
            {
                // 首帧已就绪或已终止：预览图不再需要，释放资源。
                if (bundleHandle != null)
                {
                    App.Resource.UnloadAsset(bundleHandle).Forget(Debug.LogException);
                }
                else if (destroyOnRelease)
                {
                    Object.Destroy(texture);
                }

                if (m_FirstFrame)
                {
                    Debug.Log("[VideoPlayableHandle] Preview ready after first frame; skipped.");
                }

                return;
            }

            m_PreviewAssetHandle = bundleHandle;
            m_PreviewDestroyTexture = destroyOnRelease;
            m_PreviewTexture = texture;
            if (m_Surface != null)
            {
                ApplyPreviewToSurface();
                Debug.Log($"[VideoPlayableHandle] Preview shown. path:{m_PreviewPath}");
            }
            else
            {
                // surface 尚未绑定（PlayAsync 返回前加载完成）：保留纹理，由 AttachSurface 显示。
                Debug.Log($"[VideoPlayableHandle] Preview loaded, awaiting surface. path:{m_PreviewPath}");
            }
        }

        private void ApplyPreviewToSurface()
        {
            if (m_Surface == null || m_PreviewTexture == null || m_FirstFrame)
            {
                return;
            }

            m_Surface.texture = m_PreviewTexture;
            m_Surface.color = Color.white;
            m_Surface.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        private void ReleasePreview()
        {
            m_PreviewCancellation?.Cancel();
            m_PreviewCancellation?.Dispose();
            m_PreviewCancellation = null;
            if (m_PreviewTexture != null)
            {
                if (m_PreviewDestroyTexture)
                {
                    Object.Destroy(m_PreviewTexture);
                }

                m_PreviewTexture = null;
            }

            m_PreviewDestroyTexture = false;
            if (m_PreviewAssetHandle != null)
            {
                var handle = m_PreviewAssetHandle;
                m_PreviewAssetHandle = null;
                App.Resource.UnloadAsset(handle).Forget(Debug.LogException);
            }
        }

        public Texture Texture => m_StableOutputTexture != null
            ? m_StableOutputTexture
            : m_Player?.TextureProducer?.GetTexture(0);

        public bool HasFirstFrame => m_FirstFrame && Texture != null;

        public bool RequiresVerticalFlip => m_StableOutputTexture == null &&
                                            (m_Player?.TextureProducer?.RequiresVerticalFlip() ?? false);

        public bool SeekRequested => Seekable;

        public bool CanSeek => SeekRequested && IsValidDuration(DurationSeconds) && m_Player?.Control?.CanPlay() == true;

        public bool CanPause => Status is PlayableStatus.Playing or PlayableStatus.Paused;

        public bool IsPaused => Status == PlayableStatus.Paused;

        public bool Seekable { get; private set; }

        public bool CanSelectQuality =>
            (m_SupportsAutoQuality is false || SupportsNativeHlsVariantSelection) &&
            GetDistinctHeightCount(m_QualityOptions) + (m_SupportsAutoQuality ? 1 : 0) >= 2;

        public bool SupportsAutoQuality => m_SupportsAutoQuality;

        public VideoQualitySelection Quality => m_Quality;

        public IReadOnlyList<VideoQualityOption> QualityOptions => m_QualityOptions;

        public double DurationSeconds => m_Player?.Info?.GetDuration() ?? 0d;

        public double CurrentTimeSeconds => m_Player?.Control?.GetCurrentTime() ?? 0d;

        /// <summary>
        /// 当前播放倍速。
        /// </summary>
        public float PlaybackRate => m_Player?.PlaybackRate ?? 1f;

        public event Action<VideoPlayableHandle> FirstFrameReady;

        public event Action<VideoPlayableHandle> TextureChanged;

        internal event Action<VideoPlayableHandle> Terminated;

        internal void ApplyOptions(VideoPlayableOptions options)
        {
            options ??= new VideoPlayableOptions();
            var autoPath = string.IsNullOrWhiteSpace(m_AutoPath) ? Path : m_AutoPath;
            Seekable = options.Seekable;
            m_Loop = options.Loop;
            m_Parent = options.Parent;
            m_DontDestroyOnLoad = options.DontDestroyOnLoad;
            m_SupportsAutoQuality = options.SupportsAutoQuality;
            m_QualityOptions = CopyQualityOptions(options.QualityOptions);
            m_PreloadTargetHeight = ResolvePreloadTargetHeight(options.PreloadTargetHeight);
            m_PreviewPath = options.PreviewPath;
            m_DisplayMode = options.DisplayMode;
            m_AutoPath = autoPath;
            m_Quality = ResolveInitialQuality(options.InitialQuality);
            Path = m_Preloading
                ? ResolvePreloadPath() ?? ResolveInitialPath(m_Quality)
                : ResolveInitialPath(m_Quality);
            if (m_Player != null)
            {
                m_Player.Loop = m_Loop;
                ApplyPreloadAndroidTarget();
            }
        }

        public UniTask SetQualityAsync(
            VideoQualitySelection selection,
            CancellationToken cancellationToken = default)
        {
            if (CanSelectQuality is false)
            {
                throw new GameException($"Video quality cannot be selected: {Path}");
            }

            var path = ResolveQualityPath(selection);
            if (selection.Equals(m_Quality))
            {
                return UniTask.CompletedTask;
            }

            cancellationToken.ThrowIfCancellationRequested();
            CancelHlsStartupUpgrade();
            CancelQualitySwitch();
            if (TrySelectNativeQuality(selection))
            {
                return UniTask.CompletedTask;
            }

            if (m_SupportsAutoQuality)
            {
                var quality = selection.Mode == VideoQualityMode.Auto
                    ? "auto"
                    : $"{selection.Height}p";
                throw new GameException(
                    $"AVPro HLS variant is unavailable: {quality} path:{Path} variants:{DescribeNativeVariants()}");
            }

            m_QualityCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return SwitchQualityAsync(selection, path, m_QualityCancellation.Token);
        }

        internal void Preload()
        {
            m_Player.AudioMuted = true;
            Open(false, false);
            StartPreloadTimeout();
        }

        internal void Play()
        {
            EnsureStableNativeHlsOutput();
            if (Status == PlayableStatus.Preparing)
            {
                SetPlaying();
            }

            m_Player.AudioMuted = false;
            if (m_Preloading)
            {
                // 预热流即为目标清晰度，直接播放，不再升级换流。
                m_Preloading = false;
                CancelPreloadTimeout();
                m_Ready.TrySetResult();
                ReleaseHlsStartupLimit();
                m_Player.Play();
            }
            else
            {
                // 与视频缓冲并行发起预览图加载，确保首帧前能显示。
                StartPreviewLoadIfNeeded();
                var fastStartupPath = ResolveWindowsHlsFastStartupPath();
                if (string.IsNullOrWhiteSpace(fastStartupPath))
                {
                    Open(true, true);
                }
                else
                {
                    Path = fastStartupPath;
                    m_HlsStartupUpgradePending = true;
                    Open(true, false);
                }
            }
        }

        internal UniTask WaitUntilReadyAsync(CancellationToken cancellationToken)
        {
            return m_Ready.Task.AttachExternalCancellation(cancellationToken);
        }

        public void Seek(double timeSeconds)
        {
            if (!CanSeek)
            {
                throw new GameException($"Video cannot seek: {Path}");
            }

            m_Player.Control.Seek(Math.Max(0d, Math.Min(timeSeconds, DurationSeconds)));
        }

        /// <summary>
        /// 设置播放倍速。
        /// </summary>
        public void SetPlaybackRate(float rate)
        {
            if (float.IsNaN(rate) || float.IsInfinity(rate) || rate <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(rate));
            }

            if (m_Player == null)
            {
                throw new GameException($"Video player is unavailable: {Path}");
            }

            m_Player.PlaybackRate = rate;
        }

        protected override void OnPause()
        {
            m_Player.Pause();
        }

        protected override void OnResume()
        {
            m_Player.Play();
        }

        protected override void OnStop()
        {
            Terminate();
        }

        protected override void OnDispose()
        {
            m_Ready.TrySetCanceled();
            CancelPreloadTimeout();
            CancelHlsStartupUpgrade();
            CancelQualitySwitch();
            try
            {
                if (m_Player != null)
                {
                    m_Player.Events.RemoveListener(OnMediaEvent);
                }
            }
            finally
            {
                m_TextureResolver = null;
                m_StableOutputTexture = null;
                m_Player = null;
                m_GameObject = null;
                var instance = m_PlayerInstance;
                m_PlayerInstance = null;
                instance?.Dispose();
            }
        }

        private async UniTask SwitchQualityAsync(
            VideoQualitySelection selection,
            string path,
            CancellationToken cancellationToken)
        {
            var candidateInstance = new AvProVideoPlayerInstance(
                "VideoPlayableQualityCandidate",
                m_Parent,
                m_DontDestroyOnLoad,
                false);
            var candidateObject = candidateInstance.GameObject;
            var candidate = candidateInstance.Player;
            candidate.AutoOpen = false;
            candidate.AutoStart = true;
            candidate.Loop = m_Loop;
            candidate.AudioMuted = true;
            var ready = new UniTaskCompletionSource();

            void OnCandidateEvent(MediaPlayer player, MediaPlayerEvent.EventType eventType, ErrorCode errorCode)
            {
                if (eventType == MediaPlayerEvent.EventType.ReadyToPlay)
                {
                    // The current player keeps running while the replacement loads. Reading its
                    // time here prevents a slow quality switch from jumping back to the timestamp
                    // captured when the request was first issued.
                    var resumeTime = CurrentTimeSeconds;
                    if (resumeTime > 0d &&
                        double.IsNaN(resumeTime) is false &&
                        double.IsInfinity(resumeTime) is false)
                    {
                        var duration = player.Info?.GetDuration() ?? 0d;
                        player.Control.Seek(IsValidDuration(duration)
                            ? Math.Min(resumeTime, duration)
                            : resumeTime);
                    }

                    player.Play();
                }
                else if (eventType == MediaPlayerEvent.EventType.FirstFrameReady)
                {
                    ready.TrySetResult();
                }
                else if (eventType == MediaPlayerEvent.EventType.Error)
                {
                    ready.TrySetException(new GameException($"AVPro quality switch failed. path:{path} error:{errorCode}"));
                }
            }

            candidate.Events.AddListener(OnCandidateEvent);
            try
            {
                if (candidate.OpenMedia(MediaPathType.AbsolutePathOrURL, path, false) is false)
                {
                    throw new GameException($"AVPro cannot open video quality: {path}");
                }

                await ready.Task.AttachExternalCancellation(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var oldPlayer = m_Player;
                var oldInstance = m_PlayerInstance;
                var wasPaused = await AlignCandidateToCurrentPlaybackAsync(
                    oldPlayer,
                    candidate,
                    cancellationToken);
                candidate.Events.RemoveListener(OnCandidateEvent);
                candidate.Events.AddListener(OnMediaEvent);
                candidate.AudioMuted = false;
                if (wasPaused)
                {
                    candidate.Pause();
                }
                else
                {
                    candidate.Play();
                }

                m_GameObject = candidateObject;
                m_Player = candidate;
                m_PlayerInstance = candidateInstance;
                Path = path;
                m_Quality = selection;
                m_FirstFrame = true;
                m_TextureResolver = null;
                m_StableOutputTexture = null;
                oldPlayer.Events.RemoveListener(OnMediaEvent);
                oldInstance.Dispose();
                // 切档完成：绑定新纹理到 surface。
                BindVideoSurface();
                FirstFrameReady?.Invoke(this);
                candidateInstance = null;
            }
            finally
            {
                if (candidateInstance != null)
                {
                    candidate.Events.RemoveListener(OnCandidateEvent);
                    candidateInstance.Dispose();
                }

                if (m_QualityCancellation != null && m_QualityCancellation.Token == cancellationToken)
                {
                    m_QualityCancellation.Dispose();
                    m_QualityCancellation = null;
                }
            }
        }

        private async UniTask<bool> AlignCandidateToCurrentPlaybackAsync(
            MediaPlayer source,
            MediaPlayer candidate,
            CancellationToken cancellationToken)
        {
            var sourceControl = source?.Control;
            var candidateControl = candidate?.Control;
            var sourceWasPaused = IsPaused;
            if (sourceControl == null || candidateControl == null)
            {
                return sourceWasPaused;
            }

            var sourceTime = sourceControl.GetCurrentTime();
            if (RequiresFinalQualityAlignment(sourceTime) is false)
            {
                return sourceWasPaused;
            }

            if (sourceWasPaused is false)
            {
                source.Pause();
            }

            candidate.Pause();
            var alignmentCommitted = false;
            try
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                sourceTime = sourceControl.GetCurrentTime();
                var duration = candidate.Info?.GetDuration() ?? 0d;
                var targetTime = IsValidDuration(duration)
                    ? Math.Min(sourceTime, duration)
                    : sourceTime;
                var textureProducer = candidate.TextureProducer;
                var supportsFrameCount = textureProducer?.SupportsTextureFrameCount() == true;
                var frameCountBeforeSeek = supportsFrameCount
                    ? textureProducer.GetTextureFrameCount()
                    : 0;

                candidateControl.Seek(targetTime);
                var timeoutAt = Time.realtimeSinceStartupAsDouble + QualitySwitchAlignmentTimeoutSeconds;
                while (Time.realtimeSinceStartupAsDouble < timeoutAt)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var currentTime = candidateControl.GetCurrentTime();
                    var reachedTarget = currentTime >= targetTime - QualitySwitchMaximumDriftSeconds;
                    var freshFrameReady = supportsFrameCount is false ||
                                          textureProducer.GetTextureFrameCount() > frameCountBeforeSeek;
                    if (candidateControl.IsSeeking() is false && reachedTarget && freshFrameReady)
                    {
                        alignmentCommitted = true;
                        return sourceWasPaused;
                    }

                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
                }

                throw new GameException(
                    $"AVPro quality switch alignment timed out. " +
                    $"source:{sourceTime:F3}s candidate:{candidateControl.GetCurrentTime():F3}s");
            }
            finally
            {
                if (alignmentCommitted is false &&
                    sourceWasPaused is false &&
                    m_Terminated is false &&
                    ReferenceEquals(m_Player, source) &&
                    source != null)
                {
                    source.Play();
                }
            }
        }

        internal static bool RequiresFinalQualityAlignment(double sourceTime)
        {
            return IsValidPlaybackTime(sourceTime) &&
                   sourceTime > QualitySwitchMaximumDriftSeconds;
        }

        private static bool IsValidPlaybackTime(double time)
        {
            return time >= 0d && double.IsNaN(time) is false && double.IsInfinity(time) is false;
        }

        private void Open(bool autoPlay, bool useFastHlsStartup)
        {
            if (useFastHlsStartup)
            {
                ApplyHlsStartupLimit();
            }

            if (!m_Player.OpenMedia(MediaPathType.AbsolutePathOrURL, Path, autoPlay))
            {
                throw new GameException($"AVPro cannot open video: {Path}");
            }
        }

        private void OnMediaEvent(MediaPlayer player, MediaPlayerEvent.EventType eventType, ErrorCode errorCode)
        {
            switch (eventType)
            {
                case MediaPlayerEvent.EventType.ReadyToPlay:
                    if (m_Preloading)
                    {
                        m_Player.Play();
                    }
                    break;
                case MediaPlayerEvent.EventType.FirstFrameReady:
                    var beginHlsStartupUpgrade = m_Preloading is false && m_HlsStartupUpgradePending;
                    m_HlsStartupUpgradePending = false;
                    if (!m_FirstFrame)
                    {
                        m_FirstFrame = true;
                        // 首帧就绪：释放预览图并绑定视频纹理。
                        ReleasePreview();
                        BindVideoSurface();
                        FirstFrameReady?.Invoke(this);
                    }

                    if (m_Preloading)
                    {
                        TryCompletePreload();
                    }
                    else
                    {
                        ReleaseHlsStartupLimit();
                        m_Ready.TrySetResult();
                    }

                    if (beginHlsStartupUpgrade)
                    {
                        BeginHlsStartupUpgrade();
                    }
                    break;
                case MediaPlayerEvent.EventType.ResolutionChanged:
                    TextureChanged?.Invoke(this);
                    if (m_FirstFrame)
                    {
                        // 分辨率变化：纹理已更新，重新绑定 surface。
                        BindVideoSurface();
                    }

                    if (m_Preloading)
                    {
                        TryCompletePreload();
                    }

                    break;
                case MediaPlayerEvent.EventType.FinishedPlaying:
                    SetCompleted();
                    Terminate();
                    break;
                case MediaPlayerEvent.EventType.Error:
                    var exception = new GameException($"AVPro video error. path:{Path} error:{errorCode}");
                    m_Ready.TrySetException(exception);
                    SetFailed(exception);
                    Terminate();
                    break;
            }
        }

        private string ResolveWindowsHlsFastStartupPath()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (m_SupportsAutoQuality is false ||
                m_Quality.Mode != VideoQualityMode.Auto ||
                string.Equals(Path, ResolveAutoPath(), StringComparison.Ordinal) is false)
            {
                return null;
            }

            return ResolveLowestQualityPath();
#else
            return null;
#endif
        }

        private void BeginHlsStartupUpgrade()
        {
            if (m_Terminated || m_Player == null)
            {
                return;
            }

            CancelHlsStartupUpgrade();
            var cancellation = new CancellationTokenSource();
            m_HlsStartupUpgradeCancellation = cancellation;
            UpgradeHlsStartupAsync(cancellation).Forget(Debug.LogException);
        }

        private async UniTask UpgradeHlsStartupAsync(CancellationTokenSource cancellation)
        {
            try
            {
                await UpgradeHlsStartupPlayerAsync(cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"AVPro HLS startup quality upgrade failed. path:{RequestPath} " +
                    $"error:{exception.Message}");
            }
            finally
            {
                if (ReferenceEquals(m_HlsStartupUpgradeCancellation, cancellation))
                {
                    m_HlsStartupUpgradeCancellation = null;
                }

                cancellation.Dispose();
            }
        }

        private async UniTask UpgradeHlsStartupPlayerAsync(CancellationToken cancellationToken)
        {
            var sourcePlayer = m_Player;
            var sourceInstance = m_PlayerInstance;
            var autoPath = ResolveAutoPath();
            var candidateInstance = new AvProVideoPlayerInstance(
                "VideoPlayableHlsStartupUpgrade",
                m_Parent,
                m_DontDestroyOnLoad,
                true);
            var candidateObject = candidateInstance.GameObject;
            var candidate = candidateInstance.Player;
            candidate.AutoOpen = false;
            candidate.AutoStart = true;
            candidate.Loop = m_Loop;
            candidate.AudioMuted = true;
            var ready = new UniTaskCompletionSource();

            void OnCandidateEvent(MediaPlayer player, MediaPlayerEvent.EventType eventType, ErrorCode errorCode)
            {
                if (eventType == MediaPlayerEvent.EventType.ReadyToPlay)
                {
                    var resumeTime = sourcePlayer?.Control?.GetCurrentTime() ?? 0d;
                    if (resumeTime > 0d &&
                        double.IsNaN(resumeTime) is false &&
                        double.IsInfinity(resumeTime) is false)
                    {
                        var duration = player.Info?.GetDuration() ?? 0d;
                        player.Control.Seek(IsValidDuration(duration)
                            ? Math.Min(resumeTime, duration)
                            : resumeTime);
                    }

                    player.Play();
                }
                else if (eventType == MediaPlayerEvent.EventType.FirstFrameReady)
                {
                    ready.TrySetResult();
                }
                else if (eventType == MediaPlayerEvent.EventType.Error)
                {
                    ready.TrySetException(
                        new GameException($"AVPro HLS startup quality upgrade failed. path:{autoPath} error:{errorCode}"));
                }
            }

            candidate.Events.AddListener(OnCandidateEvent);
            try
            {
                if (candidate.OpenMedia(MediaPathType.AbsolutePathOrURL, autoPath, false) is false)
                {
                    throw new GameException($"AVPro cannot open HLS startup quality: {autoPath}");
                }

                await ready.Task.AttachExternalCancellation(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (ReferenceEquals(m_Player, sourcePlayer) is false ||
                    ReferenceEquals(m_PlayerInstance, sourceInstance) is false)
                {
                    return;
                }

                var wasPaused = await AlignCandidateToCurrentPlaybackAsync(
                    sourcePlayer,
                    candidate,
                    cancellationToken);
                candidate.Events.RemoveListener(OnCandidateEvent);
                candidate.Events.AddListener(OnMediaEvent);
                sourcePlayer.AudioMuted = true;
                candidate.AudioMuted = false;
                if (wasPaused)
                {
                    candidate.Pause();
                }
                else
                {
                    candidate.Play();
                }

                sourcePlayer.Events.RemoveListener(OnMediaEvent);
                m_GameObject = candidateObject;
                m_Player = candidate;
                m_PlayerInstance = candidateInstance;
                Path = autoPath;
                m_Quality = new VideoQualitySelection(VideoQualityMode.Auto);
                m_TextureResolver = null;
                m_StableOutputTexture = null;
                // 启动升级换流完成：重新绑定 surface。
                BindVideoSurface();
                TextureChanged?.Invoke(this);
                sourceInstance.Dispose();
                candidateInstance = null;
            }
            finally
            {
                if (candidateInstance != null)
                {
                    candidate.Events.RemoveListener(OnCandidateEvent);
                    candidateInstance.Dispose();
                }
            }
        }

        private void CancelHlsStartupUpgrade()
        {
            m_HlsStartupUpgradePending = false;
            var cancellation = m_HlsStartupUpgradeCancellation;
            m_HlsStartupUpgradeCancellation = null;
            if (cancellation == null)
            {
                return;
            }

            cancellation.Cancel();
        }

        private void TryCompletePreload(bool timedOut = false)
        {
            if (m_Preloading is false || m_FirstFrame is false || m_PreloadReady)
            {
                return;
            }

            var currentHeight = GetCurrentVideoHeight();
            if (timedOut is false &&
                m_PreloadTargetHeight > 0 &&
                currentHeight < m_PreloadTargetHeight)
            {
                return;
            }

            m_PreloadReady = true;
            m_Player.Pause();
            m_Player.Control?.Rewind();
            CancelPreloadTimeout();
            m_Ready.TrySetResult();
        }

        private int GetCurrentVideoHeight()
        {
            var texture = m_Player?.TextureProducer?.GetTexture(0);
            if (texture != null && texture.height > 0)
            {
                return texture.height;
            }

            return m_Player?.Info?.GetVideoHeight() ?? 0;
        }

        private int ResolvePreloadTargetHeight(int requestedHeight)
        {
            if (m_Preloading is false)
            {
                return 0;
            }

            if (requestedHeight > 0 && HasQualityOption(requestedHeight))
            {
                return requestedHeight;
            }

            if (m_SupportsAutoQuality is false)
            {
                return 0;
            }

            // auto：预热最高清晰度变体。
            var highest = 0;
            for (var i = 0; i < m_QualityOptions.Count; i++)
            {
                var height = m_QualityOptions[i].Height;
                if (height > 0)
                {
                    highest = Math.Max(highest, height);
                }
            }

            return highest;
        }

        private bool HasQualityOption(int height)
        {
            for (var i = 0; i < m_QualityOptions.Count; i++)
            {
                if (m_QualityOptions[i].Height == height)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 预热路径：目标清晰度变体，缺省回退最低变体。
        /// </summary>
        private string ResolvePreloadPath()
        {
            var targetHeight = m_PreloadTargetHeight;
            if (targetHeight > 0)
            {
                for (var i = 0; i < m_QualityOptions.Count; i++)
                {
                    var option = m_QualityOptions[i];
                    if (option.Height == targetHeight &&
                        string.IsNullOrWhiteSpace(option.Location) is false &&
                        string.Equals(option.Location, ResolveAutoPath(), StringComparison.Ordinal) is false)
                    {
                        return option.Location;
                    }
                }
            }

            return ResolveLowestQualityPath();
        }

        /// <summary>
        /// 预热按目标清晰度解码：覆盖 Android 实例默认的 426x240 上限，否则无法达到目标高度。
        /// </summary>
        private void ApplyPreloadAndroidTarget()
        {
#if UNITY_ANDROID
            if (m_Preloading is false || m_Player == null)
            {
                return;
            }

            var options = m_Player.PlatformOptionsAndroid;
            var targetHeight = m_PreloadTargetHeight;
            if (targetHeight <= 0)
            {
                options.preferredMaximumResolution = MediaPlayer.PlatformOptions.Resolution.NoPreference;
                options.preferredPeakBitRate = 0f;
                return;
            }

            for (var i = 0; i < m_QualityOptions.Count; i++)
            {
                var option = m_QualityOptions[i];
                if (option.Height != targetHeight)
                {
                    continue;
                }

                options.preferredMaximumResolution = ToPreferredResolution(option.Height);
                if (options.preferredMaximumResolution == MediaPlayer.PlatformOptions.Resolution.Custom)
                {
                    options.customPreferredMaximumResolution =
                        new Vector2Int(option.Width, option.Height);
                }

                options.preferredPeakBitRateUnits = MediaPlayer.PlatformOptions.BitRateUnits.bps;
                options.preferredPeakBitRate = option.Bitrate;
                return;
            }

            options.preferredMaximumResolution = MediaPlayer.PlatformOptions.Resolution.NoPreference;
            options.preferredPeakBitRate = 0f;
#endif
        }

        private string ResolveLowestQualityPath()
        {
            if (m_SupportsAutoQuality is false)
            {
                return null;
            }

            VideoQualityOption lowest = default;
            var found = false;
            for (var i = 0; i < m_QualityOptions.Count; i++)
            {
                var option = m_QualityOptions[i];
                if (option.Height <= 0 ||
                    string.IsNullOrWhiteSpace(option.Location) ||
                    (found && option.Height >= lowest.Height))
                {
                    continue;
                }

                lowest = option;
                found = true;
            }

            return found &&
                   string.Equals(lowest.Location, ResolveAutoPath(), StringComparison.Ordinal) is false
                ? lowest.Location
                : null;
        }

        private void StartPreloadTimeout()
        {
            if (m_PreloadTargetHeight <= 0 || m_PreloadTimeoutCancellation != null)
            {
                return;
            }

            m_PreloadTimeoutCancellation = new CancellationTokenSource();
            CompletePreloadAfterTimeoutAsync(m_PreloadTimeoutCancellation).Forget(Debug.LogException);
        }

        private async UniTask CompletePreloadAfterTimeoutAsync(CancellationTokenSource cancellation)
        {
            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(HlsPreloadTargetTimeoutSeconds),
                    ignoreTimeScale: true,
                    cancellationToken: cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return;
            }

            if (ReferenceEquals(m_PreloadTimeoutCancellation, cancellation) is false ||
                m_Preloading is false)
            {
                return;
            }

            Debug.LogWarning(
                $"AVPro HLS preload target timed out. path:{Path} " +
                $"target:{m_PreloadTargetHeight}p current:{GetCurrentVideoHeight()}p");
            TryCompletePreload(true);
        }

        private void CancelPreloadTimeout()
        {
            var cancellation = m_PreloadTimeoutCancellation;
            m_PreloadTimeoutCancellation = null;
            if (cancellation == null)
            {
                return;
            }

            cancellation.Cancel();
            cancellation.Dispose();
        }

        private void Terminate()
        {
            if (m_Terminated)
            {
                return;
            }

            m_Terminated = true;
            ReleasePreview();
            m_Surface = null;
            Terminated?.Invoke(this);
        }

        private string ResolveQualityPath(VideoQualitySelection selection)
        {
            if (selection.Mode == VideoQualityMode.Auto)
            {
                if (m_SupportsAutoQuality is false)
                {
                    throw new GameException("Auto video quality is not supported by this video.");
                }

                return m_Quality.Mode == VideoQualityMode.Auto ? Path : ResolveAutoPath();
            }

            for (var i = 0; i < m_QualityOptions.Count; i++)
            {
                if (m_QualityOptions[i].Height == selection.Height)
                {
                    return m_QualityOptions[i].Location;
                }
            }

            throw new GameException($"Video quality height is unavailable: {selection.Height}");
        }

        private string ResolveAutoPath()
        {
            return m_AutoPath ?? Path;
        }

        private string m_AutoPath;

        private VideoQualitySelection ResolveInitialQuality(VideoQualitySelection initial)
        {
            if (m_SupportsAutoQuality)
            {
                // 预热手柄携带用户选定的固定清晰度；冷启动仍从原生 auto 开始。
                if (m_Preloading &&
                    initial.Mode == VideoQualityMode.FixedHeight &&
                    initial.Height > 0 &&
                    HasQualityOption(initial.Height))
                {
                    return initial;
                }

                return new VideoQualitySelection(VideoQualityMode.Auto);
            }

            if (initial.Mode == VideoQualityMode.FixedHeight)
            {
                for (var i = 0; i < m_QualityOptions.Count; i++)
                {
                    if (m_QualityOptions[i].Height == initial.Height)
                    {
                        return initial;
                    }
                }
            }

            if (m_QualityOptions.Count > 0)
            {
                var height = initial.Mode == VideoQualityMode.FixedHeight && initial.Height > 0
                    ? initial.Height
                    : m_QualityOptions[0].Height;
                return new VideoQualitySelection(VideoQualityMode.FixedHeight, height);
            }

            return default;
        }

        private string ResolveInitialPath(VideoQualitySelection selection)
        {
            if (m_SupportsAutoQuality)
            {
                return ResolveAutoPath();
            }

            if (selection.Mode == VideoQualityMode.Auto)
            {
                return ResolveAutoPath();
            }

            for (var i = 0; i < m_QualityOptions.Count; i++)
            {
                if (m_QualityOptions[i].Height == selection.Height)
                {
                    return m_QualityOptions[i].Location;
                }
            }

            return ResolveAutoPath();
        }

        private void PrepareNativeHlsOutput()
        {
            if (m_SupportsAutoQuality is false || SupportsNativeHlsVariantSelection is false)
            {
                return;
            }

            m_TextureResolver = m_PlayerInstance.GetTextureResolver();
        }

        private void EnsureStableNativeHlsOutput()
        {
            if (m_TextureResolver == null || m_StableOutputTexture != null)
            {
                return;
            }

            var width = 0;
            var height = 0;
            var pixelCount = 0L;
            for (var i = 0; i < m_QualityOptions.Count; i++)
            {
                var option = m_QualityOptions[i];
                var optionPixelCount = (long)option.Width * option.Height;
                if (optionPixelCount <= pixelCount)
                {
                    continue;
                }

                width = option.Width;
                height = option.Height;
                pixelCount = optionPixelCount;
            }

            if (width <= 0 || height <= 0)
            {
                return;
            }

            var stableTexture = m_PlayerInstance.GetStableOutputTexture(width, height);
            if (stableTexture == null)
            {
                return;
            }

            var retainedFrame = m_TextureResolver.TargetTexture;
            if (retainedFrame != null && ReferenceEquals(retainedFrame, stableTexture) is false)
            {
                Graphics.Blit(retainedFrame, stableTexture);
            }

            m_StableOutputTexture = stableTexture;
            m_TextureResolver.ExternalTexture = stableTexture;
        }

        private bool TrySelectNativeQuality(VideoQualitySelection selection)
        {
            if (SupportsNativeHlsVariantSelection is false ||
                m_SupportsAutoQuality is false ||
                string.Equals(Path, ResolveAutoPath(), StringComparison.Ordinal) is false)
            {
                return false;
            }

            if (TryApplyAndroidNativeQuality(selection))
            {
                return true;
            }

            var variants = m_Player?.Variants;
            if (variants == null || variants.Count == 0)
            {
                return false;
            }

            Variant selectedVariant = Variant.Auto;
            if (selection.Mode == VideoQualityMode.FixedHeight)
            {
                selectedVariant = FindVariantByHeight(variants, selection.Height);
                if (selectedVariant == null)
                {
                    return false;
                }
            }

            variants.SelectVariant(selectedVariant);
            m_Quality = selection;
            Path = ResolveAutoPath();
            return true;
        }

        private bool TryApplyAndroidNativeQuality(VideoQualitySelection selection)
        {
#if UNITY_ANDROID
            var options = m_Player.PlatformOptionsAndroid;
            if (selection.Mode == VideoQualityMode.Auto)
            {
                options.preferredMaximumResolution = MediaPlayer.PlatformOptions.Resolution.NoPreference;
                options.preferredPeakBitRate = 0f;
            }
            else
            {
                VideoQualityOption selectedOption = default;
                var found = false;
                for (var i = 0; i < m_QualityOptions.Count; i++)
                {
                    if (m_QualityOptions[i].Height != selection.Height)
                    {
                        continue;
                    }

                    selectedOption = m_QualityOptions[i];
                    found = true;
                    break;
                }

                if (found is false)
                {
                    return false;
                }

                options.preferredMaximumResolution = ToPreferredResolution(selectedOption.Height);
                if (options.preferredMaximumResolution == MediaPlayer.PlatformOptions.Resolution.Custom)
                {
                    options.customPreferredMaximumResolution =
                        new Vector2Int(selectedOption.Width, selectedOption.Height);
                }

                options.preferredPeakBitRateUnits = MediaPlayer.PlatformOptions.BitRateUnits.bps;
                options.preferredPeakBitRate = selectedOption.Bitrate;
            }

            m_HlsStartupLimitApplied = false;
            m_Quality = selection;
            Path = ResolveAutoPath();
            return true;
#else
            return false;
#endif
        }

        private void ApplyHlsStartupLimit()
        {
            if (m_SupportsAutoQuality is false || m_HlsStartupLimitApplied)
            {
                return;
            }

            var options = m_Player.PlatformOptionsAndroid;
            options.preferredMaximumResolution = MediaPlayer.PlatformOptions.Resolution.Custom;
            options.customPreferredMaximumResolution = HlsStartupMaximumResolution;
            options.preferredPeakBitRateUnits = MediaPlayer.PlatformOptions.BitRateUnits.Mbps;
            options.preferredPeakBitRate = HlsStartupPeakBitRateMbps;
            m_HlsStartupLimitApplied = true;
        }

        private void ReleaseHlsStartupLimit()
        {
            if (m_HlsStartupLimitApplied is false || m_Quality.Mode != VideoQualityMode.Auto)
            {
                return;
            }

            var options = m_Player.PlatformOptionsAndroid;
            options.preferredMaximumResolution = MediaPlayer.PlatformOptions.Resolution.NoPreference;
            options.preferredPeakBitRate = 0f;
            m_HlsStartupLimitApplied = false;
        }

        private static MediaPlayer.PlatformOptions.Resolution ToPreferredResolution(int height)
        {
            return height switch
            {
                480 => MediaPlayer.PlatformOptions.Resolution._480p,
                720 => MediaPlayer.PlatformOptions.Resolution._720p,
                1080 => MediaPlayer.PlatformOptions.Resolution._1080p,
                1440 => MediaPlayer.PlatformOptions.Resolution._1440p,
                2160 => MediaPlayer.PlatformOptions.Resolution._2160p,
                _ => MediaPlayer.PlatformOptions.Resolution.Custom
            };
        }

        private static Variant FindVariantByHeight(IVariants variants, int height)
        {
            if (variants == null)
            {
                return null;
            }

            for (var i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                if (variant != null && variant.Height == height)
                {
                    return variant;
                }
            }

            return null;
        }

        private string DescribeNativeVariants()
        {
            var variants = m_Player?.Variants;
            if (variants == null || variants.Count == 0)
            {
                return "none";
            }

            var descriptions = new string[variants.Count];
            for (var i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                descriptions[i] = variant == null
                    ? "null"
                    : $"[id={variant.Id},width={variant.Width},height={variant.Height}," +
                      $"bitrate={variant.PeakDataRate},unsupported={variant.IsUnsupported}]";
            }

            return string.Join(",", descriptions);
        }

        internal static bool SupportsNativeHlsVariantSelection
        {
            get
            {
#if UNITY_EDITOR_OSX || (!UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_IOS || UNITY_TVOS || UNITY_VISIONOS || UNITY_ANDROID || UNITY_OPENHARMONY))
                return true;
#else
                return false;
#endif
            }
        }

        private void CancelQualitySwitch()
        {
            m_QualityCancellation?.Cancel();
            m_QualityCancellation?.Dispose();
            m_QualityCancellation = null;
        }

        private static IReadOnlyList<VideoQualityOption> CopyQualityOptions(IReadOnlyList<VideoQualityOption> options)
        {
            if (options == null || options.Count == 0)
            {
                return Array.Empty<VideoQualityOption>();
            }

            var copy = new VideoQualityOption[options.Count];
            var heights = new HashSet<int>();
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (heights.Add(option.Height) is false)
                {
                    return Array.Empty<VideoQualityOption>();
                }

                copy[i] = option;
            }

            return copy;
        }

        private static int GetDistinctHeightCount(IReadOnlyList<VideoQualityOption> options)
        {
            return options?.Count ?? 0;
        }

        private static bool IsValidDuration(double duration)
        {
            return duration > 0d && !double.IsNaN(duration) && !double.IsInfinity(duration);
        }
    }

    internal sealed class AvProVideoPlayerInstance : IDisposable
    {
        private ResolveToRenderTexture m_TextureResolver;
        private RenderTexture m_StableOutputTexture;
        private bool m_Disposed;

        internal AvProVideoPlayerInstance(
            string name,
            Transform parent,
            bool dontDestroyOnLoad,
            bool preferHighBitrate)
        {
            GameObject = new GameObject(name);
            if (parent != null)
            {
                GameObject.transform.SetParent(parent, false);
            }
            else if (Application.isPlaying && dontDestroyOnLoad)
            {
                Object.DontDestroyOnLoad(GameObject);
            }

            var player = GameObject.AddComponent<InitializableAvProMediaPlayer>();
            player.AutoOpen = false;
            player.AutoStart = true;
            var windowsOptions = player.PlatformOptionsWindows;
            windowsOptions.videoApi = preferHighBitrate
                ? Windows.VideoApi.WinRT
                : Windows.VideoApi.MediaFoundation;
#if UNITY_EDITOR_WIN
            windowsOptions.useHardwareDecoding = false;
#endif
            windowsOptions.startWithHighestBitrate = preferHighBitrate;
            windowsOptions.useLowLatency = preferHighBitrate is false;
            if (preferHighBitrate is false)
            {
                windowsOptions.prerollFrameCount = 1;
            }

            var androidOptions = player.PlatformOptionsAndroid;
            androidOptions.allowUnsupportedVideoTrackVariants = true;
            androidOptions.startWithHighestBitrate = preferHighBitrate;
            androidOptions.minBufferMs = 2000;
            androidOptions.maxBufferMs = 10000;
            androidOptions.bufferForPlaybackMs = 250;
            androidOptions.bufferForPlaybackAfterRebufferMs = 750;
            if (preferHighBitrate is false)
            {
                androidOptions.preferredMaximumResolution =
                    MediaPlayer.PlatformOptions.Resolution.Custom;
                androidOptions.customPreferredMaximumResolution =
                    VideoPlayableHandle.HlsStartupMaximumResolution;
                androidOptions.preferredPeakBitRateUnits =
                    MediaPlayer.PlatformOptions.BitRateUnits.Mbps;
                androidOptions.preferredPeakBitRate = VideoPlayableHandle.HlsStartupPeakBitRateMbps;
            }

            Player = player;
            PreferHighBitrate = preferHighBitrate;
            if (Application.isPlaying)
            {
                player.WarmInitialize();
            }
        }

        internal GameObject GameObject { get; }

        internal MediaPlayer Player { get; }

        internal bool PreferHighBitrate { get; }

        internal ResolveToRenderTexture GetTextureResolver()
        {
            if (m_TextureResolver == null)
            {
                m_TextureResolver = GameObject.AddComponent<ResolveToRenderTexture>();
                m_TextureResolver.MediaPlayer = Player;
            }

            m_TextureResolver.enabled = true;
            return m_TextureResolver;
        }

        internal RenderTexture GetStableOutputTexture(int width, int height)
        {
            if (m_StableOutputTexture != null &&
                m_StableOutputTexture.width == width &&
                m_StableOutputTexture.height == height)
            {
                return m_StableOutputTexture;
            }

            ReleaseStableOutputTexture();
            var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "AVProVideo_StableHlsOutput",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            if (texture.Create() is false)
            {
                DestroyObject(texture);
                return null;
            }

            m_StableOutputTexture = texture;
            return texture;
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            if (Player != null)
            {
                Player.Stop();
                Player.CloseMedia();
            }

            if (m_TextureResolver != null)
            {
                m_TextureResolver.ExternalTexture = null;
            }

            ReleaseStableOutputTexture();
            DestroyObject(GameObject);
        }

        private void ReleaseStableOutputTexture()
        {
            if (m_StableOutputTexture == null)
            {
                return;
            }

            if (m_TextureResolver != null)
            {
                m_TextureResolver.ExternalTexture = null;
            }

            m_StableOutputTexture.Release();
            DestroyObject(m_StableOutputTexture);
            m_StableOutputTexture = null;
        }

        private static void DestroyObject(Object value)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(value);
            }
            else
            {
                Object.DestroyImmediate(value);
            }
        }
    }

    internal sealed class InitializableAvProMediaPlayer : MediaPlayer
    {
        internal void WarmInitialize()
        {
            Initialise();
        }
    }
}
