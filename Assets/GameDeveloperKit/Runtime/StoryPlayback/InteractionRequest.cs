using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情交互请求类型。
    /// </summary>
    public enum InteractionRequestKind
    {
        Text = 0,
        Continue = 1,
        Choice = 2,
        Video = 3,
        Image = 4,
        Custom = 5
    }

    /// <summary>
    /// 剧情交互请求。
    /// </summary>
    public readonly struct InteractionRequest
    {
        /// <summary>
        /// 初始化剧情交互请求。
        /// </summary>
        /// <param name="kind">请求类型。</param>
        /// <param name="frame">当前帧。</param>
        /// <param name="track">当前轨道。</param>
        /// <param name="command">当前命令。</param>
        /// <param name="choices">当前选项。</param>
        public InteractionRequest(
            InteractionRequestKind kind,
            StoryFrame frame,
            StoryFrameTrack track = null,
            StoryCommand command = null,
            IReadOnlyList<StoryChoice> choices = null)
        {
            Kind = kind;
            Frame = frame;
            Track = track;
            Command = command;
            Choices = choices ?? Array.Empty<StoryChoice>();
        }

        /// <summary>
        /// 请求类型。
        /// </summary>
        public InteractionRequestKind Kind { get; }

        /// <summary>
        /// 当前帧。
        /// </summary>
        public StoryFrame Frame { get; }

        /// <summary>
        /// 当前轨道。
        /// </summary>
        public StoryFrameTrack Track { get; }

        /// <summary>
        /// 当前命令。
        /// </summary>
        public StoryCommand Command { get; }

        /// <summary>
        /// 当前选项。
        /// </summary>
        public IReadOnlyList<StoryChoice> Choices { get; }
    }
}
