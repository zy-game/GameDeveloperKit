using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Execution
{
    /// <summary>
    /// 剧情快照运行状态。
    /// </summary>
    public enum SnapshotState
    {
        /// <summary>
        /// 未启动。
        /// </summary>
        Idle = 0,

        /// <summary>
        /// 等待继续。
        /// </summary>
        AwaitingContinue = 1,

        /// <summary>
        /// 等待选项。
        /// </summary>
        AwaitingChoice = 2,

        /// <summary>
        /// 等待命令完成。
        /// </summary>
        AwaitingCommand = 3,

        /// <summary>
        /// 等待时间推进。
        /// </summary>
        AwaitingTime = 4,

        /// <summary>
        /// 已完成。
        /// </summary>
        Completed = 5
    }

    /// <summary>
    /// 剧情并行分支快照。
    /// </summary>
    public sealed class ParallelBranchSnapshot
    {
        /// <summary>
        /// 初始化并行分支快照。
        /// </summary>
        /// <param name="branchId">分支 ID。</param>
        /// <param name="episodeId">当前剧情段 ID。</param>
        /// <param name="stepId">当前步骤 ID。</param>
        /// <param name="completed">是否已完成。</param>
        /// <param name="waitElapsed">等待已推进时间。</param>
        public ParallelBranchSnapshot(
            string branchId,
            string episodeId,
            string stepId,
            bool completed,
            double waitElapsed = 0d)
        {
            if (string.IsNullOrWhiteSpace(branchId))
            {
                throw new ArgumentException("Value cannot be empty.", nameof(branchId));
            }

            BranchId = branchId;
            EpisodeId = episodeId;
            StepId = stepId;
            Completed = completed;
            if (TimeRules.IsFiniteNonNegative(waitElapsed) is false)
            {
                throw new ArgumentOutOfRangeException(nameof(waitElapsed), "Wait elapsed must be finite and non-negative.");
            }

            WaitElapsed = waitElapsed;
        }

        /// <summary>
        /// 分支 ID。
        /// </summary>
        public string BranchId { get; }

        /// <summary>
        /// 当前剧情段 ID。
        /// </summary>
        public string EpisodeId { get; }

        /// <summary>
        /// 当前步骤 ID。
        /// </summary>
        public string StepId { get; }

        /// <summary>
        /// 是否已完成。
        /// </summary>
        public bool Completed { get; }

        /// <summary>
        /// 等待已推进时间。
        /// </summary>
        public double WaitElapsed { get; }
    }

    /// <summary>
    /// 剧情运行快照。
    /// </summary>
    public sealed class Snapshot
    {
        /// <summary>
        /// 初始化快照。
        /// </summary>
        /// <param name="storyId">剧情 ID。</param>
        /// <param name="version">版本。</param>
        /// <param name="volumeId">卷 ID。</param>
        /// <param name="episodeId">剧情段 ID。</param>
        /// <param name="stepId">步骤 ID。</param>
        /// <param name="currentTime">当前时间。</param>
        /// <param name="variables">变量值。</param>
        /// <param name="history">历史。</param>
        /// <param name="completed">是否完成。</param>
        /// <param name="state">运行状态。</param>
        /// <param name="waitElapsed">当前等待已推进时间。</param>
        /// <param name="parallelBranches">并行分支快照。</param>
        /// <param name="completedExitId">完成出口 ID。</param>
        public Snapshot(
            string storyId,
            string version,
            string volumeId,
            string episodeId,
            string stepId,
            double currentTime,
            IReadOnlyDictionary<string, Value> variables,
            IReadOnlyList<HistoryEntry> history,
            bool completed,
            SnapshotState state = SnapshotState.Idle,
            double waitElapsed = 0d,
            IReadOnlyList<ParallelBranchSnapshot> parallelBranches = null,
            string completedExitId = null)
        {
            if (TimeRules.IsFiniteNonNegative(currentTime) is false)
            {
                throw new ArgumentOutOfRangeException(nameof(currentTime), "Current time must be finite and non-negative.");
            }

            if (TimeRules.IsFiniteNonNegative(waitElapsed) is false)
            {
                throw new ArgumentOutOfRangeException(nameof(waitElapsed), "Wait elapsed must be finite and non-negative.");
            }

            StoryId = storyId;
            Version = version;
            VolumeId = volumeId;
            EpisodeId = episodeId;
            StepId = stepId;
            CurrentTime = currentTime;
            Variables = CopyVariables(variables);
            History = CopyHistory(history);
            Completed = completed;
            State = completed ? SnapshotState.Completed : state;
            WaitElapsed = waitElapsed;
            ParallelBranches = CopyParallelBranches(parallelBranches);
            CompletedExitId = completedExitId;
        }

        /// <summary>
        /// 剧情 ID。
        /// </summary>
        public string StoryId { get; }

        /// <summary>
        /// 版本。
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// 当前卷 ID。
        /// </summary>
        public string VolumeId { get; }

        /// <summary>
        /// 当前剧情段 ID。
        /// </summary>
        public string EpisodeId { get; }

        /// <summary>
        /// 当前步骤 ID。
        /// </summary>
        public string StepId { get; }

        /// <summary>
        /// 当前时间。
        /// </summary>
        public double CurrentTime { get; }

        /// <summary>
        /// 变量值。
        /// </summary>
        public IReadOnlyDictionary<string, Value> Variables { get; }

        /// <summary>
        /// 历史。
        /// </summary>
        public IReadOnlyList<HistoryEntry> History { get; }

        /// <summary>
        /// 是否完成。
        /// </summary>
        public bool Completed { get; }

        /// <summary>
        /// 运行状态。
        /// </summary>
        public SnapshotState State { get; }

        /// <summary>
        /// 当前等待已推进时间。
        /// </summary>
        public double WaitElapsed { get; }

        /// <summary>
        /// 并行分支快照。
        /// </summary>
        public IReadOnlyList<ParallelBranchSnapshot> ParallelBranches { get; }

        /// <summary>
        /// 完成出口 ID。
        /// </summary>
        public string CompletedExitId { get; }

        private static IReadOnlyDictionary<string, Value> CopyVariables(IReadOnlyDictionary<string, Value> variables)
        {
            if (variables == null || variables.Count == 0)
            {
                return new Dictionary<string, Value>(0, StringComparer.Ordinal);
            }

            var copy = new Dictionary<string, Value>(StringComparer.Ordinal);
            foreach (var pair in variables)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                copy[pair.Key] = pair.Value;
            }

            return copy;
        }

        private static IReadOnlyList<HistoryEntry> CopyHistory(IReadOnlyList<HistoryEntry> history)
        {
            if (history == null || history.Count == 0)
            {
                return Array.Empty<HistoryEntry>();
            }

            return new List<HistoryEntry>(history);
        }

        private static IReadOnlyList<ParallelBranchSnapshot> CopyParallelBranches(IReadOnlyList<ParallelBranchSnapshot> branches)
        {
            if (branches == null || branches.Count == 0)
            {
                return Array.Empty<ParallelBranchSnapshot>();
            }

            var copy = new List<ParallelBranchSnapshot>();
            for (var i = 0; i < branches.Count; i++)
            {
                if (branches[i] != null)
                {
                    copy.Add(branches[i]);
                }
            }

            return copy;
        }
    }
}
