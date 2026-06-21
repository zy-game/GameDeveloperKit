using System;
using System.Collections.Generic;
using System.Text;
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
        [SerializeField] private bool m_ClearVideoWhenIdle = true;

        [Header("文本")]
        [SerializeField] private TMP_Text m_SpeakerText;
        [SerializeField] private TMP_Text m_BodyText;
        [SerializeField] private TMP_Text m_ErrorText;
        [SerializeField] private string m_CompletedText = "剧情已结束";

        [Header("交互")]
        [SerializeField] private Button m_ContinueButton;
        [SerializeField] private Transform m_ChoiceRoot;
        [SerializeField] private Button m_ChoiceButtonTemplate;

        private readonly List<Button> m_ChoiceButtons = new List<Button>();
        private readonly StringBuilder m_TextBuilder = new StringBuilder();

        private StoryModule m_StoryModule;
        private ResourceModule m_ResourceModule;
        private SoundModule m_SoundModule;
        private StoryPresenter m_Presenter;
        private StoryAvProVideoCommandPlayer m_VideoPlayer;
        private StoryImageCommandPlayer m_ImagePlayer;
        private StorySoundCommandPlayer m_AudioPlayer;
        private StoryFrame m_CurrentFrame;
        private bool m_ContinueListenerBound;
        private bool m_FirstVideoFrameReported;

        private static readonly Rect s_DefaultVideoUvRect = new Rect(0f, 0f, 1f, 1f);
        private static readonly Rect s_FlippedVideoUvRect = new Rect(0f, 1f, 1f, -1f);

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
            ExecutePlayback(() => EnsurePresenter().Start(program, chapterId));
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
            ExecutePlayback(() => EnsurePresenter().StartProgram(storyId, chapterId));
        }

        /// <summary>
        /// 停止当前播放。
        /// </summary>
        public void StopPlayback()
        {
            LastError = null;
            m_FirstVideoFrameReported = false;
            if (m_Presenter != null)
            {
                m_Presenter.Stop();
            }

            m_CurrentFrame = null;
            ClearFrameUi();
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
            ClearChoices();
        }

        private void Awake()
        {
            BindContinueButton();
            if (m_ChoiceButtonTemplate != null)
            {
                m_ChoiceButtonTemplate.gameObject.SetActive(false);
            }

            ClearError();
        }

        private void Update()
        {
            if (m_Presenter == null)
            {
                return;
            }

            UpdateVideoOutput();
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
            DisposePresenter();
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
            m_VideoPlayer.PlaybackStarted += OnVideoPlaybackStarted;

            if (m_ImageOutput != null && m_ResourceModule != null)
            {
                m_ImagePlayer = new StoryImageCommandPlayer(m_ImageOutput, m_ResourceModule);
            }

            if (m_SoundModule != null)
            {
                m_AudioPlayer = new StorySoundCommandPlayer(m_SoundModule);
            }

            m_Presenter.AddCommandHandler(new StoryMediaCommandHandler(
                m_VideoPlayer,
                m_ImagePlayer,
                m_AudioPlayer));
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
            m_FirstVideoFrameReported = false;
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

        private void RenderFrame(StoryFrame frame)
        {
            BindContinueButton();
            ClearChoices();

            if (frame == null)
            {
                ClearFrameUi();
                return;
            }

            RenderText(frame);
            RenderChoices(frame);
            SetContinueVisible(
                frame.IsCompleted is false &&
                frame.WaitsForChoice is false &&
                frame.WaitsForCommand is false &&
                frame.WaitsForTime is false);

            if (frame.IsCompleted)
            {
                SetBodyText(m_CompletedText);
            }
        }

        private void RenderText(StoryFrame frame)
        {
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

            SetSpeakerText(speaker);
            SetBodyText(m_TextBuilder.ToString());
        }

        private void RenderChoices(StoryFrame frame)
        {
            if (frame.Choices == null ||
                frame.Choices.Count == 0 ||
                m_ChoiceButtonTemplate == null)
            {
                return;
            }

            var parent = m_ChoiceRoot != null ? m_ChoiceRoot : m_ChoiceButtonTemplate.transform.parent;
            for (var i = 0; i < frame.Choices.Count; i++)
            {
                var choice = frame.Choices[i];
                if (choice == null)
                {
                    continue;
                }

                var button = Instantiate(m_ChoiceButtonTemplate, parent);
                var choiceId = choice.ChoiceId;
                button.gameObject.SetActive(true);
                SetButtonText(button, choice.TextKey);
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => Select(choiceId));
                m_ChoiceButtons.Add(button);
            }
        }

        private void ClearFrameUi()
        {
            SetSpeakerText(null);
            SetBodyText(null);
            SetContinueVisible(false);
            ClearChoices();
        }

        private void ClearChoices()
        {
            for (var i = 0; i < m_ChoiceButtons.Count; i++)
            {
                var button = m_ChoiceButtons[i];
                if (button == null)
                {
                    continue;
                }

                button.onClick.RemoveAllListeners();
                if (Application.isPlaying)
                {
                    Destroy(button.gameObject);
                }
                else
                {
                    DestroyImmediate(button.gameObject);
                }
            }

            m_ChoiceButtons.Clear();
        }

        private void BindContinueButton()
        {
            if (m_ContinueButton == null || m_ContinueListenerBound)
            {
                return;
            }

            m_ContinueButton.onClick.AddListener(Continue);
            m_ContinueListenerBound = true;
        }

        private void SetContinueVisible(bool visible)
        {
            if (m_ContinueButton != null)
            {
                m_ContinueButton.gameObject.SetActive(visible);
            }
        }

        private void SetSpeakerText(string value)
        {
            if (m_SpeakerText == null)
            {
                return;
            }

            m_SpeakerText.text = value ?? string.Empty;
            m_SpeakerText.gameObject.SetActive(string.IsNullOrWhiteSpace(value) is false);
        }

        private void SetBodyText(string value)
        {
            if (m_BodyText != null)
            {
                m_BodyText.text = value ?? string.Empty;
            }
        }

        private void SetButtonText(Button button, string value)
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
            if (m_VideoOutput == null || m_VideoPlayer == null)
            {
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

                m_VideoOutput.texture = texture;
                m_VideoOutput.uvRect = playback.RequiresVerticalFlip
                    ? s_FlippedVideoUvRect
                    : s_DefaultVideoUvRect;
                m_VideoOutput.gameObject.SetActive(true);
                return;
            }

            if (m_ClearVideoWhenIdle && playbacks.Count == 0)
            {
                m_VideoOutput.texture = null;
                m_VideoOutput.uvRect = s_DefaultVideoUvRect;
                m_VideoOutput.gameObject.SetActive(false);
            }
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
