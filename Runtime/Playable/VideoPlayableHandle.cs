using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RenderHeads.Media.AVProVideo;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Playable
{
    public sealed class VideoPlayableHandle : PlayableHandle
    {
        private GameObject m_GameObject;
        private MediaPlayer m_Player;
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

        internal VideoPlayableHandle(string path, VideoPlayableOptions options, bool preloading)
        {
            Path = path;
            m_Preloading = preloading;
            m_GameObject = new GameObject(preloading ? "VideoPlayablePreload" : "VideoPlayable");
            ApplyParent(options);
            m_Player = m_GameObject.AddComponent<MediaPlayer>();
            m_Player.AutoOpen = false;
            m_Player.AutoStart = false;
            m_Player.Events.AddListener(OnMediaEvent);
            ApplyOptions(options);
        }

        public string Path { get; private set; }

        public Texture Texture => m_Player?.TextureProducer?.GetTexture(0);

        public bool HasFirstFrame => m_FirstFrame && Texture != null;

        public bool RequiresVerticalFlip => m_Player?.TextureProducer?.RequiresVerticalFlip() ?? false;

        public bool CanSeek => Seekable && IsValidDuration(DurationSeconds) && m_Player?.Control?.CanPlay() == true;

        public bool CanPause => Status is PlayableStatus.Playing or PlayableStatus.Paused;

        public bool IsPaused => Status == PlayableStatus.Paused;

        public bool Seekable { get; private set; }

        public bool CanSelectQuality => GetDistinctHeightCount(m_QualityOptions) >= 2;

        public bool SupportsAutoQuality => m_SupportsAutoQuality;

        public VideoQualitySelection Quality => m_Quality;

        public IReadOnlyList<VideoQualityOption> QualityOptions => m_QualityOptions;

        public double DurationSeconds => m_Player?.Info?.GetDuration() ?? 0d;

        public double CurrentTimeSeconds => m_Player?.Control?.GetCurrentTime() ?? 0d;

        public event Action<VideoPlayableHandle> FirstFrameReady;

        internal event Action<VideoPlayableHandle> Terminated;

        internal void ApplyOptions(VideoPlayableOptions options)
        {
            options ??= new VideoPlayableOptions();
            Seekable = options.Seekable;
            m_Loop = options.Loop;
            m_Parent = options.Parent;
            m_DontDestroyOnLoad = options.DontDestroyOnLoad;
            m_SupportsAutoQuality = options.SupportsAutoQuality;
            m_QualityOptions = CopyQualityOptions(options.QualityOptions);
            m_Quality = ResolveInitialQuality(options.InitialQuality);
            if (m_Player != null)
            {
                m_Player.Loop = m_Loop;
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

            m_QualityCancellation?.Cancel();
            m_QualityCancellation?.Dispose();
            m_QualityCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return SwitchQualityAsync(selection, path, m_QualityCancellation.Token);
        }

        internal void Preload()
        {
            m_Player.AudioMuted = true;
            Open(false);
        }

        internal void Play()
        {
            if (Status == PlayableStatus.Preparing)
            {
                SetPlaying();
            }

            m_Player.AudioMuted = false;
            if (m_Preloading)
            {
                m_Preloading = false;
                m_Player.Play();
            }
            else
            {
                Open(true);
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
            m_QualityCancellation?.Cancel();
            m_QualityCancellation?.Dispose();
            m_QualityCancellation = null;
            try
            {
                m_Player.Events.RemoveListener(OnMediaEvent);
                try
                {
                    m_Player.Stop();
                }
                finally
                {
                    m_Player.CloseMedia();
                }
            }
            finally
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(m_GameObject);
                }
                else
                {
                    Object.DestroyImmediate(m_GameObject);
                }
            }
        }

        private async UniTask SwitchQualityAsync(
            VideoQualitySelection selection,
            string path,
            CancellationToken cancellationToken)
        {
            var candidateObject = new GameObject("VideoPlayableQualityCandidate");
            ApplyParent(candidateObject, m_Parent, m_DontDestroyOnLoad);
            var candidate = candidateObject.AddComponent<MediaPlayer>();
            candidate.AutoOpen = false;
            candidate.AutoStart = false;
            candidate.Loop = m_Loop;
            candidate.AudioMuted = true;
            var ready = new UniTaskCompletionSource();
            var oldTime = CurrentTimeSeconds;
            var wasPaused = IsPaused;

            void OnCandidateEvent(MediaPlayer player, MediaPlayerEvent.EventType eventType, ErrorCode errorCode)
            {
                if (eventType == MediaPlayerEvent.EventType.ReadyToPlay)
                {
                    if (oldTime > 0d && double.IsNaN(oldTime) is false && double.IsInfinity(oldTime) is false)
                    {
                        var duration = player.Info?.GetDuration() ?? 0d;
                        player.Control.Seek(IsValidDuration(duration)
                            ? Math.Min(oldTime, duration)
                            : oldTime);
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
                candidate.Events.RemoveListener(OnCandidateEvent);
                candidate.Events.AddListener(OnMediaEvent);
                candidate.AudioMuted = false;
                if (wasPaused)
                {
                    candidate.Pause();
                }

                var oldObject = m_GameObject;
                var oldPlayer = m_Player;
                m_GameObject = candidateObject;
                m_Player = candidate;
                Path = path;
                m_Quality = selection;
                m_FirstFrame = true;
                oldPlayer.Events.RemoveListener(OnMediaEvent);
                oldPlayer.Stop();
                oldPlayer.CloseMedia();
                DestroyObject(oldObject);
                FirstFrameReady?.Invoke(this);
                candidateObject = null;
            }
            finally
            {
                if (candidateObject != null)
                {
                    candidate.Events.RemoveListener(OnCandidateEvent);
                    candidate.Stop();
                    candidate.CloseMedia();
                    DestroyObject(candidateObject);
                }

                if (m_QualityCancellation != null && m_QualityCancellation.Token == cancellationToken)
                {
                    m_QualityCancellation.Dispose();
                    m_QualityCancellation = null;
                }
            }
        }

        private void Open(bool autoPlay)
        {
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
                    m_Ready.TrySetResult();
                    if (m_Preloading)
                    {
                        m_Player.Play();
                    }
                    break;
                case MediaPlayerEvent.EventType.FirstFrameReady:
                    if (!m_FirstFrame)
                    {
                        m_FirstFrame = true;
                        FirstFrameReady?.Invoke(this);
                    }

                    if (m_Preloading)
                    {
                        m_Player.Pause();
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

        private void Terminate()
        {
            if (m_Terminated)
            {
                return;
            }

            m_Terminated = true;
            Terminated?.Invoke(this);
        }

        private void ApplyParent(VideoPlayableOptions options)
        {
            ApplyParent(m_GameObject, options?.Parent, options?.DontDestroyOnLoad != false);
        }

        private static void ApplyParent(GameObject target, Transform parent, bool dontDestroyOnLoad)
        {
            if (parent != null)
            {
                target.transform.SetParent(parent, false);
            }
            else if (dontDestroyOnLoad)
            {
                Object.DontDestroyOnLoad(target);
            }
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
            m_AutoPath = Path;
            if (m_SupportsAutoQuality)
            {
                return new VideoQualitySelection(VideoQualityMode.Auto);
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

        private static void DestroyObject(GameObject value)
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

        private static bool IsValidDuration(double duration)
        {
            return duration > 0d && !double.IsNaN(duration) && !double.IsInfinity(duration);
        }
    }
}
