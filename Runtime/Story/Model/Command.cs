using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

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
        /// 结果端口对应的跳转目标。
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

namespace GameDeveloperKit.Story.Settlement
{
    public enum SettlementOperationKind { GrantItem = 0, SetValue = 1, UnlockChapter = 2, UnlockBranch = 3, UnlockHiddenStory = 4 }

    public sealed class SettlementOperation
    {
        public SettlementOperation(SettlementOperationKind kind, string id, int amount = 0, string key = null, GameDeveloperKit.Story.Model.Value value = default)
        {
            if (Enum.IsDefined(typeof(SettlementOperationKind), kind) is false) throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind == SettlementOperationKind.SetValue)
            {
                if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Settlement value key cannot be empty.", nameof(key));
                if (value.Kind == GameDeveloperKit.Story.Model.ValueKind.Null) throw new ArgumentException("Settlement value cannot be null.", nameof(value));
            }
            else if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Settlement operation ID cannot be empty.", nameof(id));
            if (kind == SettlementOperationKind.GrantItem && amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Kind = kind; Id = id?.Trim(); Amount = amount; Key = key?.Trim(); Value = value;
        }
        public SettlementOperationKind Kind { get; }
        public string Id { get; }
        public int Amount { get; }
        public string Key { get; }
        public GameDeveloperKit.Story.Model.Value Value { get; }
    }

    public sealed class SettlementPlan
    {
        public const int CurrentVersion = 1;
        public SettlementPlan(IReadOnlyList<SettlementOperation> operations)
        {
            if (operations == null || operations.Count == 0) throw new ArgumentException("Settlement plan requires operations.", nameof(operations));
            var copy = new SettlementOperation[operations.Count];
            for (var i = 0; i < copy.Length; i++) copy[i] = operations[i] ?? throw new ArgumentException($"Settlement operation is null. index:{i}", nameof(operations));
            Operations = copy;
        }
        public IReadOnlyList<SettlementOperation> Operations { get; }
    }

    public readonly struct SettlementContext
    {
        public SettlementContext(string storyId, string chapterId, string settlementId)
        {
            if (string.IsNullOrWhiteSpace(storyId)) throw new ArgumentException("Story ID cannot be empty.", nameof(storyId));
            if (string.IsNullOrWhiteSpace(chapterId)) throw new ArgumentException("Chapter ID cannot be empty.", nameof(chapterId));
            if (string.IsNullOrWhiteSpace(settlementId)) throw new ArgumentException("Settlement ID cannot be empty.", nameof(settlementId));
            StoryId = storyId; ChapterId = chapterId; SettlementId = settlementId;
        }
        public string StoryId { get; }
        public string ChapterId { get; }
        public string SettlementId { get; }
        public string IdempotencyKey => $"{StoryId}:{ChapterId}:{SettlementId}";
    }

    public enum SettlementStatus { Applied = 0, AlreadyApplied = 1, Failed = 2 }
    public readonly struct SettlementResult
    {
        public SettlementResult(SettlementStatus status, string errorCode = null, string errorMessage = null) { Status = status; ErrorCode = errorCode; ErrorMessage = errorMessage; }
        public SettlementStatus Status { get; }
        public string ErrorCode { get; }
        public string ErrorMessage { get; }
    }
    public interface ISettlementExecutor { UniTask<SettlementResult> ExecuteAsync(SettlementPlan plan, SettlementContext context, CancellationToken cancellationToken); }

    public static class SettlementPlanCodec
    {
        public static string Serialize(SettlementPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var data = new PlanData { Version = SettlementPlan.CurrentVersion };
            for (var i = 0; i < plan.Operations.Count; i++) data.Operations.Add(ToData(plan.Operations[i]));
            return JsonConvert.SerializeObject(data);
        }
        public static bool TryDeserialize(string json, out SettlementPlan plan, out string error)
        {
            plan = null; error = null;
            try
            {
                var data = JsonConvert.DeserializeObject<PlanData>(json);
                if (data == null || data.Version != SettlementPlan.CurrentVersion || data.Operations == null) { error = "Settlement plan version is invalid or unsupported."; return false; }
                var operations = new List<SettlementOperation>();
                for (var i = 0; i < data.Operations.Count; i++) operations.Add(FromData(data.Operations[i], i));
                plan = new SettlementPlan(operations); return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException) { error = exception.Message; return false; }
        }
        private static OperationData ToData(SettlementOperation operation) { return new OperationData { Kind = ToText(operation.Kind), Id = operation.Id, Amount = operation.Amount, Key = operation.Key, ValueKind = operation.Kind == SettlementOperationKind.SetValue ? operation.Value.Kind.ToString().ToLowerInvariant() : null, Value = operation.Kind == SettlementOperationKind.SetValue ? operation.Value.ToString() : null }; }
        private static SettlementOperation FromData(OperationData data, int index) { if (data == null || TryParseKind(data.Kind, out var kind) is false) throw new ArgumentException($"Settlement operation kind is invalid. index:{index}"); return new SettlementOperation(kind, data.Id, data.Amount, data.Key, kind == SettlementOperationKind.SetValue ? ParseValue(data.ValueKind, data.Value, index) : default); }
        private static GameDeveloperKit.Story.Model.Value ParseValue(string kind, string value, int index) { switch (kind) { case "boolean": if (bool.TryParse(value, out var boolean)) return GameDeveloperKit.Story.Model.Value.FromBoolean(boolean); break; case "number": if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) && double.IsNaN(number) is false && double.IsInfinity(number) is false) return GameDeveloperKit.Story.Model.Value.FromNumber(number); break; case "string": return GameDeveloperKit.Story.Model.Value.FromString(value ?? string.Empty); } throw new ArgumentException($"Settlement set_value is invalid. index:{index}"); }
        private static string ToText(SettlementOperationKind kind) { switch (kind) { case SettlementOperationKind.GrantItem: return "grant_item"; case SettlementOperationKind.SetValue: return "set_value"; case SettlementOperationKind.UnlockChapter: return "unlock_chapter"; case SettlementOperationKind.UnlockBranch: return "unlock_branch"; default: return "unlock_hidden_story"; } }
        private static bool TryParseKind(string value, out SettlementOperationKind kind) { switch (value) { case "grant_item": kind = SettlementOperationKind.GrantItem; return true; case "set_value": kind = SettlementOperationKind.SetValue; return true; case "unlock_chapter": kind = SettlementOperationKind.UnlockChapter; return true; case "unlock_branch": kind = SettlementOperationKind.UnlockBranch; return true; case "unlock_hidden_story": kind = SettlementOperationKind.UnlockHiddenStory; return true; default: kind = default; return false; } }
        [Serializable] private sealed class PlanData { [JsonProperty("version")] public int Version { get; set; } [JsonProperty("operations")] public List<OperationData> Operations { get; set; } = new List<OperationData>(); }
        [Serializable] private sealed class OperationData { [JsonProperty("kind")] public string Kind { get; set; } [JsonProperty("id")] public string Id { get; set; } [JsonProperty("amount")] public int Amount { get; set; } [JsonProperty("key")] public string Key { get; set; } [JsonProperty("valueKind")] public string ValueKind { get; set; } [JsonProperty("value")] public string Value { get; set; } }
    }
}
