using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Playback;
using UnityEngine;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情运行时模块的只读定义查询接口。
    /// </summary>
    public sealed partial class StoryModule
    {
        /// <summary>
        /// 尝试获取已注册剧情中的卷定义。
        /// </summary>
        /// <param name="storyId">剧情 ID。</param>
        /// <param name="volumeId">卷 ID。</param>
        /// <param name="volume">卷定义。</param>
        /// <returns>获取成功时返回 true。</returns>
        public bool TryGetVolume(string storyId, string volumeId, out Volume volume)
        {
            ValidateText(storyId, nameof(storyId), "Story id cannot be empty.");
            ValidateText(volumeId, nameof(volumeId), "Story volume id cannot be empty.");
            volume = null;
            if (!m_Programs.TryGetValue(storyId, out var program))
            {
                return false;
            }

            for (var i = 0; i < program.Volumes.Count; i++)
            {
                var candidate = program.Volumes[i];
                if (candidate != null && candidate.VolumeId == volumeId)
                {
                    volume = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取已注册剧情的卷定义快照。
        /// </summary>
        public IReadOnlyList<Volume> GetVolumes(string storyId)
        {
            ValidateText(storyId, nameof(storyId), "Story id cannot be empty.");
            return m_Programs.TryGetValue(storyId, out var program)
                ? CopySnapshot(program.Volumes)
                : Array.Empty<Volume>();
        }

        /// <summary>
        /// 尝试获取已注册剧情中的剧情段定义。
        /// </summary>
        /// <param name="storyId">剧情 ID。</param>
        /// <param name="episodeId">剧情段 ID。</param>
        /// <param name="episode">剧情段定义。</param>
        /// <returns>获取成功时返回 true。</returns>
        public bool TryGetEpisode(string storyId, string episodeId, out Episode episode)
        {
            ValidateText(storyId, nameof(storyId), "Story id cannot be empty.");
            ValidateText(episodeId, nameof(episodeId), "Story episode id cannot be empty.");
            episode = null;
            if (!m_Programs.TryGetValue(storyId, out var program))
            {
                return false;
            }

            for (var volumeIndex = 0; volumeIndex < program.Volumes.Count; volumeIndex++)
            {
                var volume = program.Volumes[volumeIndex];
                if (volume == null)
                {
                    continue;
                }

                for (var episodeIndex = 0; episodeIndex < volume.Episodes.Count; episodeIndex++)
                {
                    var candidate = volume.Episodes[episodeIndex];
                    if (candidate != null && candidate.EpisodeId == episodeId)
                    {
                        episode = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 尝试获取指定卷中的剧情章节。
        /// </summary>
        public bool TryGetEpisode(
            string storyId,
            string volumeId,
            string episodeId,
            out Episode episode)
        {
            ValidateText(storyId, nameof(storyId), "Story id cannot be empty.");
            ValidateText(volumeId, nameof(volumeId), "Story volume id cannot be empty.");
            ValidateText(episodeId, nameof(episodeId), "Story episode id cannot be empty.");
            episode = null;
            if (!TryGetVolume(storyId, volumeId, out var volume))
            {
                return false;
            }

            for (var i = 0; i < volume.Episodes.Count; i++)
            {
                var candidate = volume.Episodes[i];
                if (candidate != null && string.Equals(candidate.EpisodeId, episodeId, StringComparison.Ordinal))
                {
                    episode = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取指定卷的章节数据快照。
        /// </summary>
        public IReadOnlyList<Episode> GetEpisodes(string storyId, string volumeId)
        {
            if (!TryGetVolume(storyId, volumeId, out var volume))
            {
                return Array.Empty<Episode>();
            }

            return CopySnapshot(volume.Episodes);
        }

        /// <summary>
        /// 获取指定章节；章节不存在时抛出明确错误。
        /// </summary>
        public Episode GetEpisode(string storyId, string volumeId, string episodeId)
        {
            if (TryGetEpisode(storyId, volumeId, episodeId, out var episode))
            {
                return episode;
            }

            throw new GameException(
                $"Story episode is not registered. story:{storyId} volume:{volumeId} episode:{episodeId}");
        }

        /// <summary>
        /// 获取当前运行章节及当前步骤，不创建或推进运行器。
        /// </summary>
        public bool TryGetCurrentChapter(out Volume volume, out Episode episode, out Step step)
        {
            var runner = CurrentRunner;
            volume = runner?.CurrentVolume;
            episode = runner?.CurrentEpisode;
            step = runner?.CurrentStep;
            return volume != null && episode != null;
        }

        /// <summary>
        /// 获取当前剧情运行位置，不创建或推进运行器。
        /// </summary>
        public bool TryGetCurrentPosition(out StoryPosition position)
        {
            var runner = CurrentRunner;
            if (runner?.CurrentVolume == null || runner.CurrentEpisode == null)
            {
                position = default;
                return false;
            }

            position = new StoryPosition(
                runner.StoryId,
                runner.CurrentVolumeId,
                runner.CurrentEpisodeId,
                runner.CurrentStepId);
            return true;
        }

        /// <summary>
        /// 静态提取指定章节中的媒体提示，不创建或推进运行器。
        /// </summary>
        public IReadOnlyList<StoryMediaCue> GetEpisodeMediaCues(
            string storyId,
            string volumeId,
            string episodeId)
        {
            var episode = GetEpisode(storyId, volumeId, episodeId);
            var cues = new List<StoryMediaCue>();
            for (var i = 0; i < episode.Steps.Count; i++)
            {
                var step = episode.Steps[i];
                if (step == null ||
                    StoryInstruction.IsInstruction(step.Kind) is false ||
                    step.Kind == StepKind.Unlock)
                {
                    continue;
                }

                var instruction = StoryInstruction.Create(step);
                cues.Add(new StoryMediaCue(storyId, volumeId, episodeId, instruction));
            }

            return cues.Count == 0
                ? Array.Empty<StoryMediaCue>()
                : cues.AsReadOnly();
        }

        /// <summary>
        /// 纯播放章节中的首个视频指令，不启动 Runner，也不触发选项、解锁或历史。
        /// </summary>
        public async UniTask<VideoPlayableHandle> PlayEpisodeVideoAsync(
            string storyId,
            string volumeId,
            string episodeId,
            bool loop = false,
            bool seekable = true,
            Transform parent = null,
            CancellationToken cancellationToken = default)
        {
            var cues = GetEpisodeMediaCues(storyId, volumeId, episodeId);
            StoryInstruction.PlayVideo video = null;
            for (var i = 0; i < cues.Count; i++)
            {
                if (cues[i].Instruction is StoryInstruction.PlayVideo candidate)
                {
                    video = candidate;
                    break;
                }
            }

            if (video == null)
            {
                throw new GameException(
                    $"Story episode has no video instruction. story:{storyId} volume:{volumeId} episode:{episodeId}");
            }

            var request = VideoRequestFactory.Create(
                video.Reference,
                App.Config.MediaDelivery,
                loop,
                seekable,
                parent,
                false);
            return await App.Playable.Video.PlayAsync(request, cancellationToken);
        }

        private static IReadOnlyList<T> CopySnapshot<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<T>();
            }

            var copy = new T[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return Array.AsReadOnly(copy);
        }
    }
}
