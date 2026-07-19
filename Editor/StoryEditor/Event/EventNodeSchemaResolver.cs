using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Event;
using GameDeveloperKit.StoryEditor.Model;

namespace GameDeveloperKit.StoryEditor.Event
{
    internal static class EventNodeSchemaResolver
    {
        public static NodeSchema Resolve(AuthoringNode node)
        {
            return Resolve(node, EventDefinitionCatalog.Shared);
        }

        internal static NodeSchema Resolve(AuthoringNode node, EventDefinitionCatalog catalog)
        {
            if (node == null || node.NodeKind != NodeKind.Event)
            {
                return node == null ? null : NodeSchemaRegistry.Get(node.NodeKind);
            }

            catalog ??= EventDefinitionCatalog.Shared;
            var eventId = GetParameter(node, EventCommandCodec.EventIdParameter);
            catalog.TryGet(eventId, out var definition);
            var modeText = GetParameter(node, EventCommandCodec.ModeParameter);
            var mode = EventCommandCodec.TryParseMode(modeText, out var selectedMode)
                ? selectedMode
                : definition?.DefaultMode ?? EventMode.Notify;
            var parameters = BuildParameters(catalog, definition);
            var ports = BuildPorts(mode, definition);
            return new NodeSchema(
                NodeKind.Event,
                NodeCategory.Action,
                definition?.DisplayName ?? "事件",
                true,
                ports,
                parameters);
        }

        private static IReadOnlyList<NodeParameterDefinition> BuildParameters(
            EventDefinitionCatalog catalog,
            EventDefinition definition)
        {
            var eventIds = new List<string>(catalog.Definitions.Count);
            for (var i = 0; i < catalog.Definitions.Count; i++)
            {
                eventIds.Add(catalog.Definitions[i].EventId);
            }

            var parameters = new List<NodeParameterDefinition>
            {
                new NodeParameterDefinition(
                    EventCommandCodec.EventIdParameter,
                    "事件",
                    ParameterValueType.Option,
                    true,
                    options: eventIds),
                new NodeParameterDefinition(
                    EventCommandCodec.ModeParameter,
                    "模式",
                    ParameterValueType.Option,
                    true,
                    options: new[] { EventCommandCodec.NotifyMode, EventCommandCodec.RequestMode })
            };
            if (definition == null)
            {
                return parameters;
            }

            for (var i = 0; i < definition.Arguments.Count; i++)
            {
                var argument = definition.Arguments[i];
                parameters.Add(new NodeParameterDefinition(
                    argument.Key,
                    argument.Label,
                    argument.ValueType,
                    argument.Required,
                    options: argument.Options));
            }

            return parameters;
        }

        private static IReadOnlyList<PortDefinition> BuildPorts(
            EventMode mode,
            EventDefinition definition)
        {
            if (mode == EventMode.Notify)
            {
                return new[] { new PortDefinition("completed", "完成", PortDirection.Output) };
            }

            if (definition == null || definition.Outcomes.Count == 0)
            {
                return Array.Empty<PortDefinition>();
            }

            var ports = new PortDefinition[definition.Outcomes.Count];
            for (var i = 0; i < definition.Outcomes.Count; i++)
            {
                ports[i] = new PortDefinition(
                    definition.Outcomes[i],
                    definition.Outcomes[i],
                    PortDirection.Output);
            }

            return ports;
        }

        private static string GetParameter(AuthoringNode node, string key)
        {
            for (var i = 0; i < node.Parameters.Count; i++)
            {
                var parameter = node.Parameters[i];
                if (parameter != null && string.Equals(parameter.Key, key, StringComparison.Ordinal))
                {
                    return parameter.Value;
                }
            }

            return null;
        }
    }
}
