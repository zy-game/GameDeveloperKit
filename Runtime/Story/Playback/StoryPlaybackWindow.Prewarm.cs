using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Model;
using UnityEngine;

namespace GameDeveloperKit.Story.Playback
{
    public partial class StoryPlaybackWindow
    {
        private string m_VideoLookaheadPath;
        private string m_VideoLookaheadSourcePath;
        private readonly HashSet<string> m_ChoiceVideoLookaheadPaths =
            new HashSet<string>(StringComparer.Ordinal);
        private string m_SelectedChoiceVideoLookaheadPath;
        private Episode m_ChoiceVideoPrewarmEpisode;

        /// <summary>
        /// 当前视频首帧后预热下一段确定性视频，降低后续视频的冷启动等待。
        /// </summary>
        private void PrewarmNextVideo(VideoPlayableHandle playback)
        {
            if (playback == null || m_CurrentFrame == null || m_CurrentVideoInstruction == null)
            {
                return;
            }

            if (string.Equals(m_VideoLookaheadPath, playback.RequestPath, StringComparison.Ordinal))
            {
                m_VideoLookaheadPath = null;
            }

            m_ChoiceVideoLookaheadPaths.Remove(playback.RequestPath);

            var instruction = m_CurrentVideoInstruction;
            // 按播放句柄路径去重（InstructionId 是步骤 ID，跨集重复，不能用作去重键）。
            if (string.Equals(m_VideoLookaheadSourcePath, playback.RequestPath, StringComparison.Ordinal))
            {
                return;
            }

            m_VideoLookaheadSourcePath = playback.RequestPath;
            ReleaseVideoLookahead(false);
            var next = EpisodeVideoPrewarmer.FindNextVideoInstruction(
                m_CurrentFrame,
                instruction.Step,
                RequireStoryModule().FunctionResolver);
            if (next == null)
            {
                return;
            }

            var request = VideoRequestFactory.Create(
                next.Reference,
                App.Config.MediaDelivery,
                next.Loop,
                next.Seekable,
                PlaybackRoot,
                false,
                PreferredPreloadTargetHeight);
            m_VideoLookaheadPath = request.Path;
            PrewarmLookaheadAsync(next.InstructionId, request, SessionCancellationToken)
                .Forget(Debug.LogException);
        }

        /// <summary>
        /// 预热当前集所有选项分支视频，每个 episode 只预热一次；进入新集时立即调用，
        /// 也由视频首帧兜底触发（覆盖未走换集路径的情况）。
        /// </summary>
        private void PrewarmEpisodeChoiceVideos(Frame frame)
        {
            var episode = frame?.Episode;
            if (episode == null || ReferenceEquals(m_ChoiceVideoPrewarmEpisode, episode))
            {
                return;
            }

            m_ChoiceVideoPrewarmEpisode = episode;
            PrewarmChoiceVideos(frame);
            Debug.Log(
                $"Story choice prewarm started. episode:{episode.EpisodeId} " +
                $"choices:{m_ChoiceVideoLookaheadPaths.Count}");
        }

        private void PrewarmChoiceVideos(Frame frame)
        {
            var preservedPath = m_SelectedChoiceVideoLookaheadPath;
            m_SelectedChoiceVideoLookaheadPath = null;
            ReleaseChoiceVideoLookaheads(preservedPath);
            if (frame == null)
            {
                return;
            }

            var instructions = EpisodeVideoPrewarmer.CollectChoiceVideoInstructions(
                frame,
                RequireStoryModule().FunctionResolver);
            for (var i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                var request = VideoRequestFactory.Create(
                    instruction.Reference,
                    App.Config.MediaDelivery,
                    instruction.Loop,
                    instruction.Seekable,
                    PlaybackRoot,
                    false,
                    PreferredPreloadTargetHeight);
                if (m_ChoiceVideoLookaheadPaths.Add(request.Path) is false)
                {
                    continue;
                }

                PrewarmChoiceLookaheadAsync(instruction.InstructionId, request, SessionCancellationToken)
                    .Forget(Debug.LogException);
            }
        }

        /// <summary>
        /// 选项被选中时保留该分支的视频预热，释放其余分支的预热。
        /// </summary>
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

            var instruction = EpisodeVideoPrewarmer.FindChoiceVideoInstruction(
                m_CurrentFrame,
                choiceId,
                RequireStoryModule().FunctionResolver);
            var selectedPath = instruction == null
                ? null
                : VideoRequestFactory.Create(
                    instruction.Reference,
                    App.Config.MediaDelivery,
                    instruction.Loop,
                    instruction.Seekable,
                    PlaybackRoot,
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
                m_VideoLookaheadSourcePath = null;
            }

            if (!string.IsNullOrWhiteSpace(path) &&
                m_ChoiceVideoLookaheadPaths.Contains(path) is false)
            {
                App.Playable.Video.ReleasePreload(path);
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
            m_ChoiceVideoLookaheadPaths.Clear();
            m_SelectedChoiceVideoLookaheadPath = null;
            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                if (string.Equals(path, preservedPath, StringComparison.Ordinal))
                {
                    m_ChoiceVideoLookaheadPaths.Add(path);
                    continue;
                }

                App.Playable.Video.ReleasePreload(path);
            }
        }

        private async UniTask PrewarmLookaheadAsync(
            string instructionId,
            VideoPlayableRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                await App.Playable.Video.PreloadAsync(request, cancellationToken);
                Debug.Log(
                    $"Story video lookahead first frame ready. instruction:{instructionId} path:{request.Path}");
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
                    $"Story video lookahead failed. instruction:{instructionId} path:{request.Path} " +
                    $"error:{exception.Message}");
            }
        }

        private async UniTask PrewarmChoiceLookaheadAsync(
            string instructionId,
            VideoPlayableRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                await App.Playable.Video.PreloadAsync(request, cancellationToken);
                Debug.Log(
                    $"Story choice video lookahead first frame ready. instruction:{instructionId} path:{request.Path}");
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                m_ChoiceVideoLookaheadPaths.Remove(request.Path);
                Debug.LogWarning(
                    $"Story choice video lookahead failed. instruction:{instructionId} path:{request.Path} " +
                    $"error:{exception.Message}");
            }
        }
    }
}
