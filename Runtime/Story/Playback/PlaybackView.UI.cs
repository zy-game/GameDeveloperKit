using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Resource;
using GameDeveloperKit.Timer;
using GameDeveloperKit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Text;

namespace GameDeveloperKit.Story.Playback
{
    /// <summary>
    /// 基于 UGUI 和 AVProVideo 的 Story 播放窗口。
    /// </summary>
    [UIOption("Assets/Bundles/Playback/PlaybackView.prefab", 500 /* UILayer.StoryPlayback */, CacheEnabled = false)]
    public partial class PlaybackView : UIWindow, IFramePresenter, IPlaybackHost
    {
        private ITextResolver m_TextResolver;
        private bool m_UseAppModules = true;
        private Transform m_PlaybackRoot;
        private RawImage m_VideoOutput;
        private RawImage m_ImageOutput;
        private RectTransform m_VideoSeekRoot;
        private Slider m_VideoSeekSlider;
        private TMP_Text m_VideoSeekTimeText;
        private Button m_VideoSeekPauseButton;
        private RectTransform m_VideoQualityRoot;
        private Button m_VideoQualityButton;
        private TMP_Text m_VideoQualityText;
        private RectTransform m_VideoQualityMenuRoot;
        private RectTransform m_VideoQualityOptionsRoot;
        private Button m_VideoQualityOptionTemplate;
        private bool m_ClearVideoWhenIdle = true;
        private TMP_Text m_SpeakerText;
        private TMP_Text m_BodyText;
        private TMP_Text m_ErrorText;
        private string m_CompletedText = "剧情已结束";
        private RectTransform m_DialogueRoot;
        private Button m_ContinueButton;
        private Transform m_ChoiceRoot;
        private readonly List<Button> m_DefaultChoiceButtons = new List<Button>();

        private readonly List<Button> m_BoundChoiceButtons = new List<Button>();
        private readonly StringBuilder m_TextBuilder = new StringBuilder();

        private StoryModule m_StoryModule;
        private StoryModule m_StoryEventSource;
        private ResourceModule m_ResourceModule;
        private PlayableModule m_PlayableModule;
        private Presenter m_Presenter;
        private MediaCommandHandler m_StoryPlayable;
        private VideoPlayable m_VideoPlayable;
        private string m_VideoLookaheadPath;
        private string m_VideoLookaheadSourceCommandId;
        private readonly HashSet<string> m_ChoiceVideoLookaheadPaths =
            new HashSet<string>(StringComparer.Ordinal);
        private string m_SelectedChoiceVideoLookaheadPath;
        private DefaultInteractionChannel m_DefaultInteractionChannel;
        private IInteractionChannel m_InteractionChannelOverride;
        private IInteractionChannel m_ActiveInteractionChannel;
        private RawImage m_CurrentVideoOutput;
        private RawImage m_RetainedVideoOutput;
        private RawImage m_CurrentImageOutput;
        private VideoSeekSurface m_CurrentVideoSeek;
        private VideoQualitySurface m_CurrentVideoQuality;
        private PlaybackSurfaceView m_CustomPlaybackSurface;
        private bool m_UseDefaultPlaybackSurface;
        private VideoSeekBinder m_VideoSeekBinder;
        private VideoQualityBinder m_VideoQualityBinder;
        private CancellationTokenSource m_PlaybackCancellation;
        private Frame m_CurrentFrame;
        private Episode m_CurrentEpisode;
        private string m_ActiveStoryId;
        private Button m_BoundContinueButton;
        private bool m_FirstVideoFrameReported;
        private bool m_VideoTransitionPending;
        private UpdateTimerHandle m_UpdateHandle;

        private const int DefaultCanvasSortingOrder = 1000;
        private const float MinimumVisibleScale = 0.0001f;
        private static readonly Vector2 s_DefaultReferenceResolution = new Vector2(1920f, 1080f);
        private static readonly Rect s_DefaultVideoUvRect = new Rect(0f, 0f, 1f, 1f);

        /// <summary>
        /// 当前 Story 表现协调器。
        /// </summary>
        public Presenter Presenter => m_Presenter;

        /// <summary>
        /// 当前帧。
        /// </summary>
        public Frame CurrentFrame => m_CurrentFrame;

        /// <summary>
        /// 最近一次播放错误。
        /// </summary>
        public Exception LastError { get; private set; }

