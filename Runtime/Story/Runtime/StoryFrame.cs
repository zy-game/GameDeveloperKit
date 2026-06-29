using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情帧轨道类型。
    /// </summary>
    public enum StoryFrameTrackKind
    {
        /// <summary>
        /// 文本轨。
        /// </summary>
        Text = 0,

        /// <summary>
        /// 命令轨。
        /// </summary>
        Command = 1,

        /// <summary>
        /// 等待轨。
        /// </summary>
        Wait = 2
    }

    /// <summary>
    /// 剧情帧轨道。
    /// </summary>
    public sealed class StoryFrameTrack
    {
        private StoryFrameTrack(
            StoryFrameTrackKind kind,
            StoryStep step,
            string textKey,
            string speaker,
            StoryCommand command,
            double waitSeconds,
            IReadOnlyList<string> tags,
            string branchId,
            string branchLabel)
        {
            Step = step ?? throw new ArgumentNullException(nameof(step));
            Kind = kind;
            TextKey = textKey;
            Speaker = speaker;
            Command = command;
            WaitSeconds = waitSeconds;
            Tags = CopyList(tags);
            BranchId = branchId;
            BranchLabel = branchLabel;
        }

        /// <summary>
        /// 轨道类型。
        /// </summary>
        public StoryFrameTrackKind Kind { get; }

        /// <summary>
        /// 来源步骤。
        /// </summary>
        public StoryStep Step { get; }

        /// <summary>
        /// 文本键。
        /// </summary>
        public string TextKey { get; }

        /// <summary>
        /// 说话人。
        /// </summary>
        public string Speaker { get; }

        /// <summary>
        /// 命令。
        /// </summary>
        public StoryCommand Command { get; }

        /// <summary>
        /// 等待秒数。
        /// </summary>
        public double WaitSeconds { get; }

        /// <summary>
        /// 标签。
        /// </summary>
        public IReadOnlyList<string> Tags { get; }

        /// <summary>
        /// 所属并行分支 ID。
        /// </summary>
        public string BranchId { get; }

        /// <summary>
        /// 所属并行分支标签。
        /// </summary>
        public string BranchLabel { get; }

        /// <summary>
        /// 创建文本轨。
        /// </summary>
        /// <param name="step">来源步骤。</param>
        /// <param name="branchId">并行分支 ID。</param>
        /// <param name="branchLabel">并行分支标签。</param>
        /// <returns>文本轨。</returns>
        public static StoryFrameTrack CreateText(StoryStep step, string branchId = null, string branchLabel = null)
        {
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            return new StoryFrameTrack(
                StoryFrameTrackKind.Text,
                step,
                step.Data.TextKey,
                step.Data.Speaker,
                null,
                0d,
                step.Tags,
                branchId,
                branchLabel);
        }

        /// <summary>
        /// 创建命令轨。
        /// </summary>
        /// <param name="step">来源步骤。</param>
        /// <param name="branchId">并行分支 ID。</param>
        /// <param name="branchLabel">并行分支标签。</param>
        /// <returns>命令轨。</returns>
        public static StoryFrameTrack CreateCommand(StoryStep step, string branchId = null, string branchLabel = null)
        {
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            return new StoryFrameTrack(
                StoryFrameTrackKind.Command,
                step,
                null,
                null,
                step.Data.Command,
                0d,
                step.Tags,
                branchId,
                branchLabel);
        }

        /// <summary>
        /// 创建等待轨。
        /// </summary>
        /// <param name="step">来源步骤。</param>
        /// <param name="waitSeconds">等待秒数。</param>
        /// <param name="branchId">并行分支 ID。</param>
        /// <param name="branchLabel">并行分支标签。</param>
        /// <returns>等待轨。</returns>
        public static StoryFrameTrack CreateWait(StoryStep step, double waitSeconds, string branchId = null, string branchLabel = null)
        {
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            return new StoryFrameTrack(
                StoryFrameTrackKind.Wait,
                step,
                null,
                null,
                null,
                waitSeconds,
                step.Tags,
                branchId,
                branchLabel);
        }

        private static IReadOnlyList<T> CopyList<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<T>();
            }

            return new List<T>(items);
        }
    }

    /// <summary>
    /// 剧情运行帧。
    /// </summary>
    public sealed class StoryFrame
    {
        /// <summary>
        /// 初始化剧情运行帧。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="chapter">当前章节。</param>
        /// <param name="anchorStep">锚点步骤。</param>
        /// <param name="tracks">帧轨道。</param>
        /// <param name="choices">选项。</param>
        /// <param name="waitsForChoice">是否等待选项。</param>
        /// <param name="waitsForCommand">是否等待命令。</param>
        /// <param name="waitsForTime">是否等待时间。</param>
        /// <param name="isCompleted">是否已完成。</param>
        public StoryFrame(
            StoryProgram program,
            StoryChapter chapter,
            StoryStep anchorStep,
            IReadOnlyList<StoryFrameTrack> tracks = null,
            IReadOnlyList<StoryChoice> choices = null,
            bool waitsForChoice = false,
            bool waitsForCommand = false,
            bool waitsForTime = false,
            bool isCompleted = false)
        {
            Program = program ?? throw new ArgumentNullException(nameof(program));
            Chapter = chapter;
            AnchorStep = anchorStep;
            Tracks = CopyTracks(tracks);
            Choices = CopyChoices(choices);
            WaitsForChoice = waitsForChoice;
            WaitsForCommand = waitsForCommand;
            WaitsForTime = waitsForTime;
            IsCompleted = isCompleted;
        }

        /// <summary>
        /// 剧情程序。
        /// </summary>
        public StoryProgram Program { get; }

        /// <summary>
        /// 当前章节。
        /// </summary>
        public StoryChapter Chapter { get; }

        /// <summary>
        /// 锚点步骤。
        /// </summary>
        public StoryStep AnchorStep { get; }

        /// <summary>
        /// 帧轨道。
        /// </summary>
        public IReadOnlyList<StoryFrameTrack> Tracks { get; }

        /// <summary>
        /// 选项。
        /// </summary>
        public IReadOnlyList<StoryChoice> Choices { get; }

        /// <summary>
        /// 是否等待选项。
        /// </summary>
        public bool WaitsForChoice { get; }

        /// <summary>
        /// 是否等待命令。
        /// </summary>
        public bool WaitsForCommand { get; }

        /// <summary>
        /// 是否等待时间。
        /// </summary>
        public bool WaitsForTime { get; }

        /// <summary>
        /// 是否已完成。
        /// </summary>
        public bool IsCompleted { get; }

        /// <summary>
        /// 创建文本帧。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="chapter">当前章节。</param>
        /// <param name="step">来源步骤。</param>
        /// <returns>文本帧。</returns>
        public static StoryFrame CreateText(StoryProgram program, StoryChapter chapter, StoryStep step)
        {
            return new StoryFrame(
                program,
                chapter,
                step,
                new[] { StoryFrameTrack.CreateText(step) });
        }

        /// <summary>
        /// 创建选项帧。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="chapter">当前章节。</param>
        /// <param name="step">来源步骤。</param>
        /// <param name="choices">选项。</param>
        /// <returns>选项帧。</returns>
        public static StoryFrame CreateChoice(StoryProgram program, StoryChapter chapter, StoryStep step, IReadOnlyList<StoryChoice> choices)
        {
            return new StoryFrame(
                program,
                chapter,
                step,
                null,
                choices,
                true);
        }

        /// <summary>
        /// 创建命令帧。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="chapter">当前章节。</param>
        /// <param name="step">来源步骤。</param>
        /// <param name="waitsForCommand">是否等待命令。</param>
        /// <returns>命令帧。</returns>
        public static StoryFrame CreateCommand(StoryProgram program, StoryChapter chapter, StoryStep step, bool waitsForCommand)
        {
            return new StoryFrame(
                program,
                chapter,
                step,
                new[] { StoryFrameTrack.CreateCommand(step) },
                null,
                false,
                waitsForCommand);
        }

        /// <summary>
        /// 创建等待帧。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="chapter">当前章节。</param>
        /// <param name="step">来源步骤。</param>
        /// <param name="waitSeconds">等待秒数。</param>
        /// <returns>等待帧。</returns>
        public static StoryFrame CreateWait(StoryProgram program, StoryChapter chapter, StoryStep step, double waitSeconds)
        {
            return new StoryFrame(
                program,
                chapter,
                step,
                new[] { StoryFrameTrack.CreateWait(step, waitSeconds) },
                null,
                false,
                false,
                true);
        }

        /// <summary>
        /// 创建完成帧。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="chapter">当前章节。</param>
        /// <param name="anchorStep">锚点步骤。</param>
        /// <returns>完成帧。</returns>
        public static StoryFrame CreateCompleted(StoryProgram program, StoryChapter chapter, StoryStep anchorStep)
        {
            return new StoryFrame(
                program,
                chapter,
                anchorStep,
                null,
                null,
                false,
                false,
                false,
                true);
        }

        private static IReadOnlyList<StoryFrameTrack> CopyTracks(IReadOnlyList<StoryFrameTrack> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<StoryFrameTrack>();
            }

            var result = new List<StoryFrameTrack>();
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                {
                    result.Add(items[i]);
                }
            }

            return result;
        }

        private static IReadOnlyList<StoryChoice> CopyChoices(IReadOnlyList<StoryChoice> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<StoryChoice>();
            }

            return new List<StoryChoice>(items);
        }
    }
}
