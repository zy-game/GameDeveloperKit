using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Event;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Settlement;
using GameDeveloperKit.StoryEditor.Event;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Settlement;

namespace GameDeveloperKit.StoryEditor.Migration
{
    internal static class LegacyNodeKinds
    {
        public const int JumpEpisode = 2;
        public const int MiniGame = 204;
        public const int Qte = 205;
        public const int Unlock = 206;
        public const int SettleEpisode = 207;
        public const int TargetEpisode = 1;

        public static bool IsSpecializedEvent(NodeKind kind)
        {
            var value = (int)kind;
            return value == MiniGame || value == Qte || value == Unlock;
        }
    }

    internal static class LegacyEventCodec
    {
        public static void Convert(
            string storyId,
            string volumeId,
            AuthoringEpisode episode,
            AuthoringNode node,
            EventDefinitionCatalog catalog,
            MigrationReport report)
        {
            var eventId = EventId(node.NodeKind);
            var location = Location(storyId, volumeId, episode.EpisodeId, node.NodeId);
            var issueCount = report.Issues.Count;
            if (!catalog.TryGet(eventId, out var definition))
            {
                report.AddConflict(
                    "missing_event_definition",
                    location,
                    $"Legacy interaction requires a registered Event definition. event:{eventId}");
                return;
            }

            var values = Parameters(node.Parameters);
            var declared = definition.Arguments.ToDictionary(x => x.Key, StringComparer.Ordinal);
            foreach (var pair in values)
            {
                if (string.Equals(pair.Key, "wait", StringComparison.Ordinal) ||
                    string.Equals(pair.Key, "tags", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!declared.ContainsKey(pair.Key))
                {
                    report.AddConflict(
                        "unmapped_event_field",
                        location + $"/field:{pair.Key}",
                        $"Legacy interaction field is not declared by Event definition. event:{eventId}");
                }
            }

            for (var i = 0; i < definition.Arguments.Count; i++)
            {
                var argument = definition.Arguments[i];
                values.TryGetValue(argument.Key, out var value);
                if (argument.Required && string.IsNullOrWhiteSpace(value))
                {
                    report.AddConflict(
                        "missing_event_field",
                        location + $"/field:{argument.Key}",
                        $"Legacy interaction field is required by Event definition. event:{eventId}");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(value) && !IsValid(argument, value))
                {
                    report.AddConflict(
                        "invalid_event_field",
                        location + $"/field:{argument.Key}",
                        $"Legacy interaction field cannot be converted to the declared type. event:{eventId}");
                }
            }

            var outgoing = episode.Edges.Where(x => x != null && string.Equals(x.FromNodeId, node.NodeId, StringComparison.Ordinal));
            foreach (var edge in outgoing)
            {
                if (!definition.Outcomes.Contains(edge.FromPortId, StringComparer.Ordinal))
                {
                    report.AddConflict(
                        "unmapped_event_outcome",
                        location + $"/edge:{edge.EdgeId}",
                        $"Legacy interaction outcome is not declared by Event definition. event:{eventId} outcome:{edge.FromPortId}");
                }
            }

            if (report.Issues.Count != issueCount)
            {
                return;
            }

            Set(node.Parameters, EventCommandCodec.EventIdParameter, eventId);
            Set(node.Parameters, EventCommandCodec.ModeParameter, EventCommandCodec.SerializeMode(definition.DefaultMode));
            Remove(node.Parameters, "wait");
            node.NodeKind = NodeKind.Event;
            report.AddChange(MigrationChangeKind.Converted, location, $"legacy interaction -> Event({eventId})");
        }

        private static string EventId(NodeKind kind)
        {
            switch ((int)kind)
            {
                case LegacyNodeKinds.MiniGame:
                    return "gameplay.minigame";
                case LegacyNodeKinds.Qte:
                    return "gameplay.qte";
                case LegacyNodeKinds.Unlock:
                    return "gameplay.unlock";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static bool IsValid(EventArgumentDefinition definition, string value)
        {
            switch (definition.ValueType)
            {
                case ParameterValueType.Boolean:
                    return bool.TryParse(value, out _);
                case ParameterValueType.Number:
                    return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
                           !double.IsNaN(number) && !double.IsInfinity(number);
                case ParameterValueType.Option:
                    return definition.Options.Contains(value, StringComparer.Ordinal);
                default:
                    return true;
            }
        }

        private static Dictionary<string, string> Parameters(IReadOnlyList<AuthoringParameter> parameters)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < (parameters?.Count ?? 0); i++)
            {
                var parameter = parameters[i];
                if (parameter != null && !string.IsNullOrWhiteSpace(parameter.Key))
                {
                    result[parameter.Key] = parameter.Value;
                }
            }

            return result;
        }

        private static void Set(List<AuthoringParameter> parameters, string key, string value)
        {
            for (var i = 0; i < parameters.Count; i++)
            {
                if (parameters[i] != null && string.Equals(parameters[i].Key, key, StringComparison.Ordinal))
                {
                    parameters[i].Value = value;
                    return;
                }
            }

            parameters.Add(new AuthoringParameter { Key = key, Value = value });
        }

        private static void Remove(List<AuthoringParameter> parameters, string key)
        {
            parameters.RemoveAll(x => x != null && string.Equals(x.Key, key, StringComparison.Ordinal));
        }

        private static string Location(string storyId, string volumeId, string episodeId, string nodeId)
        {
            return $"story:{storyId}/volume:{volumeId}/episode:{episodeId}/node:{nodeId}";
        }
    }

    internal static class LegacySettlementCodec
    {
        public static void Validate(
            string storyId,
            string volumeId,
            AuthoringEpisode episode,
            AuthoringNode node,
            SettlementDefinitionCatalog catalog,
            MigrationReport report)
        {
            var location = $"story:{storyId}/volume:{volumeId}/episode:{episode.EpisodeId}/node:{node.NodeId}";
            var json = Value(node.Parameters, SettlementCommandNames.PlanArgument);
            if (!SettlementPlanCodec.TryDeserialize(json, out var plan, out var error))
            {
                report.AddConflict("invalid_settlement_plan", location + $"/field:{SettlementCommandNames.PlanArgument}", error);
                return;
            }

            if (!catalog.TryValidate(plan, out error))
            {
                report.AddConflict("missing_settlement_definition", location, error);
                return;
            }

            node.NodeKind = NodeKind.SettleEpisode;
            report.AddChange(MigrationChangeKind.Renamed, location, "Chapter Settlement -> Episode Settlement");
        }

        private static string Value(IReadOnlyList<AuthoringParameter> parameters, string key)
        {
            for (var i = 0; i < (parameters?.Count ?? 0); i++)
            {
                if (parameters[i] != null && string.Equals(parameters[i].Key, key, StringComparison.Ordinal))
                {
                    return parameters[i].Value;
                }
            }

            return null;
        }
    }
}
