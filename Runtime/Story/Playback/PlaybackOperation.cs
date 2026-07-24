using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Playback
{
    /// <summary>
    /// 剧情播放展示动作类型。
    /// </summary>
    public enum PlaybackOperationKind
    {
        Dialogue = 0,
        Narration = 1,
        Choices = 2,
        Continue = 3,
        Video = 4,
        Image = 5,
        Audio = 6,
        Command = 7,
        Wait = 8,
        Completed = 9
    }

    /// <summary>
    /// 已解析的剧情选项展示数据。
    /// </summary>
    public sealed class PlaybackChoice
    {
        internal PlaybackChoice(Choice choice, string text)
        {
            Choice = choice ?? throw new ArgumentNullException(nameof(choice));
            Text = text ?? string.Empty;
        }

        /// <summary>
        /// 原始剧情选项。
        /// </summary>
        public Choice Choice { get; }

        /// <summary>
        /// 已解析的显示文本。
        /// </summary>
        public string Text { get; }
    }

    /// <summary>
    /// 由剧情帧生成的一项播放展示动作。
    /// </summary>
    public sealed class PlaybackOperation
    {
        internal PlaybackOperation(
            PlaybackOperationKind kind,
            Frame frame,
            FrameTrack track = null,
            global::GameDeveloperKit.Story.Model.Command command = null,
            string speaker = null,
            string text = null,
            IReadOnlyList<PlaybackChoice> choices = null,
            double waitSeconds = 0d)
        {
            Kind = kind;
            Frame = frame ?? throw new ArgumentNullException(nameof(frame));
            Track = track;
            Command = command;
            Speaker = speaker;
            Text = text;
            Choices = choices ?? Array.Empty<PlaybackChoice>();
            WaitSeconds = waitSeconds;
        }

        /// <summary>
        /// 动作类型。
        /// </summary>
        public PlaybackOperationKind Kind { get; }

        /// <summary>
        /// 动作所属帧。
        /// </summary>
        public Frame Frame { get; }

        /// <summary>
        /// 动作来源轨道。
        /// </summary>
        public FrameTrack Track { get; }

        /// <summary>
        /// 媒体或业务命令。
        /// </summary>
        public global::GameDeveloperKit.Story.Model.Command Command { get; }

        /// <summary>
        /// 已解析的说话人文本。
        /// </summary>
        public string Speaker { get; }

        /// <summary>
        /// 已解析的显示文本。
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// 已解析的选项列表。
        /// </summary>
        public IReadOnlyList<PlaybackChoice> Choices { get; }

        /// <summary>
        /// 等待动作的秒数。
        /// </summary>
        public double WaitSeconds { get; }
    }
}
