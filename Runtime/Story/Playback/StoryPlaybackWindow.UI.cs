using System;
using System.Collections.Generic;
using System.Text;
using GameDeveloperKit.Playable;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Playback
{
    public partial class StoryPlaybackWindow
    {
        private const float LoadingSpinnerSpeed = 220f;

        private readonly List<Button> m_DefaultChoiceButtons = new List<Button>();
        private readonly List<ChoiceBinding> m_BoundChoiceButtons = new List<ChoiceBinding>();
        private readonly StringBuilder m_TextBuilder = new StringBuilder();
        private RawImage m_ImageOutput;
        private RawImage m_CurrentImageOutput;
        private CanvasGroup m_PresentationCanvasGroup;
        private RectTransform m_LoadingRoot;
        private RectTransform m_LoadingSpinner;
        private RectTransform m_DialogueRoot;
        private TMP_Text m_SpeakerText;
        private TMP_Text m_BodyText;
        private TMP_Text m_ErrorText;
        private Button m_ContinueButton;
        private Transform m_ChoiceRoot;
        private Button m_BoundContinueButton;
        private string m_CompletedText = "剧情已结束";

        private readonly struct ChoiceBinding
        {
            public ChoiceBinding(Button button, UnityAction action)
            {
                Button = button;
                Action = action;
            }

            public Button Button { get; }

            public UnityAction Action { get; }
        }

        internal IReadOnlyList<Button> DefaultChoiceButtons => m_DefaultChoiceButtons;

        public void SetPresentationVisible(bool visible)
        {
            if (m_PresentationCanvasGroup == null && GameObject != null)
            {
                m_PresentationCanvasGroup = GameObject.GetComponent<CanvasGroup>();
                if (m_PresentationCanvasGroup == null)
                {
                    m_PresentationCanvasGroup = GameObject.AddComponent<CanvasGroup>();
                }
            }

            if (m_PresentationCanvasGroup == null)
            {
                return;
            }

            m_PresentationCanvasGroup.alpha = visible ? 1f : 0f;
            m_PresentationCanvasGroup.interactable = visible;
            m_PresentationCanvasGroup.blocksRaycasts = visible;
        }

        public void SetLoadingVisible(bool visible)
        {
            if (m_LoadingRoot == null)
            {
                return;
            }

            m_LoadingRoot.gameObject.SetActive(visible);
            if (visible)
            {
                m_LoadingRoot.SetAsLastSibling();
            }
        }

        public PlaybackSurfaceView CreateDefaultSurfaceView()
        {
            return CreateDefaultSurfaceView(Array.Empty<Button>());
        }

        internal PlaybackSurfaceView CreateDefaultSurfaceView(IReadOnlyList<Button> choiceButtons)
        {
            return new PlaybackSurfaceView(
                m_ImageOutput,
                m_SpeakerText,
                m_BodyText,
                m_ContinueButton,
                choiceButtons);
        }

        private void BindStoryDocument()
        {
            if (Document == null)
            {
                throw new GameException("Story playback prefab is missing UIDocument.");
            }

            m_ImageOutput = Document.GetComponent<RawImage>("ImageOutput");
            m_SpeakerText = Document.GetComponent<TMP_Text>("SpeakerText");
            m_BodyText = Document.GetComponent<TMP_Text>("BodyText");
            m_ErrorText = Document.GetComponent<TMP_Text>("ErrorText");
            m_ContinueButton = Document.GetComponent<Button>("ContinueButton");
            m_ChoiceRoot = Document.GetComponent<Transform>("ChoiceRoot");
            Document.TryGetComponent("LoadingRoot", out m_LoadingRoot);
            Document.TryGetComponent("LoadingSpinner", out m_LoadingSpinner);
            if (Document.TryGetComponent("DialogueRoot", out RectTransform dialogueRoot))
            {
                m_DialogueRoot = dialogueRoot;
            }
            else
            {
                m_DialogueRoot = m_BodyText.transform.parent as RectTransform;
            }

            m_DefaultChoiceButtons.Clear();
            m_DefaultChoiceButtons.AddRange(m_ChoiceRoot.GetComponentsInChildren<Button>(true));
        }

        private void ReleaseStoryDocument()
        {
            ClearBoundInputs();
            m_DefaultChoiceButtons.Clear();
            m_ImageOutput = null;
            m_CurrentImageOutput = null;
            m_PresentationCanvasGroup = null;
            m_LoadingRoot = null;
            m_LoadingSpinner = null;
            m_DialogueRoot = null;
            m_SpeakerText = null;
            m_BodyText = null;
            m_ErrorText = null;
            m_ContinueButton = null;
            m_ChoiceRoot = null;
        }

        private void ResetStoryPresentation()
        {
            for (var i = 0; i < m_DefaultChoiceButtons.Count; i++)
            {
                if (m_DefaultChoiceButtons[i] != null)
                {
                    m_DefaultChoiceButtons[i].gameObject.SetActive(false);
                }
            }

            SetDefaultDialogueVisible(false);
            SetLoadingVisible(false);
            ClearError();
            ClearImageOutput(m_ImageOutput);
        }

        private void PresentFrame(Frame frame)
        {
            var channel = ResolveInteractionChannel();
            ClearBoundInputs();
            m_DefaultInteractionChannel?.ClearTransientInputs();
            NotifyEpisodeChanged(channel, frame);
            m_CurrentFrame = frame;
            channel.OnFrameChanged(frame);
            RenderTextSurface(channel, frame);
            BindContinueSurface(channel, frame);
            BindChoiceSurface(channel, frame);
            ResolveImageOutput(channel, frame);
            DispatchStoryInstructions(frame);
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
            m_ChoiceVideoPrewarmEpisode = null;
            if (nextEpisode == null)
            {
                return;
            }

            // 进入新集立即预热本集选项分支视频（含纯选项帧开头的集）。
            PrewarmEpisodeChoiceVideos(frame);

            var context = new EpisodeInteractionContext(
                RequireStoryModule(),
                frame.Program?.StoryId,
                frame.Program,
                previousEpisode,
                nextEpisode,
                frame);
            channel.OnEpisodeChanged(context);
            OnEpisodeChanged(context);
        }

        private void RenderTextSurface(IInteractionChannel channel, Frame frame)
        {
            var surface = RequireSurface(channel, new InteractionRequest(
                InteractionRequestKind.Text,
                frame,
                choices: frame?.Choices));
            if (frame == null)
            {
                SetText(surface.SpeakerText, null);
                SetText(surface.BodyText, null);
                SetDefaultDialogueVisible(false);
                return;
            }

            m_TextBuilder.Length = 0;
            string speaker = null;
            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track?.Kind != FrameTrackKind.Text)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(speaker) && track.SpeakerText.HasValue)
                {
                    speaker = ResolveText(track.SpeakerText.Value);
                }

                if (m_TextBuilder.Length > 0)
                {
                    m_TextBuilder.AppendLine();
                }

                if (track.Text.HasValue)
                {
                    m_TextBuilder.Append(ResolveText(track.Text.Value));
                }
            }

            var body = frame.IsCompleted ? m_CompletedText : m_TextBuilder.ToString();
            SetText(surface.SpeakerText, speaker);
            SetText(surface.BodyText, body);
            SetDefaultDialogueVisible(
                UsesDefaultDialogueSurface(surface) &&
                (string.IsNullOrWhiteSpace(speaker) is false ||
                 string.IsNullOrWhiteSpace(body) is false ||
                 CanContinue(frame)));
        }

        private void BindContinueSurface(IInteractionChannel channel, Frame frame)
        {
            if (CanContinue(frame) is false)
            {
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

            m_BoundContinueButton = surface.ContinueButton;
            m_BoundContinueButton.onClick.AddListener(ContinueFromInteraction);
            m_BoundContinueButton.gameObject.SetActive(true);
        }

        private void BindChoiceSurface(IInteractionChannel channel, Frame frame)
        {
            if (!ReferenceEquals(channel, m_DefaultInteractionChannel) ||
                frame?.Choices == null ||
                frame.Choices.Count == 0)
            {
                return;
            }

            var surface = RequireSurface(channel, new InteractionRequest(
                InteractionRequestKind.Choice,
                frame,
                choices: frame.Choices));
            if (surface.ChoiceButtons.Count != frame.Choices.Count)
            {
                throw new GameException(
                    $"Story choice button count does not match choices. choices:{frame.Choices.Count} " +
                    $"buttons:{surface.ChoiceButtons.Count}");
            }

            for (var i = 0; i < frame.Choices.Count; i++)
            {
                var button = surface.ChoiceButtons[i];
                var choice = frame.Choices[i];
                if (button == null || choice == null)
                {
                    throw new GameException($"Story choice surface is missing. index:{i}");
                }

                var choiceId = choice.ChoiceId;
                UnityAction action = () => Select(choiceId);
                button.onClick.AddListener(action);
                m_BoundChoiceButtons.Add(new ChoiceBinding(button, action));
            }
        }

        private void ResolveImageOutput(IInteractionChannel channel, Frame frame)
        {
            m_CurrentImageOutput = m_ImageOutput;
            if (frame?.Tracks == null)
            {
                return;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track?.Kind != FrameTrackKind.Instruction ||
                    track.Instruction is not StoryInstruction.ShowImage)
                {
                    continue;
                }

                var surface = RequireSurface(channel, new InteractionRequest(
                    InteractionRequestKind.Image,
                    frame,
                    track,
                    frame.Choices));
                m_CurrentImageOutput = surface.ImageOutput ??
                    throw new GameException(
                        $"Story image output surface is missing. instruction:{track.Instruction.InstructionId}");
                return;
            }
        }

        private void ClearStoryPresentation()
        {
            ClearBoundInputs();
            m_DefaultInteractionChannel?.ClearTransientInputs();
            SetText(m_SpeakerText, null);
            SetText(m_BodyText, null);
            SetDefaultDialogueVisible(false);
            ClearError();
            ClearImageOutput(m_CurrentImageOutput);
            if (!ReferenceEquals(m_CurrentImageOutput, m_ImageOutput))
            {
                ClearImageOutput(m_ImageOutput);
            }

            m_CurrentImageOutput = null;
        }

        private void ClearBoundInputs()
        {
            if (m_BoundContinueButton != null)
            {
                m_BoundContinueButton.onClick.RemoveListener(ContinueFromInteraction);
                m_BoundContinueButton.gameObject.SetActive(false);
                m_BoundContinueButton = null;
            }

            for (var i = 0; i < m_BoundChoiceButtons.Count; i++)
            {
                var binding = m_BoundChoiceButtons[i];
                binding.Button?.onClick.RemoveListener(binding.Action);
            }

            m_BoundChoiceButtons.Clear();
        }

        private void ContinueFromInteraction()
        {
            Continue();
        }

        private IInteractionChannel ResolveInteractionChannel()
        {
            EnsureDefaultInteractionChannel();
            return m_InteractionChannelOverride ?? m_DefaultInteractionChannel;
        }

        private void EnsureDefaultInteractionChannel()
        {
            m_DefaultInteractionChannel ??= new DefaultInteractionChannel(this);
        }

        private static PlaybackSurfaceView RequireSurface(
            IInteractionChannel channel,
            InteractionRequest request)
        {
            return channel.GetPlaybackSurfaceView(request) ??
                throw new GameException($"Story interaction surface is missing. kind:{request.Kind}");
        }

        private void SetDefaultDialogueVisible(bool visible)
        {
            if (m_DialogueRoot != null)
            {
                m_DialogueRoot.gameObject.SetActive(visible);
            }

            if (!visible && m_ContinueButton != null)
            {
                m_ContinueButton.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// surface 是否使用默认播放器的对白控件。
        /// 自定义对白 surface 的交互通道（如选项页通道）激活时，默认对白栏保持隐藏。
        /// </summary>
        private bool UsesDefaultDialogueSurface(PlaybackSurfaceView surface)
        {
            return surface != null &&
                   ReferenceEquals(surface.SpeakerText, m_SpeakerText) &&
                   ReferenceEquals(surface.BodyText, m_BodyText);
        }

        private void UpdateLoadingSpinner(float unscaledDeltaTime)
        {
            if (m_LoadingSpinner != null && m_LoadingSpinner.gameObject.activeInHierarchy)
            {
                m_LoadingSpinner.Rotate(0f, 0f, -LoadingSpinnerSpeed * Mathf.Max(0f, unscaledDeltaTime));
            }
        }

        private void SetError(Exception exception)
        {
            LastError = exception;
            if (m_ErrorText == null)
            {
                return;
            }

            m_ErrorText.text = exception?.Message ?? string.Empty;
            m_ErrorText.gameObject.SetActive(exception != null);
        }

        private void ClearError()
        {
            SetError(null);
        }

        private static bool CanContinue(Frame frame)
        {
            return frame != null &&
                   frame.IsCompleted is false &&
                   frame.WaitsForChoice is false &&
                   frame.WaitsForInstruction is false &&
                   frame.WaitsForTime is false;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }

        private static void ClearImageOutput(RawImage output)
        {
            if (output == null)
            {
                return;
            }

            VideoSurfaceBinder.Bind(output, null, false, VideoDisplayMode.FitInside);
            output.gameObject.SetActive(false);
        }
    }
}
