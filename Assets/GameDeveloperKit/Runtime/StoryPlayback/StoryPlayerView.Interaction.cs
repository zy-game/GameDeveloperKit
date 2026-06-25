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
    public sealed partial class StoryPlayerView : MonoBehaviour, IStoryFramePresenter, IStoryPlaybackHost
    {
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
    }
}
