using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情程序运行器。
    /// </summary>
    public sealed class StoryRunner
    {
        private enum RunnerState
        {
            Idle = 0,
            AwaitingContinue = 1,
            AwaitingChoice = 2,
            AwaitingCommand = 3,
            AwaitingTime = 4,
            Completed = 5
        }

        private sealed class StoryParallelFrame
        {
            public StoryParallelFrame(StoryStep parallelStep, IReadOnlyList<StoryBranchCursor> branches)
            {
                ParallelStep = parallelStep ?? throw new ArgumentNullException(nameof(parallelStep));
                Branches = branches ?? throw new ArgumentNullException(nameof(branches));
            }

            public StoryStep ParallelStep { get; }

            public IReadOnlyList<StoryBranchCursor> Branches { get; }
        }

        private sealed class StoryBranchCursor
        {
            public StoryBranchCursor(
                StoryParallelBranch branch,
                StoryChapter chapter,
                StoryStep step,
                StoryFrame currentFrame,
                bool completed,
                double waitElapsed = 0d)
            {
                Branch = branch ?? throw new ArgumentNullException(nameof(branch));
                Chapter = chapter;
                Step = step;
                CurrentFrame = currentFrame;
                Completed = completed;
                WaitElapsed = waitElapsed < 0d ? 0d : waitElapsed;
            }

            public StoryParallelBranch Branch { get; }

            public string BranchId => Branch.BranchId;

            public string BranchLabel => Branch.Label;

            public StoryChapter Chapter { get; }

            public StoryStep Step { get; }

            public bool Completed { get; }

            public StoryFrame CurrentFrame { get; }

            public double WaitElapsed { get; }
        }

        private readonly StoryProgram m_Program;
        private readonly Dictionary<string, StoryChapter> m_Chapters;
        private readonly Dictionary<string, Dictionary<string, int>> m_Steps;
        private readonly StoryVariableStore m_VariableStore;
        private readonly IStoryFunctionResolver m_FunctionResolver;
        private readonly List<HistoryEntry> m_History = new List<HistoryEntry>();

        private StoryChapter m_CurrentChapter;
        private int m_CurrentStepIndex = -1;
        private double m_CurrentTime;
        private double m_CurrentWaitElapsed;
        private RunnerState m_State = RunnerState.Idle;
        private StoryFrame m_CurrentFrame;
        private StoryParallelFrame m_CurrentParallelFrame;
        private bool m_HasPendingWaitElapsed;
        private double m_PendingWaitElapsed;

        /// <summary>
        /// 初始化剧情运行器。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="functionResolver">外部函数解析器。</param>
        public StoryRunner(StoryProgram program, IStoryFunctionResolver functionResolver = null)
        {
            m_Program = program ?? throw new ArgumentNullException(nameof(program));
            m_FunctionResolver = functionResolver;
            m_Chapters = new Dictionary<string, StoryChapter>(StringComparer.Ordinal);
            m_Steps = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
            m_VariableStore = new StoryVariableStore();
            BuildMaps(program);
            ResetVariables();
        }

        /// <summary>
        /// 剧情 ID。
        /// </summary>
        public string StoryId => m_Program.StoryId;

        /// <summary>
        /// 剧情程序。
        /// </summary>
        public StoryProgram Program => m_Program;

        /// <summary>
        /// 版本。
        /// </summary>
        public string Version => m_Program.Version;

        /// <summary>
        /// 当前章节 ID。
        /// </summary>
        public string CurrentChapterId => m_CurrentChapter?.ChapterId;

        /// <summary>
        /// 当前步骤 ID。
        /// </summary>
        public string CurrentStepId => CurrentStep?.StepId;

        /// <summary>
        /// 当前章节。
        /// </summary>
        public StoryChapter CurrentChapter => m_CurrentChapter;

        /// <summary>
        /// 当前步骤。
        /// </summary>
        public StoryStep CurrentStep
        {
            get
            {
                if (m_CurrentChapter == null ||
                    m_CurrentStepIndex < 0 ||
                    m_CurrentStepIndex >= m_CurrentChapter.Steps.Count)
                {
                    return null;
                }

                return m_CurrentChapter.Steps[m_CurrentStepIndex];
            }
        }

        /// <summary>
        /// 当前帧。
        /// </summary>
        public StoryFrame CurrentFrame => m_CurrentFrame;

        /// <summary>
        /// 当前时间。
        /// </summary>
        public double CurrentTime => m_CurrentTime;

        /// <summary>
        /// 是否已完成。
        /// </summary>
        public bool Completed => m_State == RunnerState.Completed;

        /// <summary>
        /// 变量存储。
        /// </summary>
        public IStoryVariableStore VariableStore => m_VariableStore;

        /// <summary>
        /// 剧情历史。
        /// </summary>
        public IReadOnlyList<HistoryEntry> History => m_History;

        /// <summary>
        /// 启动剧情。
        /// </summary>
        /// <param name="chapterId">章节 ID。</param>
        /// <returns>第一个帧。</returns>
        public StoryFrame Start(string chapterId = null)
        {
            if (m_State != RunnerState.Idle)
            {
                throw new GameException($"Story runner has already started. story:{StoryId}");
            }

            ResetVariables();
            m_History.Clear();
            m_CurrentTime = 0d;
            m_CurrentWaitElapsed = 0d;
            m_HasPendingWaitElapsed = false;
            m_PendingWaitElapsed = 0d;
            m_CurrentFrame = null;
            m_CurrentParallelFrame = null;
            m_State = RunnerState.Idle;
            m_CurrentChapter = GetChapter(string.IsNullOrWhiteSpace(chapterId) ? m_Program.EntryChapterId : chapterId);
            EnterStep(m_CurrentChapter.EntryStepId);
            return ResolveFrameUntilStop();
        }

        /// <summary>
        /// 继续剧情。
        /// </summary>
        /// <returns>当前或下一个帧。</returns>
        public StoryFrame Continue()
        {
            EnsureRunning();
            if (m_CurrentParallelFrame != null)
            {
                return ContinueParallel();
            }

            switch (m_State)
            {
                case RunnerState.AwaitingChoice:
                case RunnerState.AwaitingCommand:
                    return m_CurrentFrame;
                case RunnerState.AwaitingTime:
                    if (m_CurrentFrame != null && m_CurrentWaitElapsed < GetWaitSeconds(m_CurrentFrame))
                    {
                        return m_CurrentFrame;
                    }

                    ClearFrame();
                    AdvanceFromCurrentStep();
                    return ResolveFrameUntilStop();
                case RunnerState.AwaitingContinue:
                    ClearFrame();
                    AdvanceFromCurrentStep();
                    return ResolveFrameUntilStop();
                default:
                    return ResolveFrameUntilStop();
            }
        }

        /// <summary>
        /// 选择一个选项。
        /// </summary>
        /// <param name="choiceId">选项 ID。</param>
        /// <returns>选择后的帧。</returns>
        public StoryFrame Select(string choiceId)
        {
            EnsureRunning();
            if (m_CurrentParallelFrame != null)
            {
                return SelectParallel(choiceId);
            }

            if (m_State != RunnerState.AwaitingChoice || m_CurrentFrame == null)
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

            m_History.Add(new HistoryEntry(CurrentChapterId, CurrentStepId, choice.ChoiceId, choice.ChoiceId, null, null, (float)m_CurrentTime));
            ClearFrame();
            JumpTo(choice.Target);
            return ResolveFrameUntilStop();
        }

        /// <summary>
        /// 完成外部命令。
        /// </summary>
        /// <param name="commandId">命令 ID。</param>
        /// <param name="outcomeId">结果 ID。</param>
        /// <returns>完成后的帧。</returns>
        public StoryFrame CompleteCommand(string commandId, string outcomeId)
        {
            EnsureRunning();
            if (m_CurrentParallelFrame != null)
            {
                return CompleteParallelCommand(commandId, outcomeId);
            }

            if (m_State != RunnerState.AwaitingCommand || m_CurrentFrame == null)
            {
                throw new GameException($"Story command is not active. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId}");
            }

            if (string.IsNullOrWhiteSpace(commandId))
            {
                throw new ArgumentException("Command id cannot be empty.", nameof(commandId));
            }

            var command = GetBlockingCommand(m_CurrentFrame);
            if (command == null || !string.Equals(command.CommandId, commandId, StringComparison.Ordinal))
            {
                throw new GameException($"Story command does not match current output. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId} command:{commandId}");
            }

            ValidateCommandOutcome(command, outcomeId);
            var target = command.GetOutcomeTarget(outcomeId);
            if (target == null)
            {
                target = CurrentStep.Data.Target;
            }

            m_History.Add(new HistoryEntry(CurrentChapterId, CurrentStepId, outcomeId, null, commandId, outcomeId, (float)m_CurrentTime));
            ClearFrame();
            if (target != null)
            {
                JumpTo(target);
            }
            else
            {
                AdvanceSequential();
            }

            return ResolveFrameUntilStop();
        }

        /// <summary>
        /// 推进等待时间。
        /// </summary>
        /// <param name="time">时间增量。</param>
        /// <returns>当前或下一个帧。</returns>
        public StoryFrame Evaluate(double time)
        {
            EnsureRunning();
            if (m_CurrentParallelFrame != null)
            {
                return EvaluateParallel(time);
            }

            if (m_State != RunnerState.AwaitingTime || m_CurrentFrame == null)
            {
                throw new GameException($"Story wait is not active. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId}");
            }

            var deltaTime = ValidateDeltaTime(time);
            m_CurrentTime += deltaTime;
            m_CurrentWaitElapsed += deltaTime;
            if (m_CurrentWaitElapsed >= GetWaitSeconds(m_CurrentFrame))
            {
                ClearFrame();
                AdvanceFromCurrentStep();
                return ResolveFrameUntilStop();
            }

            return m_CurrentFrame;
        }

        /// <summary>
        /// 创建快照。
        /// </summary>
        /// <returns>快照。</returns>
        public StorySnapshot CreateSnapshot()
        {
            return new StorySnapshot(
                StoryId,
                Version,
                CurrentChapterId,
                CurrentSnapshotStepId(),
                m_CurrentTime,
                CaptureVariables(),
                new List<HistoryEntry>(m_History),
                Completed,
                ToSnapshotState(m_State),
                m_CurrentParallelFrame == null ? m_CurrentWaitElapsed : 0d,
                CaptureParallelBranches());
        }

        /// <summary>
        /// 从快照恢复。
        /// </summary>
        /// <param name="snapshot">快照。</param>
        /// <returns>恢复后的帧。</returns>
        public StoryFrame Restore(StorySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!string.Equals(snapshot.StoryId, StoryId, StringComparison.Ordinal))
            {
                throw new GameException($"Story snapshot story id does not match. story:{snapshot.StoryId}");
            }

            if (!string.Equals(snapshot.Version, Version, StringComparison.Ordinal))
            {
                throw new GameException($"Story snapshot version does not match. story:{snapshot.StoryId} version:{snapshot.Version}");
            }

            ResetVariables();
            ApplyVariables(snapshot.Variables);
            m_History.Clear();
            if (snapshot.History != null)
            {
                for (var i = 0; i < snapshot.History.Count; i++)
                {
                    m_History.Add(snapshot.History[i]);
                }
            }

            m_CurrentTime = snapshot.CurrentTime;
            m_CurrentWaitElapsed = 0d;
            m_HasPendingWaitElapsed = false;
            m_PendingWaitElapsed = 0d;
            m_CurrentChapter = GetChapter(snapshot.ChapterId);
            EnterStep(snapshot.StepId);
            m_CurrentFrame = null;
            m_CurrentParallelFrame = null;
            m_State = RunnerState.Idle;
            if (snapshot.Completed)
            {
                m_State = RunnerState.Completed;
                m_CurrentFrame = StoryFrame.CreateCompleted(m_Program, m_CurrentChapter, CurrentStep);
                return m_CurrentFrame;
            }

            if (snapshot.ParallelBranches.Count > 0)
            {
                return RestoreParallelFrame(snapshot);
            }

            m_HasPendingWaitElapsed = snapshot.WaitElapsed > 0d;
            m_PendingWaitElapsed = snapshot.WaitElapsed;
            return ResolveFrameUntilStop();
        }

        private StoryFrame RestoreParallelFrame(StorySnapshot snapshot)
        {
            var parallelStep = CurrentStep;
            if (parallelStep == null || parallelStep.Kind != StoryStepKind.Parallel)
            {
                throw new GameException($"Story snapshot parallel anchor is invalid. story:{StoryId} chapter:{CurrentChapterId} step:{snapshot.StepId}");
            }

            var branches = new List<StoryBranchCursor>();
            for (var i = 0; i < parallelStep.Data.Branches.Count; i++)
            {
                var branch = parallelStep.Data.Branches[i];
                var branchSnapshot = FindBranchSnapshot(snapshot.ParallelBranches, branch.BranchId);
                if (branchSnapshot == null)
                {
                    throw new GameException($"Story snapshot parallel branch is missing. story:{StoryId} chapter:{CurrentChapterId} step:{parallelStep.StepId} branch:{branch.BranchId}");
                }

                var chapter = GetChapter(string.IsNullOrWhiteSpace(branchSnapshot.ChapterId) ? branch.Entry.ChapterId : branchSnapshot.ChapterId);
                StoryStep step = null;
                if (string.IsNullOrWhiteSpace(branchSnapshot.StepId) is false)
                {
                    step = GetStep(chapter, branchSnapshot.StepId);
                }

                if (branchSnapshot.Completed)
                {
                    branches.Add(new StoryBranchCursor(branch, chapter, step, null, true, branchSnapshot.WaitElapsed));
                    continue;
                }

                if (step == null)
                {
                    throw new GameException($"Story snapshot parallel branch step is missing. story:{StoryId} chapter:{chapter.ChapterId} branch:{branch.BranchId}");
                }

                branches.Add(BuildBranchCursor(branch, chapter, step, branchSnapshot.WaitElapsed));
            }

            m_CurrentParallelFrame = new StoryParallelFrame(parallelStep, branches);
            m_CurrentFrame = CombineParallelFrame(m_CurrentParallelFrame);
            m_State = ParallelFrameState(m_CurrentFrame);
            return m_CurrentFrame;
        }

        private StoryFrame ResolveFrameUntilStop()
        {
            while (!Completed)
            {
                var step = CurrentStep;
                if (step == null)
                {
                    CompleteStory();
                    return m_CurrentFrame;
                }

                switch (step.Kind)
                {
                    case StoryStepKind.Start:
                        AdvanceSequential();
                        continue;
                    case StoryStepKind.Branch:
                        if (EvaluateCondition(step.Data.Condition))
                        {
                            if (step.Data.Target == null)
                            {
                                throw new GameException($"Story branch target is missing. story:{StoryId} chapter:{CurrentChapterId} step:{step.StepId}");
                            }

                            JumpTo(step.Data.Target);
                        }
                        else
                        {
                            AdvanceSequential();
                        }

                        continue;
                    case StoryStepKind.Jump:
                        if (step.Data.Target == null)
                        {
                            throw new GameException($"Story jump target is missing. story:{StoryId} chapter:{CurrentChapterId} step:{step.StepId}");
                        }

                        JumpTo(step.Data.Target);
                        continue;
                    case StoryStepKind.Line:
                    case StoryStepKind.Choice:
                    case StoryStepKind.Command:
                    case StoryStepKind.Wait:
                        return BuildFrame();
                    case StoryStepKind.Parallel:
                        return BuildParallelFrame(step);
                    case StoryStepKind.Merge:
                        AdvanceFromCurrentStep();
                        continue;
                    case StoryStepKind.End:
                        CompleteStory();
                        return m_CurrentFrame;
                    default:
                        throw new GameException($"Story step kind is invalid. story:{StoryId} chapter:{CurrentChapterId} step:{step.StepId} kind:{step.Kind}");
                }
            }

            return m_CurrentFrame;
        }

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
            m_CurrentFrame = CombineParallelFrame(m_CurrentParallelFrame);
            m_State = ParallelFrameState(m_CurrentFrame);
            return m_CurrentFrame;
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
                        step = ResolveBranchStep(chapter, step);
                        continue;
                    case StoryStepKind.Jump:
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
                        throw new GameException($"Nested story parallel is not supported. story:{StoryId} chapter:{chapter.ChapterId} step:{step.StepId}");
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
                branches.Add(waitElapsed >= waitSeconds
                    ? AdvanceBranchSequential(branch)
                    : new StoryBranchCursor(branch.Branch, branch.Chapter, branch.Step, branch.CurrentFrame, false, waitElapsed));
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

            if (target.TargetKind != StoryTargetKind.Step ||
                string.Equals(target.ChapterId, branch.Chapter.ChapterId, StringComparison.Ordinal) is false)
            {
                throw new GameException($"Story parallel branch target must stay in the same chapter. story:{StoryId} chapter:{branch.Chapter.ChapterId} step:{branch.Step.StepId} branch:{branch.BranchId}");
            }

            return BuildBranchCursorAt(branch, branch.Chapter, GetStep(branch.Chapter, target.StepId));
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

        private void AdvanceSequential()
        {
            if (m_CurrentChapter == null)
            {
                throw new GameException($"Story runner chapter is missing. story:{StoryId}");
            }

            m_CurrentStepIndex++;
            if (m_CurrentStepIndex >= m_CurrentChapter.Steps.Count)
            {
                CompleteStory();
            }
        }

        private void AdvanceFromCurrentStep()
        {
            var step = CurrentStep;
            if (step?.Data.Target != null)
            {
                JumpTo(step.Data.Target);
                return;
            }

            AdvanceSequential();
        }

        private void JumpTo(StoryTarget target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            switch (target.TargetKind)
            {
                case StoryTargetKind.Step:
                    if (string.IsNullOrWhiteSpace(target.ChapterId) || string.IsNullOrWhiteSpace(target.StepId))
                    {
                        throw new GameException($"Story step target is invalid. story:{StoryId}");
                    }

                    m_CurrentChapter = GetChapter(target.ChapterId);
                    EnterStep(target.StepId);
                    break;
                case StoryTargetKind.Chapter:
                    if (string.IsNullOrWhiteSpace(target.ChapterId))
                    {
                        throw new GameException($"Story chapter target is invalid. story:{StoryId}");
                    }

                    m_CurrentChapter = GetChapter(target.ChapterId);
                    EnterStep(m_CurrentChapter.EntryStepId);
                    break;
                case StoryTargetKind.StoryEnd:
                    CompleteStory();
                    break;
                default:
                    throw new GameException($"Story target kind is invalid. story:{StoryId} kind:{target.TargetKind}");
            }
        }

        private void EnterStep(string stepId)
        {
            if (m_CurrentChapter == null)
            {
                throw new GameException($"Story chapter is missing. story:{StoryId}");
            }

            if (!m_Steps.TryGetValue(m_CurrentChapter.ChapterId, out var stepMap) ||
                !stepMap.TryGetValue(stepId, out m_CurrentStepIndex))
            {
                throw new GameException($"Story step does not exist. story:{StoryId} chapter:{m_CurrentChapter.ChapterId} step:{stepId}");
            }
        }

        private StoryStep GetStep(StoryChapter chapter, string stepId)
        {
            if (chapter == null)
            {
                throw new GameException($"Story chapter is missing. story:{StoryId}");
            }

            if (!m_Steps.TryGetValue(chapter.ChapterId, out var stepMap) ||
                !stepMap.TryGetValue(stepId, out var stepIndex))
            {
                throw new GameException($"Story step does not exist. story:{StoryId} chapter:{chapter.ChapterId} step:{stepId}");
            }

            return chapter.Steps[stepIndex];
        }

        private StoryStep GetNextStep(StoryChapter chapter, StoryStep step)
        {
            if (chapter == null || step == null)
            {
                return null;
            }

            if (!m_Steps.TryGetValue(chapter.ChapterId, out var stepMap) ||
                !stepMap.TryGetValue(step.StepId, out var stepIndex))
            {
                throw new GameException($"Story step does not exist. story:{StoryId} chapter:{chapter.ChapterId} step:{step.StepId}");
            }

            stepIndex++;
            return stepIndex >= chapter.Steps.Count ? null : chapter.Steps[stepIndex];
        }

        private StoryStep ResolveBranchStep(StoryChapter chapter, StoryStep step)
        {
            if (EvaluateCondition(step.Data.Condition))
            {
                if (step.Data.Target == null)
                {
                    throw new GameException($"Story branch target is missing. story:{StoryId} chapter:{chapter.ChapterId} step:{step.StepId}");
                }

                if (step.Data.Target.TargetKind != StoryTargetKind.Step ||
                    string.Equals(step.Data.Target.ChapterId, chapter.ChapterId, StringComparison.Ordinal) is false)
                {
                    throw new GameException($"Story parallel branch target must stay in the same chapter. story:{StoryId} chapter:{chapter.ChapterId} step:{step.StepId}");
                }

                return GetStep(chapter, step.Data.Target.StepId);
            }

            return GetNextStep(chapter, step);
        }

        private StoryStep ResolveJumpStep(StoryChapter chapter, StoryStep step)
        {
            if (step.Data.Target == null)
            {
                throw new GameException($"Story jump target is missing. story:{StoryId} chapter:{chapter.ChapterId} step:{step.StepId}");
            }

            if (step.Data.Target.TargetKind != StoryTargetKind.Step ||
                string.Equals(step.Data.Target.ChapterId, chapter.ChapterId, StringComparison.Ordinal) is false)
            {
                throw new GameException($"Story parallel branch jump must stay in the same chapter. story:{StoryId} chapter:{chapter.ChapterId} step:{step.StepId}");
            }

            return GetStep(chapter, step.Data.Target.StepId);
        }

        private void CompleteStory()
        {
            m_CurrentParallelFrame = null;
            m_CurrentWaitElapsed = 0d;
            m_HasPendingWaitElapsed = false;
            m_PendingWaitElapsed = 0d;
            m_State = RunnerState.Completed;
            if (m_CurrentChapter == null)
            {
                m_CurrentFrame = null;
                return;
            }

            var step = CurrentStep;
            m_CurrentFrame = StoryFrame.CreateCompleted(m_Program, m_CurrentChapter, step);
        }

        private void ClearFrame()
        {
            m_CurrentFrame = null;
            m_CurrentParallelFrame = null;
            m_CurrentWaitElapsed = 0d;
            m_HasPendingWaitElapsed = false;
            m_PendingWaitElapsed = 0d;
            if (m_State != RunnerState.Completed)
            {
                m_State = RunnerState.Idle;
            }
        }

        private StoryChapter GetChapter(string chapterId)
        {
            if (string.IsNullOrWhiteSpace(chapterId) || !m_Chapters.TryGetValue(chapterId, out var chapter))
            {
                throw new GameException($"Story chapter does not exist. story:{StoryId} chapter:{chapterId}");
            }

            return chapter;
        }

        private StoryChoice FindChoice(string choiceId)
        {
            var frame = m_CurrentFrame;
            if (frame?.Choices == null)
            {
                return null;
            }

            for (var i = 0; i < frame.Choices.Count; i++)
            {
                var choice = frame.Choices[i];
                if (choice != null && string.Equals(choice.ChoiceId, choiceId, StringComparison.Ordinal))
                {
                    return choice;
                }
            }

            return null;
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

        private string CurrentSnapshotStepId()
        {
            if (m_CurrentFrame != null &&
                m_CurrentFrame.IsCompleted is false &&
                m_CurrentFrame.AnchorStep != null)
            {
                return m_CurrentFrame.AnchorStep.StepId;
            }

            return CurrentStepId;
        }

        private bool EvaluateCondition(StoryExpression expression)
        {
            if (expression == null)
            {
                return true;
            }

            var value = EvaluateExpression(expression);
            if (value.TryGetBoolean(out var booleanValue))
            {
                return booleanValue;
            }

            if (value.IsNull)
            {
                return false;
            }

            if (value.TryGetNumber(out var numberValue))
            {
                return Math.Abs(numberValue) > double.Epsilon;
            }

            if (value.TryGetString(out var stringValue))
            {
                return string.IsNullOrWhiteSpace(stringValue) is false;
            }

            return false;
        }

        private StoryValue EvaluateExpression(StoryExpression expression)
        {
            if (expression == null)
            {
                return StoryValue.Null;
            }

            switch (expression.Kind)
            {
                case StoryExpressionKind.Literal:
                    return expression.Literal;
                case StoryExpressionKind.Variable:
                    return m_VariableStore.TryGet(expression.VariableName, out var value) ? value : StoryValue.Null;
                case StoryExpressionKind.Function:
                    if (m_FunctionResolver == null)
                    {
                        throw new GameException($"Story function resolver is missing. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId} function:{expression.FunctionName}");
                    }

                    return m_FunctionResolver.Evaluate(
                        expression.FunctionName,
                        EvaluateArguments(expression.Inputs),
                        CreateContext());
                case StoryExpressionKind.Not:
                    return StoryValue.FromBoolean(!EvaluateCondition(expression.Inputs[0]));
                case StoryExpressionKind.And:
                    for (var i = 0; i < expression.Inputs.Count; i++)
                    {
                        if (!EvaluateCondition(expression.Inputs[i]))
                        {
                            return StoryValue.FromBoolean(false);
                        }
                    }

                    return StoryValue.FromBoolean(true);
                case StoryExpressionKind.Or:
                    for (var i = 0; i < expression.Inputs.Count; i++)
                    {
                        if (EvaluateCondition(expression.Inputs[i]))
                        {
                            return StoryValue.FromBoolean(true);
                        }
                    }

                    return StoryValue.FromBoolean(false);
                case StoryExpressionKind.Equal:
                    return StoryValue.FromBoolean(Compare(expression.Inputs[0], expression.Inputs[1]) == 0);
                case StoryExpressionKind.NotEqual:
                    return StoryValue.FromBoolean(Compare(expression.Inputs[0], expression.Inputs[1]) != 0);
                case StoryExpressionKind.Greater:
                    return StoryValue.FromBoolean(Compare(expression.Inputs[0], expression.Inputs[1]) > 0);
                case StoryExpressionKind.GreaterOrEqual:
                    return StoryValue.FromBoolean(Compare(expression.Inputs[0], expression.Inputs[1]) >= 0);
                case StoryExpressionKind.Less:
                    return StoryValue.FromBoolean(Compare(expression.Inputs[0], expression.Inputs[1]) < 0);
                case StoryExpressionKind.LessOrEqual:
                    return StoryValue.FromBoolean(Compare(expression.Inputs[0], expression.Inputs[1]) <= 0);
                default:
                    throw new GameException($"Story expression kind is invalid. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId} kind:{expression.Kind}");
            }
        }

        private IReadOnlyList<StoryValue> EvaluateArguments(IReadOnlyList<StoryExpression> inputs)
        {
            if (inputs == null || inputs.Count == 0)
            {
                return Array.Empty<StoryValue>();
            }

            var values = new StoryValue[inputs.Count];
            for (var i = 0; i < inputs.Count; i++)
            {
                values[i] = EvaluateExpression(inputs[i]);
            }

            return values;
        }

        private int Compare(StoryExpression left, StoryExpression right)
        {
            var leftValue = EvaluateExpression(left);
            var rightValue = EvaluateExpression(right);

            if (leftValue.Kind != rightValue.Kind)
            {
                throw new GameException($"Story expression comparison kinds do not match. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId}");
            }

            switch (leftValue.Kind)
            {
                case StoryValueKind.Boolean:
                    return leftValue.BooleanValue.CompareTo(rightValue.BooleanValue);
                case StoryValueKind.Number:
                    return leftValue.NumberValue.CompareTo(rightValue.NumberValue);
                case StoryValueKind.String:
                    return string.Compare(leftValue.StringValue, rightValue.StringValue, StringComparison.Ordinal);
                case StoryValueKind.Null:
                    return 0;
                default:
                    throw new GameException($"Story value kind is invalid. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId} kind:{leftValue.Kind}");
            }
        }

        private StoryRuntimeContext CreateContext()
        {
            return new StoryRuntimeContext(
                m_Program,
                m_CurrentChapter,
                CurrentStep,
                m_CurrentTime,
                m_VariableStore,
                m_History);
        }

        private IReadOnlyDictionary<string, StoryValue> CaptureVariables()
        {
            var copy = new Dictionary<string, StoryValue>(StringComparer.Ordinal);
            var schema = m_Program.VariableSchema;
            if (schema == null || schema.Definitions == null)
            {
                return copy;
            }

            for (var i = 0; i < schema.Definitions.Count; i++)
            {
                var variable = schema.Definitions[i];
                if (variable == null || string.IsNullOrWhiteSpace(variable.Name))
                {
                    continue;
                }

                if (m_VariableStore.TryGet(variable.Name, out var value))
                {
                    copy[variable.Name] = value;
                }
                else
                {
                    copy[variable.Name] = variable.DefaultValue;
                }
            }

            return copy;
        }

        private IReadOnlyList<StoryParallelBranchSnapshot> CaptureParallelBranches()
        {
            if (m_CurrentParallelFrame == null || m_CurrentParallelFrame.Branches.Count == 0)
            {
                return Array.Empty<StoryParallelBranchSnapshot>();
            }

            var snapshots = new List<StoryParallelBranchSnapshot>();
            for (var i = 0; i < m_CurrentParallelFrame.Branches.Count; i++)
            {
                var branch = m_CurrentParallelFrame.Branches[i];
                if (branch == null)
                {
                    continue;
                }

                snapshots.Add(new StoryParallelBranchSnapshot(
                    branch.BranchId,
                    branch.Chapter?.ChapterId,
                    branch.Step?.StepId,
                    branch.Completed,
                    branch.WaitElapsed));
            }

            return snapshots;
        }

        private static StoryParallelBranchSnapshot FindBranchSnapshot(
            IReadOnlyList<StoryParallelBranchSnapshot> snapshots,
            string branchId)
        {
            if (snapshots == null || string.IsNullOrWhiteSpace(branchId))
            {
                return null;
            }

            for (var i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                if (snapshot != null && string.Equals(snapshot.BranchId, branchId, StringComparison.Ordinal))
                {
                    return snapshot;
                }
            }

            return null;
        }

        private void ApplyVariables(IReadOnlyDictionary<string, StoryValue> variables)
        {
            if (variables == null)
            {
                return;
            }

            foreach (var pair in variables)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                m_VariableStore.Set(pair.Key, pair.Value);
            }
        }

        private void ResetVariables()
        {
            m_VariableStore.Clear();
            var definitions = m_Program.VariableSchema?.Definitions;
            if (definitions == null)
            {
                return;
            }

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Name))
                {
                    continue;
                }

                m_VariableStore.Set(definition.Name, definition.DefaultValue);
            }
        }

        private void BuildMaps(StoryProgram program)
        {
            for (var i = 0; i < program.Chapters.Count; i++)
            {
                var chapter = program.Chapters[i];
                if (chapter == null)
                {
                    throw new GameException($"Story chapter cannot be null. story:{StoryId} index:{i}");
                }

                if (m_Chapters.ContainsKey(chapter.ChapterId))
                {
                    throw new GameException($"Duplicate story chapter id. story:{StoryId} chapter:{chapter.ChapterId}");
                }

                m_Chapters.Add(chapter.ChapterId, chapter);

                var stepMap = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var stepIndex = 0; stepIndex < chapter.Steps.Count; stepIndex++)
                {
                    var step = chapter.Steps[stepIndex];
                    if (step == null)
                    {
                        throw new GameException($"Story step cannot be null. story:{StoryId} chapter:{chapter.ChapterId} index:{stepIndex}");
                    }

                    if (stepMap.ContainsKey(step.StepId))
                    {
                        throw new GameException($"Duplicate story step id. story:{StoryId} chapter:{chapter.ChapterId} step:{step.StepId}");
                    }

                    stepMap.Add(step.StepId, stepIndex);
                }

                m_Steps.Add(chapter.ChapterId, stepMap);
            }

            if (!m_Chapters.ContainsKey(program.EntryChapterId))
            {
                throw new GameException($"Story entry chapter does not exist. story:{StoryId} chapter:{program.EntryChapterId}");
            }
        }

        private void EnsureRunning()
        {
            if (m_State == RunnerState.Idle)
            {
                throw new GameException($"Story runner has not started. story:{StoryId}");
            }

            if (m_State == RunnerState.Completed)
            {
                throw new GameException($"Story runner is completed. story:{StoryId}");
            }
        }

        private static bool RequiresCommandCompletion(StoryCommand command)
        {
            return command != null && (command.WaitForCompletion || command.OutcomePorts.Count > 0);
        }

        private static double ValidateDeltaTime(double deltaTime)
        {
            if (double.IsNaN(deltaTime) || double.IsInfinity(deltaTime))
            {
                throw new ArgumentException("Time delta must be a finite number.", nameof(deltaTime));
            }

            if (deltaTime < 0d)
            {
                throw new ArgumentException("Time delta cannot be negative.", nameof(deltaTime));
            }

            return deltaTime;
        }

        private static StorySnapshotState ToSnapshotState(RunnerState state)
        {
            switch (state)
            {
                case RunnerState.AwaitingContinue:
                    return StorySnapshotState.AwaitingContinue;
                case RunnerState.AwaitingChoice:
                    return StorySnapshotState.AwaitingChoice;
                case RunnerState.AwaitingCommand:
                    return StorySnapshotState.AwaitingCommand;
                case RunnerState.AwaitingTime:
                    return StorySnapshotState.AwaitingTime;
                case RunnerState.Completed:
                    return StorySnapshotState.Completed;
                default:
                    return StorySnapshotState.Idle;
            }
        }

        private static StoryCommand GetBlockingCommand(StoryFrame frame)
        {
            if (frame?.Tracks == null)
            {
                return null;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track?.Kind == StoryFrameTrackKind.Command && RequiresCommandCompletion(track.Command))
                {
                    return track.Command;
                }
            }

            return null;
        }

        private void ValidateCommandOutcome(StoryCommand command, string outcomeId)
        {
            if (command == null)
            {
                return;
            }

            var hasOutcomePorts = command.OutcomePorts != null && command.OutcomePorts.Count > 0;
            if (hasOutcomePorts is false)
            {
                if (string.IsNullOrWhiteSpace(outcomeId) is false)
                {
                    throw new GameException($"Story command outcome is not declared. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId} command:{command.CommandId} outcome:{outcomeId}");
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(outcomeId))
            {
                throw new GameException($"Story command outcome cannot be empty. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId} command:{command.CommandId}");
            }

            for (var i = 0; i < command.OutcomePorts.Count; i++)
            {
                if (string.Equals(command.OutcomePorts[i], outcomeId, StringComparison.Ordinal))
                {
                    return;
                }
            }

            throw new GameException($"Story command outcome is not declared. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId} command:{command.CommandId} outcome:{outcomeId}");
        }

        private static double GetWaitSeconds(StoryFrame frame)
        {
            if (frame?.Tracks == null)
            {
                return 0d;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track?.Kind == StoryFrameTrackKind.Wait)
                {
                    return track.WaitSeconds;
                }
            }

            return 0d;
        }

    }
}
