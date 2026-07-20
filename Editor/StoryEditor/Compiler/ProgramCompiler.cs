using System;
using System.Collections.Generic;
using System.Globalization;
using GameDeveloperKit.Story;
using UnityEditor;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Text;
using GameDeveloperKit.StoryEditor.Media;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Playback;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Logic;

namespace GameDeveloperKit.StoryEditor.Compiler
{
    /// <summary>
    /// 将 Story Editor authoring 数据编译为 Program。
    /// </summary>
    public static partial class ProgramCompiler
    {
        public static Program Compile(AuthoringAsset asset, out ValidationReport report)
        {
            return Compile(
                asset,
                LogicDefinitionCatalog.Shared,
                out report);
        }

        private static Program Compile(
            AuthoringAsset asset,
            LogicDefinitionCatalog logicDefinitions,
            out ValidationReport report)
        {
            report = new ValidationReport();
            logicDefinitions ??= LogicDefinitionCatalog.Shared;
            for (var i = 0; i < logicDefinitions.Errors.Count; i++)
            {
                report.AddError("logic-definitions", logicDefinitions.Errors[i]);
            }

            if (asset == null)
            {
                report.AddError("asset", "Story authoring asset is missing.");
                return null;
            }

            asset.EnsureDefaults();
            ValidateText(asset.StoryId, "story", report);
            ValidateText(asset.Version, "version", report);
            var episodeLookup = BuildEpisodeLookup(asset, report);

            if (report.HasErrors)
            {
                return null;
            }

            var commandDefinitions = new List<CommandDefinition>();
            var commandNames = new HashSet<string>(StringComparer.Ordinal);
            var routeEdgeIds = new HashSet<string>(StringComparer.Ordinal);
            var volumes = new List<Volume>();
            for (var volumeIndex = 0; volumeIndex < asset.Volumes.Count; volumeIndex++)
            {
                var sourceVolume = asset.Volumes[volumeIndex];
                if (sourceVolume == null)
                {
                    continue;
                }

                var episodes = new List<Episode>();
                for (var episodeIndex = 0; episodeIndex < sourceVolume.Episodes.Count; episodeIndex++)
                {
                    var episode = sourceVolume.Episodes[episodeIndex];
                    if (episode == null)
                    {
                        continue;
                    }

                    var compiled = CompileEpisode(
                        asset.StoryId,
                        episode,
                        episodeLookup,
                        logicDefinitions,
                        commandDefinitions,
                        commandNames,
                        report);
                    if (compiled != null)
                    {
                        episodes.Add(compiled);
                    }
                }

                var route = RouteCompiler.Compile(asset, sourceVolume, episodes, routeEdgeIds, report);
                var layouts = LayoutCompiler.Compile(asset.StoryId, sourceVolume, episodes, route, report);
                volumes.Add(new Volume(
                    TrimToNull(sourceVolume.VolumeId),
                    TrimToNull(sourceVolume.Title),
                    episodes,
                    route,
                    GetPreviewImagePath(sourceVolume),
                    TrimToNull(sourceVolume.Description),
                    layouts));
            }

            if (report.HasErrors)
            {
                return null;
            }

            var program = new Program(
                TrimToNull(asset.StoryId),
                TrimToNull(asset.Version),
                volumes,
                new VariableSchema(),
                new CommandSchema(commandDefinitions));
            AddPublishedIdentityIssues(asset, program, report);
            return report.HasErrors ? null : program;
        }

        public static ValidationReport Validate(AuthoringAsset asset)
        {
            Compile(asset, out var report);
            return report;
        }

        private static Episode CompileEpisode(
            string storyId,
            AuthoringEpisode episode,
            IReadOnlyDictionary<string, AuthoringEpisode> episodeLookup,
            LogicDefinitionCatalog logicDefinitions,
            List<CommandDefinition> commandDefinitions,
            ISet<string> commandNames,
            ValidationReport report)
        {
            var episodeId = TrimToNull(episode.EpisodeId);
            var source = $"story:{storyId}/episode:{episodeId}";
            ValidateText(episode.EpisodeId, $"{source}/episodeId", report);
            ValidateText(episode.EntryNodeId, $"{source}/entryNode", report);

            var nodeLookup = BuildNodeLookup(storyId, episode, report);
            var outgoingEdges = BuildOutgoingEdgeLookup(storyId, episode, nodeLookup, report);
            if (string.IsNullOrWhiteSpace(episode.EntryNodeId) is false &&
                nodeLookup.ContainsKey(episode.EntryNodeId) is false)
            {
                report.AddError(source, $"Entry step does not exist. step:{episode.EntryNodeId}");
            }

            if (report.HasErrors)
            {
                return null;
            }

            var parallelContext = BuildParallelContext(storyId, episodeId, nodeLookup, outgoingEdges, report);
            var orderedNodes = BuildOrderedRuntimeNodes(episode, nodeLookup, outgoingEdges);
            var hiddenChoiceNodes = BuildHiddenChoiceNodeIds(orderedNodes, nodeLookup, outgoingEdges);
            var steps = new List<Step>();
            var stepIds = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < orderedNodes.Count; i++)
            {
                var node = orderedNodes[i];
                if (node == null || hiddenChoiceNodes.Contains(node.NodeId))
                {
                    continue;
                }

                if (NodeSchemaRegistry.IsDefaultAuthoringNode(node.NodeKind) is false)
                {
                    report.AddError(
                        $"story:{storyId}/episode:{episodeId}/node:{TrimToNull(node.NodeId)}",
                        "Node kind is no longer supported in Story default authoring path.");
                    continue;
                }

                var step = CompileNode(
                    storyId,
                    episodeId,
                    node,
                    episodeLookup,
                    nodeLookup,
                    outgoingEdges,
                    parallelContext,
                    logicDefinitions,
                    commandDefinitions,
                    commandNames,
                    report,
                    stepIds);

                if (step == null)
                {
                    continue;
                }

                if (stepIds.Add(step.StepId) is false)
                {
                    report.AddError($"{source}/step:{step.StepId}", "Duplicate step id.");
                    continue;
                }

                steps.Add(step);

                if (CanOwnChoiceItems(node.NodeKind))
                {
                    var choiceStep = BuildOwnedChoiceStep(
                        storyId,
                        episodeId,
                        node,
                        outgoingEdges,
                        episodeLookup,
                        nodeLookup,
                        report,
                        stepIds,
                        BuildTags(node.Parameters));
                    if (choiceStep != null)
                    {
                        if (stepIds.Add(choiceStep.StepId) is false)
                        {
                            report.AddError($"{source}/step:{choiceStep.StepId}", "Duplicate step id.");
                        }
                        else
                        {
                            steps.Add(choiceStep);
                        }
                    }
                }
            }

