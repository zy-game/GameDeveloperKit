using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Execution
{
    /// <summary>
    /// 剧情程序运行器。
    /// </summary>
    public sealed partial class Runner
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

        private sealed class ParallelFrame
        {
            public ParallelFrame(Step parallelStep, IReadOnlyList<BranchCursor> branches)
            {
                ParallelStep = parallelStep ?? throw new ArgumentNullException(nameof(parallelStep));
                Branches = branches ?? throw new ArgumentNullException(nameof(branches));
            }

            public Step ParallelStep { get; }

            public IReadOnlyList<BranchCursor> Branches { get; }
        }

        private sealed class BranchCursor
        {
            public BranchCursor(
                ParallelBranch branch,
                Chapter chapter,
                Step step,
                Frame currentFrame,
                bool completed,
                double waitElapsed = 0d,
                Target exitTarget = null)
            {
                Branch = branch ?? throw new ArgumentNullException(nameof(branch));
                Chapter = chapter;
                Step = step;
                CurrentFrame = currentFrame;
                Completed = completed;
                if (TimeRules.IsFiniteNonNegative(waitElapsed) is false)
                {
                    throw new ArgumentOutOfRangeException(nameof(waitElapsed), "Wait elapsed must be finite and non-negative.");
                }

                WaitElapsed = waitElapsed;
                ExitTarget = exitTarget;
            }

            public ParallelBranch Branch { get; }

            public string BranchId => Branch.BranchId;

            public string BranchLabel => Branch.Label;

            public Chapter Chapter { get; }

            public Step Step { get; }

            public bool Completed { get; }

            public Frame CurrentFrame { get; }

            public double WaitElapsed { get; }

            public Target ExitTarget { get; }
        }

        private readonly Program m_Program;
        private readonly Dictionary<string, Chapter> m_Chapters;
        private readonly Dictionary<string, Dictionary<string, int>> m_Steps;
        private readonly VariableStore m_VariableStore;
        private readonly IFunctionResolver m_FunctionResolver;
        private readonly List<HistoryEntry> m_History = new List<HistoryEntry>();

        private Chapter m_CurrentChapter;
        private int m_CurrentStepIndex = -1;
        private double m_CurrentTime;
        private double m_CurrentWaitElapsed;
        private RunnerState m_State = RunnerState.Idle;
        private Frame m_CurrentFrame;
        private ParallelFrame m_CurrentParallelFrame;
        private bool m_HasPendingWaitElapsed;
        private double m_PendingWaitElapsed;

        /// <summary>
        /// 初始化剧情运行器。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="functionResolver">外部函数解析器。</param>
        public Runner(Program program, IFunctionResolver functionResolver = null)
        {
            m_Program = program ?? throw new ArgumentNullException(nameof(program));
            m_FunctionResolver = functionResolver;
            m_Chapters = new Dictionary<string, Chapter>(StringComparer.Ordinal);
            m_Steps = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
            m_VariableStore = new VariableStore();
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
        public Program Program => m_Program;

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
        public Chapter CurrentChapter => m_CurrentChapter;

        /// <summary>
        /// 当前步骤。
        /// </summary>
        public Step CurrentStep
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
        public Frame CurrentFrame => m_CurrentFrame;

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
        public IVariableStore VariableStore => m_VariableStore;

        /// <summary>
        /// 剧情历史。
        /// </summary>
        public IReadOnlyList<HistoryEntry> History => m_History;

        /// <summary>
        /// 启动剧情。
        /// </summary>
        /// <param name="chapterId">章节 ID。</param>
        /// <returns>第一个帧。</returns>
        public Frame Start(string chapterId = null)
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
        public Frame Continue()
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
        public Frame Select(string choiceId)
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
        public Frame CompleteCommand(string commandId, string outcomeId)
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
        public Frame Evaluate(double time)
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

        private Frame ResolveFrameUntilStop()
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
                    case StepKind.Start:
                        AdvanceSequential();
                        continue;
                    case StepKind.Branch:
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
                    case StepKind.Jump:
                        if (step.Data.Target == null)
                        {
                            throw new GameException($"Story jump target is missing. story:{StoryId} chapter:{CurrentChapterId} step:{step.StepId}");
                        }

                        JumpTo(step.Data.Target);
                        continue;
                    case StepKind.Line:
                    case StepKind.Choice:
                    case StepKind.Command:
                    case StepKind.Wait:
                        return BuildFrame();
                    case StepKind.Parallel:
                        return BuildParallelFrame(step);
                    case StepKind.Merge:
                        AdvanceFromCurrentStep();
                        continue;
                    case StepKind.End:
                        CompleteStory();
                        return m_CurrentFrame;
                    default:
                        throw new GameException($"Story step kind is invalid. story:{StoryId} chapter:{CurrentChapterId} step:{step.StepId} kind:{step.Kind}");
                }
            }

            return m_CurrentFrame;
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

        private void JumpTo(Target target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            switch (target.TargetKind)
            {
                case TargetKind.Step:
                    if (string.IsNullOrWhiteSpace(target.ChapterId) || string.IsNullOrWhiteSpace(target.StepId))
                    {
                        throw new GameException($"Story step target is invalid. story:{StoryId}");
                    }

                    m_CurrentChapter = GetChapter(target.ChapterId);
                    EnterStep(target.StepId);
                    break;
                case TargetKind.Chapter:
                    if (string.IsNullOrWhiteSpace(target.ChapterId))
                    {
                        throw new GameException($"Story chapter target is invalid. story:{StoryId}");
                    }

                    m_CurrentChapter = GetChapter(target.ChapterId);
                    EnterStep(m_CurrentChapter.EntryStepId);
                    break;
                case TargetKind.StoryEnd:
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

        private Step GetStep(Chapter chapter, string stepId)
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

        private Step GetNextStep(Chapter chapter, Step step)
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

        private Step ResolveBranchStep(Chapter chapter, Step step)
        {
            if (EvaluateCondition(step.Data.Condition))
            {
                if (step.Data.Target == null)
                {
                    throw new GameException($"Story branch target is missing. story:{StoryId} chapter:{chapter.ChapterId} step:{step.StepId}");
                }

                if (step.Data.Target.TargetKind != TargetKind.Step ||
                    string.Equals(step.Data.Target.ChapterId, chapter.ChapterId, StringComparison.Ordinal) is false)
                {
                    throw new GameException($"Story parallel branch target must stay in the same chapter. story:{StoryId} chapter:{chapter.ChapterId} step:{step.StepId}");
                }

                return GetStep(chapter, step.Data.Target.StepId);
            }

            return GetNextStep(chapter, step);
        }

        private Step ResolveJumpStep(Chapter chapter, Step step)
        {
            if (step.Data.Target == null)
            {
                throw new GameException($"Story jump target is missing. story:{StoryId} chapter:{chapter.ChapterId} step:{step.StepId}");
            }

            if (step.Data.Target.TargetKind != TargetKind.Step ||
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
            m_CurrentFrame = Frame.CreateCompleted(m_Program, m_CurrentChapter, step);
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

        private Chapter GetChapter(string chapterId)
        {
            if (string.IsNullOrWhiteSpace(chapterId) || !m_Chapters.TryGetValue(chapterId, out var chapter))
            {
                throw new GameException($"Story chapter does not exist. story:{StoryId} chapter:{chapterId}");
            }

            return chapter;
        }

        private Choice FindChoice(string choiceId)
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

        private void BuildMaps(Program program)
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

        private static bool RequiresCommandCompletion(global::GameDeveloperKit.Story.Model.Command command)
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


        private static global::GameDeveloperKit.Story.Model.Command GetBlockingCommand(Frame frame)
        {
            if (frame?.Tracks == null)
            {
                return null;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track?.Kind == FrameTrackKind.Command && RequiresCommandCompletion(track.Command))
                {
                    return track.Command;
                }
            }

            return null;
        }

        private void ValidateCommandOutcome(global::GameDeveloperKit.Story.Model.Command command, string outcomeId)
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

        private static double GetWaitSeconds(Frame frame)
        {
            if (frame?.Tracks == null)
            {
                return 0d;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track?.Kind == FrameTrackKind.Wait)
                {
                    return track.WaitSeconds;
                }
            }

            return 0d;
        }

    }
}
