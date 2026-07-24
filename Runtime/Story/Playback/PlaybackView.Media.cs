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

namespace GameDeveloperKit.Story.Playback
{
    public partial class PlaybackView
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

            if (m_StoryPlayable == null)
            {
                return;
            }

            var commands = EpisodeVideoPrewarmer.CollectInitialVideoCommands(
                m_StoryModule,
                storyId,
                program,
                volumeId,
                episodeId);
            if (commands.Count > 0)
            {
                ShowInitialVideoPlaceholder();
                await UniTask.NextFrame(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            for (var i = 0; i < commands.Count; i++)
            {
                await m_StoryPlayable.PreloadVideoAsync(commands[i], cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private void HandleVideoPlaybackStarted(VideoPlayableHandle playback)
        {
            UpdateVideoOutput();
            if (playback == null || playback.HasFirstFrame is false)
            {
                return;
            }

            OnVideoPlaybackStarted(playback);
            if (m_FirstVideoFrameReported)
            {
                return;
            }

            m_FirstVideoFrameReported = true;
            FirstVideoFrameReady?.Invoke(playback);
        }

        private void UpdateVideoOutput()
        {
            var output = m_CurrentVideoOutput ??
                         m_RetainedVideoOutput ??
                         (m_UseDefaultPlaybackSurface ? m_VideoOutput : null);
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
                CompleteVideoTransition(output);
                EnsureVideoSeekBinder().Bind(playback.Seekable ? m_CurrentVideoSeek : null, playback);
                EnsureVideoQualityBinder().Bind(m_CurrentVideoQuality, playback);
                return;
            }

            if (m_VideoTransitionPending)
            {
                EnsurePendingVideoOutputVisible(output);
            }

            if (m_VideoTransitionPending is false &&
                m_ClearVideoWhenIdle &&
                playbacks.Count == 0)
            {
                ClearMediaOutput(output);
            }

            m_VideoSeekBinder?.Unbind();
            m_VideoQualityBinder?.Unbind();
        }

        private void ShowInitialVideoPlaceholder()
        {
            var output = m_CurrentVideoOutput ??
                         m_CustomPlaybackSurface?.VideoOutput ??
                         m_VideoOutput;
            if (output == null)
            {
                return;
            }

            m_RetainedVideoOutput = output;
            m_VideoTransitionPending = true;
            ShowBlackVideoOutput(output);
        }

        private static void ShowBlackVideoOutput(RawImage output)
        {
            output.texture = Texture2D.blackTexture;
            output.uvRect = s_DefaultVideoUvRect;
            output.gameObject.SetActive(true);
        }

        private static void EnsurePendingVideoOutputVisible(RawImage output)
        {
            if (output.texture == null)
            {
                ShowBlackVideoOutput(output);
                return;
            }

            output.gameObject.SetActive(true);
        }

        private RawImage ResolveImageOutput()
        {
            return m_CurrentImageOutput ??
                   (m_UseDefaultPlaybackSurface ? m_ImageOutput : null);
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

        internal sealed class VideoQualityBinder
        {
            private VideoQualitySurface m_Surface;
            private VideoPlayableHandle m_Playback;

            public void Bind(VideoQualitySurface surface, VideoPlayableHandle playback)
            {
                if (surface?.Button == null || HasDisplayableQuality(playback) is false)
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
                if (m_Playback?.CanSelectQuality != true)
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

                SetInteractable(m_Playback?.CanSelectQuality == true);
                SetVisible(HasDisplayableQuality(m_Playback));
            }

            private static bool HasDisplayableQuality(VideoPlayableHandle playback)
            {
                return playback != null &&
                       (playback.SupportsAutoQuality || playback.QualityOptions.Count > 0);
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
