using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Playable
{
    /// <summary>
    /// 可直接打开、也可被业务继承的通用视频播放器窗口。
    /// </summary>
    [UIOption("Assets/Bundles/Playback/VideoPlayerWindow.prefab", 500, CacheEnabled = false)]
    public class VideoPlayerWindow : UIWindow
    {
        private static readonly float[] s_PlaybackRates = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f };

        private readonly List<Button> m_QualityOptionButtons = new List<Button>();
        private VideoPlayableHandle m_Playback;
        private Transform m_PlaybackRoot;
        private RawImage m_VideoOutput;
        private RectTransform m_ChromeRoot;
        private Button m_ToggleChromeButton;
        private Button m_BackButton;
        private TMP_Text m_TitleText;
        private Button m_PlayPauseButton;
        private TMP_Text m_PlayPauseText;
        private TMP_Text m_TimeText;
        private RectTransform m_ProgressRoot;
        private Slider m_ProgressSlider;
        private Button m_SpeedButton;
        private TMP_Text m_SpeedText;
        private Button m_QualityButton;
        private TMP_Text m_QualityText;
        private RectTransform m_QualityMenuRoot;
        private RectTransform m_QualityOptionsRoot;
        private Button m_QualityOptionTemplate;
        private CancellationTokenSource m_QualityCancellation;
        private Texture m_BoundTexture;
        private bool m_BoundVerticalFlip;
        private bool m_UpdatingProgress;
        private float m_ChromeIdleSeconds;

        public string Title { get; private set; } = string.Empty;

        public bool ShowProgress { get; private set; } = true;

        public bool AreControlsVisible { get; private set; } = true;

        public float ChromeAutoHideDelaySeconds { get; set; } = 3f;

        public VideoPlayableHandle Playback => m_Playback;

        public double CurrentTimeSeconds => m_Playback?.CurrentTimeSeconds ?? 0d;

        public double DurationSeconds => m_Playback?.DurationSeconds ?? 0d;

        public bool IsPlaying => m_Playback?.Status == PlayableStatus.Playing;

        public float PlaybackRate => m_Playback?.PlaybackRate ?? 1f;

        public event Action BackRequested;

        public event Action<bool> ControlsVisibilityChanged;

        public event Action<VideoPlayableHandle> PlaybackOpened;

        public event Action<VideoPlayableHandle> PlaybackCompleted;

        public event Action<VideoPlayableHandle> PlaybackStateChanged;

        public event Action<float> PlaybackSpeedChanged;

        public event Action<VideoQualitySelection> QualityChanged;

        public event Action<double, double> ProgressChanged;

        protected Transform PlaybackRoot => m_PlaybackRoot;

        protected RawImage VideoOutput => m_VideoOutput;

        public override UniTask OnAwakeAsync()
        {
            BindDocument();
            BindControls();
            ResetPresentation();
            return UniTask.CompletedTask;
        }

        public override UniTask OnOpenAsync()
        {
            ShowChromeForInteraction();
            return UniTask.CompletedTask;
        }

        public override void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            Tick(unscaledDeltaTime);
        }

        public virtual async UniTask<VideoPlayableHandle> PlayAsync(
            string url,
            string title = null,
            bool showProgress = true,
            VideoPlayableOptions options = null,
            CancellationToken cancellationToken = default)
        {
            ValidateFinalVideoUrl(url);
            StopPlayback();
            Title = title?.Trim() ?? string.Empty;
            ShowProgress = showProgress;
            ApplyTitle();
            ApplyProgressVisibility();
            ShowChromeForInteraction();

            m_Playback = await App.Playable.Video.PlayAsync(
                new VideoPlayableRequest(url.Trim(), CreatePlaybackOptions(options)),
                cancellationToken);
            RefreshPlaybackSurface();
            RefreshPlaybackControls();
            RebuildQualityOptions();
            OnVideoOpened(m_Playback);
            PlaybackOpened?.Invoke(m_Playback);
            ObserveCompletionAsync(m_Playback).Forget(Debug.LogException);
            return m_Playback;
        }

        public virtual void TogglePlayback()
        {
            if (m_Playback == null)
            {
                return;
            }

            ShowChromeForInteraction();
            if (m_Playback.Status == PlayableStatus.Playing)
            {
                m_Playback.Pause();
            }
            else if (m_Playback.Status == PlayableStatus.Paused)
            {
                m_Playback.Resume();
            }

            RefreshPlaybackControls();
            OnPlaybackStateChanged(m_Playback);
            PlaybackStateChanged?.Invoke(m_Playback);
        }

        public virtual void Seek(double timeSeconds)
        {
            if (m_Playback == null || !m_Playback.CanSeek)
            {
                return;
            }

            ShowChromeForInteraction();
            m_Playback.Seek(timeSeconds);
            PublishProgress();
        }

        public virtual void SetPlaybackSpeed(float rate)
        {
            if (m_Playback == null)
            {
                return;
            }

            ShowChromeForInteraction();
            m_Playback.SetPlaybackRate(rate);
            RefreshSpeedText();
            OnPlaybackSpeedChanged(rate);
            PlaybackSpeedChanged?.Invoke(rate);
        }

        public virtual async UniTask SetQualityAsync(
            VideoQualitySelection selection,
            CancellationToken cancellationToken = default)
        {
            if (m_Playback == null)
            {
                return;
            }

            ShowChromeForInteraction();
            CancelQualitySwitch();
            var qualityCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            m_QualityCancellation = qualityCancellation;
            try
            {
                await m_Playback.SetQualityAsync(selection, qualityCancellation.Token);
                RefreshQualityText();
                SetQualityMenuVisible(false);
                OnQualityChanged(selection);
                QualityChanged?.Invoke(selection);
            }
            finally
            {
                if (ReferenceEquals(m_QualityCancellation, qualityCancellation))
                {
                    m_QualityCancellation = null;
                }

                qualityCancellation.Dispose();
            }
        }

        public virtual void ToggleControls()
        {
            SetControlsVisible(!AreControlsVisible);
        }

        public virtual void SetControlsVisible(bool visible)
        {
            m_ChromeIdleSeconds = 0f;
            if (AreControlsVisible == visible)
            {
                return;
            }

            AreControlsVisible = visible;
            if (m_ChromeRoot != null)
            {
                m_ChromeRoot.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                SetQualityMenuVisible(false);
            }

            OnControlsVisibilityChanged(visible);
            ControlsVisibilityChanged?.Invoke(visible);
        }

        public virtual void RequestBack()
        {
            ShowChromeForInteraction();
            OnBackRequested();
            BackRequested?.Invoke();
        }

        /// <summary>
        /// 刷新播放器 UI；由 UIModule 的统一窗口更新入口驱动，也可由测试或业务显式调用。
        /// </summary>
        public virtual void Tick()
        {
            Tick(0f);
        }

        public override void Release()
        {
            UnbindControls();
            StopPlayback();
            ClearQualityOptions();
            m_PlaybackRoot = null;
            m_VideoOutput = null;
            m_ChromeRoot = null;
            m_ToggleChromeButton = null;
            m_BackButton = null;
            m_TitleText = null;
            m_PlayPauseButton = null;
            m_PlayPauseText = null;
            m_TimeText = null;
            m_ProgressRoot = null;
            m_ProgressSlider = null;
            m_SpeedButton = null;
            m_SpeedText = null;
            m_QualityButton = null;
            m_QualityText = null;
            m_QualityMenuRoot = null;
            m_QualityOptionsRoot = null;
            m_QualityOptionTemplate = null;
            Title = string.Empty;
            base.Release();
        }

        protected virtual void OnVideoOpened(VideoPlayableHandle playback)
        {
        }

        protected virtual void OnPlaybackStateChanged(VideoPlayableHandle playback)
        {
        }

        protected virtual void OnPlaybackSpeedChanged(float rate)
        {
        }

        protected virtual void OnQualityChanged(VideoQualitySelection selection)
        {
        }

        protected virtual void OnProgressChanged(double currentSeconds, double durationSeconds)
        {
        }

        protected virtual void OnControlsVisibilityChanged(bool visible)
        {
        }

        protected virtual void OnBackRequested()
        {
            App.UI.Back().Forget(Debug.LogException);
        }

        protected virtual void OnPlaybackStopped()
        {
        }

        protected virtual void OnVideoPlaybackCompleted(VideoPlayableHandle playback)
        {
        }

        protected void StopCurrentVideo()
        {
            StopPlayback();
        }

        private void BindDocument()
        {
            if (Document == null)
            {
                throw new GameException("Video player prefab is missing UIDocument.");
            }

            m_PlaybackRoot = Document.GetComponent<Transform>("PlaybackRoot");
            m_VideoOutput = Document.GetComponent<RawImage>("VideoOutput");
            m_ChromeRoot = Document.GetComponent<RectTransform>("ChromeRoot");
            m_ToggleChromeButton = Document.GetComponent<Button>("ToggleChromeButton");
            m_BackButton = Document.GetComponent<Button>("BackButton");
            m_TitleText = Document.GetComponent<TMP_Text>("TitleText");
            m_PlayPauseButton = Document.GetComponent<Button>("PlayPauseButton");
            m_PlayPauseText = Document.GetComponent<TMP_Text>("PlayPauseText");
            m_TimeText = Document.GetComponent<TMP_Text>("TimeText");
            m_ProgressRoot = Document.GetComponent<RectTransform>("ProgressRoot");
            m_ProgressSlider = Document.GetComponent<Slider>("ProgressSlider");
            m_SpeedButton = Document.GetComponent<Button>("SpeedButton");
            m_SpeedText = Document.GetComponent<TMP_Text>("SpeedText");
            m_QualityButton = Document.GetComponent<Button>("QualityButton");
            m_QualityText = Document.GetComponent<TMP_Text>("QualityText");
            m_QualityMenuRoot = Document.GetComponent<RectTransform>("QualityMenuRoot");
            m_QualityOptionsRoot = Document.GetComponent<RectTransform>("QualityOptionsRoot");
            m_QualityOptionTemplate = Document.GetComponent<Button>("QualityOptionTemplate");
        }

        private void BindControls()
        {
            m_ToggleChromeButton.onClick.AddListener(ToggleControls);
            m_BackButton.onClick.AddListener(RequestBack);
            m_PlayPauseButton.onClick.AddListener(TogglePlayback);
            m_ProgressSlider.onValueChanged.AddListener(OnProgressSliderChanged);
            m_SpeedButton.onClick.AddListener(CyclePlaybackSpeed);
            m_QualityButton.onClick.AddListener(ToggleQualityMenu);
        }

        private void UnbindControls()
        {
            m_ToggleChromeButton?.onClick.RemoveListener(ToggleControls);
            m_BackButton?.onClick.RemoveListener(RequestBack);
            m_PlayPauseButton?.onClick.RemoveListener(TogglePlayback);
            m_ProgressSlider?.onValueChanged.RemoveListener(OnProgressSliderChanged);
            m_SpeedButton?.onClick.RemoveListener(CyclePlaybackSpeed);
            m_QualityButton?.onClick.RemoveListener(ToggleQualityMenu);
        }

        private void Tick(float unscaledDeltaTime)
        {
            RefreshPlaybackSurface();
            RefreshPlaybackControls();
            PublishProgress();

            if (m_Playback?.Status != PlayableStatus.Playing || !AreControlsVisible)
            {
                return;
            }

            m_ChromeIdleSeconds += Mathf.Max(0f, unscaledDeltaTime);
            if (ChromeAutoHideDelaySeconds > 0f && m_ChromeIdleSeconds >= ChromeAutoHideDelaySeconds)
            {
                SetControlsVisible(false);
            }
        }

        private VideoPlayableOptions CreatePlaybackOptions(VideoPlayableOptions source)
        {
            if (source == null)
            {
                return new VideoPlayableOptions
                {
                    Seekable = true,
                    Parent = m_PlaybackRoot,
                    DontDestroyOnLoad = false
                };
            }

            return new VideoPlayableOptions
            {
                Loop = source.Loop,
                Seekable = source.Seekable,
                Parent = source.Parent != null ? source.Parent : m_PlaybackRoot,
                DontDestroyOnLoad = source.DontDestroyOnLoad,
                SupportsAutoQuality = source.SupportsAutoQuality,
                InitialQuality = source.InitialQuality,
                QualityOptions = source.QualityOptions
            };
        }

        private void RefreshPlaybackSurface()
        {
            if (m_VideoOutput == null)
            {
                return;
            }

            var texture = m_Playback?.Texture;
            var verticalFlip = m_Playback?.RequiresVerticalFlip ?? false;
            if (ReferenceEquals(texture, m_BoundTexture) && verticalFlip == m_BoundVerticalFlip)
            {
                return;
            }

            m_BoundTexture = texture;
            m_BoundVerticalFlip = verticalFlip;
            VideoSurfaceBinder.BindCover(m_VideoOutput, texture, verticalFlip);
        }

        private void RefreshPlaybackControls()
        {
            if (m_PlayPauseText != null)
            {
                m_PlayPauseText.text = m_Playback?.Status == PlayableStatus.Playing ? "II" : ">";
            }

            if (m_PlayPauseButton != null)
            {
                m_PlayPauseButton.interactable = m_Playback != null &&
                    m_Playback.Status is PlayableStatus.Playing or PlayableStatus.Paused;
            }

            RefreshSpeedText();
            RefreshQualityText();
        }

        private void PublishProgress()
        {
            var current = CurrentTimeSeconds;
            var duration = DurationSeconds;
            if (m_TimeText != null)
            {
                m_TimeText.text = $"{FormatTime(current)} / {FormatTime(duration)}";
            }

            if (m_ProgressSlider != null)
            {
                m_UpdatingProgress = true;
                m_ProgressSlider.minValue = 0f;
                m_ProgressSlider.maxValue = Mathf.Max(0.001f, (float)duration);
                m_ProgressSlider.SetValueWithoutNotify((float)Math.Min(current, duration));
                m_ProgressSlider.interactable = m_Playback?.CanSeek == true;
                m_UpdatingProgress = false;
            }

            OnProgressChanged(current, duration);
            ProgressChanged?.Invoke(current, duration);
        }

        private void ApplyTitle()
        {
            if (m_TitleText != null)
            {
                m_TitleText.text = Title;
            }
        }

        private void ApplyProgressVisibility()
        {
            if (m_ProgressRoot != null)
            {
                m_ProgressRoot.gameObject.SetActive(ShowProgress);
            }
        }

        private void ShowChromeForInteraction()
        {
            m_ChromeIdleSeconds = 0f;
            SetControlsVisible(true);
        }

        private void OnProgressSliderChanged(float value)
        {
            if (!m_UpdatingProgress)
            {
                Seek(value);
            }
        }

        private void CyclePlaybackSpeed()
        {
            var current = PlaybackRate;
            for (var i = 0; i < s_PlaybackRates.Length; i++)
            {
                if (s_PlaybackRates[i] > current + 0.001f)
                {
                    SetPlaybackSpeed(s_PlaybackRates[i]);
                    return;
                }
            }

            SetPlaybackSpeed(s_PlaybackRates[0]);
        }

        private void RefreshSpeedText()
        {
            if (m_SpeedText != null)
            {
                m_SpeedText.text = $"{PlaybackRate:0.##}x";
            }

            if (m_SpeedButton != null)
            {
                m_SpeedButton.interactable = m_Playback != null;
            }
        }

        private void ToggleQualityMenu()
        {
            ShowChromeForInteraction();
            SetQualityMenuVisible(m_QualityMenuRoot != null && !m_QualityMenuRoot.gameObject.activeSelf);
        }

        private void SetQualityMenuVisible(bool visible)
        {
            if (m_QualityMenuRoot != null)
            {
                m_QualityMenuRoot.gameObject.SetActive(visible && m_Playback?.CanSelectQuality == true);
            }
        }

        private void RebuildQualityOptions()
        {
            ClearQualityOptions();
            if (m_Playback == null || m_QualityOptionTemplate == null || m_QualityOptionsRoot == null)
            {
                return;
            }

            if (m_Playback.SupportsAutoQuality)
            {
                AddQualityOption("自动", new VideoQualitySelection(VideoQualityMode.Auto));
            }

            var heights = new HashSet<int>();
            for (var i = 0; i < m_Playback.QualityOptions.Count; i++)
            {
                var option = m_Playback.QualityOptions[i];
                if (heights.Add(option.Height))
                {
                    AddQualityOption(option.Label, new VideoQualitySelection(VideoQualityMode.FixedHeight, option.Height));
                }
            }

            if (m_QualityButton != null)
            {
                m_QualityButton.interactable = m_Playback.CanSelectQuality;
            }

            RefreshQualityText();
        }

        private void AddQualityOption(string label, VideoQualitySelection selection)
        {
            var button = Object.Instantiate(m_QualityOptionTemplate, m_QualityOptionsRoot, false);
            button.gameObject.name = selection.Mode == VideoQualityMode.Auto
                ? "QualityAuto"
                : $"Quality{selection.Height}";
            button.gameObject.SetActive(true);
            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
            }

            button.onClick.AddListener(() => SetQualityAsync(selection).Forget(Debug.LogException));
            m_QualityOptionButtons.Add(button);
        }

        private void ClearQualityOptions()
        {
            for (var i = 0; i < m_QualityOptionButtons.Count; i++)
            {
                if (m_QualityOptionButtons[i] != null)
                {
                    Object.Destroy(m_QualityOptionButtons[i].gameObject);
                }
            }

            m_QualityOptionButtons.Clear();
            SetQualityMenuVisible(false);
        }

        private void RefreshQualityText()
        {
            if (m_QualityText != null)
            {
                m_QualityText.text = m_Playback == null || m_Playback.Quality.Mode == VideoQualityMode.Auto
                    ? "自动"
                    : FormatQuality(m_Playback.Quality.Height);
            }

            if (m_QualityButton != null)
            {
                m_QualityButton.interactable = m_Playback?.CanSelectQuality == true;
            }
        }

        private void ResetPresentation()
        {
            AreControlsVisible = true;
            m_ChromeIdleSeconds = 0f;
            m_ChromeRoot.gameObject.SetActive(true);
            m_QualityMenuRoot.gameObject.SetActive(false);
            m_QualityOptionTemplate.gameObject.SetActive(false);
            ApplyTitle();
            ApplyProgressVisibility();
            RefreshPlaybackSurface();
            RefreshPlaybackControls();
            PublishProgress();
        }

        private void StopPlayback()
        {
            CancelQualitySwitch();
            var playback = m_Playback;
            m_Playback = null;
            if (playback != null)
            {
                playback.Stop();
                playback.Dispose();
                OnPlaybackStopped();
            }

            m_BoundTexture = null;
            m_BoundVerticalFlip = false;
            if (m_VideoOutput != null)
            {
                VideoSurfaceBinder.BindCover(m_VideoOutput, null, false);
            }

            ClearQualityOptions();
            RefreshPlaybackControls();
        }

        private void CancelQualitySwitch()
        {
            if (m_QualityCancellation == null)
            {
                return;
            }

            m_QualityCancellation.Cancel();
            m_QualityCancellation.Dispose();
            m_QualityCancellation = null;
        }

        private async UniTask ObserveCompletionAsync(VideoPlayableHandle playback)
        {
            try
            {
                await playback.WaitForCompletionAsync();
            }
            catch
            {
                return;
            }

            if (!ReferenceEquals(m_Playback, playback) || playback.Status != PlayableStatus.Completed)
            {
                return;
            }

            ShowChromeForInteraction();
            RefreshPlaybackControls();
            OnVideoPlaybackCompleted(playback);
            PlaybackCompleted?.Invoke(playback);
        }

        private static void ValidateFinalVideoUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) ||
                !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Video player requires a final absolute HTTPS URL.", nameof(url));
            }
        }

        private static string FormatTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
            {
                seconds = 0d;
            }

            var value = TimeSpan.FromSeconds(Math.Floor(seconds));
            return value.TotalHours >= 1d
                ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}"
                : $"{value.Minutes:00}:{value.Seconds:00}";
        }

        private static string FormatQuality(int height)
        {
            return height switch
            {
                1440 => "2K",
                2160 => "4K",
                _ => $"{height}P"
            };
        }
    }
}
