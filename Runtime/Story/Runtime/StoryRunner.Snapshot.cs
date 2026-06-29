using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story
{
    public sealed partial class StoryRunner
    {
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

    }
}
