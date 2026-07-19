using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Settlement
{
    public sealed class SettlementArgumentDefinition
    {
        public SettlementArgumentDefinition(
            string key,
            string label,
            ParameterValueType valueType = ParameterValueType.String,
            bool required = false,
            IReadOnlyList<string> options = null)
        {
            ValidateText(key, nameof(key));
            if (!Enum.IsDefined(typeof(ParameterValueType), valueType))
            {
                throw new ArgumentOutOfRangeException(nameof(valueType));
            }

            Key = key.Trim();
            Label = string.IsNullOrWhiteSpace(label) ? Key : label.Trim();
            ValueType = valueType;
            Required = required;
            Options = CopyStrings(options);
        }

        public string Key { get; }

        public string Label { get; }

        public ParameterValueType ValueType { get; }

        public bool Required { get; }

        public IReadOnlyList<string> Options { get; }

        private static IReadOnlyList<string> CopyStrings(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>(values.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < values.Count; i++)
            {
                ValidateText(values[i], nameof(values));
                if (!seen.Add(values[i]))
                {
                    throw new ArgumentException("Settlement argument options must be unique.", nameof(values));
                }

                result.Add(values[i]);
            }

            return result;
        }

        private static void ValidateText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }
    }

    public sealed class SettlementDefinition
    {
        public SettlementDefinition(
            string kind,
            string displayName,
            string group,
            IReadOnlyList<SettlementArgumentDefinition> arguments = null)
        {
            if (string.IsNullOrWhiteSpace(kind))
            {
                throw new ArgumentException("Settlement kind cannot be empty.", nameof(kind));
            }

            Kind = kind.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Kind : displayName.Trim();
            Group = string.IsNullOrWhiteSpace(group) ? string.Empty : group.Trim();
            Arguments = CopyArguments(arguments);
        }

        public string Kind { get; }

        public string DisplayName { get; }

        public string Group { get; }

        public IReadOnlyList<SettlementArgumentDefinition> Arguments { get; }

        private static IReadOnlyList<SettlementArgumentDefinition> CopyArguments(
            IReadOnlyList<SettlementArgumentDefinition> arguments)
        {
            if (arguments == null || arguments.Count == 0)
            {
                return Array.Empty<SettlementArgumentDefinition>();
            }

            var result = new List<SettlementArgumentDefinition>(arguments.Count);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i] ??
                               throw new ArgumentException($"Settlement argument definition cannot be null. index:{i}", nameof(arguments));
                if (!keys.Add(argument.Key))
                {
                    throw new ArgumentException($"Settlement argument key is duplicated. key:{argument.Key}", nameof(arguments));
                }

                result.Add(argument);
            }

            return result;
        }
    }

    public interface ISettlementDefinitionProvider
    {
        IReadOnlyList<SettlementDefinition> GetDefinitions();
    }

    public readonly struct SettlementOperation
    {
        public SettlementOperation(string operationId, string kind, ArgumentBag arguments = null)
        {
            ValidateText(operationId, nameof(operationId));
            ValidateText(kind, nameof(kind));
            OperationId = operationId.Trim();
            Kind = kind.Trim();
            Arguments = arguments ?? new ArgumentBag();
        }

        public string OperationId { get; }

        public string Kind { get; }

        public ArgumentBag Arguments { get; }

        private static void ValidateText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }
    }

    public sealed class SettlementPlan
    {
        public const int CurrentVersion = 1;

        public SettlementPlan(
            string settlementId,
            int version,
            IReadOnlyList<SettlementOperation> operations)
        {
            if (string.IsNullOrWhiteSpace(settlementId))
            {
                throw new ArgumentException("Settlement ID cannot be empty.", nameof(settlementId));
            }

            if (version <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(version));
            }

            if (operations == null || operations.Count == 0)
            {
                throw new ArgumentException("Settlement plan requires operations.", nameof(operations));
            }

            var copy = new SettlementOperation[operations.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < copy.Length; i++)
            {
                var operation = operations[i];
                if (string.IsNullOrWhiteSpace(operation.OperationId) ||
                    string.IsNullOrWhiteSpace(operation.Kind) ||
                    operation.Arguments == null)
                {
                    throw new ArgumentException($"Settlement operation is invalid. index:{i}", nameof(operations));
                }

                if (!ids.Add(operation.OperationId))
                {
                    throw new ArgumentException($"Settlement operation ID is duplicated. operation:{operation.OperationId}", nameof(operations));
                }

                copy[i] = operation;
            }

            SettlementId = settlementId.Trim();
            Version = version;
            Operations = copy;
        }

        public string SettlementId { get; }

        public int Version { get; }

        public IReadOnlyList<SettlementOperation> Operations { get; }
    }

    public sealed class SettlementContext
    {
        public SettlementContext(
            string storyId,
            string volumeId,
            string episodeId,
            string settlementId,
            int planVersion)
        {
            ValidateText(storyId, nameof(storyId));
            ValidateText(volumeId, nameof(volumeId));
            ValidateText(episodeId, nameof(episodeId));
            ValidateText(settlementId, nameof(settlementId));
            if (planVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(planVersion));
            }

            StoryId = storyId.Trim();
            VolumeId = volumeId.Trim();
            EpisodeId = episodeId.Trim();
            SettlementId = settlementId.Trim();
            PlanVersion = planVersion;
        }

        public string StoryId { get; }

        public string VolumeId { get; }

        public string EpisodeId { get; }

        public string SettlementId { get; }

        public int PlanVersion { get; }

        public string IdempotencyKey => $"{StoryId}:{EpisodeId}:{SettlementId}:v{PlanVersion}";

        private static void ValidateText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }
    }

    public enum SettlementStatus
    {
        Applied = 0,
        AlreadyApplied = 1,
        Failed = 2
    }

    public readonly struct SettlementResult
    {
        public SettlementResult(SettlementStatus status, string errorCode = null, string errorMessage = null)
        {
            Status = status;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public SettlementStatus Status { get; }

        public string ErrorCode { get; }

        public string ErrorMessage { get; }
    }

    public interface ISettlementExecutor
    {
        UniTask<SettlementResult> ExecuteAsync(
            SettlementPlan plan,
            SettlementContext context,
            CancellationToken cancellationToken);
    }
}