        /// <summary>
        /// 当前播放会话中第一个视频首帧就绪。
        /// </summary>
        public event Action<VideoPlayableHandle> FirstVideoFrameReady;

        /// <summary>
        /// 剧情段完成时通知业务播放窗口。
        /// </summary>
        /// <param name="completion">剧情段完成信息。</param>
        protected virtual void OnEpisodeCompleted(EpisodeCompletion completion)
        {
        }

        /// <summary>
        /// 剧情段切换时通知业务播放窗口。
        /// </summary>
        /// <param name="context">剧情段切换上下文。</param>
        protected virtual void OnEpisodeChanged(EpisodeInteractionContext context)
        {
        }

        /// <summary>
        /// 新的视频开始显示首帧时通知业务播放窗口。
        /// </summary>
        /// <param name="playback">视频播放句柄。</param>
        protected virtual void OnVideoPlaybackStarted(VideoPlayableHandle playback)
        {
        }

        /// <summary>
        /// 设置当前播放视图使用的交互通道。
        /// </summary>
        /// <param name="channel">交互通道。</param>
        public void SetInteractionChannel(IInteractionChannel channel)
        {
            m_InteractionChannelOverride = channel;
            m_ActiveInteractionChannel = null;
        }

        public void SetTextResolver(ITextResolver resolver)
        {
            m_TextResolver = resolver;
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
                GetVideoSeekSurface(),
                GetVideoQualitySurface());
        }

        internal IReadOnlyList<Button> DefaultChoiceButtons => m_DefaultChoiceButtons;

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
        /// <param name="audioPlayable">音频播放器。</param>
        public void ConfigureModules(
            StoryModule storyModule,
            ResourceModule resourceModule = null,
            AudioPlayable audioPlayable = null)
        {
            m_StoryModule = storyModule ?? throw new ArgumentNullException(nameof(storyModule));
            m_ResourceModule = resourceModule;
            m_PlayableModule = App.Playable;
            m_UseAppModules = false;
            RebuildPresenter();
        }

        /// <summary>
        /// 播放传入的剧情程序。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="volumeId">卷 ID。</param>
        /// <param name="episodeId">剧情段 ID。</param>
        public void Play(Program program, string volumeId, string episodeId)
        {
            if (program == null)
            {
                throw new ArgumentNullException(nameof(program));
            }

            m_FirstVideoFrameReported = false;
            PlayAsync(program, volumeId, episodeId).Forget(Debug.LogException);
        }

        /// <summary>
        /// 异步播放传入的剧情程序。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="volumeId">卷 ID。</param>
        /// <param name="episodeId">剧情段 ID。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>播放启动任务。</returns>
        public UniTask PlayAsync(
            Program program,
            string volumeId,
            string episodeId,
            CancellationToken cancellationToken = default)
        {
            if (program == null)
            {
                throw new ArgumentNullException(nameof(program));
            }

            m_FirstVideoFrameReported = false;
            return ExecutePlaybackAsync(
                presenter => presenter.Start(program, volumeId, episodeId),
                program.StoryId,
                program,
                volumeId,
                episodeId,
                cancellationToken);
        }

        /// <summary>
        /// 播放已注册的剧情程序。
        /// </summary>
        /// <param name="storyId">剧情 ID。</param>
        /// <param name="volumeId">卷 ID。</param>
        /// <param name="episodeId">剧情段 ID。</param>
        public void PlayRegistered(string storyId, string volumeId, string episodeId)
        {
            if (string.IsNullOrWhiteSpace(storyId))
            {
                throw new ArgumentException("Story id cannot be empty.", nameof(storyId));
            }

            m_FirstVideoFrameReported = false;
            PlayRegisteredAsync(storyId, volumeId, episodeId).Forget(Debug.LogException);
        }

        /// <summary>
        /// 异步播放已注册的剧情程序。
        /// </summary>
        /// <param name="storyId">剧情 ID。</param>
        /// <param name="volumeId">卷 ID。</param>
        /// <param name="episodeId">剧情段 ID。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>播放启动任务。</returns>
        public UniTask PlayRegisteredAsync(
            string storyId,
            string volumeId,
            string episodeId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(storyId))
            {
                throw new ArgumentException("Story id cannot be empty.", nameof(storyId));
            }

