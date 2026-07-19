using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Text;

namespace GameDeveloperKit.Story.Execution
{
    /// <summary>
    /// 剧情帧轨道类型。
    /// </summary>
    public enum FrameTrackKind
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
    public sealed class FrameTrack
    {
        private FrameTrack(
            FrameTrackKind kind,
            Step step,
            string textKey,
            string speaker,
            global::GameDeveloperKit.Story.Model.Command command,
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
        public FrameTrackKind Kind { get; }

        /// <summary>
        /// 来源步骤。
        /// </summary>
        public Step Step { get; }

        /// <summary>
        /// 文本键。
        /// </summary>
        public string TextKey { get; }

        public TextReference? Text => string.IsNullOrWhiteSpace(TextKey)
            ? (TextReference?)null
            : TextReferenceCodec.DeserializeOrLegacy(TextKey);

        /// <summary>
        /// 说话人。
        /// </summary>
        public string Speaker { get; }

        public TextReference? SpeakerText => string.IsNullOrWhiteSpace(Speaker)
            ? (TextReference?)null
            : TextReferenceCodec.DeserializeOrLegacy(Speaker);

        /// <summary>
        /// 命令。
        /// </summary>
        public global::GameDeveloperKit.Story.Model.Command Command { get; }

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
        public static FrameTrack CreateText(Step step, string branchId = null, string branchLabel = null)
        {
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            return new FrameTrack(
                FrameTrackKind.Text,
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
        public static FrameTrack CreateCommand(Step step, string branchId = null, string branchLabel = null)
        {
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            return new FrameTrack(
                FrameTrackKind.Command,
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
        public static FrameTrack CreateWait(Step step, double waitSeconds, string branchId = null, string branchLabel = null)
        {
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            return new FrameTrack(
                FrameTrackKind.Wait,
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
    public sealed class Frame
    {
        /// <summary>
        /// 初始化剧情运行帧。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="volume">当前卷。</param>
        /// <param name="episode">当前剧情段。</param>
        /// <param name="anchorStep">锚点步骤。</param>
        /// <param name="tracks">帧轨道。</param>
        /// <param name="choices">选项。</param>
        /// <param name="waitsForChoice">是否等待选项。</param>
        /// <param name="waitsForCommand">是否等待命令。</param>
        /// <param name="waitsForTime">是否等待时间。</param>
        /// <param name="isCompleted">是否已完成。</param>
        /// <param name="completedExitId">完成出口 ID。</param>
        public Frame(
            Program program,
            Volume volume,
            Episode episode,
            Step anchorStep,
            IReadOnlyList<FrameTrack> tracks = null,
            IReadOnlyList<Choice> choices = null,
            bool waitsForChoice = false,
            bool waitsForCommand = false,
            bool waitsForTime = false,
            bool isCompleted = false,
            string completedExitId = null)
        {
            Program = program ?? throw new ArgumentNullException(nameof(program));
            Volume = volume;
            Episode = episode;
            AnchorStep = anchorStep;
            Tracks = CopyTracks(tracks);
            Choices = CopyChoices(choices);
            WaitsForChoice = waitsForChoice;
            WaitsForCommand = waitsForCommand;
            WaitsForTime = waitsForTime;
            IsCompleted = isCompleted;
            CompletedExitId = completedExitId;
        }

        /// <summary>
        /// 剧情程序。
        /// </summary>
        public Program Program { get; }

        /// <summary>
        /// 当前卷。
        /// </summary>
        public Volume Volume { get; }

        /// <summary>
        /// 当前剧情段。
        /// </summary>
        public Episode Episode { get; }

        /// <summary>
        /// 锚点步骤。
        /// </summary>
        public Step AnchorStep { get; }

        /// <summary>
        /// 帧轨道。
        /// </summary>
        public IReadOnlyList<FrameTrack> Tracks { get; }

        /// <summary>
        /// 选项。
        /// </summary>
        public IReadOnlyList<Choice> Choices { get; }

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
        /// 当前剧情段完成出口 ID。
        /// </summary>
        public string CompletedExitId { get; }

        /// <summary>
        /// 创建文本帧。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="volume">当前卷。</param>
        /// <param name="episode">当前剧情段。</param>
        /// <param name="step">来源步骤。</param>
        /// <returns>文本帧。</returns>
        public static Frame CreateText(Program program, Volume volume, Episode episode, Step step)
        {
            return new Frame(
                program,
                volume,
                episode,
                step,
                new[] { FrameTrack.CreateText(step) });
        }

        /// <summary>
        /// 创建选项帧。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="volume">当前卷。</param>
        /// <param name="episode">当前剧情段。</param>
        /// <param name="step">来源步骤。</param>
        /// <param name="choices">选项。</param>
        /// <returns>选项帧。</returns>
        public static Frame CreateChoice(Program program, Volume volume, Episode episode, Step step, IReadOnlyList<Choice> choices)
        {
            return new Frame(
                program,
                volume,
                episode,
                step,
                null,
                choices,
                true);
        }

        /// <summary>
        /// 创建命令帧。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="volume">当前卷。</param>
        /// <param name="episode">当前剧情段。</param>
        /// <param name="step">来源步骤。</param>
        /// <param name="waitsForCommand">是否等待命令。</param>
        /// <returns>命令帧。</returns>
        public static Frame CreateCommand(Program program, Volume volume, Episode episode, Step step, bool waitsForCommand)
        {
            return new Frame(
                program,
                volume,
                episode,
                step,
                new[] { FrameTrack.CreateCommand(step) },
                null,
                false,
                waitsForCommand);
        }

        /// <summary>
        /// 创建等待帧。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="volume">当前卷。</param>
        /// <param name="episode">当前剧情段。</param>
        /// <param name="step">来源步骤。</param>
        /// <param name="waitSeconds">等待秒数。</param>
        /// <returns>等待帧。</returns>
        public static Frame CreateWait(Program program, Volume volume, Episode episode, Step step, double waitSeconds)
        {
            return new Frame(
                program,
                volume,
                episode,
                step,
                new[] { FrameTrack.CreateWait(step, waitSeconds) },
                null,
                false,
                false,
                true);
        }

        /// <summary>
        /// 创建完成帧。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="volume">当前卷。</param>
        /// <param name="episode">当前剧情段。</param>
        /// <param name="anchorStep">锚点步骤。</param>
        /// <param name="completedExitId">完成出口 ID。</param>
        /// <returns>完成帧。</returns>
        public static Frame CreateCompleted(
            Program program,
            Volume volume,
            Episode episode,
            Step anchorStep,
            string completedExitId)
        {
            return new Frame(
                program,
                volume,
                episode,
                anchorStep,
                null,
                null,
                false,
                false,
                false,
                true,
                completedExitId);
        }

        private static IReadOnlyList<FrameTrack> CopyTracks(IReadOnlyList<FrameTrack> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<FrameTrack>();
            }

            var result = new List<FrameTrack>();
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                {
                    result.Add(items[i]);
                }
            }

            return result;
        }

        private static IReadOnlyList<Choice> CopyChoices(IReadOnlyList<Choice> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<Choice>();
            }

            return new List<Choice>(items);
        }
    }
}
