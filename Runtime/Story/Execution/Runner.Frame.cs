using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Execution
{
    public sealed partial class Runner
    {
        private Frame BuildFrame()
        {
            var step = CurrentStep;
            if (step == null)
            {
                CompleteStory();
                return m_CurrentFrame;
            }

            switch (step.Kind)
            {
                case StepKind.Line:
                    m_CurrentFrame = BuildLineFrame(m_CurrentChapter, step);
                    m_State = m_CurrentFrame.WaitsForChoice ? RunnerState.AwaitingChoice : RunnerState.AwaitingContinue;
                    return m_CurrentFrame;
                case StepKind.Command:
                    m_State = RequiresCommandCompletion(step.Data.Command)
                        ? RunnerState.AwaitingCommand
                        : RunnerState.AwaitingContinue;
                    m_CurrentFrame = Frame.CreateCommand(m_Program, m_CurrentChapter, step, m_State == RunnerState.AwaitingCommand);
                    return m_CurrentFrame;
                case StepKind.Wait:
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

                    m_CurrentFrame = Frame.CreateWait(m_Program, m_CurrentChapter, step, step.Data.WaitSeconds);
                    return m_CurrentFrame;
                case StepKind.Choice:
                    var choices = BuildChoices(step);
                    if (choices.Count == 0)
                    {
                        throw new GameException($"Story choice has no available options. story:{StoryId} chapter:{CurrentChapterId} step:{step.StepId}");
                    }

                    m_State = RunnerState.AwaitingChoice;
                    m_CurrentFrame = Frame.CreateChoice(m_Program, m_CurrentChapter, step, choices);
                    return m_CurrentFrame;
                default:
                    throw new GameException($"Story frame step kind is invalid. story:{StoryId} chapter:{CurrentChapterId} step:{step.StepId} kind:{step.Kind}");
            }
        }

        private Frame BuildParallelFrame(Step parallelStep)
        {
            var branches = new List<BranchCursor>();
            for (var i = 0; i < parallelStep.Data.Branches.Count; i++)
            {
                branches.Add(BuildBranchCursor(parallelStep, parallelStep.Data.Branches[i]));
            }

            m_CurrentParallelFrame = new ParallelFrame(parallelStep, branches);
            return ResolveParallelBranches(branches);
        }

        private BranchCursor BuildBranchCursor(Step parallelStep, ParallelBranch branch)
        {
            if (branch == null)
            {
                throw new GameException($"Story parallel branch is missing. story:{StoryId} chapter:{CurrentChapterId} step:{parallelStep.StepId}");
            }

            var chapter = GetChapter(branch.Entry.ChapterId);
            return BuildBranchCursor(branch, chapter, GetStep(chapter, branch.Entry.StepId));
        }

        private BranchCursor BuildBranchCursor(ParallelBranch branch, Chapter chapter, Step step, double waitElapsed = 0d)
        {
            while (step != null)
            {
                switch (step.Kind)
                {
                    case StepKind.Start:
                        step = GetNextStep(chapter, step);
                        continue;
                    case StepKind.Branch:
                        if (EvaluateCondition(step.Data.Condition) && step.Data.Target?.TargetKind == TargetKind.StoryEnd)
                        {
                            return new BranchCursor(branch, chapter, step, null, true);
                        }

                        if (TryResolveParallelControlTarget(chapter, step.Data.Target, out var branchTarget, out var branchStep))
                        {
                            return new BranchCursor(branch, chapter, step, null, false, 0d, branchTarget);
                        }

                        step = ResolveBranchStep(chapter, step);
                        continue;
                    case StepKind.Jump:
                        if (step.Data.Target?.TargetKind == TargetKind.StoryEnd)
                        {
                            return new BranchCursor(branch, chapter, step, null, true);
                        }

                        if (TryResolveParallelControlTarget(chapter, step.Data.Target, out var jumpTarget, out var jumpStep))
                        {
                            return new BranchCursor(branch, chapter, step, null, false, 0d, jumpTarget);
                        }

                        step = ResolveJumpStep(chapter, step);
                        continue;
                    case StepKind.Line:
                    case StepKind.Command:
                    case StepKind.Wait:
                    case StepKind.Choice:
                        return new BranchCursor(
                            branch,
                            chapter,
                            step,
                            BuildBranchFrame(chapter, step, branch, waitElapsed),
                            false,
                            step.Kind == StepKind.Wait ? waitElapsed : 0d);
                    case StepKind.Merge:
                    case StepKind.End:
                        return new BranchCursor(branch, chapter, step, null, true);
                    case StepKind.Parallel:
                        return new BranchCursor(
                            branch,
                            chapter,
                            step,
                            null,
                            false,
                            0d,
                            Target.Step(chapter.ChapterId, step.StepId));
                    default:
                        throw new GameException($"Story step kind is invalid. story:{StoryId} chapter:{chapter.ChapterId} step:{step.StepId} kind:{step.Kind}");
                }
            }

            return new BranchCursor(branch, chapter, null, null, true);
        }

        private Frame BuildBranchFrame(Chapter chapter, Step step, ParallelBranch branch, double waitElapsed = 0d)
        {
            switch (step.Kind)
            {
                case StepKind.Line:
                    return BuildLineFrame(chapter, step, branch);
                case StepKind.Command:
                    return new Frame(
                        m_Program,
                        chapter,
                        step,
                        new[] { FrameTrack.CreateCommand(step, branch.BranchId, branch.Label) },
                        null,
                        false,
                        RequiresCommandCompletion(step.Data.Command));
                case StepKind.Wait:
                    return new Frame(
                        m_Program,
                        chapter,
                        step,
                        new[] { FrameTrack.CreateWait(step, step.Data.WaitSeconds, branch.BranchId, branch.Label) },
                        null,
                        false,
                        false,
                        true);
                case StepKind.Choice:
                    var choices = BuildChoices(step, branch.BranchId);
                    if (choices.Count == 0)
                    {
                        throw new GameException($"Story choice has no available options. story:{StoryId} chapter:{chapter.ChapterId} step:{step.StepId}");
                    }

                    return new Frame(m_Program, chapter, step, null, choices, true);
                default:
                    throw new GameException($"Story branch frame step kind is invalid. story:{StoryId} chapter:{chapter.ChapterId} step:{step.StepId} kind:{step.Kind}");
            }
        }

        private Frame CombineParallelFrame(ParallelFrame parallelFrame)
        {
            var tracks = new List<FrameTrack>();
            var choices = new List<Choice>();
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

            return new Frame(
                m_Program,
                m_CurrentChapter,
                parallelFrame.ParallelStep,
                tracks,
                choices,
                waitsForChoice,
                waitsForCommand,
                waitsForTime);
        }

        private Frame BuildLineFrame(Chapter chapter, Step step, ParallelBranch branch = null)
        {
            var choices = BuildInlineChoices(chapter, step, branch?.BranchId);
            return new Frame(
                m_Program,
                chapter,
                step,
                new[] { FrameTrack.CreateText(step, branch?.BranchId, branch?.Label) },
                choices.Count == 0 ? null : choices,
                choices.Count > 0);
        }

        private List<Choice> BuildInlineChoices(Chapter chapter, Step step, string branchId)
        {
            if (chapter == null ||
                step == null ||
                step.Kind != StepKind.Line ||
                step.Data.Target == null ||
                step.Data.Target.TargetKind != TargetKind.Step ||
                string.Equals(step.Data.Target.ChapterId, chapter.ChapterId, StringComparison.Ordinal) is false)
            {
                return new List<Choice>();
            }

            var target = GetStep(chapter, step.Data.Target.StepId);
            return target.Kind == StepKind.Choice ? BuildChoices(target, branchId) : new List<Choice>();
        }

        private RunnerState ParallelFrameState(Frame frame)
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

        private Frame ContinueParallel()
        {
            if (m_CurrentParallelFrame == null)
            {
                return m_CurrentFrame;
            }

            var branches = new List<BranchCursor>();
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

        private Frame SelectParallel(string choiceId)
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

        private Frame CompleteParallelCommand(string commandId, string outcomeId)
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

        private Frame EvaluateParallel(double time)
        {
            if (m_CurrentFrame == null || m_CurrentFrame.WaitsForTime is false)
            {
                throw new GameException($"Story wait is not active. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId}");
            }

            var deltaTime = ValidateDeltaTime(time);
            m_CurrentTime += deltaTime;
            var branches = new List<BranchCursor>();
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

                branches.Add(new BranchCursor(branch.Branch, branch.Chapter, branch.Step, branch.CurrentFrame, false, waitElapsed));
            }

            return ResolveParallelBranches(branches);
        }

        private Frame ReplaceBranch(string branchId, BranchCursor nextBranch)
        {
            var branches = new List<BranchCursor>();
            for (var i = 0; i < m_CurrentParallelFrame.Branches.Count; i++)
            {
                var branch = m_CurrentParallelFrame.Branches[i];
                branches.Add(string.Equals(branch.BranchId, branchId, StringComparison.Ordinal) ? nextBranch : branch);
            }

            return ResolveParallelBranches(branches);
        }

        private Frame ResolveParallelBranches(IReadOnlyList<BranchCursor> branches)
        {
            m_CurrentParallelFrame = new ParallelFrame(m_CurrentParallelFrame.ParallelStep, branches);
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

        private bool TryResolveParallelExit(IReadOnlyList<BranchCursor> branches, out Frame frame)
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

        private BranchCursor AdvanceBranchSequential(BranchCursor branch)
        {
            if (branch?.Step?.Data.Target != null)
            {
                return AdvanceBranchToTarget(branch, branch.Step.Data.Target);
            }

            return new BranchCursor(branch.Branch, branch.Chapter, null, null, true);
        }

        private BranchCursor AdvanceBranchToTarget(BranchCursor branch, Target target)
        {
            if (target == null)
            {
                return AdvanceBranchSequential(branch);
            }

            if (target.TargetKind == TargetKind.StoryEnd)
            {
                return new BranchCursor(branch.Branch, branch.Chapter, null, null, true);
            }

            if (target.TargetKind == TargetKind.Chapter)
            {
                return new BranchCursor(branch.Branch, branch.Chapter, branch.Step, null, false, 0d, target);
            }

            if (target.TargetKind != TargetKind.Step)
            {
                throw new GameException($"Story parallel branch target is invalid. story:{StoryId} chapter:{branch.Chapter.ChapterId} step:{branch.Step.StepId} branch:{branch.BranchId}");
            }

            if (string.Equals(target.ChapterId, branch.Chapter.ChapterId, StringComparison.Ordinal) is false)
            {
                return new BranchCursor(branch.Branch, branch.Chapter, branch.Step, null, false, 0d, target);
            }

            var step = GetStep(branch.Chapter, target.StepId);
            if (step.Kind == StepKind.Parallel)
            {
                return new BranchCursor(branch.Branch, branch.Chapter, branch.Step, null, false, 0d, target);
            }

            return BuildBranchCursorAt(branch, branch.Chapter, step);
        }

        private BranchCursor BuildBranchCursorAt(BranchCursor branch, Chapter chapter, Step step)
        {
            return BuildBranchCursor(branch.Branch, chapter, step);
        }

        private bool IsBranchBlocked(BranchCursor branch)
        {
            return branch?.CurrentFrame != null &&
                   (branch.CurrentFrame.WaitsForChoice || branch.CurrentFrame.WaitsForCommand || branch.CurrentFrame.WaitsForTime);
        }

        private BranchCursor FindBranch(string branchId)
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

        private BranchCursor FindBranchWithCommand(string commandId)
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

        private static bool AllBranchesCompleted(IReadOnlyList<BranchCursor> branches)
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

        private static Step FindCompletedMerge(IReadOnlyList<BranchCursor> branches)
        {
            if (branches == null)
            {
                return null;
            }

            for (var i = 0; i < branches.Count; i++)
            {
                var step = branches[i].Step;
                if (step != null && step.Kind == StepKind.Merge)
                {
                    return step;
                }
            }

            return null;
        }

        private bool TryResolveParallelControlTarget(
            Chapter chapter,
            Target target,
            out Target exitTarget,
            out Step nextStep)
        {
            exitTarget = null;
            nextStep = null;
            if (target == null)
            {
                return false;
            }

            if (target.TargetKind == TargetKind.StoryEnd || target.TargetKind == TargetKind.Chapter)
            {
                exitTarget = target;
                return true;
            }

            if (target.TargetKind != TargetKind.Step)
            {
                return false;
            }

            if (chapter != null &&
                string.Equals(target.ChapterId, chapter.ChapterId, StringComparison.Ordinal))
            {
                var step = GetStep(chapter, target.StepId);
                if (step != null && step.Kind != StepKind.Parallel)
                {
                    nextStep = step;
                    return false;
                }
            }

            exitTarget = target;
            return true;
        }

        private List<Choice> BuildChoices(Step step, string branchId = null)
        {
            var choices = new List<Choice>();
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
