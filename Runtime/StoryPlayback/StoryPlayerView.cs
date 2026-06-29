using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using GameDeveloperKit.Sound;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 基于 UGUI 和 AVProVideo 的 Story 播放视图。
    /// </summary>
    public sealed partial class StoryPlayerView : MonoBehaviour, IStoryFramePresenter, IStoryPlaybackHost
    {
        [Header("模块")]
        [SerializeField] private bool m_UseAppModules = true;

        [Header("媒体")]
        [SerializeField] private Transform m_PlaybackRoot;
        [SerializeField] private RawImage m_VideoOutput;
        [SerializeField] private RawImage m_ImageOutput;
        [SerializeField] private RectTransform m_VideoSeekRoot;
        [SerializeField] private Slider m_VideoSeekSlider;
        [SerializeField] private TMP_Text m_VideoSeekTimeText;
        [SerializeField] private Button m_VideoSeekPauseButton;
        [SerializeField] private bool m_ClearVideoWhenIdle = true;
        [SerializeField] private int m_VideoPreloadCapacity = 2;
        [SerializeField] private int m_VideoPreloadLookAheadCount = 1;

        [Header("文本")]
        [SerializeField] private TMP_Text m_SpeakerText;
        [SerializeField] private TMP_Text m_BodyText;
        [SerializeField] private TMP_Text m_ErrorText;
        [SerializeField] private string m_CompletedText = "剧情已结束";

        [Header("交互")]
        [SerializeField] private Button m_ContinueButton;
        [SerializeField] private Transform m_ChoiceRoot;
        [SerializeField] private Button m_ChoiceButtonTemplate;

        private readonly List<Button> m_BoundChoiceButtons = new List<Button>();
        private readonly StringBuilder m_TextBuilder = new StringBuilder();

        private StoryModule m_StoryModule;
        private ResourceModule m_ResourceModule;
        private SoundModule m_SoundModule;
        private StoryPresenter m_Presenter;
        private StoryAvProVideoCommandPlayer m_VideoPlayer;
        private StoryImageCommandPlayer m_ImagePlayer;
        private StorySoundCommandPlayer m_AudioPlayer;
        private DefaultInteractionChannel m_DefaultInteractionChannel;
        private IInteractionChannel m_InteractionChannelOverride;
        private IInteractionChannel m_ActiveInteractionChannel;
        private SessionUnlockStateProvider m_DefaultUnlockStateProvider;
        private RawImage m_CurrentVideoOutput;
        private RawImage m_CurrentImageOutput;
        private RectTransform m_CurrentCustomRoot;
        private VideoSeekSurface m_CurrentVideoSeek;
        private VideoSeekBinder m_VideoSeekBinder;
        private CancellationTokenSource m_PlaybackCancellation;
        private StoryFrame m_CurrentFrame;
        private StoryChapter m_CurrentChapter;
        private Button m_BoundContinueButton;
        private bool m_FirstVideoFrameReported;

        private const int DefaultCanvasSortingOrder = 1000;
        private const float MinimumVisibleScale = 0.0001f;
        private const string DefaultTextFontResourcePath = "SIMSUN SDF";

        private static readonly Vector2 s_DefaultReferenceResolution = new Vector2(1920f, 1080f);
        private static readonly Rect s_DefaultVideoUvRect = new Rect(0f, 0f, 1f, 1f);
        private static readonly Rect s_FlippedVideoUvRect = new Rect(0f, 1f, 1f, -1f);
        private static TMP_FontAsset s_DefaultTextFont;

        /// <summary>
        /// 当前 Story 表现协调器。
        /// </summary>
        public StoryPresenter Presenter => m_Presenter;

        /// <summary>
        /// 当前帧。
        /// </summary>
        public StoryFrame CurrentFrame => m_CurrentFrame;

        /// <summary>
        /// 最近一次播放错误。
        /// </summary>
        public Exception LastError { get; private set; }

        /// <summary>
        /// 当前播放会话中第一个视频首帧就绪。
        /// </summary>
        public event Action<StoryAvProVideoPlayback> FirstVideoFrameReady;

        /// <summary>
        /// 设置当前播放视图使用的交互通道。
        /// </summary>
        /// <param name="channel">交互通道。</param>
        public void SetInteractionChannel(IInteractionChannel channel)
        {
            m_InteractionChannelOverride = channel;
            m_ActiveInteractionChannel = null;
        }

        /// <summary>
        /// 创建默认播放 surface。
        /// </summary>
        /// <returns>默认播放 surface。</returns>
        public PlaybackSurfaceView CreateDefaultSurfaceView()
        {
            return CreateDefaultSurfaceView(Array.Empty<Button>());
        }

        internal PlaybackSurfaceView CreateDefaultSurfaceView(IReadOnlyList<Button> choiceButtons)
        {
            return new PlaybackSurfaceView(
                m_VideoOutput,
                m_ImageOutput,
                m_SpeakerText,
                m_BodyText,
                m_ContinueButton,
                choiceButtons,
                transform as RectTransform,
                GetVideoSeekSurface());
        }

        internal Transform DefaultChoiceRoot => m_ChoiceRoot;

        internal Button DefaultChoiceButtonTemplate => m_ChoiceButtonTemplate;

        internal string CompletedText => m_CompletedText;

        internal void ContinueFromInteraction()
        {
            Continue();
        }

        internal void SelectFromInteraction(string choiceId)
        {
            Select(choiceId);
        }

        /// <summary>
        /// 设置播放器使用的模块。
        /// </summary>
        /// <param name="storyModule">剧情模块。</param>
        /// <param name="resourceModule">资源模块。</param>
        /// <param name="soundModule">声音模块。</param>
        public void ConfigureModules(
            StoryModule storyModule,
            ResourceModule resourceModule = null,
            SoundModule soundModule = null)
        {
            m_StoryModule = storyModule ?? throw new ArgumentNullException(nameof(storyModule));
            m_ResourceModule = resourceModule;
            m_SoundModule = soundModule;
            m_UseAppModules = false;
            RebuildPresenter();
        }

        /// <summary>
        /// 播放传入的剧情程序。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="chapterId">章节 ID。</param>
        public void Play(StoryProgram program, string chapterId = null)
        {
            if (program == null)
            {
                throw new ArgumentNullException(nameof(program));
            }

            m_FirstVideoFrameReported = false;
            PlayAsync(program, chapterId).Forget();
        }

        /// <summary>
        /// 异步播放传入的剧情程序。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="chapterId">章节 ID。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>播放启动任务。</returns>
        public UniTask PlayAsync(
            StoryProgram program,
            string chapterId = null,
            CancellationToken cancellationToken = default)
        {
            if (program == null)
            {
                throw new ArgumentNullException(nameof(program));
            }

            m_FirstVideoFrameReported = false;
            return ExecutePlaybackAsync(
                presenter => presenter.Start(program, chapterId),
                program.StoryId,
                program,
                chapterId,
                cancellationToken);
        }

        /// <summary>
        /// 播放已注册的剧情程序。
        /// </summary>
        /// <param name="storyId">剧情 ID。</param>
        /// <param name="chapterId">章节 ID。</param>
        public void PlayRegistered(string storyId, string chapterId = null)
        {
            if (string.IsNullOrWhiteSpace(storyId))
            {
                throw new ArgumentException("Story id cannot be empty.", nameof(storyId));
            }

            m_FirstVideoFrameReported = false;
            PlayRegisteredAsync(storyId, chapterId).Forget();
        }

        /// <summary>
        /// 异步播放已注册的剧情程序。
        /// </summary>
        /// <param name="storyId">剧情 ID。</param>
        /// <param name="chapterId">章节 ID。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>播放启动任务。</returns>
        public UniTask PlayRegisteredAsync(
            string storyId,
            string chapterId = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(storyId))
            {
                throw new ArgumentException("Story id cannot be empty.", nameof(storyId));
            }

            m_FirstVideoFrameReported = false;
            var program = ResolveRegisteredProgram(storyId);
            return ExecutePlaybackAsync(
                presenter => presenter.StartProgram(storyId, chapterId),
                storyId,
                program,
                chapterId,
                cancellationToken);
        }

        /// <summary>
        /// 停止当前播放。
        /// </summary>
        public void StopPlayback()
        {
            LastError = null;
            m_FirstVideoFrameReported = false;
            CancelPlaybackSession();
            m_ActiveInteractionChannel?.OnStoryStopped();
            if (m_Presenter != null)
            {
                m_Presenter.Stop();
            }

            m_CurrentFrame = null;
            m_CurrentChapter = null;
            m_CurrentVideoOutput = null;
            m_CurrentImageOutput = null;
            m_CurrentCustomRoot = null;
            m_CurrentVideoSeek = null;
            m_ActiveInteractionChannel = null;
            m_VideoSeekBinder?.Unbind();
            ClearBoundInputs();
            m_DefaultInteractionChannel?.ClearTransientInputs();
            UpdateVideoOutput();
        }

        /// <summary>
        /// 继续当前剧情。
        /// </summary>
        public void Continue()
        {
            ExecutePlayback(() => EnsurePresenter().Continue());
        }

        /// <summary>
        /// 选择当前剧情选项。
        /// </summary>
        /// <param name="choiceId">选项 ID。</param>
        public void Select(string choiceId)
        {
            ExecutePlayback(() => EnsurePresenter().Select(choiceId));
        }

        /// <inheritdoc />
        public void Present(StoryFrame frame, StoryPresenter presenter)
        {
            m_CurrentFrame = frame;
            RenderFrame(frame);
        }

        /// <inheritdoc />
        public void Clear(StoryFrame frame)
        {
            m_DefaultInteractionChannel?.ClearTransientInputs();
        }

        private void Awake()
        {
            EnsureDefaultVideoSeekSurface();
            EnsureRenderableCanvas();
            EnsureDefaultInteractionChannel();
            if (m_ChoiceButtonTemplate != null)
            {
                m_ChoiceButtonTemplate.gameObject.SetActive(false);
            }

            ClearError();
        }

        private void EnsureRenderableCanvas()
        {
            RestoreVisibleScale(transform);

            var canvas = GetComponentInParent<Canvas>(true);
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = DefaultCanvasSortingOrder;
            }
            else if (canvas.transform == transform &&
                     canvas.renderMode == RenderMode.ScreenSpaceCamera &&
                     canvas.worldCamera == null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            if (canvas.transform == transform)
            {
                EnsureCanvasScaler(gameObject);
                EnsureGraphicRaycaster(gameObject);
            }

            EnsureReferenceCanvas(m_VideoOutput);
            EnsureReferenceCanvas(m_ImageOutput);
            EnsureReferenceCanvas(m_SpeakerText);
            EnsureReferenceCanvas(m_BodyText);
            EnsureReferenceCanvas(m_ErrorText);
            EnsureReferenceCanvas(m_ContinueButton);
            EnsureReferenceCanvas(m_ChoiceButtonTemplate);
            EnsureReferenceCanvas(m_VideoSeekSlider);
            EnsureReferenceCanvas(m_VideoSeekTimeText);
            EnsureReferenceCanvas(m_VideoSeekPauseButton);
        }

        private void EnsureReferenceCanvas(Component component)
        {
            if (component == null ||
                component.GetComponentInParent<Canvas>(true) != null)
            {
                return;
            }

            var canvasHost = FindCanvasHost(component.transform);
            var canvas = canvasHost.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasHost.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = DefaultCanvasSortingOrder;
            }
            else if (canvas.renderMode == RenderMode.ScreenSpaceCamera &&
                     canvas.worldCamera == null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            EnsureCanvasScaler(canvasHost.gameObject);
            EnsureGraphicRaycaster(canvasHost.gameObject);
            RestoreVisibleScale(canvasHost);
        }

        private Transform FindCanvasHost(Transform target)
        {
            if (target.IsChildOf(transform))
            {
                return transform;
            }

            var host = target;
            var current = target.parent;
            while (current != null &&
                   current.GetComponent<Canvas>() == null &&
                   current is RectTransform)
            {
                host = current;
                current = current.parent;
            }

            return host;
        }

        private static void EnsureCanvasScaler(GameObject owner)
        {
            var scaler = owner.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = owner.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = s_DefaultReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        private static void EnsureGraphicRaycaster(GameObject owner)
        {
            if (owner.GetComponent<GraphicRaycaster>() == null)
            {
                owner.AddComponent<GraphicRaycaster>();
            }
        }

        private static void RestoreVisibleScale(Transform target)
        {
            var scale = target.localScale;
            if (Mathf.Abs(scale.x) < MinimumVisibleScale ||
                Mathf.Abs(scale.y) < MinimumVisibleScale ||
                Mathf.Abs(scale.z) < MinimumVisibleScale)
            {
                target.localScale = Vector3.one;
            }
        }

        private void Update()
        {
            if (m_Presenter == null)
            {
                return;
            }

            UpdateVideoOutput();
            m_VideoSeekBinder?.Refresh();
            m_ActiveInteractionChannel?.Tick(Time.deltaTime);
            if (m_CurrentFrame != null && m_CurrentFrame.WaitsForTime && m_CurrentFrame.IsCompleted is false)
            {
                ExecutePlayback(() => m_Presenter.Evaluate(Time.deltaTime));
            }

            if (m_Presenter.LastError != null && !ReferenceEquals(LastError, m_Presenter.LastError))
            {
                SetError(m_Presenter.LastError);
            }
        }

        private void OnDestroy()
        {
            CancelPlaybackSession();
            DisposePresenter();
            m_DefaultInteractionChannel?.Dispose();
            m_DefaultInteractionChannel = null;
        }

        private StoryPresenter EnsurePresenter()
        {
            if (m_Presenter != null)
            {
                return m_Presenter;
            }

            ResolveModules();
            m_Presenter = new StoryPresenter(m_StoryModule, this);
            m_VideoPlayer = new StoryAvProVideoCommandPlayer(
                m_PlaybackRoot != null ? m_PlaybackRoot : transform,
                false);
            m_VideoPlayer.PreloadQueue = new StoryAvProVideoPreloadQueue(
                m_PlaybackRoot != null ? m_PlaybackRoot : transform,
                false,
                Mathf.Max(1, m_VideoPreloadCapacity));
            m_VideoPlayer.PreloadLookAheadCount = Mathf.Max(0, m_VideoPreloadLookAheadCount);
            m_VideoPlayer.PlaybackStarted += OnVideoPlaybackStarted;

            if (m_ResourceModule != null)
            {
                m_ImagePlayer = new StoryImageCommandPlayer(ResolveImageOutput, m_ResourceModule);
            }

            if (m_SoundModule != null)
            {
                m_AudioPlayer = new StorySoundCommandPlayer(m_SoundModule);
            }

            m_Presenter.AddCommandHandler(new StoryMediaCommandHandler(
                m_VideoPlayer,
                m_ImagePlayer,
                m_AudioPlayer));
            m_Presenter.AddCommandHandler(new StoryQteCommandHandler(() => m_CurrentCustomRoot));
            m_Presenter.AddCommandHandler(new StoryUnlockCommandHandler(() => m_CurrentCustomRoot, ResolveUnlockStateProvider));
            return m_Presenter;
        }

        private void ResolveModules()
        {
            if (m_StoryModule == null)
            {
                m_StoryModule = m_UseAppModules ? App.Story : new StoryModule();
                if (m_UseAppModules is false)
                {
                    m_StoryModule.Startup();
                }
            }

            if (m_UseAppModules)
            {
                if (m_ResourceModule == null)
                {
                    m_ResourceModule = App.Resource;
                }

                if (m_SoundModule == null)
                {
                    m_SoundModule = App.Sound;
                }
            }
        }

        private void RebuildPresenter()
        {
            DisposePresenter();
            if (isActiveAndEnabled)
            {
                EnsurePresenter();
            }
        }

        private void DisposePresenter()
        {
            if (m_Presenter != null)
            {
                m_Presenter.Dispose();
                m_Presenter = null;
            }

            if (m_VideoPlayer != null)
            {
                m_VideoPlayer.PlaybackStarted -= OnVideoPlaybackStarted;
                m_VideoPlayer.Dispose();
                m_VideoPlayer = null;
            }

            m_ImagePlayer?.Dispose();
            m_ImagePlayer = null;
            m_AudioPlayer = null;
            m_CurrentFrame = null;
            m_CurrentVideoOutput = null;
            m_CurrentImageOutput = null;
            m_CurrentCustomRoot = null;
            m_CurrentVideoSeek = null;
            m_CurrentChapter = null;
            m_FirstVideoFrameReported = false;
            m_VideoSeekBinder?.Unbind();
        }

        private StoryProgram ResolveRegisteredProgram(string storyId)
        {
            ResolveModules();
            return m_StoryModule.TryGetProgram(storyId, out var program) ? program : null;
        }

        private void ExecutePlayback(Func<StoryFrame> playback)
        {
            try
            {
                LastError = null;
                ClearError();
                var frame = playback();
                if (!ReferenceEquals(m_CurrentFrame, frame))
                {
                    m_CurrentFrame = frame;
                    RenderFrame(frame);
                }
            }
            catch (Exception exception)
            {
                SetError(exception);
            }
        }

        private async UniTask ExecutePlaybackAsync(
            Func<StoryPresenter, StoryFrame> playback,
            string storyId,
            StoryProgram program,
            string chapterId,
            CancellationToken cancellationToken)
        {
            CancelPlaybackSession();
            m_PlaybackCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var sessionToken = m_PlaybackCancellation.Token;
            try
            {
                LastError = null;
                ClearError();
                m_CurrentChapter = null;
                m_ActiveInteractionChannel = null;
                var presenter = EnsurePresenter();
                var channel = ResolveInteractionChannel();
                var context = new InteractionContext(m_StoryModule, presenter, storyId, program);
                await channel.OnAwake(context, sessionToken);
                sessionToken.ThrowIfCancellationRequested();
                await PrewarmPlaybackAsync(storyId, program, chapterId, sessionToken);
                sessionToken.ThrowIfCancellationRequested();
                channel.OnStoryStarted(context);
                var frame = playback(presenter);
                if (!ReferenceEquals(m_CurrentFrame, frame))
                {
                    m_CurrentFrame = frame;
                    RenderFrame(frame);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                SetError(exception);
            }
        }

        private void CancelPlaybackSession()
        {
            if (m_PlaybackCancellation == null)
            {
                return;
            }

            m_PlaybackCancellation.Cancel();
            m_PlaybackCancellation.Dispose();
            m_PlaybackCancellation = null;
        }

        private void SetError(Exception exception)
        {
            LastError = exception;
            if (m_ErrorText != null)
            {
                m_ErrorText.text = exception?.Message ?? string.Empty;
                m_ErrorText.gameObject.SetActive(exception != null);
            }
        }

        private void ClearError()
        {
            if (m_ErrorText != null)
            {
                m_ErrorText.text = string.Empty;
                m_ErrorText.gameObject.SetActive(false);
            }
        }

        Transform IStoryPlaybackHost.GetPlaybackRoot()
        {
            return transform;
        }

        void IStoryPlaybackHost.OnPlaybackStarted()
        {
            ClearError();
        }

        void IStoryPlaybackHost.OnPlaybackStopped()
        {
            StopPlayback();
        }
    }
}
