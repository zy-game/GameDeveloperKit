using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Media;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story.Events;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;
using UnityEngine;

namespace GameDeveloperKit.Story.Playback
{
    public partial class StoryPlaybackWindow
    {
        private const char CommandKeySeparator = '\u001f';

        private readonly HashSet<string> m_DispatchedCommandKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private Frame m_DispatchedFrame;
        private ImagePlayableHandle m_ImagePlayback;
        private AudioPlayableHandle m_AudioPlayback;
        private StoryInstruction.PlayVideo m_CurrentVideoInstruction;
        private string m_CurrentVideoCommandKey;
        private string m_CurrentAudioCommandKey;
        private int m_VideoOperationVersion;
        private int m_ImageOperationVersion;
        private int m_AudioOperationVersion;

        private void DispatchStoryCommands(Frame frame)
        {
            if (ReferenceEquals(m_DispatchedFrame, frame) is false)
            {
                StopCommandsMissingFromFrame(frame);
                m_DispatchedFrame = frame;
                m_DispatchedCommandKeys.Clear();
            }

            EnsureStoryInstructionsSupported(frame);
            if (frame?.Instructions == null)
            {
                return;
            }

            for (var i = 0; i < frame.Instructions.Count; i++)
            {
                var instruction = frame.Instructions[i];
                var key = BuildInstructionKey(instruction);
                if (m_DispatchedCommandKeys.Add(key) is false)
                {
                    continue;
                }

                DispatchStoryInstruction(frame, instruction, key);
                if (ReferenceEquals(m_CurrentFrame, frame) is false)
                {
                    return;
                }
            }
        }

        private void DispatchStoryInstruction(
            Frame frame,
            StoryInstruction instruction,
            string commandKey)
        {
            switch (instruction)
            {
                case StoryInstruction.PlayVideo video:
                    PlayVideoInstructionAsync(video, commandKey, SessionVersion, SessionCancellationToken)
                        .Forget(SetPlaybackError);
                    return;
                case StoryInstruction.ShowImage image:
                    PlayImageInstructionAsync(image, SessionVersion, SessionCancellationToken)
                        .Forget(SetPlaybackError);
                    return;
                case StoryInstruction.PlayAudio audio:
                    PlayAudioInstructionAsync(audio, commandKey, SessionVersion, SessionCancellationToken)
                        .Forget(SetPlaybackError);
                    return;
                case StoryInstruction.Unlock unlock:
                    DispatchUnlock(frame, unlock);
                    return;
                default:
                    throw new GameException(
                        $"Story instruction type is not supported. type:{instruction?.GetType().Name}");
            }
        }

        private async UniTask PlayVideoInstructionAsync(
            StoryInstruction.PlayVideo instruction,
            string commandKey,
            int sessionVersion,
            System.Threading.CancellationToken cancellationToken)
        {
            var operationVersion = ++m_VideoOperationVersion;
            m_CurrentVideoInstruction = instruction;
            m_CurrentVideoCommandKey = commandKey;
            var request = VideoRequestFactory.Create(
                instruction.Reference,
                App.Config.MediaDelivery,
                instruction.Loop,
                instruction.Seekable,
                PlaybackRoot,
                false);
            var playback = await base.PlayAsync(
                request.Path,
                CurrentFrame?.Episode?.Title,
                request.Options.Seekable,
                request.Options,
                cancellationToken);
            if (!IsSessionCurrent(sessionVersion) || operationVersion != m_VideoOperationVersion)
            {
                playback.Stop();
                playback.Dispose();
            }
        }

        private async UniTask PlayImageInstructionAsync(
            StoryInstruction.ShowImage instruction,
            int sessionVersion,
            System.Threading.CancellationToken cancellationToken)
        {
            var output = m_CurrentImageOutput ??
                throw new GameException($"Story image output surface is missing. command:{instruction.CommandId}");
            var operationVersion = ++m_ImageOperationVersion;
            StopAndDispose(ref m_ImagePlayback);
            var playback = await App.Playable.Image.PlayAsync(
                new ImagePlayableRequest(instruction.Location, texture =>
                {
                    if (operationVersion != m_ImageOperationVersion)
                    {
                        return;
                    }

                    output.texture = texture;
                    output.gameObject.SetActive(texture != null);
                }),
                cancellationToken);
            if (!IsSessionCurrent(sessionVersion) || operationVersion != m_ImageOperationVersion)
            {
                playback.Stop();
                playback.Dispose();
                return;
            }

            m_ImagePlayback = playback;
            AdvanceCompletedInstruction(instruction);
        }

        private async UniTask PlayAudioInstructionAsync(
            StoryInstruction.PlayAudio instruction,
            string commandKey,
            int sessionVersion,
            System.Threading.CancellationToken cancellationToken)
        {
            var operationVersion = ++m_AudioOperationVersion;
            StopAndDispose(ref m_AudioPlayback);
            m_CurrentAudioCommandKey = commandKey;
            var playback = await App.Playable.Audio.PlayAsync(CreateAudioRequest(instruction), cancellationToken);
            if (!IsSessionCurrent(sessionVersion) || operationVersion != m_AudioOperationVersion)
            {
                playback.Stop();
                playback.Dispose();
                return;
            }

            m_AudioPlayback = playback;
            await playback.WaitForCompletionAsync();
            if (!IsSessionCurrent(sessionVersion) ||
                operationVersion != m_AudioOperationVersion ||
                playback.Status != PlayableStatus.Completed)
            {
                return;
            }

            m_CurrentAudioCommandKey = null;
            AdvanceCompletedInstruction(instruction);
        }

