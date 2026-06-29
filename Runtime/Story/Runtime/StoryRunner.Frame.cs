using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story
{
    public sealed partial class StoryRunner
    {
        private StoryFrame BuildFrame()
        {
            var step = CurrentStep;
            if (step == null)
            {
                CompleteStory();
                return m_CurrentFrame;
            }

            switch (step.Kind)
            {
                case StoryStepKind.Line:
                    m_CurrentFrame = BuildLineFrame(m_CurrentChapter, step);
                    m_State = m_CurrentFrame.WaitsForChoice ? RunnerState.AwaitingChoice : RunnerState.AwaitingContinue;
                    return m_CurrentFrame;
                case StoryStepKind.Command:
                    m_State = RequiresCommandCompletion(step.Data.Command)
                        ? RunnerState.AwaitingCommand
                        : RunnerState.AwaitingContinue;
                    m_CurrentFrame = StoryFrame.CreateCommand(m_Program, m_CurrentChapter, step, m_State == RunnerState.AwaitingCommand);
                    return m_CurrentFrame;
                case StoryStepKind.Wait:
                    m_State = RunnerState.AwaitingTime;
                    if (m_HasPendingWaitElapsed)
                    {
                        m_CurrentWaitElapsed = m_PendingWaitElapsed;
                        m_HasPendingWaitElapsed = false;
                        m_PendingWaitElapsed = 0d;
                    }
                    else
                    {
                        m_CurrentWaitElapsed = 0d;
                    }

                    m_CurrentFrame = StoryFrame.CreateWait(m_Program, m_CurrentChapter, step, step.Data.WaitSeconds);
                    return m_CurrentFrame;
                case StoryStepKind.Choice:
                    var choices = BuildChoices(step);
                    if (choices.Count == 0)
                    {
                        throw new GameException($"Story choice has no available options. story:{StoryId} chapter:{CurrentChapterId} step:{step.StepId}");
                    }

                    m_State = RunnerState.AwaitingChoice;
                    m_CurrentFrame = StoryFrame.CreateChoice(m_Program, m_CurrentChapter, step, choices);
                    return m_CurrentFrame;
                default:
                    throw new GameException($"Story frame step kind is invalid. story:{StoryId} chapter:{CurrentChapterId} step:{step.StepId} kind:{step.Kind}");
            }
        }

        private StoryFrame BuildParallelFrame(StoryStep parallelStep)
        {
            var branches = new List<StoryBranchCursor>();
            for (var i = 0; i < parallelStep.Data.Branches.Count; i++)
            {
                branches.Add(BuildBranchCursor(parallelStep, parallelStep.Data.Branches[i]));
            }

            m_CurrentParallelFrame = new StoryParallelFrame(parallelStep, branches);
            return ResolveParallelBranches(branches);
        }

        private StoryBranchCursor BuildBranchCursor(StoryStep parallelStep, StoryParallelBranch branch)
        {
            if (branch == null)
            {
                throw new GameException($"Story parallel branch is missing. story:{StoryId} chapter:{CurrentChapterId} step:{parallelStep.StepId}");
            }

            var chapter = GetChapter(branch.Entry.ChapterId);
            return BuildBranchCursor(branch, chapter, GetStep(chapter, branch.Entry.StepId));
        }

        private StoryBranchCursor BuildBranchCursor(StoryParallelBranch branch, StoryChapter chapter, StoryStep step, double waitElapsed = 0d)
        {
            while (step != null)
            {
                switch (step.Kind)
                {
                    case StoryStepKind.Start:
                        step = GetNextStep(chapter, step);
                        continue;
                    case StoryStepKind.Branch:
                        if (EvaluateCondition(step.Data.Condition) && step.Data.Target?.TargetKind == StoryTargetKind.StoryEnd)
                        {
                            return new StoryBranchCursor(branch, chapter, step, null, true);
                        }

                        if (TryResolveParallelControlTarget(chapter, step.Data.Target, out var branchTarget, out var branchStep))
                        {
                            return new StoryBranchCursor(branch, chapter, step, null, false, 0d, branchTarget);
                        }

                        step = ResolveBranchStep(chapter, step);
                        continue;
                    case StoryStepKind.Jump:
                        if (step.Data.Target?.TargetKind == StoryTargetKind.StoryEnd)
                        {
                            return new StoryBranchCursor(branch, chapter, step, null, true);
                        }

                        if (TryResolveParallelControlTarget(chapter, step.Data.Target, out var jumpTarget, out var jumpStep))
                        {
                            return new StoryBranchCursor(branch, chapter, step, null, false, 0d, jumpTarget);
                        }

                        step = ResolveJumpStep(chapter, step);
                        continue;
                    case StoryStepKind.Line:
                    case StoryStepKind.Command:
                    case StoryStepKind.Wait:
                    case StoryStepKind.Choice:
                        return new StoryBranchCursor(
                            branch,
                            chapter,
                            step,
                            BuildBranchFrame(chapter, step, branch, waitElapsed),
                            false,
                            step.Kind == StoryStepKind.Wait ? waitElapsed : 0d);
                    case StoryStepKind.Merge:
                    case StoryStepKind.End:
                        return new StoryBranchCursor(branch, chapter, step, null, true);
                    case StoryStepKind.Parallel:
                        return new StoryBranchCursor(
                            branch,
                            chapter,
                            step,
                            null,
                            false,
                            0d,
                            StoryTarget.Step(chapter.ChapterId, step.StepId));
                    default:
                        throw new GameException($"Story step kind is invalid. story:{StoryId} chapter:{chapter.ChapterId} step:{step.StepId} kind:{step.Kind}");
                }
            }

            return new StoryBranchCursor(branch, chapter, null, null, true);
        }

        private StoryFrame BuildBranchFrame(StoryChapter chapter, StoryStep step, StoryParallelBranch branch, double waitElapsed = 0d)
        {
            switch (step.Kind)
            {
                case StoryStepKind.Line:
                    return BuildLineFrame(chapter, step, branch);
                case StoryStepKind.Command:
                    return new StoryFrame(
                        m_Program,
                        chapter,
                        step,
                        new[] { StoryFrameTrack.CreateCommand(step, branch.BranchId, branch.Label) },
                        null,
                        false,
                        RequiresCommandCompletion(step.Data.Command));
                case StoryStepKind.Wait:
                    return new StoryFrame(
                        m_Program,
                        chapter,
                        step,
                        new[] { StoryFrameTrack.CreateWait(step, step.Data.WaitSeconds, branch.BranchId, branch.Label) },
                        null,
                        false,
                        false,
                        true);
                case StoryStepKind.Choice:
                    var choices = BuildChoices(step, branch.BranchId);
                    if (choices.Count == 0)
                    {
                        throw new GameException($"Story choice has no available options. story:{StoryId} chapter:{chapter.ChapterId} step:{step.StepId}");
                    }

                    return new StoryFrame(m_Program, chapter, step, null, choices, true);
                default:
                    throw new GameException($"Story branch frame step kind is invalid. story:{StoryId} chapter:{chapter.ChapterId} step:{step.StepId} kind:{step.Kind}");
            }
        }

        private StoryFrame CombineParallelFrame(StoryParallelFrame parallelFrame)
        {
            var tracks = new List<StoryFrameTrack>();
            var choices = new List<StoryChoice>();
            var waitsForCommand = false;
            var waitsForTime = false;
            var waitsForChoice = false;

            for (var i = 0; i < parallelFrame.Branches.Count; i++)
            {
                var frame = parallelFrame.Branches[i].CurrentFrame;
                if (frame == null)
                {
                    continue;
                }

                if (frame.Tracks != null)
                {
                    for (var trackIndex = 0; trackIndex < frame.Tracks.Count; trackIndex++)
                    {
                        tracks.Add(frame.Tracks[trackIndex]);
                    }
                }

                if (frame.Choices != null)
                {
                    for (var choiceIndex = 0; choiceIndex < frame.Choices.Count; choiceIndex++)
                    {
                        choices.Add(frame.Choices[choiceIndex]);
                    }
                }

                waitsForCommand |= frame.WaitsForCommand;
                waitsForTime |= frame.WaitsForTime;
                waitsForChoice |= frame.WaitsForChoice;
            }

            return new StoryFrame(
                m_Program,
                m_CurrentChapter,
                parallelFrame.ParallelStep,
                tracks,
                choices,
                waitsForChoice,
                waitsForCommand,
                waitsForTime);
        }

        private StoryFrame BuildLineFrame(StoryChapter chapter, StoryStep step, StoryParallelBranch branch = null)
        {
            var choices = BuildInlineChoices(chapter, step, branch?.BranchId);
            return new StoryFrame(
                m_Program,
                chapter,
                step,
                new[] { StoryFrameTrack.CreateText(step, branch?.BranchId, branch?.Label) },
                choices.Count == 0 ? null : choices,
                choices.Count > 0);
        }

        private List<StoryChoice> BuildInlineChoices(StoryChapter chapter, StoryStep step, string branchId)
        {
            if (chapter == null ||
                step == null ||
                step.Kind != StoryStepKind.Line ||
                step.Data.Target == null ||
                step.Data.Target.TargetKind != StoryTargetKind.Step ||
                string.Equals(step.Data.Target.ChapterId, chapter.ChapterId, StringComparison.Ordinal) is false)
            {
                return new List<StoryChoice>();
            }

            var target = GetStep(chapter, step.Data.Target.StepId);
            return target.Kind == StoryStepKind.Choice ? BuildChoices(target, branchId) : new List<StoryChoice>();
        }

        private RunnerState ParallelFrameState(StoryFrame frame)
        {
            if (frame == null)
            {
                return RunnerState.AwaitingContinue;
            }

            if (frame.WaitsForChoice)
            {
                return RunnerState.AwaitingChoice;
            }

            if (frame.WaitsForCommand)
            {
                return RunnerState.AwaitingCommand;
            }

            if (frame.WaitsForTime)
            {
                return RunnerState.AwaitingTime;
            }

            return RunnerState.AwaitingContinue;
        }

        private StoryFrame ContinueParallel()
        {
            if (m_CurrentParallelFrame == null)
            {
                return m_CurrentFrame;
            }

            var branches = new List<StoryBranchCursor>();
            for (var i = 0; i < m_CurrentParallelFrame.Branches.Count; i++)
            {
                var branch = m_CurrentParallelFrame.Branches[i];
                if (branch.Completed || IsBranchBlocked(branch))
                {
                    branches.Add(branch);
                    continue;
                }

                branches.Add(AdvanceBranchSequential(branch));
            }

            return ResolveParallelBranches(branches);
        }

        private StoryFrame SelectParallel(string choiceId)
        {
            if (m_CurrentFrame == null || m_CurrentFrame.WaitsForChoice is false)
            {
                throw new GameException($"Story choice is not active. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId}");
            }

            if (string.IsNullOrWhiteSpace(choiceId))
            {
                throw new ArgumentException("Choice id cannot be empty.", nameof(choiceId));
            }

            var choice = FindChoice(choiceId);
            if (choice == null)
            {
                throw new GameException($"Story choice does not exist. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId} choice:{choiceId}");
            }

            var branch = FindBranch(choice.BranchId);
            if (branch == null)
            {
                throw new GameException($"Story choice branch does not exist. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId} choice:{choiceId} branch:{choice.BranchId}");
            }

            m_History.Add(new HistoryEntry(branch.Chapter.ChapterId, branch.Step.StepId, choice.ChoiceId, choice.ChoiceId, null, null, (float)m_CurrentTime));
            ClearFrame();
            JumpTo(choice.Target);
            return ResolveFrameUntilStop();
        }

        private StoryFrame CompleteParallelCommand(string commandId, string outcomeId)
        {
            if (m_CurrentFrame == null || m_CurrentFrame.WaitsForCommand is false)
            {
                throw new GameException($"Story command is not active. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId}");
            }

            if (string.IsNullOrWhiteSpace(commandId))
            {
                throw new ArgumentException("Command id cannot be empty.", nameof(commandId));
            }

            var branch = FindBranchWithCommand(commandId);
            if (branch == null)
            {
                throw new GameException($"Story command does not match current output. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId} command:{commandId}");
            }

            var command = branch.Step.Data.Command;
            ValidateCommandOutcome(command, outcomeId);
            var target = command.GetOutcomeTarget(outcomeId) ?? branch.Step.Data.Target;
            m_History.Add(new HistoryEntry(branch.Chapter.ChapterId, branch.Step.StepId, outcomeId, null, commandId, outcomeId, (float)m_CurrentTime));

            return ReplaceBranch(
                branch.BranchId,
                target == null ? AdvanceBranchSequential(branch) : AdvanceBranchToTarget(branch, target));
        }

        private StoryFrame EvaluateParallel(double time)
        {
            if (m_CurrentFrame == null || m_CurrentFrame.WaitsForTime is false)
            {
                throw new GameException($"Story wait is not active. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId}");
            }

            var deltaTime = ValidateDeltaTime(time);
            m_CurrentTime += deltaTime;
            var branches = new List<StoryBranchCursor>();
            for (var i = 0; i < m_CurrentParallelFrame.Branches.Count; i++)
            {
                var branch = m_CurrentParallelFrame.Branches[i];
                if (branch.Completed || branch.CurrentFrame == null || branch.CurrentFrame.WaitsForTime is false)
                {
                    branches.Add(branch);
                    continue;
                }

                var waitSeconds = GetWaitSeconds(branch.CurrentFrame);
                var waitElapsed = branch.WaitElapsed + deltaTime;
                if (waitElapsed >= waitSeconds)
                {
                    branches.Add(AdvanceBranchSequential(branch));
                    continue;
                }

                branches.Add(new StoryBranchCursor(branch.Branch, branch.Chapter, branch.Step, branch.CurrentFrame, false, waitElapsed));
            }

            return ResolveParallelBranches(branches);
        }

        private StoryFrame ReplaceBranch(string branchId, StoryBranchCursor nextBranch)
        {
            var branches = new List<StoryBranchCursor>();
            for (var i = 0; i < m_CurrentParallelFrame.Branches.Count; i++)
            {
                var branch = m_CurrentParallelFrame.Branches[i];
                branches.Add(string.Equals(branch.BranchId, branchId, StringComparison.Ordinal) ? nextBranch : branch);
            }

            return ResolveParallelBranches(branches);
        }

        private StoryFrame ResolveParallelBranches(IReadOnlyList<StoryBranchCursor> branches)
        {
            m_CurrentParallelFrame = new StoryParallelFrame(m_CurrentParallelFrame.ParallelStep, branches);
            if (TryResolveParallelExit(branches, out var exitFrame))
            {
                return exitFrame;
            }

            if (AllBranchesCompleted(branches))
            {
                var merge = FindCompletedMerge(branches);
                if (merge != null)
                {
                    ClearFrame();
                    if (merge.Data.Target == null)
                    {
                        CompleteStory();
                        return m_CurrentFrame;
                    }

                    JumpTo(merge.Data.Target);
                    return ResolveFrameUntilStop();
                }

                ClearFrame();
                CompleteStory();
                return m_CurrentFrame;
            }

            m_CurrentFrame = CombineParallelFrame(m_CurrentParallelFrame);
            m_State = ParallelFrameState(m_CurrentFrame);
            return m_CurrentFrame;
        }

        private bool TryResolveParallelExit(IReadOnlyList<StoryBranchCursor> branches, out StoryFrame frame)
        {
            frame = null;
            if (branches == null)
            {
                return false;
            }

            for (var i = 0; i < branches.Count; i++)
            {
                var target = branches[i]?.ExitTarget;
                if (target == null)
                {
                    continue;
                }

                ClearFrame();
                JumpTo(target);
                frame = ResolveFrameUntilStop();
                return true;
            }

            return false;
        }

        private StoryBranchCursor AdvanceBranchSequential(StoryBranchCursor branch)
        {
            if (branch?.Step?.Data.Target != null)
            {
                return AdvanceBranchToTarget(branch, branch.Step.Data.Target);
            }

            return new StoryBranchCursor(branch.Branch, branch.Chapter, null, null, true);
        }

        private StoryBranchCursor AdvanceBranchToTarget(StoryBranchCursor branch, StoryTarget target)
        {
            if (target == null)
            {
                return AdvanceBranchSequential(branch);
            }

            if (target.TargetKind == StoryTargetKind.StoryEnd)
            {
                return new StoryBranchCursor(branch.Branch, branch.Chapter, null, null, true);
            }

            if (target.TargetKind == StoryTargetKind.Chapter)
            {
                return new StoryBranchCursor(branch.Branch, branch.Chapter, branch.Step, null, false, 0d, target);
            }

            if (target.TargetKind != StoryTargetKind.Step)
            {
                throw new GameException($"Story parallel branch target is invalid. story:{StoryId} chapter:{branch.Chapter.ChapterId} step:{branch.Step.StepId} branch:{branch.BranchId}");
            }

            if (string.Equals(target.ChapterId, branch.Chapter.ChapterId, StringComparison.Ordinal) is false)
            {
                return new StoryBranchCursor(branch.Branch, branch.Chapter, branch.Step, null, false, 0d, target);
            }

            var step = GetStep(branch.Chapter, target.StepId);
            if (step.Kind == StoryStepKind.Parallel)
            {
                return new StoryBranchCursor(branch.Branch, branch.Chapter, branch.Step, null, false, 0d, target);
            }

            return BuildBranchCursorAt(branch, branch.Chapter, step);
        }

        private StoryBranchCursor BuildBranchCursorAt(StoryBranchCursor branch, StoryChapter chapter, StoryStep step)
        {
            return BuildBranchCursor(branch.Branch, chapter, step);
        }

        private bool IsBranchBlocked(StoryBranchCursor branch)
        {
            return branch?.CurrentFrame != null &&
                   (branch.CurrentFrame.WaitsForChoice || branch.CurrentFrame.WaitsForCommand || branch.CurrentFrame.WaitsForTime);
        }

        private StoryBranchCursor FindBranch(string branchId)
        {
            if (m_CurrentParallelFrame == null || string.IsNullOrWhiteSpace(branchId))
            {
                return null;
            }

            for (var i = 0; i < m_CurrentParallelFrame.Branches.Count; i++)
            {
                var branch = m_CurrentParallelFrame.Branches[i];
                if (string.Equals(branch.BranchId, branchId, StringComparison.Ordinal))
                {
                    return branch;
                }
            }

            return null;
        }

        private StoryBranchCursor FindBranchWithCommand(string commandId)
        {
            if (m_CurrentParallelFrame == null)
            {
                return null;
            }

            for (var i = 0; i < m_CurrentParallelFrame.Branches.Count; i++)
            {
                var branch = m_CurrentParallelFrame.Branches[i];
                var command = branch.Step?.Data.Command;
                if (command != null && string.Equals(command.CommandId, commandId, StringComparison.Ordinal))
                {
                    return branch;
                }
            }

            return null;
        }

        private static bool AllBranchesCompleted(IReadOnlyList<StoryBranchCursor> branches)
        {
            if (branches == null || branches.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < branches.Count; i++)
            {
                if (branches[i].Completed is false)
                {
                    return false;
                }
            }

            return true;
        }

        private static StoryStep FindCompletedMerge(IReadOnlyList<StoryBranchCursor> branches)
        {
            if (branches == null)
            {
                return null;
            }

            for (var i = 0; i < branches.Count; i++)
            {
                var step = branches[i].Step;
                if (step != null && step.Kind == StoryStepKind.Merge)
                {
                    return step;
                }
            }

            return null;
        }

        private bool TryResolveParallelControlTarget(
            StoryChapter chapter,
            StoryTarget target,
            out StoryTarget exitTarget,
            out StoryStep nextStep)
        {
            exitTarget = null;
            nextStep = null;
            if (target == null)
            {
                return false;
            }

            if (target.TargetKind == StoryTargetKind.StoryEnd || target.TargetKind == StoryTargetKind.Chapter)
            {
                exitTarget = target;
                return true;
            }

            if (target.TargetKind != StoryTargetKind.Step)
            {
                return false;
            }

            if (chapter != null &&
                string.Equals(target.ChapterId, chapter.ChapterId, StringComparison.Ordinal))
            {
                var step = GetStep(chapter, target.StepId);
                if (step != null && step.Kind != StoryStepKind.Parallel)
                {
                    nextStep = step;
                    return false;
                }
            }

            exitTarget = target;
            return true;
        }

        private List<StoryChoice> BuildChoices(StoryStep step, string branchId = null)
        {
            var choices = new List<StoryChoice>();
            if (step.Data.Choices == null)
            {
                return choices;
            }

            for (var i = 0; i < step.Data.Choices.Count; i++)
            {
                var choice = step.Data.Choices[i];
                if (choice == null)
                {
                    continue;
                }

                if (choice.Condition == null || EvaluateCondition(choice.Condition))
                {
                    choices.Add(string.IsNullOrWhiteSpace(branchId) ? choice : choice.WithBranch(branchId));
                }
            }

            return choices;
        }

    }
}