            m_FirstVideoFrameReported = false;
            var program = ResolveRegisteredProgram(storyId);
            return ExecutePlaybackAsync(
                presenter => presenter.StartEpisode(storyId, volumeId, episodeId),
                storyId,
                program,
                volumeId,
                episodeId,
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
            m_ActiveStoryId = null;
            m_ActiveInteractionChannel?.OnStoryStopped();
            if (m_Presenter != null)
            {
                m_Presenter.Stop();
            }

            ClearMediaOutput(m_CurrentVideoOutput);
            ClearRetainedVideoOutput();
            m_CurrentFrame = null;
            m_CurrentEpisode = null;
            m_CurrentVideoOutput = null;
            m_CurrentImageOutput = null;
            m_CurrentVideoSeek = null;
            m_CurrentVideoQuality = null;
            m_CustomPlaybackSurface = null;
            m_UseDefaultPlaybackSurface = false;
            m_ActiveInteractionChannel = null;
            m_VideoSeekBinder?.Unbind();
            m_VideoQualityBinder?.Unbind();
            ClearBoundInputs();
            m_DefaultInteractionChannel?.ClearTransientInputs();
            HideDefaultMediaOutputs();
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
            PrepareChoiceVideoSelection(choiceId);
            ExecutePlayback(() => EnsurePresenter().Select(choiceId));
        }

        /// <inheritdoc />
        public void Present(Frame frame, Presenter presenter)
        {
            m_CurrentFrame = frame;
            RenderFrame(frame);
        }

        /// <inheritdoc />
        public void Clear(Frame frame)
        {
            ClearOperations();
        }

