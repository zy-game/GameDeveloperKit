using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story.Model
{
    /// <summary>
    /// 剧情命令。
    /// </summary>
    public sealed class Command
    {
        /// <summary>
        /// 初始化剧情命令。
        /// </summary>
        /// <param name="commandId">命令 ID。</param>
        /// <param name="name">命令名。</param>
        /// <param name="arguments">命令参数。</param>
        /// <param name="waitForCompletion">是否等待完成。</param>
        /// <param name="outcomePorts">结果端口。</param>
        /// <param name="outcomeTargets">结果端口对应的跳转目标。</param>
        public Command(
            string commandId,
            string name,
            ArgumentBag arguments = null,
            bool waitForCompletion = false,
            IReadOnlyList<string> outcomePorts = null,
            IReadOnlyDictionary<string, Target> outcomeTargets = null)
        {
            ValidateText(commandId, nameof(commandId));
            ValidateText(name, nameof(name));

            CommandId = commandId;
            Name = name;
            Arguments = arguments ?? new ArgumentBag();
            WaitForCompletion = waitForCompletion;
            OutcomePorts = CopyList(outcomePorts);
            OutcomeTargets = CopyTargets(outcomeTargets);
        }

        /// <summary>
        /// 命令 ID。
        /// </summary>
        public string CommandId { get; }

        /// <summary>
        /// 命令名。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 命令参数。
        /// </summary>
        public ArgumentBag Arguments { get; }

        /// <summary>
        /// 是否等待完成。
        /// </summary>
        public bool WaitForCompletion { get; }

        /// <summary>
        /// 结果端口。
        /// </summary>
        public IReadOnlyList<string> OutcomePorts { get; }

        /// <summary>
        /// 结果端口对应的目标。
        /// </summary>
        public IReadOnlyDictionary<string, Target> OutcomeTargets { get; }

        /// <summary>
        /// 获取指定结果端口对应的目标。
        /// </summary>
        /// <param name="outcomeId">结果端口 ID。</param>
        /// <returns>目标，缺失时返回 null。</returns>
        public Target GetOutcomeTarget(string outcomeId)
        {
            if (string.IsNullOrWhiteSpace(outcomeId))
            {
                return null;
            }

            return OutcomeTargets.TryGetValue(outcomeId, out var target) ? target : null;
        }

        private static IReadOnlyList<T> CopyList<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<T>();
            }

            return new List<T>(items);
        }

        private static IReadOnlyDictionary<string, Target> CopyTargets(IReadOnlyDictionary<string, Target> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                return new Dictionary<string, Target>(0, StringComparer.Ordinal);
            }

            var copy = new Dictionary<string, Target>(StringComparer.Ordinal);
            foreach (var pair in targets)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                copy[pair.Key] = pair.Value;
            }

            return copy;
        }

        private static void ValidateText(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }
    }
}
