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

namespace GameDeveloperKit.Story
{
    public sealed partial class StoryPlayerView : MonoBehaviour, IStoryFramePresenter, IStoryPlaybackHost
    {
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
            if (frame?.Tracks == null || m_StoryPlayable == null)
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

                output.texture = texture;
                output.uvRect = playback.RequiresVerticalFlip
                    ? s_FlippedVideoUvRect
                    : s_DefaultVideoUvRect;
                output.gameObject.SetActive(true);
                EnsureVideoSeekBinder().Bind(playback.Seekable ? m_CurrentVideoSeek : null, playback);
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
