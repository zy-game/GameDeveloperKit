using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Protocol;
using UnityEngine.UI;

namespace GameDeveloperKit.Story.Playback
{
    public partial class PlaybackView
    {
        /// <summary>
        /// 播放会话预热前初始化业务展示界面。
        /// </summary>
        /// <param name="context">播放上下文。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>初始化任务。</returns>
        protected virtual UniTask OnPlaybackAwakeAsync(
            InteractionContext context,
            System.Threading.CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 展示当前剧情帧的全部动作。
        /// </summary>
        /// <param name="operations">已解析的展示动作。</param>
        protected virtual void ShowOperation(params PlaybackOperation[] operations)
        {
            ShowDefaultOperations(m_CurrentFrame);
        }

        /// <summary>
        /// 清理当前剧情帧的展示。
        /// </summary>
        protected virtual void ClearOperations()
        {
            ClearDefaultOperations();
        }

        /// <summary>
        /// 设置业务展示界面提供的媒体 surface。
        /// </summary>
        /// <param name="surface">业务展示界面提供的 surface。</param>
        protected void SetPlaybackSurface(PlaybackSurfaceView surface)
        {
            m_CustomPlaybackSurface = surface ?? throw new ArgumentNullException(nameof(surface));
            m_CurrentVideoOutput = surface.VideoOutput;
            m_CurrentImageOutput = surface.ImageOutput;
            m_CurrentVideoSeek = surface.VideoSeek;
            m_CurrentVideoQuality = surface.VideoQuality;
        }

        private void RenderFrame(Frame frame)
        {
            var channel = ResolveInteractionChannel();
            NotifyEpisodeChanged(channel, frame);
            BeginVideoTransition(frame);
            PrepareOperations();
            ShowOperation(BuildOperations(frame));
            UpdateVideoOutput();
        }

        private void PrepareOperations()
        {
            ClearBoundInputs();
            m_DefaultInteractionChannel?.ClearTransientInputs();
            m_VideoSeekBinder?.Unbind();
            m_VideoQualityBinder?.Unbind();
            HideDefaultMediaOutput(m_VideoOutput, m_RetainedVideoOutput);
            HideDefaultMediaOutput(m_ImageOutput, null);
            m_UseDefaultPlaybackSurface = false;
            m_CurrentVideoOutput = m_CustomPlaybackSurface?.VideoOutput;
            m_CurrentImageOutput = m_CustomPlaybackSurface?.ImageOutput;
            m_CurrentVideoSeek = m_CustomPlaybackSurface?.VideoSeek;
            m_CurrentVideoQuality = m_CustomPlaybackSurface?.VideoQuality;
            SetSpeakerText(m_SpeakerText, null);
            SetBodyText(m_BodyText, null);
            SetDefaultDialogueVisible(false);
        }

        private void ShowDefaultOperations(Frame frame)
        {
            m_UseDefaultPlaybackSurface = true;
            var channel = ResolveInteractionChannel();
            SetDefaultDialogueVisible(ReferenceEquals(channel, m_DefaultInteractionChannel));
            channel.OnFrameChanged(frame);
            RenderTextSurface(channel, frame);
            BindContinueSurface(channel, frame);
            BindChoiceSurface(channel, frame);
            UpdateMediaSurfaces(channel, frame);
        }

        private void ClearDefaultOperations()
        {
            RetainCurrentVideoOutput();
            ClearBoundInputs();
            m_DefaultInteractionChannel?.ClearTransientInputs();
            m_VideoSeekBinder?.Unbind();
            m_VideoQualityBinder?.Unbind();
            m_UseDefaultPlaybackSurface = false;
            m_CurrentVideoOutput = null;
            m_CurrentImageOutput = null;
            m_CurrentVideoSeek = null;
            m_CurrentVideoQuality = null;
            SetSpeakerText(m_SpeakerText, null);
            SetBodyText(m_BodyText, null);
            SetDefaultDialogueVisible(false);
            HideDefaultMediaOutput(m_VideoOutput, m_RetainedVideoOutput);
            HideDefaultMediaOutput(m_ImageOutput, null);
        }

        private void BeginVideoTransition(Frame frame)
        {
            if (m_RetainedVideoOutput == null)
            {
                RetainCurrentVideoOutput();
            }

            m_VideoTransitionPending = ContainsVideo(frame);
            if (m_VideoTransitionPending is false)
            {
                ClearRetainedVideoOutput();
            }
        }

        private void RetainCurrentVideoOutput()
        {
            var output = m_CurrentVideoOutput ??
                         (m_UseDefaultPlaybackSurface ? m_VideoOutput : null);
            m_RetainedVideoOutput = output != null &&
                                    output.gameObject.activeSelf &&
                                    output.texture != null
                ? output
                : null;
        }

        private void CompleteVideoTransition(RawImage output)
        {
            var retainedOutput = m_RetainedVideoOutput;
            m_RetainedVideoOutput = null;
            m_VideoTransitionPending = false;
            if (retainedOutput != null && ReferenceEquals(retainedOutput, output) is false)
            {
                ClearMediaOutput(retainedOutput);
            }
        }

        private void ClearRetainedVideoOutput()
        {
            var retainedOutput = m_RetainedVideoOutput;
            m_RetainedVideoOutput = null;
            m_VideoTransitionPending = false;
            ClearMediaOutput(retainedOutput);
        }

        private static bool ContainsVideo(Frame frame)
        {
            if (frame?.Tracks == null)
            {
                return false;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track?.Kind == FrameTrackKind.Command &&
                    string.Equals(track.Command?.Name, MediaCommandNames.PlayVideo, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private PlaybackOperation[] BuildOperations(Frame frame)
        {
            var operations = new List<PlaybackOperation>();
            if (frame?.Tracks != null)
            {
                for (var i = 0; i < frame.Tracks.Count; i++)
                {
                    var track = frame.Tracks[i];
                    if (track == null)
                    {
                        continue;
                    }

                    switch (track.Kind)
                    {
                        case FrameTrackKind.Text:
                            AddTextOperation(operations, frame, track);
                            break;
                        case FrameTrackKind.Command:
                            AddCommandOperation(operations, frame, track);
                            break;
                        case FrameTrackKind.Wait:
                            operations.Add(new PlaybackOperation(
                                PlaybackOperationKind.Wait,
                                frame,
                                track,
                                waitSeconds: track.WaitSeconds));
                            break;
                    }
                }
            }

            if (frame?.Choices != null && frame.Choices.Count > 0)
            {
                operations.Add(new PlaybackOperation(
                    PlaybackOperationKind.Choices,
                    frame,
                    choices: BuildChoices(frame)));
            }

            if (frame != null &&
                frame.IsCompleted is false &&
                frame.WaitsForChoice is false &&
                frame.WaitsForCommand is false &&
                frame.WaitsForTime is false)
            {
                operations.Add(new PlaybackOperation(PlaybackOperationKind.Continue, frame));
            }

            if (frame?.IsCompleted == true)
            {
                operations.Add(new PlaybackOperation(
                    PlaybackOperationKind.Completed,
                    frame,
                    text: m_CompletedText));
            }

            return operations.ToArray();
        }

        private void AddTextOperation(List<PlaybackOperation> operations, Frame frame, FrameTrack track)
        {
            var speaker = track.SpeakerText.HasValue ? ResolveText(track.SpeakerText.Value) : null;
            var text = track.Text.HasValue ? ResolveText(track.Text.Value) : string.Empty;
            var kind = track.SpeakerText.HasValue
                ? PlaybackOperationKind.Dialogue
                : PlaybackOperationKind.Narration;
            operations.Add(new PlaybackOperation(kind, frame, track, speaker: speaker, text: text));
        }

        private void AddCommandOperation(List<PlaybackOperation> operations, Frame frame, FrameTrack track)
        {
            var command = track.Command;
            if (command == null)
            {
                return;
            }

            var kind = string.Equals(command.Name, MediaCommandNames.PlayVideo, StringComparison.Ordinal)
                ? PlaybackOperationKind.Video
                : string.Equals(command.Name, MediaCommandNames.ShowImage, StringComparison.Ordinal)
                    ? PlaybackOperationKind.Image
                    : string.Equals(command.Name, MediaCommandNames.PlayAudio, StringComparison.Ordinal)
                        ? PlaybackOperationKind.Audio
                        : PlaybackOperationKind.Command;
            operations.Add(new PlaybackOperation(kind, frame, track, command));
        }

        private IReadOnlyList<PlaybackChoice> BuildChoices(Frame frame)
        {
            var choices = new List<PlaybackChoice>(frame.Choices.Count);
            for (var i = 0; i < frame.Choices.Count; i++)
            {
                var choice = frame.Choices[i];
                if (choice != null)
                {
                    choices.Add(new PlaybackChoice(choice, ResolveText(choice.Text)));
                }
            }

            return choices;
        }

        private void HideDefaultMediaOutputs()
        {
            HideDefaultMediaOutput(m_VideoOutput, m_CurrentVideoOutput);
            HideDefaultMediaOutput(m_ImageOutput, m_CurrentImageOutput);
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

        private static void HideDefaultMediaOutput(RawImage defaultOutput, RawImage activeOutput)
        {
            if (defaultOutput == null || ReferenceEquals(defaultOutput, activeOutput))
            {
                return;
            }

            ClearMediaOutput(defaultOutput);
        }

        private static void ClearMediaOutput(RawImage output)
        {
            if (output == null)
            {
                return;
            }

            output.texture = null;
            output.uvRect = s_DefaultVideoUvRect;
            output.gameObject.SetActive(false);
        }
    }
}