            if (report.HasErrors)
            {
                return null;
            }

            var previewImagePath = GetPreviewImagePath(episode);

            var exits = new List<EpisodeExit>();
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].Kind == StepKind.Choice)
                {
                    for (var choiceIndex = 0; choiceIndex < steps[i].Choices.Count; choiceIndex++)
                    {
                        var choice = steps[i].Choices[choiceIndex];
                        if (choice != null && !string.IsNullOrWhiteSpace(choice.ExitId))
                        {
                            exits.Add(new EpisodeExit(choice.ExitId, choice.ExitId));
                        }
                    }
                }
                else if (steps[i].Kind == StepKind.End && !string.IsNullOrWhiteSpace(steps[i].Data.ExitId))
                {
                    exits.Add(new EpisodeExit(steps[i].Data.ExitId, steps[i].Data.ExitId));
                }
            }

            return new Episode(
                episodeId,
                TrimToNull(episode.Title) ?? episodeId,
                TrimToNull(episode.EntryNodeId),
                exits,
                steps,
                previewImagePath,
                TrimToNull(episode.Description));
        }

        private static Step CompileNode(
            string storyId,
            string episodeId,
            AuthoringNode node,
            IReadOnlyDictionary<string, AuthoringEpisode> episodeLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            ParallelCompileContext parallelContext,
            LogicDefinitionCatalog logicDefinitions,
            List<CommandDefinition> commandDefinitions,
            ISet<string> commandNames,
            ValidationReport report,
            ISet<string> existingStepIds)
        {
            var nodeId = TrimToNull(node.NodeId);
            var edges = GetOutgoingEdges(outgoingEdges, nodeId);
            var tags = BuildTags(node.Parameters);

            if (NodeSchemaRegistry.IsDefaultAuthoringNode(node.NodeKind) is false)
            {
                report.AddError(
                    $"story:{storyId}/episode:{episodeId}/node:{nodeId}",
                    "Node kind is no longer supported in Story default authoring path.");
                return null;
            }

            switch (node.NodeKind)
            {
                case NodeKind.Start:
                    return new Step(nodeId, StepKind.Start, new StepData(tags: tags));
                case NodeKind.End:
                    return new Step(nodeId, StepKind.End, new StepData(tags: tags, exitId: nodeId));
                case NodeKind.Dialogue:
                case NodeKind.Narration:
                    return BuildLineStep(node, nodeId, outgoingEdges, episodeLookup, nodeLookup, report, existingStepIds, tags);
                case NodeKind.Choice:
                    return BuildChoiceStep(storyId, episodeId, node, edges, episodeLookup, nodeLookup, report, existingStepIds, tags);
                case NodeKind.PlayVideo:
                case NodeKind.ShowImage:
                case NodeKind.PlayAudio:
                    return BuildCommandStep(
                        storyId,
                        episodeId,
                        node,
                        edges,
                        outgoingEdges,
                        episodeLookup,
                        nodeLookup,
                        parallelContext,
                        commandDefinitions,
                        commandNames,
                        report,
                        tags);
                case NodeKind.Logic:
                    return BuildLogicCommandStep(
                        storyId,
                        episodeId,
                        node,
                        edges,
                        episodeLookup,
                        nodeLookup,
                        logicDefinitions,
                        commandDefinitions,
                        commandNames,
                        report,
                        tags);
                case NodeKind.Parallel:
                    return BuildParallelStep(storyId, episodeId, node, parallelContext, report, tags);
                case NodeKind.Wait:
                    return BuildWaitStep(storyId, episodeId, node, edges, episodeLookup, nodeLookup, report, existingStepIds, tags);
                default:
                    report.AddError($"story:{storyId}/episode:{episodeId}/node:{nodeId}", $"Unsupported node kind '{node.NodeKind}'.");
                    return null;
            }
        }

        private static Step BuildLineStep(
            AuthoringNode node,
            string nodeId,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            IReadOnlyDictionary<string, AuthoringEpisode> episodeLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ValidationReport report,
            ISet<string> existingStepIds,
            IReadOnlyList<string> tags)
        {
            var textKey = GetString(node.Parameters, "textKey");
            if (string.IsNullOrWhiteSpace(textKey))
            {
                textKey = TrimToNull(node.Title) ?? nodeId;
            }
            ValidateLocalizedText(textKey, $"node:{node.NodeId}/field:textKey", report);
            var speaker = GetString(node.Parameters, "speaker");
            ValidateLocalizedText(speaker, $"node:{node.NodeId}/field:speaker", report);

            var edges = GetOutgoingEdges(outgoingEdges, nodeId);
            var choiceItemEdges = GetChoiceItemEdges(node, edges, nodeLookup);
            var directEdges = ExcludeChoiceItemEdges(node, edges, nodeLookup);
            Target target = null;
            if (choiceItemEdges.Count > 0)
            {
                if (directEdges.Count > 0)
                {
                    report.AddError($"node:{node.NodeId}/port:completed", "Line completed output cannot mix choice items and direct flow targets.");
                    return null;
                }

                target = Target.Step(MakeSyntheticChoiceStepId(nodeId, existingStepIds, nodeLookup));
            }
            else
            {
                target = FirstDirectTarget(node, outgoingEdges, episodeLookup, nodeLookup, report);
                if (target == null)
                {
                    target = Target.EpisodeEnd();
                }
            }

            return new Step(
                nodeId,
                StepKind.Line,
                new StepData(
                    textKey: textKey,
                    speaker: speaker,
                    target: target,
                    tags: tags));
        }

        private static Step BuildChoiceStep(
            string storyId,
            string episodeId,
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, AuthoringEpisode> episodeLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ValidationReport report,
            ISet<string> existingStepIds,
            IReadOnlyList<string> tags)
        {
            var choices = new List<Choice>();
            var choiceItemEdges = GetChoiceItemEdges(node, edges, nodeLookup);
            if (choiceItemEdges.Count > 0)
            {
                for (var i = 0; i < choiceItemEdges.Count; i++)
                {
                    var edge = choiceItemEdges[i];
                    var optionNode = nodeLookup[edge.TargetNodeId];
                    var textKey = GetString(optionNode.Parameters, "textKey");
                    if (string.IsNullOrWhiteSpace(textKey))
                    {
                        report.AddWarning(
                            $"story:{storyId}/episode:{episodeId}/node:{optionNode.NodeId}/field:textKey",
                            "Choice item textKey is missing; node title is used as fallback.");
                        textKey = TrimToNull(optionNode.Title) ?? optionNode.NodeId;
                    }
                    ValidateLocalizedText(
                        textKey,
                        $"story:{storyId}/episode:{episodeId}/node:{optionNode.NodeId}/field:textKey",
                        report);
                    choices.Add(new Choice(
                        optionNode.NodeId,
                        optionNode.NodeId,
                        textKey,
                        BuildCondition(edge.Conditions)));
                }
            }
            else
            {
                for (var i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    if (edge == null)
                    {
                        continue;
                    }

                    var choiceId = NormalizeChoiceId(edge.FromPortId, i);
                    choices.Add(new Choice(
                        choiceId,
                        choiceId,
                        TrimToNull(edge.FromPortLabel) ?? choiceId,
                        BuildCondition(edge.Conditions)));
                }
            }

            if (choices.Count == 0)
            {
                report.AddError($"story:{storyId}/episode:{episodeId}/node:{node.NodeId}", "Choice node has no outgoing choices.");
                return null;
            }

            return new Step(
                TrimToNull(node.NodeId),
                StepKind.Choice,
                new StepData(choices: choices, tags: tags));
        }

        private static Step BuildOwnedChoiceStep(
            string storyId,
            string episodeId,
            AuthoringNode ownerNode,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            IReadOnlyDictionary<string, AuthoringEpisode> episodeLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ValidationReport report,
            ISet<string> existingStepIds,
            IReadOnlyList<string> tags)
        {
            var choiceItemEdges = GetChoiceItemEdges(ownerNode, GetOutgoingEdges(outgoingEdges, ownerNode.NodeId), nodeLookup);
            if (choiceItemEdges.Count == 0)
            {
                return null;
            }

            var choiceNode = new AuthoringNode
            {
                NodeId = MakeSyntheticChoiceStepId(ownerNode.NodeId, existingStepIds, nodeLookup),
                Title = ownerNode.Title,
                NodeKind = ownerNode.NodeKind
            };
            return BuildChoiceStep(storyId, episodeId, choiceNode, choiceItemEdges, episodeLookup, nodeLookup, report, existingStepIds, tags);
        }

        private static Step BuildParallelStep(
            string storyId,
            string episodeId,
            AuthoringNode node,
            ParallelCompileContext parallelContext,
            ValidationReport report,
            IReadOnlyList<string> tags)
        {
            var nodeId = TrimToNull(node.NodeId);
            if (parallelContext == null ||
                parallelContext.Blocks.TryGetValue(nodeId, out var block) is false)
            {
                report.AddError($"story:{storyId}/episode:{episodeId}/node:{nodeId}/port:branch", "Parallel node must define a valid branch block.");
                return null;
            }

            return new Step(
                nodeId,
                StepKind.Parallel,
                new StepData(
                    tags: tags,
                    branches: block.Branches));
        }

        private static Step BuildBranchStep(
            string storyId,
            string episodeId,
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, AuthoringEpisode> episodeLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ValidationReport report,
            IReadOnlyList<string> tags)
        {
            var target = FirstOutgoingTarget(storyId, episodeId, node, edges, episodeLookup, nodeLookup, report, "branch");
            var condition = edges.Count > 0 ? BuildCondition(edges[0].Conditions) : null;
            if (target == null || condition == null)
            {
                if (condition == null)
                {
                    report.AddError($"story:{storyId}/episode:{episodeId}/node:{node.NodeId}", "Branch condition cannot be empty.");
                }

                return null;
            }

            return new Step(
                TrimToNull(node.NodeId),
                StepKind.Branch,
                new StepData(condition: condition, target: target, tags: tags));
        }

        private static Step BuildWaitStep(
            string storyId,
            string episodeId,
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, AuthoringEpisode> episodeLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ValidationReport report,
            ISet<string> existingStepIds,
            IReadOnlyList<string> tags)
        {
            var source = $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/field:duration";
            var durationValue = GetString(node.Parameters, "duration");
            if (string.IsNullOrWhiteSpace(durationValue) is false &&
                float.TryParse(durationValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _) is false)
            {
                report.AddError(source, "Wait duration must be a number.");
                return null;
            }

            var waitSeconds = GetFloat(node.Parameters, "duration");
            if (TimeRules.IsFiniteNonNegative(waitSeconds) is false)
            {
                report.AddError(source, "Wait duration must be finite and non-negative.");
                return null;
            }

            Target target;
            var choiceItemEdges = GetChoiceItemEdges(node, edges, nodeLookup);
            var directEdges = ExcludeChoiceItemEdges(node, edges, nodeLookup);
            if (choiceItemEdges.Count > 0)
            {
                if (directEdges.Count > 0)
                {
                    report.AddError(
                        $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/port:completed",
                        "Wait completed output cannot mix choice items and direct flow targets.");
                    return null;
                }

                target = Target.Step(MakeSyntheticChoiceStepId(node.NodeId, existingStepIds, nodeLookup));
            }
            else
            {
                target = FirstOutgoingTarget(storyId, episodeId, node, edges, episodeLookup, nodeLookup, report, "wait") ?? Target.EpisodeEnd();
            }

            return new Step(
                TrimToNull(node.NodeId),
                StepKind.Wait,
                new StepData(
                    target: target,
                    waitSeconds: waitSeconds,
                    tags: tags));
        }

        private static Step BuildRedirectStep(
            string storyId,
            string episodeId,
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, AuthoringEpisode> episodeLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ValidationReport report,
            IReadOnlyList<string> tags)
        {
            var target = FirstOutgoingTarget(storyId, episodeId, node, edges, episodeLookup, nodeLookup, report, "routing");
            return target == null
                ? new Step(TrimToNull(node.NodeId), StepKind.Start, new StepData(tags: tags))
                : new Step(TrimToNull(node.NodeId), StepKind.Jump, new StepData(target: target, tags: tags));
        }
        private static Step BuildCommandStep(
            string storyId,
            string episodeId,
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            IReadOnlyDictionary<string, AuthoringEpisode> episodeLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ParallelCompileContext parallelContext,
            List<CommandDefinition> commandDefinitions,
            ISet<string> commandNames,
            ValidationReport report,
            IReadOnlyList<string> tags)
        {
            var schema = NodeSchemaRegistry.Get(node.NodeKind);
            var commandName = GetCommandName(node);
            var arguments = BuildArguments(storyId, episodeId, node, schema, report);
            var argumentDefinitions = node.NodeKind == NodeKind.PlayVideo
                ? BuildVideoArgumentDefinitions()
                : node.NodeKind == NodeKind.PlayAudio
                    ? BuildAudioArgumentDefinitions()
                    : BuildArgumentDefinitions(schema);
            var outcomePorts = BuildOutcomePorts(edges);
            var outcomeTargets = BuildOutcomeTargets(storyId, episodeId, node, edges, episodeLookup, nodeLookup, report);
            var waitForCompletion = GetBoolean(node.Parameters, "wait") ||
                                    outcomePorts.Count > 0 ||
                                    node.NodeKind == NodeKind.PlayVideo;

            RegisterCommandSchema(
                commandDefinitions,
                commandNames,
                commandName,
                TrimToNull(node.Title) ?? commandName,
                waitForCompletion,
                argumentDefinitions,
                outcomePorts);

            var command = new global::GameDeveloperKit.Story.Model.Command(
                TrimToNull(node.NodeId),
                commandName,
                new ArgumentBag(arguments),
                waitForCompletion,
                outcomePorts,
                outcomeTargets);

            return new Step(
                TrimToNull(node.NodeId),
                StepKind.Command,
                new StepData(
                    command: command,
                    target: FirstOutcomeTarget(outcomeTargets) ?? (edges.Count == 0 ? Target.EpisodeEnd() : null),
                    tags: tags));
        }

        private static Dictionary<string, Value> BuildArguments(
            string storyId,
            string episodeId,
            AuthoringNode node,
            NodeSchema schema,
            ValidationReport report)
        {
            if (node.NodeKind == NodeKind.PlayVideo)
            {
                return BuildVideoArguments(storyId, episodeId, node, report);
            }

            if (node.NodeKind == NodeKind.PlayAudio)
            {
                return BuildAudioArguments(storyId, episodeId, node, report);
            }

            var arguments = new Dictionary<string, Value>(StringComparer.Ordinal);
            if (schema?.Parameters == null || schema.Parameters.Count == 0)
            {
                return arguments;
            }

            for (var i = 0; i < schema.Parameters.Count; i++)
            {
                var parameter = schema.Parameters[i];
                if (string.IsNullOrWhiteSpace(parameter.Key) ||
                    string.Equals(parameter.Key, "wait", StringComparison.Ordinal))
                {
                    continue;
                }

                var value = GetString(node.Parameters, parameter.Key);
                var source = $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/field:{parameter.Key}";
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (parameter.Required)
                    {
                        report.AddError(source, "Required command field is missing.");
                    }

                    continue;
                }

                var validateAssetReference = node.NodeKind != NodeKind.PlayVideo ||
                                             string.Equals(parameter.Key, MediaCommandNames.ClipArgument, StringComparison.Ordinal) is false;
                if (TryBuildArgumentValue(parameter, value, source, report, validateAssetReference, out var storyValue))
                {
                    arguments[parameter.Key] = storyValue;
                }
            }

            return arguments;
        }

        private static bool TryBuildArgumentValue(
            NodeParameterDefinition parameter,
            string value,
            string source,
            ValidationReport report,
            bool validateAssetReference,
            out Value storyValue)
        {
            switch (parameter.ValueType)
            {
                case ParameterValueType.Number:
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numberValue))
                    {
                        storyValue = Value.FromNumber(numberValue);
                        return true;
                    }

                    report.AddError(source, "Command field must be a number.");
                    storyValue = Value.Null;
                    return false;
                case ParameterValueType.Boolean:
                    if (bool.TryParse(value, out var booleanValue))
                    {
                        storyValue = Value.FromBoolean(booleanValue);
                        return true;
                    }

                    report.AddError(source, "Command field must be a boolean.");
                    storyValue = Value.Null;
                    return false;
                case ParameterValueType.Option:
                    if (IsValidOption(parameter, value) is false)
                    {
                        report.AddError(source, "Command field must use a valid option.");
                        storyValue = Value.Null;
                        return false;
                    }

                    storyValue = Value.FromString(value);
                    return true;
                case ParameterValueType.AssetReference:
                    if (validateAssetReference && IsProjectAssetReference(value) is false)
                    {
                        report.AddWarning(source, "Asset reference uses a manual string fallback.");
                    }

                    storyValue = Value.FromString(value);
                    return true;
                default:
                    storyValue = Value.FromString(value);
                    return true;
            }
        }

        private static void ValidateLocalizedText(string value, string source, ValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(value) || value.TrimStart().StartsWith("{", StringComparison.Ordinal) is false)
            {
                return;
            }

            if (TextReferenceCodec.TryDeserialize(value, out var reference, out _, out var error) is false)
            {
                report.AddError(source, $"Text reference is invalid. {error}");
                return;
            }

            if (reference.Mode != TextMode.LocalizationKey)
            {
                return;
            }

            var catalog = LocalizationTextCatalog.Build();
            if (string.IsNullOrWhiteSpace(catalog.Error) is false)
            {
                report.AddError(source, catalog.Error);
            }
            else if (catalog.TryGetText(reference.Value, out _) is false)
            {
                report.AddError(source, $"Localization key is missing from zh-CN pack. key:{reference.Value}");
            }
        }

        private static bool IsValidOption(NodeParameterDefinition parameter, string value)
        {
            if (parameter.Options == null || parameter.Options.Count == 0)
            {
                return true;
            }

            for (var i = 0; i < parameter.Options.Count; i++)
            {
                if (string.Equals(parameter.Options[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsProjectAssetReference(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(value) != null;
            }

            return false;
        }

        private static IReadOnlyDictionary<string, Target> BuildOutcomeTargets(
            string storyId,
            string episodeId,
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, AuthoringEpisode> episodeLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ValidationReport report)
        {
            var targets = new Dictionary<string, Target>(StringComparer.Ordinal);
            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge == null)
                {
                    continue;
                }

                var portId = NormalizeOutcomePortId(edge.FromPortId, i);
                var target = BuildTarget(
                    storyId,
                    episodeId,
                    edge,
                    episodeLookup,
                    nodeLookup,
                    report,
                    $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/outcome:{portId}");
                if (target == null)
                {
                    continue;
                }

                if (targets.ContainsKey(portId))
                {
                    report.AddError($"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/outcome:{portId}", "Duplicate outcome port.");
                    continue;
                }

                targets[portId] = target;
            }

            return targets;
        }

        private static Target FirstDirectTarget(
            AuthoringNode node,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            IReadOnlyDictionary<string, AuthoringEpisode> episodeLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ValidationReport report)
        {
            var edges = GetOutgoingEdges(outgoingEdges, TrimToNull(node.NodeId));
            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge == null || IsChoiceItemEdge(edge, nodeLookup))
                {
                    continue;
                }

                return BuildTarget(
                    string.Empty,
                    FindEpisodeId(episodeLookup, node),
                    edge,
                    episodeLookup,
                    nodeLookup,
                    report,
                    $"node:{node.NodeId}/port:{edge.FromPortId}");
            }

            return null;
        }

        private static Target FirstOutgoingTarget(
            string storyId,
            string episodeId,
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, AuthoringEpisode> episodeLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ValidationReport report,
            string label)
        {
            for (var i = 0; i < edges.Count; i++)
            {
                var target = BuildTarget(
                    storyId,
                    episodeId,
                    edges[i],
                    episodeLookup,
                    nodeLookup,
                    report,
                    $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/{label}:{i}");
                if (target != null)
                {
                    return target;
                }
            }

            return null;
        }

        private static Target BuildTarget(
            string storyId,
            string currentEpisodeId,
            AuthoringEdge edge,
            IReadOnlyDictionary<string, AuthoringEpisode> episodeLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ValidationReport report,
            string source)
        {
            if (edge == null)
            {
                report.AddError(source, "Target is missing.");
                return null;
            }

            switch (edge.TargetKind)
            {
                case TransitionTargetKind.Node:
                    {
                        var targetNodeId = TrimToNull(edge.TargetNodeId);
                        if (string.IsNullOrWhiteSpace(targetNodeId))
                        {
                            report.AddError(source, "Step target is invalid.");
                            return null;
                        }

                        if (nodeLookup.ContainsKey(targetNodeId) is false)
                        {
                            report.AddError(source, $"Target step does not exist. episode:{currentEpisodeId} step:{targetNodeId}");
                            return null;
                        }

                        return Target.Step(targetNodeId);
                    }
                case TransitionTargetKind.StoryEnd:
                    return Target.EpisodeEnd();
                default:
                    report.AddError(source, $"Unsupported target kind '{edge.TargetKind}'.");
                    return null;
            }
        }

        private static IReadOnlyDictionary<string, AuthoringEpisode> BuildEpisodeLookup(AuthoringAsset asset, ValidationReport report)
        {
            var episodes = new Dictionary<string, AuthoringEpisode>(StringComparer.Ordinal);
            for (var i = 0; i < asset.Episodes.Count; i++)
            {
                var episode = asset.Episodes[i];
                if (episode == null)
                {
                    report.AddError($"story:{asset.StoryId}/episode[{i}]", "Episode cannot be null.");
                    continue;
                }

                var episodeId = TrimToNull(episode.EpisodeId);
                if (string.IsNullOrWhiteSpace(episodeId))
                {
                    report.AddError($"story:{asset.StoryId}/episode[{i}]", "Episode id cannot be empty.");
                    continue;
                }

                if (episodes.ContainsKey(episodeId))
                {
                    report.AddError($"story:{asset.StoryId}/episode:{episodeId}", "Duplicate episode id.");
                    continue;
                }

                episodes.Add(episodeId, episode);
            }

            return episodes;
        }

        private static IReadOnlyDictionary<string, AuthoringNode> BuildNodeLookup(
            string storyId,
            AuthoringEpisode episode,
            ValidationReport report)
        {
            var nodes = new Dictionary<string, AuthoringNode>(StringComparer.Ordinal);
            for (var i = 0; i < episode.Nodes.Count; i++)
            {
                var node = episode.Nodes[i];
                if (node == null)
                {
                    report.AddError($"story:{storyId}/episode:{episode.EpisodeId}/node[{i}]", "Node cannot be null.");
                    continue;
                }

                var nodeId = TrimToNull(node.NodeId);
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    report.AddError($"story:{storyId}/episode:{episode.EpisodeId}/node[{i}]", "Node id cannot be empty.");
                    continue;
                }

                if (nodes.ContainsKey(nodeId))
                {
                    report.AddError($"story:{storyId}/episode:{episode.EpisodeId}/node:{nodeId}", "Duplicate node id.");
                    continue;
                }

                nodes.Add(nodeId, node);
            }

            return nodes;
        }

        private static IReadOnlyDictionary<string, List<AuthoringEdge>> BuildOutgoingEdgeLookup(
            string storyId,
            AuthoringEpisode episode,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ValidationReport report)
        {
            var lookup = new Dictionary<string, List<AuthoringEdge>>(StringComparer.Ordinal);
            for (var i = 0; i < episode.Edges.Count; i++)
            {
                var edge = episode.Edges[i];
                if (edge == null)
                {
                    report.AddError($"story:{storyId}/episode:{episode.EpisodeId}/edge[{i}]", "Edge cannot be null.");
                    continue;
                }

                var fromNodeId = TrimToNull(edge.FromNodeId);
                if (string.IsNullOrWhiteSpace(fromNodeId))
                {
                    report.AddError($"story:{storyId}/episode:{episode.EpisodeId}/edge:{edge.EdgeId}", "Edge from node id cannot be empty.");
                    continue;
                }

                if (nodeLookup.ContainsKey(fromNodeId) is false)
                {
                    report.AddError($"story:{storyId}/episode:{episode.EpisodeId}/edge:{edge.EdgeId}", "Edge source node does not exist.");
                    continue;
                }

                var fromNode = nodeLookup[fromNodeId];
                if (fromNode.NodeKind == NodeKind.Choice || fromNode.NodeKind == NodeKind.End)
                {
                    report.AddError(
                        $"story:{storyId}/episode:{episode.EpisodeId}/node:{fromNodeId}/edge:{edge.EdgeId}",
                        "Choice and End nodes are terminal Episode exits and cannot target a detail step.");
                    continue;
                }

                if (string.Equals(edge.FromPortId, "selected", StringComparison.Ordinal))
                {
                    report.AddError(
                        $"story:{storyId}/episode:{episode.EpisodeId}/node:{fromNodeId}/port:selected",
                        "Legacy Choice selected flow is not supported. Use the Choice Exit and a Volume RouteEdge.");
                    continue;
                }

                if (edge.TargetKind != TransitionTargetKind.Node && edge.TargetKind != TransitionTargetKind.StoryEnd)
                {
                    report.AddError(
                        $"story:{storyId}/episode:{episode.EpisodeId}/edge:{edge.EdgeId}",
                        "Cross-Episode detail targets are not supported. Use a Volume RouteEdge.");
                    continue;
                }

                if (!lookup.TryGetValue(fromNodeId, out var edges))
                {
                    edges = new List<AuthoringEdge>();
                    lookup.Add(fromNodeId, edges);
                }

                edges.Add(edge);
            }

            return lookup;
        }
        private static ParallelCompileContext BuildParallelContext(
            string storyId,
            string episodeId,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            ValidationReport report)
        {
            var context = new ParallelCompileContext();
            foreach (var pair in nodeLookup)
            {
                var node = pair.Value;
                if (node == null || node.NodeKind != NodeKind.Parallel)
                {
                    continue;
                }

                var block = BuildParallelBlock(storyId, episodeId, node, nodeLookup, outgoingEdges, report);
                if (block != null)
                {
                    context.Blocks[node.NodeId] = block;
                }
            }

            return context;
        }

        private static ParallelBlockInfo BuildParallelBlock(
            string storyId,
            string episodeId,
            AuthoringNode parallelNode,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            ValidationReport report)
        {
            var branches = new List<ParallelBranch>();
            var branchIds = new HashSet<string>(StringComparer.Ordinal);
            var allEdges = GetOutgoingEdges(outgoingEdges, parallelNode.NodeId);
            var edges = GetParallelBranchEdges(allEdges);
            var hasErrors = false;

            for (var i = 0; i < allEdges.Count; i++)
            {
                var edge = allEdges[i];
                if (edge == null || IsParallelBranchPort(edge.FromPortId))
                {
                    continue;
                }

                report.AddError(
                    $"story:{storyId}/episode:{episodeId}/node:{parallelNode.NodeId}/port:{edge.FromPortId}",
                    "Parallel output must use a branch port.");
                hasErrors = true;
            }

            if (edges.Count < 2)
            {
                report.AddError(
                    $"story:{storyId}/episode:{episodeId}/node:{parallelNode.NodeId}/port:branch",
                    "Parallel node must have at least two branch outputs.");
                return null;
            }

            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge == null)
                {
                    continue;
                }

                var branchId = NormalizeParallelBranchId(edge.FromPortId, i);
                if (branchIds.Add(branchId) is false)
                {
                    report.AddError(
                        $"story:{storyId}/episode:{episodeId}/node:{parallelNode.NodeId}/port:{edge.FromPortId}",
                        "Parallel branch port id must be unique.");
                    continue;
                }

                if (edge.TargetKind != TransitionTargetKind.Node ||
                    string.IsNullOrWhiteSpace(edge.TargetNodeId) ||
                    nodeLookup.ContainsKey(edge.TargetNodeId) is false)
                {
                    report.AddError(
                        $"story:{storyId}/episode:{episodeId}/node:{parallelNode.NodeId}/port:{edge.FromPortId}",
                        "Parallel branch must target a node in the same episode.");
                    continue;
                }

                if (!ValidateParallelBranchTermination(
                    storyId,
                    episodeId,
                    parallelNode.NodeId,
                    branchId,
                    edge.TargetNodeId,
                    nodeLookup,
                    outgoingEdges,
                    new HashSet<string>(StringComparer.Ordinal),
                    report))
                {
                    hasErrors = true;
                    continue;
                }

                branches.Add(new ParallelBranch(
                    branchId,
                    TrimToNull(edge.FromPortLabel) ?? branchId,
                    Target.Step(edge.TargetNodeId)));
            }

            if (branches.Count < 2 || hasErrors)
            {
                return null;
            }

            return new ParallelBlockInfo(parallelNode.NodeId, branches);
        }

        private static bool ValidateParallelBranchTermination(
            string storyId,
            string episodeId,
            string parallelNodeId,
            string branchId,
            string nodeId,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            ISet<string> visited,
            ValidationReport report)
        {
            nodeId = TrimToNull(nodeId);
            if (string.IsNullOrWhiteSpace(nodeId) || nodeLookup.TryGetValue(nodeId, out var node) is false)
            {
                report.AddError(
                    $"story:{storyId}/episode:{episodeId}/node:{parallelNodeId}/port:{branchId}",
                    "Parallel branch target does not exist.");
                return false;
            }

            if (visited.Add(nodeId) is false)
            {
                report.AddError(
                    $"story:{storyId}/episode:{episodeId}/node:{nodeId}",
                    "Parallel branch contains a cycle before it ends.");
                return false;
            }

            switch (node.NodeKind)
            {
                case NodeKind.End:
                case NodeKind.Parallel:
                case NodeKind.Choice:
                    return true;
            }

            var edges = GetOutgoingEdges(outgoingEdges, node.NodeId);
            if (edges.Count == 0)
            {
                return true;
            }

            var valid = true;
            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge == null)
                {
                    continue;
                }

                if (edge.TargetKind == TransitionTargetKind.StoryEnd)
                {
                    continue;
                }

                if (edge.TargetKind != TransitionTargetKind.Node)
                {
                    report.AddError(
                        $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/port:{edge.FromPortId}",
                        "Parallel branch target is invalid.");
                    valid = false;
                    continue;
                }

                if (!ValidateParallelBranchTermination(
                    storyId,
                    episodeId,
                    parallelNodeId,
                    branchId,
                    edge.TargetNodeId,
                    nodeLookup,
                    outgoingEdges,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    report))
                {
                    valid = false;
                }
            }

            return valid;
        }

        private static List<AuthoringNode> BuildOrderedRuntimeNodes(
            AuthoringEpisode episode,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges)
        {
            var ordered = new List<AuthoringNode>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            VisitNode(episode.EntryNodeId, nodeLookup, outgoingEdges, visited, ordered);

            for (var i = 0; i < episode.Nodes.Count; i++)
            {
                var node = episode.Nodes[i];
                if (node != null && visited.Contains(node.NodeId) is false)
                {
                    VisitNode(node.NodeId, nodeLookup, outgoingEdges, visited, ordered);
                }
            }

            return ordered;
        }

        private static void VisitNode(
            string nodeId,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            ISet<string> visited,
            List<AuthoringNode> ordered)
        {
            nodeId = TrimToNull(nodeId);
            if (string.IsNullOrWhiteSpace(nodeId) ||
                visited.Contains(nodeId) ||
                nodeLookup.TryGetValue(nodeId, out var node) is false)
            {
                return;
            }

            visited.Add(nodeId);
            ordered.Add(node);

            var edges = GetOutgoingEdges(outgoingEdges, nodeId);
            for (var i = 0; i < edges.Count; i++)
            {
                var targetNodeId = TrimToNull(edges[i]?.TargetNodeId);
                if (edges[i]?.TargetKind == TransitionTargetKind.Node && string.IsNullOrWhiteSpace(targetNodeId) is false)
                {
                    VisitNode(targetNodeId, nodeLookup, outgoingEdges, visited, ordered);
                }
            }
        }

        private static HashSet<string> BuildHiddenChoiceNodeIds(
            IReadOnlyList<AuthoringNode> orderedNodes,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < orderedNodes.Count; i++)
            {
                var node = orderedNodes[i];
                if (node == null || CanOwnChoiceItems(node.NodeKind) is false)
                {
                    continue;
                }

                var edges = GetOutgoingEdges(outgoingEdges, node.NodeId);
                for (var edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
                {
                    var edge = edges[edgeIndex];
                    if (IsChoiceItemEdge(edge, nodeLookup))
                    {
                        result.Add(edge.TargetNodeId);
                    }
                }
            }

            return result;
        }

        private static List<AuthoringEdge> ExcludeChoiceItemEdges(
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup)
        {
            var result = new List<AuthoringEdge>();
            if (edges == null)
            {
                return result;
            }

            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge != null &&
                    (CanOwnChoiceItems(node?.NodeKind ?? default) is false || IsChoiceItemEdge(edge, nodeLookup) is false))
                {
                    result.Add(edge);
                }
            }

            return result;
        }

        private static List<AuthoringEdge> GetChoiceItemEdges(
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup)
        {
            var result = new List<AuthoringEdge>();
            if (node == null || CanOwnChoiceItems(node.NodeKind) is false)
            {
                return result;
            }

            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (IsChoiceItemEdge(edge, nodeLookup))
                {
                    result.Add(edge);
                }
            }

            return result;
        }

        private static bool IsChoiceItemEdge(AuthoringEdge edge, IReadOnlyDictionary<string, AuthoringNode> nodeLookup)
        {
            return edge != null &&
                   edge.TargetKind == TransitionTargetKind.Node &&
                   string.Equals(edge.FromPortId, "completed", StringComparison.Ordinal) &&
                   string.IsNullOrWhiteSpace(edge.TargetNodeId) is false &&
                   nodeLookup.TryGetValue(edge.TargetNodeId, out var target) &&
                   target.NodeKind == NodeKind.Choice;
        }

        private static List<AuthoringEdge> GetOutgoingEdges(
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            string nodeId)
        {
            return outgoingEdges != null && outgoingEdges.TryGetValue(nodeId, out var edges)
                ? edges
                : new List<AuthoringEdge>();
        }

        private static string FindEpisodeId(
            IReadOnlyDictionary<string, AuthoringEpisode> episodeLookup,
            AuthoringNode node)
        {
            if (episodeLookup == null || node == null)
            {
                return null;
            }

            foreach (var pair in episodeLookup)
            {
                if (pair.Value != null && pair.Value.Nodes.Contains(node))
                {
                    return pair.Key;
                }
            }

            return null;
        }

        private static Expression BuildCondition(IReadOnlyList<AuthoringCondition> conditions)
        {
            if (conditions == null || conditions.Count == 0)
            {
                return null;
            }

            var expressions = new List<Expression>();
            for (var i = 0; i < conditions.Count; i++)
            {
                var expression = BuildCondition(conditions[i]);
                if (expression != null)
                {
                    expressions.Add(expression);
                }
            }

            if (expressions.Count == 0)
            {
                return null;
            }

            return expressions.Count == 1 ? expressions[0] : Expression.CreateAnd(expressions.ToArray());
        }

        private static Expression BuildCondition(AuthoringCondition condition)
        {
            if (condition == null || string.IsNullOrWhiteSpace(condition.ConditionId))
            {
                return null;
            }

            var arguments = new List<Expression>();
            for (var i = 0; i < condition.Parameters.Count; i++)
            {
                var parameter = condition.Parameters[i];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Key))
                {
                    continue;
                }

                arguments.Add(Expression.FromLiteral(Value.FromString($"{parameter.Key}={parameter.Value}")));
            }

            return arguments.Count == 0
                ? Expression.FromFunction(condition.ConditionId)
                : Expression.FromFunction(condition.ConditionId, arguments.ToArray());
        }

        private static IReadOnlyDictionary<string, Target> BuildNoTargets()
        {
            return new Dictionary<string, Target>(0, StringComparer.Ordinal);
        }

        private static Target FirstOutcomeTarget(IReadOnlyDictionary<string, Target> outcomeTargets)
        {
            if (outcomeTargets == null)
            {
                return null;
            }

            foreach (var pair in outcomeTargets)
            {
                if (pair.Value != null)
                {
                    return pair.Value;
                }
            }

            return null;
        }

        private static Expression CombineConditions(Expression left, Expression right)
        {
            if (left == null)
            {
                return right;
            }

            return right == null ? left : Expression.CreateAnd(left, right);
        }

        private static bool IsLineNode(NodeKind kind)
        {
            return kind == NodeKind.Dialogue || kind == NodeKind.Narration;
        }

        private static bool CanOwnChoiceItems(NodeKind kind)
        {
            return IsLineNode(kind) || kind == NodeKind.Wait;
        }

        private static List<AuthoringEdge> GetParallelBranchEdges(IReadOnlyList<AuthoringEdge> edges)
        {
            var result = new List<AuthoringEdge>();
            if (edges == null)
            {
                return result;
            }

            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge != null && IsParallelBranchPort(edge.FromPortId))
                {
                    result.Add(edge);
                }
            }

            return result;
        }

        private static IReadOnlyList<string> BuildOutcomePorts(IReadOnlyList<AuthoringEdge> edges)
        {
            if (edges == null || edges.Count == 0)
            {
                return Array.Empty<string>();
            }

            var ports = new List<string>();
            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge == null)
                {
                    continue;
                }

                var portId = NormalizeOutcomePortId(edge.FromPortId, i);
                if (ports.Contains(portId) is false)
                {
                    ports.Add(portId);
                }
            }

            return ports;
        }

        private static void RegisterCommandSchema(
            List<CommandDefinition> commandDefinitions,
            ISet<string> commandNames,
            string commandName,
            string displayName,
            bool waitForCompletion,
            IReadOnlyList<CommandArgumentDefinition> argumentDefinitions,
            IReadOnlyList<string> outcomePorts)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                return;
            }

            var existingIndex = commandDefinitions.FindIndex(definition =>
                definition != null && string.Equals(definition.Name, commandName, StringComparison.Ordinal));
            if (existingIndex >= 0)
            {
                var existing = commandDefinitions[existingIndex];
                var mergedArguments = new List<CommandArgumentDefinition>(existing.ArgumentDefinitions);
                for (var i = 0; i < argumentDefinitions.Count; i++)
                {
                    var argument = argumentDefinitions[i];
                    if (argument == null)
                    {
                        continue;
                    }

                    var argumentExists = false;
                    for (var j = 0; j < mergedArguments.Count; j++)
                    {
                        if (string.Equals(mergedArguments[j].Key, argument.Key, StringComparison.Ordinal))
                        {
                            argumentExists = true;
                            break;
                        }
                    }

                    if (argumentExists is false)
                    {
                        mergedArguments.Add(argument);
                    }
                }

                var mergedOutcomes = new List<string>(existing.OutcomePorts);
                for (var i = 0; i < outcomePorts.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(outcomePorts[i]) is false && mergedOutcomes.Contains(outcomePorts[i]) is false)
                    {
                        mergedOutcomes.Add(outcomePorts[i]);
                    }
                }

                commandDefinitions[existingIndex] = new CommandDefinition(
                    existing.Name,
                    existing.DisplayName,
                    existing.WaitForCompletion || waitForCompletion,
                    mergedArguments,
                    mergedOutcomes);
                return;
            }

            commandNames.Add(commandName);
            commandDefinitions.Add(new CommandDefinition(
                commandName,
                displayName,
                waitForCompletion,
                argumentDefinitions,
                outcomePorts));
        }

        private static IReadOnlyList<CommandArgumentDefinition> BuildArgumentDefinitions(NodeSchema schema)
        {
            if (schema?.Parameters == null || schema.Parameters.Count == 0)
            {
                return Array.Empty<CommandArgumentDefinition>();
            }

            var definitions = new List<CommandArgumentDefinition>();
            for (var i = 0; i < schema.Parameters.Count; i++)
            {
                var parameter = schema.Parameters[i];
                if (string.IsNullOrWhiteSpace(parameter.Key) ||
                    string.Equals(parameter.Key, "wait", StringComparison.Ordinal))
                {
                    continue;
                }

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

        private static string GetCommandName(AuthoringNode node)
        {
            var fallback = TrimToNull(node.Title) ?? TrimToNull(node.NodeId);
            switch (node.NodeKind)
            {
                case NodeKind.PlayVideo:
                    return "play_video";
                case NodeKind.ShowImage:
                    return "show_image";
                case NodeKind.PlayAudio:
                    return "play_audio";
                default:
                    return fallback;
            }
        }

        private static IReadOnlyList<string> BuildTags(IReadOnlyList<AuthoringParameter> parameters)
        {
            var value = GetString(parameters, "tags");
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            var tags = new List<string>();
            var parts = value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                var tag = TrimToNull(parts[i]);
                if (string.IsNullOrWhiteSpace(tag) is false && tags.Contains(tag) is false)
                {
                    tags.Add(tag);
                }
            }

            return tags;
        }

        private static string GetString(IReadOnlyList<AuthoringParameter> parameters, string key, string fallback = null)
        {
            if (parameters == null || string.IsNullOrWhiteSpace(key))
            {
                return fallback;
            }

            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter != null && string.Equals(parameter.Key, key, StringComparison.Ordinal))
                {
                    return TrimToNull(parameter.Value) ?? fallback;
                }
            }

            return fallback;
        }

        private static bool GetBoolean(IReadOnlyList<AuthoringParameter> parameters, string key, bool fallback = false)
        {
            var value = GetString(parameters, key);
            return string.IsNullOrWhiteSpace(value) ? fallback : bool.TryParse(value, out var result) && result;
        }

        private static float GetFloat(IReadOnlyList<AuthoringParameter> parameters, string key, float fallback = 0f)
        {
            var value = GetString(parameters, key);
            return string.IsNullOrWhiteSpace(value) ||
                   float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) is false
                ? fallback
                : result;
        }

        private static string NormalizeOutcomePortId(string portId, int index)
        {
            portId = TrimToNull(portId);
            return string.IsNullOrWhiteSpace(portId) ? "completed" : portId;
        }

        private static string NormalizeChoiceId(string portId, int index)
        {
            portId = TrimToNull(portId);
            return string.IsNullOrWhiteSpace(portId) ? $"choice_{index + 1}" : portId;
        }

        private static string NormalizeParallelBranchId(string portId, int index)
        {
            portId = TrimToNull(portId);
            return string.IsNullOrWhiteSpace(portId) || string.Equals(portId, "branch", StringComparison.Ordinal)
                ? $"branch_{index + 1}"
                : portId;
        }

        private static bool IsParallelBranchPort(string portId)
        {
            portId = TrimToNull(portId);
            return string.Equals(portId, "branch", StringComparison.Ordinal) ||
                   (string.IsNullOrWhiteSpace(portId) is false && portId.StartsWith("branch_", StringComparison.Ordinal));
        }

        private static string MakeSyntheticChoiceStepId(
            string lineNodeId,
            ISet<string> existingStepIds,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup)
        {
            var baseId = $"{(string.IsNullOrWhiteSpace(lineNodeId) ? "line" : lineNodeId)}_choices";
            var candidate = baseId;
            var index = 2;
            while ((existingStepIds != null && existingStepIds.Contains(candidate)) ||
                   (nodeLookup != null && nodeLookup.ContainsKey(candidate)))
            {
                candidate = $"{baseId}_{index}";
                index++;
            }

            return candidate;
        }

        private static string TrimToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static void ValidateText(string value, string source, ValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                report.AddError(source, "Value cannot be empty.");
            }
        }

        private static string GetPreviewImagePath(AuthoringEpisode episode)
        {
            if (episode?.PreviewImage == null)
            {
                return null;
            }

#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(episode.PreviewImage);
            return string.IsNullOrWhiteSpace(path) ? null : path;
#else
            return null;
#endif
        }

        private static string GetPreviewImagePath(AuthoringVolume volume)
        {
            if (volume?.PreviewImage == null)
            {
                return null;
            }

#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(volume.PreviewImage);
            return string.IsNullOrWhiteSpace(path) ? null : path;
#else
            return null;
#endif
        }

        private sealed class ParallelCompileContext
        {
            public readonly Dictionary<string, ParallelBlockInfo> Blocks = new Dictionary<string, ParallelBlockInfo>(StringComparer.Ordinal);
        }

        private sealed class ParallelBlockInfo
        {
            public ParallelBlockInfo(string parallelNodeId, IReadOnlyList<ParallelBranch> branches)
            {
                ParallelNodeId = parallelNodeId;
                Branches = branches ?? Array.Empty<ParallelBranch>();
            }

            public string ParallelNodeId { get; }

            public IReadOnlyList<ParallelBranch> Branches { get; }
        }
    }
}