        /// <inheritdoc />
        public override UniTask OnAwakeAsync()
        {
            BindDocument();
            EnsureRenderableCanvas();
            ShowInitialVideoPlaceholder();
            EnsureDefaultInteractionChannel();
            for (var i = 0; i < m_DefaultChoiceButtons.Count; i++)
            {
                m_DefaultChoiceButtons[i].gameObject.SetActive(false);
            }

            SetDefaultDialogueVisible(false);
            ClearError();
            RegisterUpdate();
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public override void OnEnable()
        {
            RegisterUpdate();
            if (m_UpdateHandle != null)
            {
                m_UpdateHandle.Enabled = true;
            }
        }

        /// <inheritdoc />
        public override void OnDisable()
        {
            if (m_UpdateHandle != null)
            {
                m_UpdateHandle.Enabled = false;
            }
        }

        private void EnsureRenderableCanvas()
        {
            var windowTransform = GameObject.transform;
            RestoreVisibleScale(windowTransform);

            var canvas = GameObject.GetComponentInParent<Canvas>(true);
            if (canvas == null)
            {
                canvas = GameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = DefaultCanvasSortingOrder;
            }
            else if (canvas.transform == windowTransform &&
                     canvas.renderMode == RenderMode.ScreenSpaceCamera &&
                     canvas.worldCamera == null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            if (canvas.transform == windowTransform)
            {
                EnsureCanvasScaler(GameObject);
                EnsureGraphicRaycaster(GameObject);
            }

            EnsureReferenceCanvas(m_VideoOutput);
            EnsureReferenceCanvas(m_ImageOutput);
            EnsureReferenceCanvas(m_SpeakerText);
            EnsureReferenceCanvas(m_BodyText);
            EnsureReferenceCanvas(m_ErrorText);
            EnsureReferenceCanvas(m_ContinueButton);
            for (var i = 0; i < m_DefaultChoiceButtons.Count; i++)
            {
                EnsureReferenceCanvas(m_DefaultChoiceButtons[i]);
            }
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
            if (target.IsChildOf(GameObject.transform))
            {
                return GameObject.transform;
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

        private void UpdatePlayback(TimerUpdateContext context)
        {
            if (m_Presenter == null)
            {
                return;
            }

            UpdateVideoOutput();
            m_VideoSeekBinder?.Refresh();
            m_ActiveInteractionChannel?.Tick(context.DeltaTime);
            if (m_CurrentFrame != null && m_CurrentFrame.WaitsForTime && m_CurrentFrame.IsCompleted is false)
            {
                ExecutePlayback(() => m_Presenter.Evaluate(context.DeltaTime));
            }

            if (m_Presenter.LastError != null && !ReferenceEquals(LastError, m_Presenter.LastError))
            {
                SetError(m_Presenter.LastError);
            }
        }

        /// <inheritdoc />
        public override void Release()
        {
            CancelPlaybackSession();
            DisposePresenter();
            m_DefaultInteractionChannel?.Dispose();
            m_DefaultInteractionChannel = null;
            m_UpdateHandle?.Cancel();
            m_UpdateHandle = null;
            base.Release();
        }

        private Presenter EnsurePresenter()
        {
            if (m_Presenter != null)
            {
                return m_Presenter;
            }

            ResolveModules();
            SubscribeStoryEvents();
            m_Presenter = new Presenter(m_StoryModule, this);
            m_StoryPlayable = new MediaCommandHandler(
                m_PlayableModule,
                ResolveImageOutput,
                m_PlaybackRoot != null ? m_PlaybackRoot : GameObject.transform);
            m_VideoPlayable = m_StoryPlayable.Video;
            m_VideoPlayable.PlaybackStarted += HandleVideoPlaybackStarted;
            m_Presenter.AddCommandHandler(m_StoryPlayable);
            m_Presenter.AddCommandHandler(new LogicCommandHandler());
            return m_Presenter;
        }

        internal string ResolveText(TextReference reference)
        {
            m_TextResolver ??= new LocalizationTextResolver();
            return m_TextResolver.Resolve(reference);
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

                m_PlayableModule ??= App.Playable;
            }
        }

        private void RebuildPresenter()
        {
            DisposePresenter();
            if (GameObject != null && GameObject.activeInHierarchy)
            {
                EnsurePresenter();
            }
        }

        private void DisposePresenter()
        {
            UnsubscribeStoryEvents();
            if (m_Presenter != null)
            {
                m_Presenter.Dispose();
                m_Presenter = null;
            }

            if (m_StoryPlayable != null)
            {
                if (m_VideoPlayable != null)
                {
                    m_VideoPlayable.PlaybackStarted -= HandleVideoPlaybackStarted;
                    m_VideoPlayable = null;
                }

                m_StoryPlayable.Dispose();
                m_StoryPlayable = null;
            }
            ClearMediaOutput(m_CurrentVideoOutput);
            ClearRetainedVideoOutput();
            m_CurrentFrame = null;
            m_CurrentVideoOutput = null;
            m_CurrentImageOutput = null;
            m_CurrentVideoSeek = null;
            m_CurrentVideoQuality = null;
            m_CustomPlaybackSurface = null;
            m_UseDefaultPlaybackSurface = false;
            m_CurrentEpisode = null;
            m_ActiveStoryId = null;
            m_FirstVideoFrameReported = false;
            m_VideoSeekBinder?.Unbind();
            m_VideoQualityBinder?.Unbind();
        }

        private Program ResolveRegisteredProgram(string storyId)
        {
            ResolveModules();
            return m_StoryModule.TryGetProgram(storyId, out var program) ? program : null;
        }

        private void ExecutePlayback(Func<Frame> playback)
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
            Func<Presenter, Frame> playback,
            string storyId,
            Program program,
            string volumeId,
            string episodeId,
            CancellationToken cancellationToken)
        {
            CancelPlaybackSession();
            m_PlaybackCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var sessionToken = m_PlaybackCancellation.Token;
            try
            {
                LastError = null;
                ClearError();
                m_CurrentEpisode = null;
                m_ActiveStoryId = null;
                m_ActiveInteractionChannel = null;
                var presenter = EnsurePresenter();
                var channel = ResolveInteractionChannel();
                var context = new InteractionContext(m_StoryModule, presenter, storyId, program);
                await OnPlaybackAwakeAsync(context, sessionToken);
                sessionToken.ThrowIfCancellationRequested();
                await channel.OnAwake(context, sessionToken);
                sessionToken.ThrowIfCancellationRequested();
                await PrewarmPlaybackAsync(storyId, program, volumeId, episodeId, sessionToken);
                sessionToken.ThrowIfCancellationRequested();
                m_ActiveStoryId = storyId;
                channel.OnStoryStarted(context);
                var frame = playback(presenter);
                if (!ReferenceEquals(m_CurrentFrame, frame))
                {
                    m_CurrentFrame = frame;
                    RenderFrame(frame);
                }
            }
            catch (OperationCanceledException exception)
            {
                if (!sessionToken.IsCancellationRequested)
                {
                    SetError(exception);
                }
            }
            catch (Exception exception)
            {
                SetError(exception);
            }
        }

        private void SubscribeStoryEvents()
        {
            if (ReferenceEquals(m_StoryEventSource, m_StoryModule))
            {
                return;
            }

            UnsubscribeStoryEvents();
            m_StoryEventSource = m_StoryModule;
            if (m_StoryEventSource != null)
            {
                m_StoryEventSource.EpisodeCompleted += HandleEpisodeCompleted;
            }
        }

        private void UnsubscribeStoryEvents()
        {
            if (m_StoryEventSource == null)
            {
                return;
            }

            m_StoryEventSource.EpisodeCompleted -= HandleEpisodeCompleted;
            m_StoryEventSource = null;
        }

        private void HandleEpisodeCompleted(EpisodeCompletion completion)
        {
            if (completion == null ||
                string.Equals(completion.StoryId, m_ActiveStoryId, StringComparison.Ordinal) is false)
            {
                return;
            }

            OnEpisodeCompleted(completion);
        }

        private void CancelPlaybackSession()
        {
            var cancellation = m_PlaybackCancellation;
            m_PlaybackCancellation = null;
            cancellation?.Cancel();
            ReleaseVideoLookahead();
            ReleaseChoiceVideoLookaheads();
            cancellation?.Dispose();
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

        Transform IPlaybackHost.GetPlaybackRoot()
        {
            return GameObject.transform;
        }

        void IPlaybackHost.OnPlaybackStarted()
        {
            ClearError();
        }

        void IPlaybackHost.OnPlaybackStopped()
        {
            StopPlayback();
        }

        private void BindDocument()
        {
            if (Document == null)
            {
                throw new GameException("Playback view requires a UIDocument.");
            }

            m_PlaybackRoot = Document.GetComponent<Transform>("PlaybackRoot");
            m_VideoOutput = Document.GetComponent<RawImage>("VideoOutput");
            m_ImageOutput = Document.GetComponent<RawImage>("ImageOutput");
            m_VideoSeekRoot = Document.GetComponent<RectTransform>("VideoSeekRoot");
            m_VideoSeekSlider = Document.GetComponent<Slider>("VideoSeekSlider");
            m_VideoSeekTimeText = Document.GetComponent<TMP_Text>("VideoSeekTimeText");
            m_VideoSeekPauseButton = Document.GetComponent<Button>("VideoSeekPauseButton");
            m_VideoQualityRoot = Document.GetComponent<RectTransform>("VideoQualityRoot");
            m_VideoQualityButton = Document.GetComponent<Button>("VideoQualityButton");
            m_VideoQualityText = Document.GetComponent<TMP_Text>("VideoQualityText");
            Document.TryGetComponent("VideoQualityMenuRoot", out m_VideoQualityMenuRoot);
            Document.TryGetComponent("VideoQualityOptionsRoot", out m_VideoQualityOptionsRoot);
            Document.TryGetComponent("VideoQualityOptionTemplate", out m_VideoQualityOptionTemplate);
            m_SpeakerText = Document.GetComponent<TMP_Text>("SpeakerText");
            m_BodyText = Document.GetComponent<TMP_Text>("BodyText");
            m_ErrorText = Document.GetComponent<TMP_Text>("ErrorText");
            if (Document.TryGetComponent("DialogueRoot", out RectTransform dialogueRoot))
            {
                m_DialogueRoot = dialogueRoot;
            }
            else
            {
                m_DialogueRoot = m_BodyText != null ? m_BodyText.transform.parent as RectTransform : null;
            }
            m_ContinueButton = Document.GetComponent<Button>("ContinueButton");
            m_ChoiceRoot = Document.GetComponent<Transform>("ChoiceRoot");
            m_DefaultChoiceButtons.Clear();
            if (m_ChoiceRoot != null)
            {
                m_DefaultChoiceButtons.AddRange(m_ChoiceRoot.GetComponentsInChildren<Button>(true));
            }
        }

        private void RegisterUpdate()
        {
            if (m_UpdateHandle != null &&
                m_UpdateHandle.IsCancelled is false &&
                m_UpdateHandle.IsCompleted is false)
            {
                return;
            }

            if (App.TryGetRegistered<TimerModule>(out var timer))
            {
                m_UpdateHandle = timer.OnUpdate(UpdatePlayback, this, nameof(PlaybackView));
            }
        }

    }
}
