using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Text;

namespace GameDeveloperKit.Story.Playback
{
    public sealed partial class PlayerView : MonoBehaviour, IFramePresenter, IPlaybackHost
    {
        private void RenderFrame(Frame frame)
        {
            var channel = ResolveInteractionChannel();
            NotifyEpisodeChanged(channel, frame);
            ClearBoundInputs();
            channel.OnFrameChanged(frame);
            RenderTextSurface(channel, frame);
            BindContinueSurface(channel, frame);
            BindChoiceSurface(channel, frame);
            UpdateMediaSurfaces(channel, frame);
        }

        private void ClearFrameUi()
        {
            ClearBoundInputs();
            var channel = ResolveInteractionChannel();
            channel.OnFrameChanged(null);
            RenderTextSurface(channel, null);
            BindContinueSurface(channel, null);
        }

        private void NotifyEpisodeChanged(IInteractionChannel channel, Frame frame)
        {
            var nextEpisode = frame?.Episode;
            if (ReferenceEquals(m_CurrentEpisode, nextEpisode))
            {
                return;
            }

            var previousEpisode = m_CurrentEpisode;
            m_CurrentEpisode = nextEpisode;
            if (nextEpisode == null)
            {
                return;
            }

            channel.OnEpisodeChanged(new EpisodeInteractionContext(
                m_StoryModule,
                m_Presenter,
                frame.Program?.StoryId,
                frame.Program,
                previousEpisode,
                nextEpisode,
                frame));
        }

        private void UpdateMediaSurfaces(IInteractionChannel channel, Frame frame)
        {
            m_CurrentVideoOutput = null;
            m_CurrentImageOutput = null;
            m_CurrentVideoSeek = null;
            m_CurrentVideoQuality = null;
            if (frame?.Tracks == null)
            {
                m_VideoSeekBinder?.Unbind();
                m_VideoQualityBinder?.Unbind();
                return;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                var command = track?.Command;
                if (track?.Kind != FrameTrackKind.Command || command == null)
                {
                    continue;
                }

                if (string.Equals(command.Name, MediaCommandNames.PlayVideo, StringComparison.Ordinal))
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
                    m_CurrentVideoQuality = surface.VideoQuality;
                }
                else if (string.Equals(command.Name, MediaCommandNames.ShowImage, StringComparison.Ordinal))
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

        private void RenderTextSurface(IInteractionChannel channel, Frame frame)
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
                    if (track?.Kind != FrameTrackKind.Text)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(speaker))
                    {
                        speaker = track.SpeakerText.HasValue ? ResolveText(track.SpeakerText.Value) : null;
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

                    if (track.Text.HasValue)
                    {
                        m_TextBuilder.Append(ResolveText(track.Text.Value));
                    }
                }
            }

            SetSpeakerText(speakerText, speaker);
            SetBodyText(bodyText, frame.IsCompleted ? m_CompletedText : m_TextBuilder.ToString());
        }

        private void BindContinueSurface(IInteractionChannel channel, Frame frame)
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

        private void BindChoiceSurface(IInteractionChannel channel, Frame frame)
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
                SetButtonText(button, ResolveText(choice.Text));
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

            var legacyText = button.GetComponentInChildren<UnityEngine.UI.Text>(true);
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

    }

    internal sealed class LocalizationTextResolver : ITextResolver
    {
        public string Resolve(TextReference reference)
        {
            return reference.Mode == TextMode.Literal
                ? reference.Value
                : App.Localization.GetText(reference.Value);
        }
    }
}
