using GameDeveloperKit.Story.Model;

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
    }
}
