using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Media;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story.Events;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Model;
using UnityEngine;

namespace GameDeveloperKit.Story.Playback
{
    public partial class StoryPlaybackWindow
    {
        private const char InstructionKeySeparator = '\u001f';

        private readonly HashSet<string> m_DispatchedInstructionKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private Frame m_DispatchedFrame;
        private ImagePlayableHandle m_ImagePlayback;
        private AudioPlayableHandle m_AudioPlayback;
        private StoryInstruction.PlayVideo m_CurrentVideoInstruction;
        private string m_CurrentVideoInstructionKey;
        private string m_CurrentAudioInstructionKey;
        private int m_VideoOperationVersion;
        private int m_ImageOperationVersion;
        private int m_AudioOperationVersion;

        private void DispatchStoryInstructions(Frame frame)
        {
            if (ReferenceEquals(m_DispatchedFrame, frame) is false)
            {
                StopInstructionsMissingFromFrame(frame);
                m_DispatchedFrame = frame;
                m_DispatchedInstructionKeys.Clear();
            }

            if (frame?.Instructions == null)
            {
                return;
            }

            for (var i = 0; i < frame.Instructions.Count; i++)
            {
                var instruction = frame.Instructions[i];
                var key = BuildInstructionKey(instruction);
                if (m_DispatchedInstructionKeys.Add(key) is false)
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
            string instructionKey)
        {
            switch (instruction)
            {
                case StoryInstruction.PlayVideo video:
                    if (ShouldSkipVideoRestart(video))
                    {
                        return;
                    }

                    PlayVideoInstructionAsync(video, instructionKey, SessionVersion, SessionCancellationToken)
                        .Forget(SetPlaybackError);
                    return;
                case StoryInstruction.ShowImage image:
                    PlayImageInstructionAsync(image, SessionVersion, SessionCancellationToken)
                        .Forget(SetPlaybackError);
                    return;
                case StoryInstruction.PlayAudio audio:
                    PlayAudioInstructionAsync(audio, instructionKey, SessionVersion, SessionCancellationToken)
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
            string instructionKey,
            int sessionVersion,
            System.Threading.CancellationToken cancellationToken)
        {
            var operationVersion = ++m_VideoOperationVersion;
            m_CurrentVideoInstruction = instruction;
            m_CurrentVideoInstructionKey = instructionKey;
            var request = VideoRequestFactory.Create(
                instruction.Reference,
                App.Config.MediaDelivery,
                instruction.Loop,
                instruction.Seekable,
                PlaybackRoot,
                false,
                PreferredPreloadTargetHeight,
                App.Config.MediaDelivery.UseDisplayUGUI);
            var playback = await base.PlayAsync(
                request.Path,
                CurrentFrame?.Episode?.Title,
                request.Options.Seekable,
                request.Options,
                cancellationToken,
                showPlaybackFeatures: true);
            if (!IsSessionCurrent(sessionVersion) || operationVersion != m_VideoOperationVersion)
            {
                playback.Stop();
                playback.Dispose();
            }
        }

        private int PreferredPreloadTargetHeight =>
            m_PreferredQuality.Mode == VideoQualityMode.FixedHeight
                ? m_PreferredQuality.Height
                : 0;

        /// <summary>
        /// 并行帧合并时同一视频指令会随帧变化重复分发（如循环视频背景轨道）：
        /// 若正在播放同一路径的视频则跳过重启，避免停止后冷启动重播。
        /// </summary>
        private bool ShouldSkipVideoRestart(StoryInstruction.PlayVideo video)
        {
            if (video == null || m_CurrentVideoInstruction == null || Playback == null)
            {
                return false;
            }

            var request = VideoRequestFactory.Create(
                video.Reference,
                App.Config.MediaDelivery,
                video.Loop,
                video.Seekable,
                PlaybackRoot,
                false,
                PreferredPreloadTargetHeight,
                App.Config.MediaDelivery.UseDisplayUGUI);
            return string.Equals(request.Path, Playback.RequestPath, StringComparison.Ordinal);
        }

        private async UniTask PlayImageInstructionAsync(
            StoryInstruction.ShowImage instruction,
            int sessionVersion,
            System.Threading.CancellationToken cancellationToken)
        {
            var output = m_CurrentImageOutput ??
                throw new GameException($"Story image output surface is missing. instruction:{instruction.InstructionId}");
            var operationVersion = ++m_ImageOperationVersion;
            StopAndDispose(ref m_ImagePlayback);
            var playback = await App.Playable.Image.PlayAsync(
                new ImagePlayableRequest(instruction.Location, texture =>
                {
                    if (operationVersion != m_ImageOperationVersion)
                    {
                        return;
                    }

                    VideoSurfaceBinder.Bind(output, texture, false, VideoDisplayMode.FitInside);
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
            string instructionKey,
            int sessionVersion,
            System.Threading.CancellationToken cancellationToken)
        {
            var operationVersion = ++m_AudioOperationVersion;
            StopAndDispose(ref m_AudioPlayback);
            m_CurrentAudioInstructionKey = instructionKey;
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

            m_CurrentAudioInstructionKey = null;
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
            m_CurrentVideoInstructionKey = null;
            AdvanceCompletedInstruction(instruction);
        }

        private void StopInstructionsMissingFromFrame(Frame frame)
        {
            var keys = CollectInstructionKeys(frame);
            if (m_CurrentVideoInstruction != null &&
                keys.Contains(m_CurrentVideoInstructionKey) is false)
            {
                m_VideoOperationVersion++;
                m_CurrentVideoInstruction = null;
                m_CurrentVideoInstructionKey = null;
                StopCurrentVideo();
            }

            if (string.IsNullOrEmpty(m_CurrentAudioInstructionKey) is false &&
                keys.Contains(m_CurrentAudioInstructionKey) is false)
            {
                m_AudioOperationVersion++;
                m_CurrentAudioInstructionKey = null;
                StopAndDispose(ref m_AudioPlayback);
            }
        }

        private void StopStoryMedia()
        {
            m_VideoOperationVersion++;
            m_ImageOperationVersion++;
            m_AudioOperationVersion++;
            m_CurrentVideoInstruction = null;
            m_CurrentVideoInstructionKey = null;
            m_CurrentAudioInstructionKey = null;
            m_DispatchedFrame = null;
            m_DispatchedInstructionKeys.Clear();
            StopAndDispose(ref m_ImagePlayback);
            StopAndDispose(ref m_AudioPlayback);
        }

        private static AudioPlayableRequest CreateAudioRequest(StoryInstruction.PlayAudio instruction)
        {
            var reference = instruction.Reference;
            return new AudioPlayableRequest(
                MediaUrlResolver.Resolve(reference.Path, App.Config.MediaDelivery),
                AudioLocationKind.Url,
                new AudioPlayableOptions
                {
                    Loop = instruction.Loop,
                    Volume = instruction.Volume,
                    Priority = instruction.Priority
                });
        }

        private static string BuildInstructionKey(StoryInstruction instruction)
        {
            var branchId = string.IsNullOrWhiteSpace(instruction.BranchId)
                ? string.Empty
                : instruction.BranchId;
            return branchId + InstructionKeySeparator + instruction.InstructionId;
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
