using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Execution;

namespace GameDeveloperKit.Story.Playback
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
            Frame frame,
            FrameTrack track = null,
            global::GameDeveloperKit.Story.Model.Command command = null,
            IReadOnlyList<Choice> choices = null)
        {
            Kind = kind;
            Frame = frame;
            Track = track;
            Command = command;
            Choices = choices ?? Array.Empty<Choice>();
        }

        /// <summary>
        /// 请求类型。
        /// </summary>
        public InteractionRequestKind Kind { get; }

        /// <summary>
        /// 当前帧。
        /// </summary>
        public Frame Frame { get; }

        /// <summary>
        /// 当前轨道。
        /// </summary>
        public FrameTrack Track { get; }

        /// <summary>
        /// 当前命令。
        /// </summary>
        public global::GameDeveloperKit.Story.Model.Command Command { get; }

        /// <summary>
        /// 当前选项。
        /// </summary>
        public IReadOnlyList<Choice> Choices { get; }
    }
}
