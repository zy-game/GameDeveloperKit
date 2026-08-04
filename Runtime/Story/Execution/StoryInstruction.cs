using System;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Execution
{
    /// <summary>
    /// 剧情帧输出的有限业务指令。
    /// </summary>
    public abstract class StoryInstruction
    {
        private StoryInstruction(Step step, string branchId)
        {
            Step = step ?? throw new ArgumentNullException(nameof(step));
            InstructionId = step.StepId;
            BranchId = string.IsNullOrWhiteSpace(branchId) ? string.Empty : branchId;
        }

        public string InstructionId { get; }

        public Step Step { get; }

        public string BranchId { get; }

        public sealed class PlayVideo : StoryInstruction
        {
            internal PlayVideo(Step step, string branchId)
                : base(step, branchId)
            {
                Reference = step.Data.VideoReference ??
                    throw Invalid(step, "Video reference is missing.");
                Loop = step.Data.Loop;
                Seekable = step.Data.Seekable;
            }

            public VideoReference Reference { get; }

            public bool Loop { get; }

            public bool Seekable { get; }
        }

        public sealed class ShowImage : StoryInstruction
        {
            internal ShowImage(Step step, string branchId)
                : base(step, branchId)
            {
                Location = RequireText(step, step.Data.ImageLocation, "Image location");
            }

            public string Location { get; }
        }

        public sealed class PlayAudio : StoryInstruction
        {
            internal PlayAudio(Step step, string branchId)
                : base(step, branchId)
            {
                Reference = step.Data.AudioReference ??
                    throw Invalid(step, "Audio reference is missing.");
                Loop = step.Data.Loop;
                Volume = step.Data.Volume;
                Priority = step.Data.Priority;
            }

            public AudioReference Reference { get; }

            public bool Loop { get; }

            public float Volume { get; }

            public int Priority { get; }
        }

        public sealed class Unlock : StoryInstruction
        {
            internal Unlock(Step step, string branchId)
                : base(step, branchId)
            {
                UnlockId = RequireText(step, step.Data.UnlockId, "Unlock id");
            }

            public string UnlockId { get; }
        }

        internal static StoryInstruction Create(Step step, string branchId = null)
        {
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            switch (step.Kind)
            {
                case StepKind.PlayVideo:
                    return new PlayVideo(step, branchId);
                case StepKind.ShowImage:
                    return new ShowImage(step, branchId);
                case StepKind.PlayAudio:
                    return new PlayAudio(step, branchId);
                case StepKind.Unlock:
                    return new Unlock(step, branchId);
                default:
                    throw Invalid(step, $"Step kind is not an instruction: {step.Kind}");
            }
        }

        internal static bool IsInstruction(StepKind kind)
        {
            return kind == StepKind.PlayVideo ||
                   kind == StepKind.ShowImage ||
                   kind == StepKind.PlayAudio ||
                   kind == StepKind.Unlock;
        }

        private static string RequireText(Step step, string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw Invalid(step, $"{field} is missing.");
            }

            return value.Trim();
        }

        private static GameException Invalid(Step step, string reason)
        {
            return new GameException(
                $"Story instruction is invalid. step:{step?.StepId} reason:{reason}");
        }
    }
}
