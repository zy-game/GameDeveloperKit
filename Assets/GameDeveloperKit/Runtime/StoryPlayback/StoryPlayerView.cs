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
    public sealed class StoryPlayerView : MonoBehaviour, IStoryFramePresenter
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
        /// 创建默认 Story 播放视图。
        /// </summary>
        /// <param name="parent">父节点。</param>
        /// <returns>Story 播放视图。</returns>
        public static StoryPlayerView CreateDefault(Transform parent = null)
        {
            var rootObject = new GameObject("StoryPlayerView", typeof(RectTransform));
            rootObject.SetActive(false);

            var root = (RectTransform)rootObject.transform;
            root.SetParent(parent, false);
            Stretch(root, 0f, 0f, 0f, 0f);

            var view = rootObject.AddComponent<StoryPlayerView>();
            var playbackRoot = new GameObject("PlaybackRoot").transform;
            playbackRoot.SetParent(root, false);

            var mediaLayer = CreateRect(root, "MediaLayer", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            Stretch(mediaLayer, 0f, 0f, 0f, 0f);

            var imageOutput = CreateRawImage(mediaLayer, "ImageOutput", Color.white);
            Stretch(imageOutput.rectTransform, 0f, 0f, 0f, 0f);
            imageOutput.gameObject.SetActive(false);

            var videoOutput = CreateRawImage(mediaLayer, "VideoOutput", Color.white);
            Stretch(videoOutput.rectTransform, 0f, 0f, 0f, 0f);
            videoOutput.gameObject.SetActive(false);

            var dialoguePanel = CreatePanel(root, "DialoguePanel", new Color(0.04f, 0.05f, 0.06f, 0.86f));
            Anchor(dialoguePanel.rectTransform, new Vector2(0.05f, 0f), new Vector2(0.95f, 0f), new Vector2(0.5f, 0f));
            dialoguePanel.rectTransform.sizeDelta = new Vector2(0f, 220f);
            dialoguePanel.rectTransform.anchoredPosition = new Vector2(0f, 36f);

            var speakerText = CreateText(dialoguePanel.transform, "SpeakerText", "旁白", 26f, FontStyles.Bold, new Color(0.95f, 0.86f, 0.62f, 1f));
            Anchor(speakerText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            speakerText.rectTransform.sizeDelta = new Vector2(300f, 42f);
            speakerText.rectTransform.anchoredPosition = new Vector2(28f, -22f);

            var bodyText = CreateText(dialoguePanel.transform, "BodyText", "剧情文本", 28f, FontStyles.Normal, new Color(0.94f, 0.95f, 0.96f, 1f));
            Anchor(bodyText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            bodyText.rectTransform.offsetMin = new Vector2(28f, 70f);
            bodyText.rectTransform.offsetMax = new Vector2(-28f, -64f);
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.enableWordWrapping = true;
            bodyText.overflowMode = TextOverflowModes.Overflow;

            var continueButton = CreateButton(dialoguePanel.transform, "ContinueButton", "继续", new Color(0.17f, 0.22f, 0.28f, 0.96f));
            var continueRect = continueButton.GetComponent<RectTransform>();
            Anchor(continueRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            continueRect.sizeDelta = new Vector2(140f, 42f);
            continueRect.anchoredPosition = new Vector2(-28f, 22f);

            var choiceRoot = CreateRect(dialoguePanel.transform, "ChoiceRoot", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f));
            choiceRoot.sizeDelta = new Vector2(0f, 52f);
            choiceRoot.offsetMin = new Vector2(28f, 16f);
            choiceRoot.offsetMax = new Vector2(-184f, 68f);
            var choiceLayout = choiceRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            choiceLayout.spacing = 12f;
            choiceLayout.childAlignment = TextAnchor.MiddleLeft;
            choiceLayout.childControlWidth = false;
            choiceLayout.childControlHeight = true;
            choiceLayout.childForceExpandWidth = false;
            choiceLayout.childForceExpandHeight = true;

            var choiceButtonTemplate = CreateButton(choiceRoot.transform, "ChoiceButtonTemplate", "选项", new Color(0.22f, 0.28f, 0.36f, 0.96f));
            choiceButtonTemplate.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 44f);
            var choiceLayoutElement = choiceButtonTemplate.gameObject.AddComponent<LayoutElement>();
            choiceLayoutElement.preferredWidth = 220f;
            choiceLayoutElement.preferredHeight = 44f;
            choiceButtonTemplate.gameObject.SetActive(false);

            var errorText = CreateText(root, "ErrorText", string.Empty, 20f, FontStyles.Normal, new Color(1f, 0.42f, 0.38f, 1f));
            Anchor(errorText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            errorText.rectTransform.sizeDelta = new Vector2(0f, 48f);
            errorText.rectTransform.offsetMin = new Vector2(24f, -68f);
            errorText.rectTransform.offsetMax = new Vector2(-24f, -20f);
            errorText.alignment = TextAlignmentOptions.TopLeft;
            errorText.gameObject.SetActive(false);

            view.m_PlaybackRoot = playbackRoot;
            view.m_VideoOutput = videoOutput;
            view.m_ImageOutput = imageOutput;
            view.m_SpeakerText = speakerText;
            view.m_BodyText = bodyText;
            view.m_ErrorText = errorText;
            view.m_ContinueButton = continueButton;
            view.m_ChoiceRoot = choiceRoot;
            view.m_ChoiceButtonTemplate = choiceButtonTemplate;
            view.EnsureDefaultVideoSeekSurface();

            rootObject.SetActive(true);
            return view;
        }

        private static Image CreatePanel(Transform parent, string name, Color color)
        {
            var rect = CreateRect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static RawImage CreateRawImage(Transform parent, string name, Color color)
        {
            var rect = CreateRect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            var rawImage = rect.gameObject.AddComponent<RawImage>();
            rawImage.color = color;
            rawImage.raycastTarget = false;
            return rawImage;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            FontStyles fontStyle,
            Color color)
        {
            var rect = CreateRect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            var font = GetDefaultTextFont();
            if (font != null)
            {
                label.font = font;
            }

            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = TextAlignmentOptions.Left;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(Transform parent, string name, string text, Color color)
        {
            var rect = CreateRect(parent, name, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var label = CreateText(rect.transform, "Label", text, 22f, FontStyles.Bold, Color.white);
            Stretch(label.rectTransform, 14f, 8f, 14f, 8f);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static Slider CreateSlider(Transform parent, string name)
        {
            var rect = CreateRect(parent, name, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
            rect.sizeDelta = new Vector2(0f, 28f);

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;

            var background = CreatePanel(rect, "Background", new Color(0.1f, 0.12f, 0.14f, 0.95f));
            Stretch(background.rectTransform, 0f, 10f, 0f, 10f);

            var fillArea = CreateRect(rect, "Fill Area", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            Stretch(fillArea, 4f, 10f, 4f, 10f);

            var fill = CreatePanel(fillArea, "Fill", new Color(0.18f, 0.62f, 0.82f, 1f));
            Stretch(fill.rectTransform, 0f, 0f, 0f, 0f);

            var handleArea = CreateRect(rect, "Handle Slide Area", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            Stretch(handleArea, 4f, 0f, 4f, 0f);

            var handle = CreatePanel(handleArea, "Handle", new Color(0.94f, 0.95f, 0.96f, 1f));
            handle.rectTransform.sizeDelta = new Vector2(18f, 28f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            return slider;
        }

        private static TMP_FontAsset GetDefaultTextFont()
        {
            if (s_DefaultTextFont == null)
            {
                s_DefaultTextFont = Resources.Load<TMP_FontAsset>(DefaultTextFontResourcePath);
            }

            return s_DefaultTextFont;
        }

        private static RectTransform CreateRect(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = (RectTransform)gameObject.transform;
            Anchor(rect, anchorMin, anchorMax, pivot);
            rect.anchoredPosition = anchoredPosition;
            return rect;
        }

        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            Anchor(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
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

        private async UniTask PrewarmPlaybackAsync(
            string storyId,
            StoryProgram program,
            string chapterId,
            CancellationToken cancellationToken)
        {
            if (program == null)
            {
                throw new GameException($"Story program is not registered. story:{storyId}");
            }

            var previewRunner = new StoryRunner(program, m_StoryModule.FunctionResolver);
            var frame = previewRunner.Start(chapterId);
            if (frame?.Tracks == null || m_VideoPlayer == null)
            {
                return;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                var command = track?.Command;
                if (track?.Kind != StoryFrameTrackKind.Command ||
                    command == null ||
                    string.Equals(command.Name, StoryMediaCommandNames.PlayVideo, StringComparison.Ordinal) is false)
                {
                    continue;
                }

                var handle = await m_VideoPlayer.PreloadVideoAsync(command, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (handle?.Error != null)
                {
                    throw handle.Error;
                }
            }
        }

        private void RenderFrame(StoryFrame frame)
        {
            var channel = ResolveInteractionChannel();
            NotifyChapterChanged(channel, frame);
            ClearBoundInputs();
            channel.OnFrameChanged(frame);
            RenderTextSurface(channel, frame);
            BindContinueSurface(channel, frame);
            BindChoiceSurface(channel, frame);
            UpdateMediaSurfaces(channel, frame);
            UpdateCustomSurfaces(channel, frame);
        }

        private void ClearFrameUi()
        {
            ClearBoundInputs();
            var channel = ResolveInteractionChannel();
            channel.OnFrameChanged(null);
            RenderTextSurface(channel, null);
            BindContinueSurface(channel, null);
        }

        private void NotifyChapterChanged(IInteractionChannel channel, StoryFrame frame)
        {
            var nextChapter = frame?.Chapter;
            if (ReferenceEquals(m_CurrentChapter, nextChapter))
            {
                return;
            }

            var previousChapter = m_CurrentChapter;
            m_CurrentChapter = nextChapter;
            if (nextChapter == null)
            {
                return;
            }

            channel.OnChapterChanged(new ChapterInteractionContext(
                m_StoryModule,
                m_Presenter,
                frame.Program?.StoryId,
                frame.Program,
                previousChapter,
                nextChapter,
                frame));
        }

        private void UpdateMediaSurfaces(IInteractionChannel channel, StoryFrame frame)
        {
            m_CurrentVideoOutput = null;
            m_CurrentImageOutput = null;
            m_CurrentVideoSeek = null;
            if (frame?.Tracks == null)
            {
                m_VideoSeekBinder?.Unbind();
                return;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                var command = track?.Command;
                if (track?.Kind != StoryFrameTrackKind.Command || command == null)
                {
                    continue;
                }

                if (string.Equals(command.Name, StoryMediaCommandNames.PlayVideo, StringComparison.Ordinal))
                {
                    var surface = RequireSurface(channel, new InteractionRequest(
                        InteractionRequestKind.Video,
                        frame,
                        track,
                        command,
                        frame.Choices));
                    if (surface.VideoOutput == null)
                    {
                        throw new GameException($"Story video output surface is missing. command:{command.CommandId}");
                    }

                    m_CurrentVideoOutput = surface.VideoOutput;
                    m_CurrentVideoSeek = surface.VideoSeek;
                }
                else if (string.Equals(command.Name, StoryMediaCommandNames.ShowImage, StringComparison.Ordinal))
                {
                    var surface = RequireSurface(channel, new InteractionRequest(
                        InteractionRequestKind.Image,
                        frame,
                        track,
                        command,
                        frame.Choices));
                    if (surface.ImageOutput == null)
                    {
                        throw new GameException($"Story image output surface is missing. command:{command.CommandId}");
                    }

                    m_CurrentImageOutput = surface.ImageOutput;
                }
            }
        }

        private void UpdateCustomSurfaces(IInteractionChannel channel, StoryFrame frame)
        {
            m_CurrentCustomRoot = null;
            if (frame?.Tracks == null)
            {
                return;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                var command = track?.Command;
                if (track?.Kind != StoryFrameTrackKind.Command ||
                    command == null ||
                    IsCustomInteractionCommand(command) is false)
                {
                    continue;
                }

                var surface = RequireSurface(channel, new InteractionRequest(
                    InteractionRequestKind.Custom,
                    frame,
                    track,
                    command,
                    frame.Choices));
                if (surface.CustomRoot == null)
                {
                    throw new GameException($"Story custom root surface is missing. command:{command.CommandId}");
                }

                m_CurrentCustomRoot = surface.CustomRoot;
            }
        }

        private void RenderTextSurface(IInteractionChannel channel, StoryFrame frame)
        {
            var surface = RequireSurface(channel, new InteractionRequest(
                InteractionRequestKind.Text,
                frame,
                choices: frame?.Choices));
            var speakerText = surface.SpeakerText;
            var bodyText = surface.BodyText;

            if (frame == null)
            {
                SetSpeakerText(speakerText, null);
                SetBodyText(bodyText, null);
                return;
            }

            m_TextBuilder.Length = 0;
            string speaker = null;
            if (frame.Tracks != null)
            {
                for (var i = 0; i < frame.Tracks.Count; i++)
                {
                    var track = frame.Tracks[i];
                    if (track?.Kind != StoryFrameTrackKind.Text)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(speaker))
                    {
                        speaker = track.Speaker;
                    }

                    if (m_TextBuilder.Length > 0)
                    {
                        m_TextBuilder.AppendLine();
                    }

                    if (string.IsNullOrWhiteSpace(track.BranchLabel) is false)
                    {
                        m_TextBuilder.Append('[');
                        m_TextBuilder.Append(track.BranchLabel);
                        m_TextBuilder.Append("] ");
                    }

                    m_TextBuilder.Append(track.TextKey);
                }
            }

            SetSpeakerText(speakerText, speaker);
            SetBodyText(bodyText, frame.IsCompleted ? m_CompletedText : m_TextBuilder.ToString());
        }

        private void BindContinueSurface(IInteractionChannel channel, StoryFrame frame)
        {
            var visible = frame != null &&
                          frame.IsCompleted is false &&
                          frame.WaitsForChoice is false &&
                          frame.WaitsForCommand is false &&
                          frame.WaitsForTime is false;
            if (!visible)
            {
                SetContinueVisible(m_BoundContinueButton, false);
                UnbindContinueButton();
                return;
            }

            var surface = RequireSurface(channel, new InteractionRequest(
                InteractionRequestKind.Continue,
                frame,
                choices: frame.Choices));
            if (surface.ContinueButton == null)
            {
                throw new GameException("Story continue button surface is missing.");
            }

            if (!ReferenceEquals(m_BoundContinueButton, surface.ContinueButton))
            {
                UnbindContinueButton();
                m_BoundContinueButton = surface.ContinueButton;
            }

            m_BoundContinueButton.onClick.RemoveListener(ContinueFromInteraction);
            m_BoundContinueButton.onClick.AddListener(ContinueFromInteraction);
            SetContinueVisible(m_BoundContinueButton, true);
        }

        private void BindChoiceSurface(IInteractionChannel channel, StoryFrame frame)
        {
            if (frame?.Choices == null || frame.Choices.Count == 0)
            {
                return;
            }

            var surface = RequireSurface(channel, new InteractionRequest(
                InteractionRequestKind.Choice,
                frame,
                choices: frame.Choices));
            if (surface.ChoiceButtons == null || surface.ChoiceButtons.Count != frame.Choices.Count)
            {
                throw new GameException(
                    $"Story choice button count does not match choices. choices:{frame.Choices.Count} buttons:{surface.ChoiceButtons?.Count ?? 0}");
            }

            for (var i = 0; i < frame.Choices.Count; i++)
            {
                var button = surface.ChoiceButtons[i];
                var choice = frame.Choices[i];
                if (button == null || choice == null)
                {
                    throw new GameException($"Story choice button surface is missing. index:{i}");
                }

                var choiceId = choice.ChoiceId;
                button.gameObject.SetActive(true);
                SetButtonText(button, choice.TextKey);
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectFromInteraction(choiceId));
                m_BoundChoiceButtons.Add(button);
            }
        }

        private void ClearBoundInputs()
        {
            UnbindContinueButton();
            for (var i = 0; i < m_BoundChoiceButtons.Count; i++)
            {
                var button = m_BoundChoiceButtons[i];
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                }
            }

            m_BoundChoiceButtons.Clear();
        }

        private void UnbindContinueButton()
        {
            if (m_BoundContinueButton != null)
            {
                SetContinueVisible(m_BoundContinueButton, false);
                m_BoundContinueButton.onClick.RemoveListener(ContinueFromInteraction);
                m_BoundContinueButton = null;
            }
        }

        private static void SetContinueVisible(Button button, bool visible)
        {
            if (button != null)
            {
                button.gameObject.SetActive(visible);
            }
        }

        private static void SetSpeakerText(TMP_Text text, string value)
        {
            if (text == null)
            {
                return;
            }

            text.text = value ?? string.Empty;
            text.gameObject.SetActive(string.IsNullOrWhiteSpace(value) is false);
        }

        private static void SetBodyText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        private static void SetButtonText(Button button, string value)
        {
            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = value ?? string.Empty;
                return;
            }

            var legacyText = button.GetComponentInChildren<Text>(true);
            if (legacyText != null)
            {
                legacyText.text = value ?? string.Empty;
            }
        }

        private static PlaybackSurfaceView RequireSurface(IInteractionChannel channel, InteractionRequest request)
        {
            var surface = channel.GetPlaybackSurfaceView(request);
            if (surface == null)
            {
                throw new GameException($"Story playback surface is missing. kind:{request.Kind}");
            }

            return surface;
        }

        private static bool IsCustomInteractionCommand(StoryCommand command)
        {
            return command != null &&
                   (string.Equals(command.Name, StoryInteractionCommandNames.Qte, StringComparison.Ordinal) ||
                    string.Equals(command.Name, StoryInteractionCommandNames.Unlock, StringComparison.Ordinal));
        }

        private void OnVideoPlaybackStarted(StoryAvProVideoPlayback playback)
        {
            UpdateVideoOutput();
            if (m_FirstVideoFrameReported || playback == null || playback.HasFirstFrame is false)
            {
                return;
            }

            m_FirstVideoFrameReported = true;
            FirstVideoFrameReady?.Invoke(playback);
        }

        private void UpdateVideoOutput()
        {
            var output = m_CurrentVideoOutput != null ? m_CurrentVideoOutput : m_VideoOutput;
            if (output == null || m_VideoPlayer == null)
            {
                m_VideoSeekBinder?.Unbind();
                return;
            }

            var playbacks = m_VideoPlayer.ActivePlaybacks;
            for (var i = playbacks.Count - 1; i >= 0; i--)
            {
                var playback = playbacks[i];
                if (playback == null || playback.HasFirstFrame is false)
                {
                    continue;
                }

                var texture = playback.CurrentTexture;
                if (texture == null)
                {
                    continue;
                }

                output.texture = texture;
                output.uvRect = playback.RequiresVerticalFlip
                    ? s_FlippedVideoUvRect
                    : s_DefaultVideoUvRect;
                output.gameObject.SetActive(true);
                EnsureVideoSeekBinder().Bind(playback.CanShowSeekControls ? m_CurrentVideoSeek : null, playback);
                return;
            }

            if (m_ClearVideoWhenIdle && playbacks.Count == 0)
            {
                output.texture = null;
                output.uvRect = s_DefaultVideoUvRect;
                output.gameObject.SetActive(false);
            }

            m_VideoSeekBinder?.Unbind();
        }

        private IInteractionChannel ResolveInteractionChannel()
        {
            if (m_ActiveInteractionChannel != null)
            {
                return m_ActiveInteractionChannel;
            }

            var registeredChannel = m_StoryModule != null ? m_StoryModule.GetInteractions() : null;
            m_ActiveInteractionChannel = m_InteractionChannelOverride ?? registeredChannel ?? EnsureDefaultInteractionChannel();
            return m_ActiveInteractionChannel;
        }

        private RawImage ResolveImageOutput()
        {
            return m_CurrentImageOutput != null ? m_CurrentImageOutput : m_ImageOutput;
        }

        private VideoSeekSurface GetVideoSeekSurface()
        {
            return m_VideoSeekSlider == null
                ? null
                : new VideoSeekSurface(m_VideoSeekRoot, m_VideoSeekSlider, m_VideoSeekTimeText, m_VideoSeekPauseButton);
        }

        private VideoSeekBinder EnsureVideoSeekBinder()
        {
            if (m_VideoSeekBinder == null)
            {
                m_VideoSeekBinder = new VideoSeekBinder();
            }

            return m_VideoSeekBinder;
        }

        private void EnsureDefaultVideoSeekSurface()
        {
            if (m_VideoSeekSlider != null)
            {
                return;
            }

            var rootPanel = CreatePanel(transform, "VideoSeek", new Color(0.04f, 0.05f, 0.06f, 0.82f));
            var root = rootPanel.rectTransform;
            Anchor(root, new Vector2(0.05f, 0f), new Vector2(0.95f, 0f), new Vector2(0.5f, 0f));
            root.sizeDelta = new Vector2(0f, 56f);
            root.anchoredPosition = new Vector2(0f, 282f);

            var pauseButton = CreateButton(root, "PauseButton", "暂停", new Color(0.18f, 0.24f, 0.30f, 0.96f));
            var pauseButtonRect = pauseButton.GetComponent<RectTransform>();
            Anchor(pauseButtonRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            pauseButtonRect.sizeDelta = new Vector2(84f, 36f);
            pauseButtonRect.anchoredPosition = new Vector2(24f, 0f);

            var slider = CreateSlider(root, "Slider");
            Stretch(slider.GetComponent<RectTransform>(), 120f, 14f, 156f, 14f);

            var timeText = CreateText(root, "TimeText", "00:00 / 00:00", 20f, FontStyles.Normal, new Color(0.94f, 0.95f, 0.96f, 1f));
            Anchor(timeText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            timeText.rectTransform.sizeDelta = new Vector2(128f, 28f);
            timeText.rectTransform.anchoredPosition = new Vector2(-24f, 0f);
            timeText.alignment = TextAlignmentOptions.MidlineRight;

            root.gameObject.SetActive(false);
            m_VideoSeekRoot = root;
            m_VideoSeekSlider = slider;
            m_VideoSeekTimeText = timeText;
            m_VideoSeekPauseButton = pauseButton;
        }

        private DefaultInteractionChannel EnsureDefaultInteractionChannel()
        {
            if (m_DefaultInteractionChannel == null)
            {
                m_DefaultInteractionChannel = new DefaultInteractionChannel(this);
            }

            return m_DefaultInteractionChannel;
        }

        private IUnlockStateProvider ResolveUnlockStateProvider()
        {
            if (m_ActiveInteractionChannel is IUnlockStateProvider channelProvider)
            {
                return channelProvider;
            }

            if (m_DefaultUnlockStateProvider == null)
            {
                m_DefaultUnlockStateProvider = new SessionUnlockStateProvider();
            }

            return m_DefaultUnlockStateProvider;
        }

        private sealed class VideoSeekBinder
        {
            private StoryAvProVideoPlayback m_Playback;
            private VideoSeekSurface m_Surface;
            private bool m_Updating;

            public void Bind(VideoSeekSurface surface, StoryAvProVideoPlayback playback)
            {
                if (surface?.Slider == null || playback == null || playback.CanShowSeekControls is false)
                {
                    Unbind();
                    return;
                }

                if (ReferenceEquals(m_Surface, surface) && ReferenceEquals(m_Playback, playback))
                {
                    SetSurfaceVisible(surface, true);
                    Refresh();
                    return;
                }

                Unbind();
                m_Surface = surface;
                m_Playback = playback;
                m_Surface.Slider.onValueChanged.AddListener(OnSliderValueChanged);
                if (m_Surface.PauseButton != null)
                {
                    m_Surface.PauseButton.onClick.AddListener(OnPauseButtonClicked);
                }

                SetSurfaceVisible(m_Surface, true);
                Refresh();
            }

            public void Refresh()
            {
                if (m_Surface?.Slider == null || m_Playback == null)
                {
                    return;
                }

                if (m_Playback.CanShowSeekControls is false)
                {
                    Unbind();
                    return;
                }

                m_Updating = true;
                m_Surface.Slider.minValue = 0f;
                if (m_Playback.CanSeek)
                {
                    var duration = m_Playback.DurationSeconds;
                    m_Surface.Slider.interactable = true;
                    m_Surface.Slider.maxValue = Mathf.Max(0.001f, (float)duration);
                    m_Surface.Slider.SetValueWithoutNotify((float)Math.Min(m_Playback.CurrentTimeSeconds, duration));
                    if (m_Surface.TimeText != null)
                    {
                        m_Surface.TimeText.text = $"{FormatTime(m_Playback.CurrentTimeSeconds)} / {FormatTime(duration)}";
                    }
                }
                else
                {
                    m_Surface.Slider.interactable = false;
                    m_Surface.Slider.maxValue = 1f;
                    m_Surface.Slider.SetValueWithoutNotify(0f);
                    if (m_Surface.TimeText != null)
                    {
                        m_Surface.TimeText.text = $"{FormatTime(m_Playback.CurrentTimeSeconds)} / --:--";
                    }
                }

                if (m_Surface.TimeText != null)
                {
                    m_Surface.TimeText.gameObject.SetActive(true);
                }

                SetPauseButtonText();
                m_Updating = false;
            }

            public void Unbind()
            {
                if (m_Surface?.Slider != null)
                {
                    m_Surface.Slider.onValueChanged.RemoveListener(OnSliderValueChanged);
                    if (m_Surface.PauseButton != null)
                    {
                        m_Surface.PauseButton.onClick.RemoveListener(OnPauseButtonClicked);
                    }

                    SetSurfaceVisible(m_Surface, false);
                }

                m_Surface = null;
                m_Playback = null;
                m_Updating = false;
            }

            private void OnSliderValueChanged(float value)
            {
                if (m_Updating || m_Playback == null || m_Playback.CanSeek is false)
                {
                    return;
                }

                m_Playback.Seek(value);
                Refresh();
            }

            private void OnPauseButtonClicked()
            {
                if (m_Playback == null || m_Playback.CanPause is false)
                {
                    return;
                }

                if (m_Playback.IsPaused)
                {
                    m_Playback.Resume();
                }
                else
                {
                    m_Playback.Pause();
                }

                Refresh();
            }

            private void SetPauseButtonText()
            {
                if (m_Surface?.PauseButton == null || m_Playback == null)
                {
                    return;
                }

                m_Surface.PauseButton.interactable = m_Playback.CanPause;
                SetButtonText(m_Surface.PauseButton, m_Playback.IsPaused ? "播放" : "暂停");
            }

            private static void SetSurfaceVisible(VideoSeekSurface surface, bool visible)
            {
                var root = surface?.Root != null
                    ? surface.Root.gameObject
                    : surface?.Slider != null
                        ? surface.Slider.gameObject
                        : null;
                if (root != null)
                {
                    root.SetActive(visible);
                }
            }

            private static string FormatTime(double seconds)
            {
                if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
                {
                    seconds = 0d;
                }

                var totalSeconds = Mathf.FloorToInt((float)seconds);
                var minutes = totalSeconds / 60;
                var remainingSeconds = totalSeconds % 60;
                return $"{minutes:00}:{remainingSeconds:00}";
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
    }
}
