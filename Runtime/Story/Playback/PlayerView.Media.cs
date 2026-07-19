using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Resource;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Protocol;

namespace GameDeveloperKit.Story.Playback
{
    public sealed partial class PlayerView : MonoBehaviour, IFramePresenter, IPlaybackHost
    {
        private async UniTask PrewarmPlaybackAsync(
            string storyId,
            Program program,
            string volumeId,
            string episodeId,
            CancellationToken cancellationToken)
        {
            if (program == null)
            {
                throw new GameException($"Story program is not registered. story:{storyId}");
            }

            var previewRunner = new Runner(program, m_StoryModule.FunctionResolver);
            var frame = previewRunner.Start(volumeId, episodeId);
            if (frame?.Tracks == null || m_StoryPlayable == null)
            {
                return;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                var command = track?.Command;
                if (track?.Kind != FrameTrackKind.Command ||
                    command == null ||
                    string.Equals(command.Name, MediaCommandNames.PlayVideo, StringComparison.Ordinal) is false)
                {
                    continue;
                }

                await m_StoryPlayable.PreloadVideoAsync(command, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private void OnVideoPlaybackStarted(VideoPlayableHandle playback)
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
            if (output == null || m_StoryPlayable == null)
            {
                m_VideoSeekBinder?.Unbind();
                return;
            }

            var playbacks = m_StoryPlayable.Video.ActiveHandles;
            for (var i = playbacks.Count - 1; i >= 0; i--)
            {
                var playback = playbacks[i];
                if (playback == null || playback.HasFirstFrame is false)
                {
                    continue;
                }

                var texture = playback.Texture;
                if (texture == null)
                {
                    continue;
                }

                VideoSurfaceBinder.BindCover(output, texture, playback.RequiresVerticalFlip);
                output.gameObject.SetActive(true);
                EnsureVideoSeekBinder().Bind(playback.Seekable ? m_CurrentVideoSeek : null, playback);
                EnsureVideoQualityBinder().Bind(m_CurrentVideoQuality, playback);
                return;
            }

            if (m_ClearVideoWhenIdle && playbacks.Count == 0)
            {
                output.texture = null;
                output.uvRect = s_DefaultVideoUvRect;
                output.gameObject.SetActive(false);
            }

            m_VideoSeekBinder?.Unbind();
            m_VideoQualityBinder?.Unbind();
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

        private VideoQualitySurface GetVideoQualitySurface()
        {
            return m_VideoQualityButton == null
                ? null
                : new VideoQualitySurface(m_VideoQualityRoot, m_VideoQualityButton, m_VideoQualityText);
        }

        private VideoQualityBinder EnsureVideoQualityBinder()
        {
            return m_VideoQualityBinder ??= new VideoQualityBinder();
        }

        private void EnsureDefaultVideoQualitySurface()
        {
            if (m_VideoQualityButton != null)
            {
                return;
            }

            var rootPanel = CreatePanel(transform, "VideoQuality", new Color(0.04f, 0.05f, 0.06f, 0.82f));
            var root = rootPanel.rectTransform;
            Anchor(root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            root.sizeDelta = new Vector2(140f, 48f);
            root.anchoredPosition = new Vector2(-24f, -24f);
            var button = CreateButton(root, "QualityButton", "自动", new Color(0.18f, 0.24f, 0.30f, 0.96f));
            Stretch(button.GetComponent<RectTransform>(), 4f, 4f, 4f, 4f);
            var label = button.GetComponentInChildren<TMP_Text>();
            root.gameObject.SetActive(false);
            m_VideoQualityRoot = root;
            m_VideoQualityButton = button;
            m_VideoQualityText = label;
        }

        private sealed class VideoQualityBinder
        {
            private VideoQualitySurface m_Surface;
            private VideoPlayableHandle m_Playback;

            public void Bind(VideoQualitySurface surface, VideoPlayableHandle playback)
            {
                if (surface?.Button == null || playback?.CanSelectQuality != true)
                {
                    Unbind();
                    return;
                }

                if (ReferenceEquals(m_Surface, surface) && ReferenceEquals(m_Playback, playback))
                {
                    Refresh();
                    return;
                }

                Unbind();
                m_Surface = surface;
                m_Playback = playback;
                m_Surface.Button.onClick.AddListener(OnClicked);
                SetVisible(true);
                Refresh();
            }

            public void Unbind()
            {
                if (m_Surface?.Button != null)
                {
                    m_Surface.Button.onClick.RemoveListener(OnClicked);
                    SetVisible(false);
                }

                m_Surface = null;
                m_Playback = null;
            }

            private void OnClicked()
            {
                if (m_Playback == null)
                {
                    return;
                }

                var selection = NextSelection(m_Playback);
                SetInteractable(false);
                SwitchAsync(selection).Forget(Debug.LogException);
            }

            private async UniTask SwitchAsync(VideoQualitySelection selection)
            {
                try
                {
                    await m_Playback.SetQualityAsync(selection);
                }
                finally
                {
                    SetInteractable(true);
                    Refresh();
                }
            }

            private void Refresh()
            {
                if (m_Surface?.Label != null && m_Playback != null)
                {
                    m_Surface.Label.text = m_Playback.Quality.Mode == VideoQualityMode.Auto
                        ? "自动"
                        : FormatQuality(m_Playback.Quality.Height);
                }

                SetVisible(m_Playback?.CanSelectQuality == true);
            }

            private static VideoQualitySelection NextSelection(VideoPlayableHandle playback)
            {
                if (playback.Quality.Mode == VideoQualityMode.Auto)
                {
                    return new VideoQualitySelection(VideoQualityMode.FixedHeight, playback.QualityOptions[0].Height);
                }

                for (var i = 0; i < playback.QualityOptions.Count; i++)
                {
                    if (playback.QualityOptions[i].Height != playback.Quality.Height)
                    {
                        continue;
                    }

                    if (i + 1 < playback.QualityOptions.Count)
                    {
                        return new VideoQualitySelection(VideoQualityMode.FixedHeight, playback.QualityOptions[i + 1].Height);
                    }

                    return playback.SupportsAutoQuality
                        ? new VideoQualitySelection(VideoQualityMode.Auto)
                        : new VideoQualitySelection(VideoQualityMode.FixedHeight, playback.QualityOptions[0].Height);
                }

                return new VideoQualitySelection(VideoQualityMode.FixedHeight, playback.QualityOptions[0].Height);
            }

            private static string FormatQuality(int height)
            {
                return height == 1440 ? "2K" : height == 2160 ? "4K" : $"{height}p";
            }

            private void SetInteractable(bool value)
            {
                if (m_Surface?.Button != null)
                {
                    m_Surface.Button.interactable = value;
                }
            }

            private void SetVisible(bool value)
            {
                var root = m_Surface?.Root != null ? m_Surface.Root.gameObject : m_Surface?.Button?.gameObject;
                root?.SetActive(value);
            }
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

        private sealed class VideoSeekBinder
        {
            private VideoPlayableHandle m_Playback;
            private VideoSeekSurface m_Surface;
            private bool m_Updating;

            public void Bind(VideoSeekSurface surface, VideoPlayableHandle playback)
            {
                if (surface?.Slider == null || playback == null || playback.Seekable is false)
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

                if (m_Playback.Seekable is false)
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
    }
}