        private void DispatchUnlock(Frame frame, StoryInstruction.Unlock instruction)
        {
            App.Event.FireNow(
                new StoryUnlockEvent(
                    frame.Program?.StoryId,
                    frame.Volume?.VolumeId,
                    frame.Episode?.EpisodeId,
                    instruction.Step?.StepId,
                    instruction.UnlockId),
                this);
            AdvanceCompletedInstruction(instruction);
        }

        private void HandleStoryVideoCompleted(VideoPlayableHandle playback)
        {
            if (!ReferenceEquals(Playback, playback) || m_CurrentVideoInstruction == null)
            {
                return;
            }

            var instruction = m_CurrentVideoInstruction;
            m_CurrentVideoInstruction = null;
            m_CurrentVideoCommandKey = null;
            AdvanceCompletedInstruction(instruction);
        }

        private void StopCommandsMissingFromFrame(Frame frame)
        {
            var keys = CollectCommandKeys(frame);
            if (m_CurrentVideoInstruction?.RequiresCompletion == true &&
                keys.Contains(m_CurrentVideoCommandKey) is false)
            {
                m_VideoOperationVersion++;
                m_CurrentVideoInstruction = null;
                m_CurrentVideoCommandKey = null;
                StopCurrentVideo();
            }

            if (string.IsNullOrEmpty(m_CurrentAudioCommandKey) is false &&
                keys.Contains(m_CurrentAudioCommandKey) is false)
            {
                m_AudioOperationVersion++;
                m_CurrentAudioCommandKey = null;
                StopAndDispose(ref m_AudioPlayback);
            }
        }

        private void StopStoryMedia()
        {
            m_VideoOperationVersion++;
            m_ImageOperationVersion++;
            m_AudioOperationVersion++;
            m_CurrentVideoInstruction = null;
            m_CurrentVideoCommandKey = null;
            m_CurrentAudioCommandKey = null;
            m_DispatchedFrame = null;
            m_DispatchedCommandKeys.Clear();
            StopAndDispose(ref m_ImagePlayback);
            StopAndDispose(ref m_AudioPlayback);
        }

        private static AudioPlayableRequest CreateAudioRequest(StoryInstruction.PlayAudio instruction)
        {
            var reference = instruction.Reference;
            var location = reference.Source == MediaSource.Cdn
                ? MediaUrlResolver.Resolve(new MediaPath(reference.Location), App.Config.MediaDelivery)
                : reference.Location;
            return new AudioPlayableRequest(
                location,
                ToAudioLocationKind(reference.Source),
                new AudioPlayableOptions
                {
                    Loop = instruction.Loop,
                    Volume = instruction.Volume,
                    Priority = instruction.Priority
                });
        }

        private static AudioLocationKind ToAudioLocationKind(MediaSource source)
        {
            return source switch
            {
                MediaSource.Cdn => AudioLocationKind.Url,
                MediaSource.StreamingAssets => AudioLocationKind.StreamingAssets,
                MediaSource.Resource => AudioLocationKind.Resource,
                _ => throw new ArgumentOutOfRangeException(nameof(source))
            };
        }

        private static void EnsureStoryInstructionsSupported(Frame frame)
        {
            if (frame?.Tracks == null)
            {
                return;
            }

            var supported = CollectInstructionKeys(frame);
            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track?.Kind != FrameTrackKind.Command || track.Command == null)
                {
                    continue;
                }

                if (supported.Contains(BuildCommandKey(track)))
                {
                    continue;
                }

                throw new GameException(
                    $"Story instruction is not supported by the playback window. " +
                    $"command:{track.Command.CommandId} name:{track.Command.Name}");
            }
        }

        private static string BuildCommandKey(FrameTrack track)
        {
            var branchId = string.IsNullOrWhiteSpace(track.BranchId) ? string.Empty : track.BranchId;
            return branchId + CommandKeySeparator + track.Command.CommandId;
        }

        private static string BuildInstructionKey(StoryInstruction instruction)
        {
            var branchId = string.IsNullOrWhiteSpace(instruction.BranchId)
                ? string.Empty
                : instruction.BranchId;
            return branchId + CommandKeySeparator + instruction.CommandId;
        }

        private static HashSet<string> CollectInstructionKeys(Frame frame)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (frame?.Instructions == null)
            {
                return keys;
            }

            for (var i = 0; i < frame.Instructions.Count; i++)
            {
                keys.Add(BuildInstructionKey(frame.Instructions[i]));
            }

            return keys;
        }

        private static HashSet<string> CollectCommandKeys(Frame frame)
        {
            return CollectInstructionKeys(frame);
        }

        private static void StopAndDispose<T>(ref T playback)
            where T : PlayableHandle
        {
            var current = playback;
            playback = null;
            current?.Stop();
            current?.Dispose();
        }
    }
}
