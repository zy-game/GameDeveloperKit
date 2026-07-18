using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Execution
{
    public sealed partial class Runner
    {
        /// <summary>
        /// 创建快照。
        /// </summary>
        /// <returns>快照。</returns>
        public Snapshot CreateSnapshot()
        {
            return new Snapshot(
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
        public Frame Restore(Snapshot snapshot)
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
                m_CurrentFrame = Frame.CreateCompleted(m_Program, m_CurrentChapter, CurrentStep);
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

        private Frame RestoreParallelFrame(Snapshot snapshot)
        {
            var parallelStep = CurrentStep;
            if (parallelStep == null || parallelStep.Kind != StepKind.Parallel)
            {
                throw new GameException($"Story snapshot parallel anchor is invalid. story:{StoryId} chapter:{CurrentChapterId} step:{snapshot.StepId}");
            }

            var branches = new List<BranchCursor>();
            for (var i = 0; i < parallelStep.Data.Branches.Count; i++)
            {
                var branch = parallelStep.Data.Branches[i];
                var branchSnapshot = FindBranchSnapshot(snapshot.ParallelBranches, branch.BranchId);
                if (branchSnapshot == null)
                {
                    throw new GameException($"Story snapshot parallel branch is missing. story:{StoryId} chapter:{CurrentChapterId} step:{parallelStep.StepId} branch:{branch.BranchId}");
                }

                var chapter = GetChapter(string.IsNullOrWhiteSpace(branchSnapshot.ChapterId) ? branch.Entry.ChapterId : branchSnapshot.ChapterId);
                Step step = null;
                if (string.IsNullOrWhiteSpace(branchSnapshot.StepId) is false)
                {
                    step = GetStep(chapter, branchSnapshot.StepId);
                }

                if (branchSnapshot.Completed)
                {
                    branches.Add(new BranchCursor(branch, chapter, step, null, true, branchSnapshot.WaitElapsed));
                    continue;
                }

                if (step == null)
                {
                    throw new GameException($"Story snapshot parallel branch step is missing. story:{StoryId} chapter:{chapter.ChapterId} branch:{branch.BranchId}");
                }

                branches.Add(BuildBranchCursor(branch, chapter, step, branchSnapshot.WaitElapsed));
            }

            m_CurrentParallelFrame = new ParallelFrame(parallelStep, branches);
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

        private bool EvaluateCondition(Expression expression)
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

        private Value EvaluateExpression(Expression expression)
        {
            if (expression == null)
            {
                return Value.Null;
            }

            switch (expression.Kind)
            {
                case ExpressionKind.Literal:
                    return expression.Literal;
                case ExpressionKind.Variable:
                    return m_VariableStore.TryGet(expression.VariableName, out var value) ? value : Value.Null;
                case ExpressionKind.Function:
                    if (m_FunctionResolver == null)
                    {
                        throw new GameException($"Story function resolver is missing. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId} function:{expression.FunctionName}");
                    }

                    return m_FunctionResolver.Evaluate(
                        expression.FunctionName,
                        EvaluateArguments(expression.Inputs),
                        CreateContext());
                case ExpressionKind.Not:
                    return Value.FromBoolean(!EvaluateCondition(expression.Inputs[0]));
                case ExpressionKind.And:
                    for (var i = 0; i < expression.Inputs.Count; i++)
                    {
                        if (!EvaluateCondition(expression.Inputs[i]))
                        {
                            return Value.FromBoolean(false);
                        }
                    }

                    return Value.FromBoolean(true);
                case ExpressionKind.Or:
                    for (var i = 0; i < expression.Inputs.Count; i++)
                    {
                        if (EvaluateCondition(expression.Inputs[i]))
                        {
                            return Value.FromBoolean(true);
                        }
                    }

                    return Value.FromBoolean(false);
                case ExpressionKind.Equal:
                    return Value.FromBoolean(Compare(expression.Inputs[0], expression.Inputs[1]) == 0);
                case ExpressionKind.NotEqual:
                    return Value.FromBoolean(Compare(expression.Inputs[0], expression.Inputs[1]) != 0);
                case ExpressionKind.Greater:
                    return Value.FromBoolean(Compare(expression.Inputs[0], expression.Inputs[1]) > 0);
                case ExpressionKind.GreaterOrEqual:
                    return Value.FromBoolean(Compare(expression.Inputs[0], expression.Inputs[1]) >= 0);
                case ExpressionKind.Less:
                    return Value.FromBoolean(Compare(expression.Inputs[0], expression.Inputs[1]) < 0);
                case ExpressionKind.LessOrEqual:
                    return Value.FromBoolean(Compare(expression.Inputs[0], expression.Inputs[1]) <= 0);
                default:
                    throw new GameException($"Story expression kind is invalid. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId} kind:{expression.Kind}");
            }
        }

        private IReadOnlyList<Value> EvaluateArguments(IReadOnlyList<Expression> inputs)
        {
            if (inputs == null || inputs.Count == 0)
            {
                return Array.Empty<Value>();
            }

            var values = new Value[inputs.Count];
            for (var i = 0; i < inputs.Count; i++)
            {
                values[i] = EvaluateExpression(inputs[i]);
            }

            return values;
        }

        private int Compare(Expression left, Expression right)
        {
            var leftValue = EvaluateExpression(left);
            var rightValue = EvaluateExpression(right);

            if (leftValue.Kind != rightValue.Kind)
            {
                throw new GameException($"Story expression comparison kinds do not match. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId}");
            }

            switch (leftValue.Kind)
            {
                case ValueKind.Boolean:
                    return leftValue.BooleanValue.CompareTo(rightValue.BooleanValue);
                case ValueKind.Number:
                    return leftValue.NumberValue.CompareTo(rightValue.NumberValue);
                case ValueKind.String:
                    return string.Compare(leftValue.StringValue, rightValue.StringValue, StringComparison.Ordinal);
                case ValueKind.Null:
                    return 0;
                default:
                    throw new GameException($"Story value kind is invalid. story:{StoryId} chapter:{CurrentChapterId} step:{CurrentStepId} kind:{leftValue.Kind}");
            }
        }

        private RuntimeContext CreateContext()
        {
            return new RuntimeContext(
                m_Program,
                m_CurrentChapter,
                CurrentStep,
                m_CurrentTime,
                m_VariableStore,
                m_History);
        }

        private IReadOnlyDictionary<string, Value> CaptureVariables()
        {
            var copy = new Dictionary<string, Value>(StringComparer.Ordinal);
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

        private IReadOnlyList<ParallelBranchSnapshot> CaptureParallelBranches()
        {
            if (m_CurrentParallelFrame == null || m_CurrentParallelFrame.Branches.Count == 0)
            {
                return Array.Empty<ParallelBranchSnapshot>();
            }

            var snapshots = new List<ParallelBranchSnapshot>();
            for (var i = 0; i < m_CurrentParallelFrame.Branches.Count; i++)
            {
                var branch = m_CurrentParallelFrame.Branches[i];
                if (branch == null)
                {
                    continue;
                }

                snapshots.Add(new ParallelBranchSnapshot(
                    branch.BranchId,
                    branch.Chapter?.ChapterId,
                    branch.Step?.StepId,
                    branch.Completed,
                    branch.WaitElapsed));
            }

            return snapshots;
        }

        private static ParallelBranchSnapshot FindBranchSnapshot(
            IReadOnlyList<ParallelBranchSnapshot> snapshots,
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

        private void ApplyVariables(IReadOnlyDictionary<string, Value> variables)
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

        private static SnapshotState ToSnapshotState(RunnerState state)
        {
            switch (state)
            {
                case RunnerState.AwaitingContinue:
                    return SnapshotState.AwaitingContinue;
                case RunnerState.AwaitingChoice:
                    return SnapshotState.AwaitingChoice;
                case RunnerState.AwaitingCommand:
                    return SnapshotState.AwaitingCommand;
                case RunnerState.AwaitingTime:
                    return SnapshotState.AwaitingTime;
                case RunnerState.Completed:
                    return SnapshotState.Completed;
                default:
                    return SnapshotState.Idle;
            }
        }

    }
}
