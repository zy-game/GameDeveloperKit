using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Event
{
    public enum EventMode
    {
        Notify = 0,
        Request = 1
    }

    public sealed class EventArgumentDefinition
    {
        public EventArgumentDefinition(
            string key,
            string label,
            ParameterValueType valueType = ParameterValueType.String,
            bool required = false,
            IReadOnlyList<string> options = null,
            string fieldRendererKey = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Event argument key cannot be empty.", nameof(key));
            }

            if (!Enum.IsDefined(typeof(ParameterValueType), valueType))
            {
                throw new ArgumentOutOfRangeException(nameof(valueType));
            }

            Key = key.Trim();
            Label = string.IsNullOrWhiteSpace(label) ? Key : label.Trim();
            ValueType = valueType;
            Required = required;
            Options = CopyStrings(options, nameof(options));
            FieldRendererKey = string.IsNullOrWhiteSpace(fieldRendererKey) ? null : fieldRendererKey.Trim();
        }

        public string Key { get; }

        public string Label { get; }

        public ParameterValueType ValueType { get; }

        public bool Required { get; }

        public IReadOnlyList<string> Options { get; }

        public string FieldRendererKey { get; }

        private static IReadOnlyList<string> CopyStrings(IReadOnlyList<string> values, string parameterName)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new List<string>(values.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                {
                    throw new ArgumentException("Event argument options must be non-empty and unique.", parameterName);
                }

                copy.Add(value);
            }

            return copy;
        }
    }

    public sealed class EventDefinition
    {
        public EventDefinition(
            string eventId,
            string displayName,
            string group,
            EventMode defaultMode,
            IReadOnlyList<EventArgumentDefinition> arguments = null,
            IReadOnlyList<string> outcomes = null)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                throw new ArgumentException("Event ID cannot be empty.", nameof(eventId));
            }

            if (!Enum.IsDefined(typeof(EventMode), defaultMode))
            {
                throw new ArgumentOutOfRangeException(nameof(defaultMode));
            }

            EventId = eventId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? EventId : displayName.Trim();
            Group = string.IsNullOrWhiteSpace(group) ? string.Empty : group.Trim();
            DefaultMode = defaultMode;
            Arguments = CopyArguments(arguments);
            Outcomes = CopyOutcomes(outcomes);
        }

        public string EventId { get; }

        public string DisplayName { get; }

        public string Group { get; }

        public EventMode DefaultMode { get; }

        public IReadOnlyList<EventArgumentDefinition> Arguments { get; }

        public IReadOnlyList<string> Outcomes { get; }

        private static IReadOnlyList<EventArgumentDefinition> CopyArguments(
            IReadOnlyList<EventArgumentDefinition> arguments)
        {
            if (arguments == null || arguments.Count == 0)
            {
                return Array.Empty<EventArgumentDefinition>();
            }

            var copy = new List<EventArgumentDefinition>(arguments.Count);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i] ??
                               throw new ArgumentException($"Event argument cannot be null. index:{i}", nameof(arguments));
                if (string.Equals(argument.Key, EventCommandCodec.ModeArgument, StringComparison.Ordinal) ||
                    !keys.Add(argument.Key))
                {
                    throw new ArgumentException($"Event argument key is reserved or duplicated. key:{argument.Key}", nameof(arguments));
                }

                copy.Add(argument);
            }

            return copy;
        }

        private static IReadOnlyList<string> CopyOutcomes(IReadOnlyList<string> outcomes)
        {
            if (outcomes == null || outcomes.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new List<string>(outcomes.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < outcomes.Count; i++)
            {
                var outcome = outcomes[i];
                if (string.IsNullOrWhiteSpace(outcome) || !seen.Add(outcome))
                {
                    throw new ArgumentException("Event outcomes must be non-empty and unique.", nameof(outcomes));
                }

                copy.Add(outcome);
            }

            return copy;
        }
    }

    public sealed class EventRequest
    {
        public EventRequest(
            string requestId,
            string eventId,
            ArgumentBag arguments,
            EventMode mode,
            IReadOnlyList<string> outcomes = null)
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new ArgumentException("Event request ID cannot be empty.", nameof(requestId));
            }

            if (string.IsNullOrWhiteSpace(eventId))
            {
                throw new ArgumentException("Event ID cannot be empty.", nameof(eventId));
            }

            if (!Enum.IsDefined(typeof(EventMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            RequestId = requestId.Trim();
            EventId = eventId.Trim();
            Arguments = arguments ?? new ArgumentBag();
            Mode = mode;
            Outcomes = CopyOutcomes(outcomes, mode);
        }

        public string RequestId { get; }

        public string EventId { get; }

        public ArgumentBag Arguments { get; }

        public EventMode Mode { get; }

        public IReadOnlyList<string> Outcomes { get; }

        private static IReadOnlyList<string> CopyOutcomes(IReadOnlyList<string> outcomes, EventMode mode)
        {
            var count = outcomes?.Count ?? 0;
            if (mode == EventMode.Notify)
            {
                if (count != 0)
                {
                    throw new ArgumentException("Notify events cannot declare outcomes.", nameof(outcomes));
                }

                return Array.Empty<string>();
            }

            if (count == 0)
            {
                throw new ArgumentException("Request events require at least one outcome.", nameof(outcomes));
            }

            var copy = new List<string>(count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < count; i++)
            {
                var outcome = outcomes[i];
                if (string.IsNullOrWhiteSpace(outcome) || !seen.Add(outcome))
                {
                    throw new ArgumentException("Event outcomes must be non-empty and unique.", nameof(outcomes));
                }

                copy.Add(outcome);
            }

            return copy;
        }
    }

    public readonly struct EventResult
    {
        public EventResult(string outcomeId, ArgumentBag payload = null)
        {
            OutcomeId = outcomeId;
            Payload = payload ?? new ArgumentBag();
        }

        public string OutcomeId { get; }

        public ArgumentBag Payload { get; }
    }

    public interface IEventHandler
    {
        UniTask<EventResult> HandleAsync(EventRequest request, CancellationToken cancellationToken);
    }

    public interface IEventDefinitionProvider
    {
        IReadOnlyList<EventDefinition> GetDefinitions();
    }
}
