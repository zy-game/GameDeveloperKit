using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Logic;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.StoryEditor.Logic;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;

namespace GameDeveloperKit.StoryEditor.Compiler
{
    public static partial class ProgramCompiler
    {
        private static Step BuildLogicCommandStep(
            string storyId,
            string episodeId,
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, AuthoringEpisode> episodeLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            LogicDefinitionCatalog logicDefinitions,
            List<CommandDefinition> commandDefinitions,
            ISet<string> commandNames,
            ValidationReport report,
            IReadOnlyList<string> tags)
        {
            var nodeId = TrimToNull(node.NodeId);
            var logicId = TrimToNull(GetString(node.Parameters, LogicCommandCodec.LogicIdParameter));
            var source = $"story:{storyId}/episode:{episodeId}/node:{nodeId}/logic:{logicId ?? "<empty>"}";
            if (!logicDefinitions.TryGet(logicId, out var definition))
            {
                report.AddError(source, "Logic definition is not registered.");
                return null;
            }

            var arguments = BuildLogicArguments(storyId, episodeId, node, definition, report);
            ValidateLogicStaleParameters(storyId, episodeId, node, definition, report);
            var authoredTargets = BuildOutcomeTargets(
                storyId,
                episodeId,
                node,
                edges,
                episodeLookup,
                nodeLookup,
                report);
            var outputPorts = BuildLogicOutputPorts(definition);
            var outputTargets = ValidateLogicOutputTargets(
                storyId,
                episodeId,
                nodeId,
                logicId,
                outputPorts,
                authoredTargets,
                report,
                out var outputsValid);
            if (!outputsValid)
            {
                return null;
            }

            var command = LogicCommandCodec.Create(
                nodeId,
                logicId,
                new ArgumentBag(arguments),
                outputPorts,
                outputTargets);
            RegisterCommandSchema(
                commandDefinitions,
                commandNames,
                logicId,
                definition.DisplayName,
                true,
                BuildLogicArgumentDefinitions(definition),
                outputPorts);
            return new Step(
                nodeId,
                StepKind.Command,
                new StepData(command: command, tags: tags));
        }

        private static Dictionary<string, Value> BuildLogicArguments(
            string storyId,
            string episodeId,
            AuthoringNode node,
            LogicDefinition definition,
            ValidationReport report)
        {
            var arguments = new Dictionary<string, Value>(StringComparer.Ordinal);
            for (var i = 0; i < definition.Parameters.Count; i++)
            {
                var parameter = definition.Parameters[i];
                var value = GetString(node.Parameters, parameter.Key);
                var source =
                    $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/logic:{definition.LogicId}/field:{parameter.Key}";
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (parameter.Required)
                    {
                        report.AddError(source, "Required logic field is missing.");
                    }

                    continue;
                }

                if (TryBuildArgumentValue(
                        parameter,
                        value,
                        source,
                        report,
                        true,
                        out var argumentValue))
                {
                    arguments.Add(parameter.Key, argumentValue);
                }
            }

            return arguments;
        }

        private static void ValidateLogicStaleParameters(
            string storyId,
            string episodeId,
            AuthoringNode node,
            LogicDefinition definition,
            ValidationReport report)
        {
            var declaredKeys = new HashSet<string>(StringComparer.Ordinal)
            {
                LogicCommandCodec.LogicIdParameter,
                "tags"
            };
            for (var i = 0; i < definition.Parameters.Count; i++)
            {
                declaredKeys.Add(definition.Parameters[i].Key);
            }

            for (var i = 0; i < node.Parameters.Count; i++)
            {
                var parameter = node.Parameters[i];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Key) ||
                    declaredKeys.Contains(parameter.Key))
                {
                    continue;
                }

                report.AddError(
                    $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/logic:{definition.LogicId}/field:{parameter.Key}",
                    "Logic field is not declared by its definition.");
            }
        }

        private static IReadOnlyList<string> BuildLogicOutputPorts(LogicDefinition definition)
        {
            var outputs = new List<string>();
            for (var i = 0; i < definition.Ports.Count; i++)
            {
                var port = definition.Ports[i];
                if (port.Direction == PortDirection.Output)
                {
                    outputs.Add(port.PortId);
                }
            }

            return outputs;
        }

        private static IReadOnlyDictionary<string, Target> ValidateLogicOutputTargets(
            string storyId,
            string episodeId,
            string nodeId,
            string logicId,
            IReadOnlyList<string> outputPorts,
            IReadOnlyDictionary<string, Target> authoredTargets,
            ValidationReport report,
            out bool valid)
        {
            valid = true;
            var targets = new Dictionary<string, Target>(StringComparer.Ordinal);
            for (var i = 0; i < outputPorts.Count; i++)
            {
                var output = outputPorts[i];
                if (!authoredTargets.TryGetValue(output, out var target) || target == null)
                {
                    report.AddError(
                        $"story:{storyId}/episode:{episodeId}/node:{nodeId}/logic:{logicId}/port:{output}",
                        "Logic output target is missing.");
                    valid = false;
                    continue;
                }

                targets.Add(output, target);
            }

            foreach (var pair in authoredTargets)
            {
                if (ContainsLogicOutput(outputPorts, pair.Key))
                {
                    continue;
                }

                report.AddError(
                    $"story:{storyId}/episode:{episodeId}/node:{nodeId}/logic:{logicId}/port:{pair.Key}",
                    "Logic output is not declared by its definition.");
                valid = false;
            }

            return targets;
        }

        private static bool ContainsLogicOutput(IReadOnlyList<string> outputPorts, string outputId)
        {
            for (var i = 0; i < outputPorts.Count; i++)
            {
                if (string.Equals(outputPorts[i], outputId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<CommandArgumentDefinition> BuildLogicArgumentDefinitions(
            LogicDefinition definition)
        {
            var definitions = new List<CommandArgumentDefinition>
            {
                new CommandArgumentDefinition(
                    LogicCommandCodec.MarkerArgument,
                    "Logic node marker",
                    ParameterValueType.Boolean,
                    true)
            };
            for (var i = 0; i < definition.Parameters.Count; i++)
            {
                var parameter = definition.Parameters[i];
                definitions.Add(new CommandArgumentDefinition(
                    parameter.Key,
                    parameter.Label,
                    parameter.ValueType,
                    parameter.Required,
                    parameter.ResourceType,
                    parameter.Options,
                    parameter.Tooltip));
            }

            return definitions;
        }
    }
}
