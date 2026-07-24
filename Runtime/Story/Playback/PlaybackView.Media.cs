using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Resource;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Protocol;
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
                PrewarmNextVideo(playback);
                return;
            }

            m_FirstVideoFrameReported = true;
            FirstVideoFrameReady?.Invoke(playback);
            PrewarmNextVideo(playback);
        }

        private void PrewarmNextVideo(VideoPlayableHandle playback)
        {
            if (playback == null || m_CurrentFrame?.Tracks == null || m_VideoPlayable == null)
            {
                return;
            }

            if (string.Equals(m_VideoLookaheadPath, playback.Path, StringComparison.Ordinal))
            {
                m_VideoLookaheadPath = null;
            }

            m_ChoiceVideoLookaheadPaths.Remove(playback.Path);

            for (var i = 0; i < m_CurrentFrame.Tracks.Count; i++)
            {
                var track = m_CurrentFrame.Tracks[i];
                if (track?.Kind != FrameTrackKind.Command ||
                    track.Command == null ||
                    string.Equals(track.Command.Name, MediaCommandNames.PlayVideo, StringComparison.Ordinal) is false)
                {
                    continue;
                }

                var currentRequest = MediaCommandHandler.CreateVideoRequest(
                    track.Command,
                    m_PlaybackRoot != null ? m_PlaybackRoot : GameObject.transform,
                    false);
                if (string.Equals(currentRequest.Path, playback.Path, StringComparison.Ordinal) is false ||
                    string.Equals(m_VideoLookaheadSourceCommandId, track.Command.CommandId, StringComparison.Ordinal))
                {
                    continue;
                }

                m_VideoLookaheadSourceCommandId = track.Command.CommandId;
                ReleaseVideoLookahead(false);
                var nextCommand = EpisodeVideoPrewarmer.FindNextVideoCommand(
                    m_CurrentFrame,
                    track.Step,
                    m_StoryModule?.FunctionResolver);
                if (nextCommand == null)
                {
                    return;
                }

                var nextRequest = MediaCommandHandler.CreateVideoRequest(
                    nextCommand,
                    m_PlaybackRoot != null ? m_PlaybackRoot : GameObject.transform,
                    false);
                m_VideoLookaheadPath = nextRequest.Path;
                PrewarmVideoLookaheadAsync(
                        nextCommand.CommandId,
                        nextRequest,
                        m_PlaybackCancellation?.Token ?? default)
                    .Forget(Debug.LogException);
                return;
            }
        }

        private async UniTask PrewarmVideoLookaheadAsync(
            string commandId,
            VideoPlayableRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                await m_VideoPlayable.PreloadAsync(request, cancellationToken);
                Debug.Log($"Story video lookahead first frame ready. command:{commandId} path:{request.Path}");
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                if (string.Equals(m_VideoLookaheadPath, request.Path, StringComparison.Ordinal))
                {
                    m_VideoLookaheadPath = null;
                }

                Debug.LogWarning(
                    $"Story video lookahead failed. command:{commandId} path:{request.Path} " +
                    $"error:{exception.Message}");
            }
        }

        private void PrewarmEpisodeChoiceVideos(Frame frame)
        {
            var preservedPath = m_SelectedChoiceVideoLookaheadPath;
            m_SelectedChoiceVideoLookaheadPath = null;
            ReleaseChoiceVideoLookaheads(preservedPath);
            if (frame == null || m_VideoPlayable == null)
            {
                return;
            }

            var commands = EpisodeVideoPrewarmer.CollectChoiceVideoCommands(
                frame,
                m_StoryModule?.FunctionResolver);
            for (var i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                var request = MediaCommandHandler.CreateVideoRequest(
                    command,
                    m_PlaybackRoot != null ? m_PlaybackRoot : GameObject.transform,
                    false);
                if (m_ChoiceVideoLookaheadPaths.Add(request.Path) is false)
                {
                    continue;
                }

                PrewarmChoiceVideoLookaheadAsync(
                        command.CommandId,
                        request,
                        m_PlaybackCancellation?.Token ?? default)
                    .Forget(Debug.LogException);
            }
        }

        private async UniTask PrewarmChoiceVideoLookaheadAsync(
            string commandId,
            VideoPlayableRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                await m_VideoPlayable.PreloadAsync(request, cancellationToken);
                Debug.Log(
                    $"Story choice video lookahead first frame ready. command:{commandId} path:{request.Path}");
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                m_ChoiceVideoLookaheadPaths.Remove(request.Path);
                Debug.LogWarning(
                    $"Story choice video lookahead failed. command:{commandId} path:{request.Path} " +
                    $"error:{exception.Message}");
            }
        }

        private void PrepareChoiceVideoSelection(string choiceId)
        {
            if (m_CurrentFrame?.Choices == null || string.IsNullOrWhiteSpace(choiceId))
            {
                return;
            }

            var choiceExists = false;
            for (var i = 0; i < m_CurrentFrame.Choices.Count; i++)
            {
                if (string.Equals(m_CurrentFrame.Choices[i]?.ChoiceId, choiceId, StringComparison.Ordinal))
                {
                    choiceExists = true;
                    break;
                }
            }

            if (choiceExists is false)
            {
                return;
            }

            var command = EpisodeVideoPrewarmer.FindChoiceVideoCommand(
                m_CurrentFrame,
                choiceId,
                m_StoryModule?.FunctionResolver);
            var selectedPath = command == null
                ? null
                : MediaCommandHandler.CreateVideoRequest(
                    command,
                    m_PlaybackRoot != null ? m_PlaybackRoot : GameObject.transform,
                    false).Path;
            m_SelectedChoiceVideoLookaheadPath = selectedPath;
            ReleaseChoiceVideoLookaheads(selectedPath);
        }

        private void ReleaseVideoLookahead(bool resetSource = true)
        {
            var path = m_VideoLookaheadPath;
            m_VideoLookaheadPath = null;
            if (resetSource)
            {
                m_VideoLookaheadSourceCommandId = null;
            }

            if (!string.IsNullOrWhiteSpace(path) &&
                m_VideoPlayable != null &&
                m_ChoiceVideoLookaheadPaths.Contains(path) is false)
            {
                m_VideoPlayable.ReleasePreload(path);
            }
        }

        private void ReleaseChoiceVideoLookaheads(string preservedPath = null)
        {
            if (m_ChoiceVideoLookaheadPaths.Count == 0)
            {
                m_SelectedChoiceVideoLookaheadPath = null;
                return;
            }

            var paths = new List<string>(m_ChoiceVideoLookaheadPaths);
            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                if (string.Equals(path, preservedPath, StringComparison.Ordinal))
                {
                    continue;
                }

                m_ChoiceVideoLookaheadPaths.Remove(path);
                if (m_VideoPlayable != null &&
                    string.Equals(m_VideoLookaheadPath, path, StringComparison.Ordinal) is false)
                {
                    m_VideoPlayable.ReleasePreload(path);
                }
            }

            if (string.IsNullOrWhiteSpace(preservedPath))
            {
                m_SelectedChoiceVideoLookaheadPath = null;
            }
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
                : new VideoQualitySurface(
                    m_VideoQualityRoot,
                    m_VideoQualityButton,
                    m_VideoQualityText,
                    m_VideoQualityMenuRoot,
                    m_VideoQualityOptionsRoot,
                    m_VideoQualityOptionTemplate);
        }

        private VideoQualityBinder EnsureVideoQualityBinder()
        {
            return m_VideoQualityBinder ??= new VideoQualityBinder();
        }

        internal sealed class VideoQualityBinder
        {
            private static readonly Color s_OptionColor = new Color(0.07f, 0.075f, 0.08f, 1f);
            private static readonly Color s_SelectedOptionColor = new Color(0.05f, 0.3f, 0.36f, 1f);
            private static readonly Color s_OptionTextColor = new Color(0.92f, 0.94f, 0.96f, 1f);
            private static readonly Color s_SelectedOptionTextColor = new Color(0.2f, 0.9f, 1f, 1f);

            private readonly List<QualityOptionBinding> m_Options = new List<QualityOptionBinding>();
            private VideoQualitySurface m_Surface;
            private VideoPlayableHandle m_Playback;
            private bool m_IsSwitching;
            private int m_BindingVersion;

            private readonly struct QualityOptionBinding
            {
                public QualityOptionBinding(Button button, VideoQualitySelection selection)
                {
                    Button = button;
                    Selection = selection;
                }

                public Button Button { get; }

                public VideoQualitySelection Selection { get; }
            }

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
                BuildOptions();
                SetMenuVisible(false);
                SetVisible(true);
                Refresh();
            }

            public void Unbind()
            {
                if (m_Surface?.Button != null)
                {
                    m_Surface.Button.onClick.RemoveListener(OnClicked);
                    SetMenuVisible(false);
                    ClearOptions();
                    SetVisible(false);
                }

                m_Surface = null;
                m_Playback = null;
                m_IsSwitching = false;
                m_BindingVersion++;
            }

            private void OnClicked()
            {
                if (m_IsSwitching || m_Playback?.CanSelectQuality != true)
                {
                    return;
                }

                if (HasOptionMenu())
                {
                    SetMenuVisible(m_Surface.MenuRoot.gameObject.activeSelf is false);
                    return;
                }

                var playback = m_Playback;
                var selection = NextSelection(playback);
                BeginSwitch(playback, selection);
            }

            private void OnOptionClicked(VideoQualitySelection selection)
            {
                if (m_IsSwitching || m_Playback?.CanSelectQuality != true)
                {
                    return;
                }

                SetMenuVisible(false);
                if (selection.Equals(m_Playback.Quality))
                {
                    return;
                }

                BeginSwitch(m_Playback, selection);
            }

            private void BeginSwitch(VideoPlayableHandle playback, VideoQualitySelection selection)
            {
                var bindingVersion = m_BindingVersion;
                m_IsSwitching = true;
                SetInteractable(false);
                SwitchAsync(playback, selection, bindingVersion).Forget(Debug.LogException);
            }

            private async UniTask SwitchAsync(
                VideoPlayableHandle playback,
                VideoQualitySelection selection,
                int bindingVersion)
            {
                try
                {
                    await playback.SetQualityAsync(selection);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                finally
                {
                    if (bindingVersion == m_BindingVersion && ReferenceEquals(m_Playback, playback))
                    {
                        m_IsSwitching = false;
                        Refresh();
                    }
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

                SetInteractable(m_IsSwitching is false && m_Playback?.CanSelectQuality == true);
                SetVisible(HasDisplayableQuality(m_Playback));
                RefreshOptions();
            }

            private bool HasOptionMenu()
            {
                return m_Surface?.MenuRoot != null &&
                       m_Surface.OptionsRoot != null &&
                       m_Surface.OptionTemplate != null;
            }

            private void BuildOptions()
            {
                if (HasOptionMenu() is false || m_Playback == null)
                {
                    return;
                }

                ClearOptions();
                m_Surface.OptionTemplate.gameObject.SetActive(false);
                if (m_Playback.SupportsAutoQuality)
                {
                    AddOption("自动", new VideoQualitySelection(VideoQualityMode.Auto));
                }

                for (var i = 0; i < m_Playback.QualityOptions.Count; i++)
                {
                    var option = m_Playback.QualityOptions[i];
                    AddOption(
                        FormatQuality(option.Height),
                        new VideoQualitySelection(VideoQualityMode.FixedHeight, option.Height));
                }
            }

            private void AddOption(string label, VideoQualitySelection selection)
            {
                var button = UnityEngine.Object.Instantiate(
                    m_Surface.OptionTemplate,
                    m_Surface.OptionsRoot,
                    false);
                button.name = selection.Mode == VideoQualityMode.Auto
                    ? "QualityAuto"
                    : $"Quality{selection.Height}P";
                var text = button.GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                {
                    text.text = label;
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnOptionClicked(selection));
                button.gameObject.SetActive(true);
                m_Options.Add(new QualityOptionBinding(button, selection));
            }

            private void ClearOptions()
            {
                for (var i = 0; i < m_Options.Count; i++)
                {
                    var button = m_Options[i].Button;
                    if (button == null)
                    {
                        continue;
                    }

                    button.onClick.RemoveAllListeners();
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(button.gameObject);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(button.gameObject);
                    }
                }

                m_Options.Clear();
            }

            private void RefreshOptions()
            {
                for (var i = 0; i < m_Options.Count; i++)
                {
                    var option = m_Options[i];
                    if (option.Button == null)
                    {
                        continue;
                    }

                    var selected = m_Playback != null && option.Selection.Equals(m_Playback.Quality);
                    option.Button.interactable = m_IsSwitching is false &&
                                                m_Playback?.CanSelectQuality == true;
                    if (option.Button.targetGraphic is Graphic graphic)
                    {
                        graphic.color = selected ? s_SelectedOptionColor : s_OptionColor;
                    }

                    var text = option.Button.GetComponentInChildren<TMP_Text>(true);
                    if (text != null)
                    {
                        text.color = selected ? s_SelectedOptionTextColor : s_OptionTextColor;
                    }
                }
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
                return height == 1440 ? "2K" : height == 2160 ? "4K" : $"{height}P";
            }

            private void SetInteractable(bool value)
            {
                if (m_Surface?.Button != null)
                {
                    m_Surface.Button.interactable = value;
                }

                RefreshOptions();
            }

            private void SetMenuVisible(bool value)
            {
                if (m_Surface?.MenuRoot != null)
                {
                    m_Surface.MenuRoot.gameObject.SetActive(value);
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
                SetButtonText(m_Surface.PauseButton, m_Playback.IsPaused ? "\u25B6" : "II");
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
