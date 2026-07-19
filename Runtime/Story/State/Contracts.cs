using System;

namespace GameDeveloperKit.Story.State
{
    /// <summary>
    /// 业务为剧情段提供的表现状态。
    /// </summary>
    public enum EpisodeState
    {
        Hidden = 0,
        Locked = 1,
        Available = 2
    }

    /// <summary>
    /// 剧情段表现状态变化通知。
    /// </summary>
    public readonly struct EpisodeStateChanged
    {
        public EpisodeStateChanged(string storyId, string episodeId, EpisodeState state)
        {
            ValidateText(storyId, nameof(storyId));
            ValidateText(episodeId, nameof(episodeId));
            if (!Enum.IsDefined(typeof(EpisodeState), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state), state, "Episode state is not defined.");
            }

            StoryId = storyId;
            EpisodeId = episodeId;
            State = state;
        }

        public string StoryId { get; }

        public string EpisodeId { get; }

        public EpisodeState State { get; }

        private static void ValidateText(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }
    }

    /// <summary>
    /// 业务可选实现的剧情段状态只读查询协议。
    /// </summary>
    public interface IEpisodeStateProvider
    {
        EpisodeState GetState(string storyId, string episodeId);

        event Action<EpisodeStateChanged> Changed;
    }
}
