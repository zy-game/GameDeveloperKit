using System;
using System.Collections.Generic;
using System.Globalization;
using GameDeveloperKit.Story;
using UnityEditor;

namespace GameDeveloperKit.StoryEditor
{
    /// <summary>
    /// 将 Story Editor authoring 数据编译为 StoryProgram。
    /// </summary>
    public static partial class StoryProgramCompiler
    {
        public static StoryProgram Compile(StoryAuthoringAsset asset, out StoryValidationReport report)
        {
            report = new StoryValidationReport();
            if (asset == null)
            {
                report.AddError("asset", "Story authoring asset is missing.");
                return null;
            }

            asset.EnsureDefaults();
            ValidateText(asset.StoryId, "story", report);
            ValidateText(asset.Version, "version", report);
            ValidateText(asset.EntryChapterId, "entryChapter", report);

            var chapterLookup = BuildChapterLookup(asset, report);
            if (string.IsNullOrWhiteSpace(asset.EntryChapterId) is false &&
                chapterLookup.ContainsKey(asset.EntryChapterId) is false)
            {
                report.AddError($"story:{asset.StoryId}", $"Entry chapter does not exist. chapter:{asset.EntryChapterId}");
            }

            if (report.HasErrors)
            {
                return null;
            }

            var commandDefinitions = new List<StoryCommandDefinition>();
            var commandNames = new HashSet<string>(StringComparer.Ordinal);
            var chapters = new List<StoryChapter>();
            for (var i = 0; i < asset.Chapters.Count; i++)
            {
                var chapter = asset.Chapters[i];
                if (chapter == null)
                {
                    continue;
                }

                var compiled = CompileChapter(
                    asset.StoryId,
                    chapter,
                    chapterLookup,
                    commandDefinitions,
                    commandNames,
                    report);
                if (compiled != null)
                {
                    chapters.Add(compiled);
                }
            }

            if (report.HasErrors)
            {
                return null;
            }

            return new StoryProgram(
                TrimToNull(asset.StoryId),
                TrimToNull(asset.Version),
                TrimToNull(asset.EntryChapterId),
                chapters,
                new StoryVariableSchema(),
                new StoryCommandSchema(commandDefinitions));
        }

        public static StoryValidationReport Validate(StoryAuthoringAsset asset)
        {
            Compile(asset, out var report);
            return report;
        }

        private static StoryChapter CompileChapter(
            string storyId,
            StoryAuthoringChapter chapter,
            IReadOnlyDictionary<string, StoryAuthoringChapter> chapterLookup,
            List<StoryCommandDefinition> commandDefinitions,
            ISet<string> commandNames,
            StoryValidationReport report)
        {
            var chapterId = TrimToNull(chapter.ChapterId);
            var source = $"story:{storyId}/chapter:{chapterId}";
            ValidateText(chapter.ChapterId, $"{source}/chapterId", report);
            ValidateText(chapter.EntryNodeId, $"{source}/entryNode", report);

            var nodeLookup = BuildNodeLookup(storyId, chapter, report);
            var outgoingEdges = BuildOutgoingEdgeLookup(storyId, chapter, nodeLookup, report);
            if (string.IsNullOrWhiteSpace(chapter.EntryNodeId) is false &&
                nodeLookup.ContainsKey(chapter.EntryNodeId) is false)
            {
                report.AddError(source, $"Entry step does not exist. step:{chapter.EntryNodeId}");
            }

            if (report.HasErrors)
            {
                return null;
            }

            var parallelContext = BuildParallelContext(storyId, chapterId, nodeLookup, outgoingEdges, report);
            var orderedNodes = BuildOrderedRuntimeNodes(chapter, nodeLookup, outgoingEdges);
            var hiddenChoiceNodes = BuildHiddenChoiceNodeIds(orderedNodes, nodeLookup, outgoingEdges);
            var steps = new List<StoryStep>();
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
                        $"story:{storyId}/chapter:{chapterId}/node:{TrimToNull(node.NodeId)}",
                        "Node kind is no longer supported in Story default authoring path.");
                    continue;
                }

                var step = CompileNode(
                    storyId,
                    chapterId,
                    node,
                    chapterLookup,
                    nodeLookup,
                    outgoingEdges,
                    parallelContext,
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
                    var choiceStep = BuildLineChoiceStep(
                        storyId,
                        chapterId,
                        node,
                        outgoingEdges,
                        chapterLookup,
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

            return new StoryChapter(
                chapterId,
                TrimToNull(chapter.Title) ?? chapterId,
                TrimToNull(chapter.EntryNodeId),
                steps);
        }

        private static StoryStep CompileNode(
            string storyId,
            string chapterId,
            StoryAuthoringNode node,
            IReadOnlyDictionary<string, StoryAuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<StoryAuthoringEdge>> outgoingEdges,
            ParallelCompileContext parallelContext,
            List<StoryCommandDefinition> commandDefinitions,
            ISet<string> commandNames,
            StoryValidationReport report,
            ISet<string> existingStepIds)
        {
            var nodeId = TrimToNull(node.NodeId);
            var edges = GetOutgoingEdges(outgoingEdges, nodeId);
            var tags = BuildTags(node.Parameters);

            if (NodeSchemaRegistry.IsDefaultAuthoringNode(node.NodeKind) is false)
            {
                report.AddError(
                    $"story:{storyId}/chapter:{chapterId}/node:{nodeId}",
                    "Node kind is no longer supported in Story default authoring path.");
                return null;
            }

            switch (node.NodeKind)
            {
                case NodeKind.Start:
                    return new StoryStep(nodeId, StoryStepKind.Start, new StoryStepData(tags: tags));
                case NodeKind.End:
                    return new StoryStep(nodeId, StoryStepKind.End, new StoryStepData(tags: tags));
                case NodeKind.Dialogue:
                case NodeKind.Narration:
                    return BuildLineStep(node, nodeId, outgoingEdges, chapterLookup, nodeLookup, report, existingStepIds, tags);
                case NodeKind.Choice:
                    return BuildChoiceStep(storyId, chapterId, node, edges, chapterLookup, nodeLookup, report, tags);
                case NodeKind.PlayVideo:
                case NodeKind.ShowImage:
                case NodeKind.PlayAudio:
                case NodeKind.EmitEvent:
                case NodeKind.MiniGame:
                    return BuildCommandStep(
                        storyId,
                        chapterId,
                        node,
                        edges,
                        chapterLookup,
                        nodeLookup,
                        commandDefinitions,
                        commandNames,
                        report,
                        tags);
                case NodeKind.Parallel:
                    return BuildParallelStep(storyId, chapterId, node, parallelContext, report, tags);
                case NodeKind.Merge:
                    return BuildMergeStep(storyId, chapterId, node, edges, chapterLookup, nodeLookup, parallelContext, report, existingStepIds, tags);
                case NodeKind.JumpChapter:
                    return BuildJumpChapterStep(storyId, chapterId, node, edges, chapterLookup, nodeLookup, report, tags);
                case NodeKind.Wait:
                    return BuildWaitStep(storyId, chapterId, node, edges, chapterLookup, nodeLookup, report, tags);
                default:
                    report.AddError($"story:{storyId}/chapter:{chapterId}/node:{nodeId}", $"Unsupported node kind '{node.NodeKind}'.");
                    return null;
            }
        }

        private static StoryStep BuildLineStep(
            StoryAuthoringNode node,
            string nodeId,
            IReadOnlyDictionary<string, List<StoryAuthoringEdge>> outgoingEdges,
            IReadOnlyDictionary<string, StoryAuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            StoryValidationReport report,
            ISet<string> existingStepIds,
            IReadOnlyList<string> tags)
        {
            var textKey = GetString(node.Parameters, "textKey");
            if (string.IsNullOrWhiteSpace(textKey))
            {
                textKey = TrimToNull(node.Title) ?? nodeId;
            }

            var edges = GetOutgoingEdges(outgoingEdges, nodeId);
            var choiceItemEdges = GetChoiceItemEdges(node, edges, nodeLookup);
            var directEdges = ExcludeChoiceItemEdges(edges, nodeLookup);
            StoryTarget target = null;
            if (choiceItemEdges.Count > 0)
            {
                if (directEdges.Count > 0)
                {
                    report.AddError($"node:{node.NodeId}/port:completed", "Line completed output cannot mix choice items and direct flow targets.");
                    return null;
                }

                target = StoryTarget.Step(
                    FindChapterId(chapterLookup, node),
                    MakeSyntheticChoiceStepId(nodeId, existingStepIds, nodeLookup));
            }
            else
            {
                target = FirstDirectTarget(node, outgoingEdges, chapterLookup, nodeLookup, report);
                if (target == null)
                {
                    target = StoryTarget.StoryEnd();
                }
            }

            return new StoryStep(
                nodeId,
                StoryStepKind.Line,
                new StoryStepData(
                    textKey: textKey,
                    speaker: GetString(node.Parameters, "speaker"),
                    target: target,
                    tags: tags));
        }

        private static StoryStep BuildChoiceStep(
            string storyId,
            string chapterId,
            StoryAuthoringNode node,
            IReadOnlyList<StoryAuthoringEdge> edges,
            IReadOnlyDictionary<string, StoryAuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            StoryValidationReport report,
            IReadOnlyList<string> tags)
        {
            var choices = new List<StoryChoice>();
            var choiceItemEdges = GetChoiceItemEdges(node, edges, nodeLookup);
            if (choiceItemEdges.Count > 0)
            {
                for (var i = 0; i < choiceItemEdges.Count; i++)
                {
                    var edge = choiceItemEdges[i];
                    var optionNode = nodeLookup[edge.TargetNodeId];
                    var selectedEdges = GetChoiceSelectedEdges(optionNode, nodeLookup, chapterLookup, report);
                    if (selectedEdges.Count != 1)
                    {
                        report.AddError(
                            $"story:{storyId}/chapter:{chapterId}/node:{optionNode.NodeId}/port:selected",
                            "Choice item node must have exactly one selected target.");
                        continue;
                    }

                    var target = BuildTarget(
                        storyId,
                        chapterId,
                        selectedEdges[0],
                        chapterLookup,
                        nodeLookup,
                        report,
                        $"story:{storyId}/chapter:{chapterId}/node:{optionNode.NodeId}/port:selected");
                    if (target == null)
                    {
                        continue;
                    }

                    var textKey = GetString(optionNode.Parameters, "textKey") ?? TrimToNull(optionNode.Title) ?? optionNode.NodeId;
                    choices.Add(new StoryChoice(
                        optionNode.NodeId,
                        textKey,
                        CombineConditions(BuildCondition(edge.Conditions), BuildCondition(selectedEdges[0].Conditions)),
                        target,
                        BuildTags(optionNode.Parameters)));
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
                    var target = BuildTarget(
                        storyId,
                        chapterId,
                        edge,
                        chapterLookup,
                        nodeLookup,
                        report,
                        $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/choice:{choiceId}");
                    if (target == null)
                    {
                        continue;
                    }

                    choices.Add(new StoryChoice(choiceId, TrimToNull(edge.FromPortLabel) ?? choiceId, BuildCondition(edge.Conditions), target));
                }
            }

            if (choices.Count == 0)
            {
                report.AddError($"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}", "Choice node has no outgoing choices.");
                return null;
            }

            return new StoryStep(
                TrimToNull(node.NodeId),
                StoryStepKind.Choice,
                new StoryStepData(choices: choices, tags: tags));
        }

        private static StoryStep BuildLineChoiceStep(
            string storyId,
            string chapterId,
            StoryAuthoringNode lineNode,
            IReadOnlyDictionary<string, List<StoryAuthoringEdge>> outgoingEdges,
            IReadOnlyDictionary<string, StoryAuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            StoryValidationReport report,
            ISet<string> existingStepIds,
            IReadOnlyList<string> tags)
        {
            var choiceItemEdges = GetChoiceItemEdges(lineNode, GetOutgoingEdges(outgoingEdges, lineNode.NodeId), nodeLookup);
            if (choiceItemEdges.Count == 0)
            {
                return null;
            }

            var choiceNode = new StoryAuthoringNode
            {
                NodeId = MakeSyntheticChoiceStepId(lineNode.NodeId, existingStepIds, nodeLookup),
                Title = lineNode.Title,
                NodeKind = lineNode.NodeKind
            };
            return BuildChoiceStep(storyId, chapterId, choiceNode, choiceItemEdges, chapterLookup, nodeLookup, report, tags);
        }

        private static StoryStep BuildParallelStep(
            string storyId,
            string chapterId,
            StoryAuthoringNode node,
            ParallelCompileContext parallelContext,
            StoryValidationReport report,
            IReadOnlyList<string> tags)
        {
            var nodeId = TrimToNull(node.NodeId);
            if (parallelContext == null ||
                parallelContext.Blocks.TryGetValue(nodeId, out var block) is false)
            {
                report.AddError($"story:{storyId}/chapter:{chapterId}/node:{nodeId}/port:branch", "Parallel node must define a valid branch block.");
                return null;
            }

            return new StoryStep(
                nodeId,
                StoryStepKind.Parallel,
                new StoryStepData(
                    tags: tags,
                    branches: block.Branches,
                    mergePolicy: StoryMergePolicy.All));
        }

        private static StoryStep BuildMergeStep(
            string storyId,
            string chapterId,
            StoryAuthoringNode node,
            IReadOnlyList<StoryAuthoringEdge> edges,
            IReadOnlyDictionary<string, StoryAuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            ParallelCompileContext parallelContext,
            StoryValidationReport report,
            ISet<string> existingStepIds,
            IReadOnlyList<string> tags)
        {
            var nodeId = TrimToNull(node.NodeId);
            if (parallelContext == null ||
                parallelContext.MergeOwners.TryGetValue(nodeId, out var parallelStepId) is false)
            {
                report.AddError($"story:{storyId}/chapter:{chapterId}/node:{nodeId}", "Merge node must belong to exactly one Parallel block.");
                return null;
            }

            StoryTarget target = null;
            var choiceItemEdges = GetChoiceItemEdges(node, edges, nodeLookup);
            var directEdges = ExcludeChoiceItemEdges(edges, nodeLookup);
            if (choiceItemEdges.Count > 0)
            {
                if (directEdges.Count > 0)
                {
                    report.AddError(
                        $"story:{storyId}/chapter:{chapterId}/node:{nodeId}/port:completed",
                        "Merge completed port cannot connect choices and ordinary targets at the same time.");
                }

                target = StoryTarget.Step(chapterId, MakeSyntheticChoiceStepId(nodeId, existingStepIds, nodeLookup));
            }
            else if (directEdges.Count == 0)
            {
                target = null;
            }
            else
            {
                if (directEdges.Count > 1)
                {
                    report.AddError($"story:{storyId}/chapter:{chapterId}/node:{nodeId}/port:completed", "Merge node must have only one completed target.");
                }

                target = BuildTarget(
                    storyId,
                    chapterId,
                    directEdges[0],
                    chapterLookup,
                    nodeLookup,
                    report,
                    $"story:{storyId}/chapter:{chapterId}/node:{nodeId}/port:completed");
                if (target == null)
                {
                    return null;
                }
            }

            return new StoryStep(
                    nodeId,
                    StoryStepKind.Merge,
                    new StoryStepData(
                        target: target,
                        tags: tags,
                        mergePolicy: StoryMergePolicy.All,
                        parallelStepId: parallelStepId));
        }

        private static StoryStep BuildBranchStep(
            string storyId,
            string chapterId,
            StoryAuthoringNode node,
            IReadOnlyList<StoryAuthoringEdge> edges,
            IReadOnlyDictionary<string, StoryAuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            StoryValidationReport report,
            IReadOnlyList<string> tags)
        {
            var target = FirstOutgoingTarget(storyId, chapterId, node, edges, chapterLookup, nodeLookup, report, "branch");
            var condition = edges.Count > 0 ? BuildCondition(edges[0].Conditions) : null;
            if (target == null || condition == null)
            {
                if (condition == null)
                {
                    report.AddError($"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}", "Branch condition cannot be empty.");
                }

                return null;
            }

            return new StoryStep(
                TrimToNull(node.NodeId),
                StoryStepKind.Branch,
                new StoryStepData(condition: condition, target: target, tags: tags));
        }

        private static StoryStep BuildJumpChapterStep(
            string storyId,
            string chapterId,
            StoryAuthoringNode node,
            IReadOnlyList<StoryAuthoringEdge> edges,
            IReadOnlyDictionary<string, StoryAuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            StoryValidationReport report,
            IReadOnlyList<string> tags)
        {
            var targetChapterId = GetString(node.Parameters, "chapterId");
            StoryTarget target = null;
            if (string.IsNullOrWhiteSpace(targetChapterId) is false)
            {
                if (chapterLookup.ContainsKey(targetChapterId))
                {
                    target = StoryTarget.Chapter(targetChapterId);
                }
                else
                {
                    report.AddError($"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/field:chapterId", $"Target chapter does not exist. chapter:{targetChapterId}");
                }
            }
            else
            {
                target = FirstOutgoingTarget(storyId, chapterId, node, edges, chapterLookup, nodeLookup, report, "jump");
            }

            return target == null
                ? null
                : new StoryStep(TrimToNull(node.NodeId), StoryStepKind.Jump, new StoryStepData(target: target, tags: tags));
        }

        private static StoryStep BuildWaitStep(
            string storyId,
            string chapterId,
            StoryAuthoringNode node,
            IReadOnlyList<StoryAuthoringEdge> edges,
            IReadOnlyDictionary<string, StoryAuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            StoryValidationReport report,
            IReadOnlyList<string> tags)
        {
            var source = $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/field:duration";
            var durationValue = GetString(node.Parameters, "duration");
            if (string.IsNullOrWhiteSpace(durationValue) is false &&
                float.TryParse(durationValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _) is false)
            {
                report.AddError(source, "Wait duration must be a number.");
                return null;
            }

            var waitSeconds = GetFloat(node.Parameters, "duration");
            if (waitSeconds < 0f)
            {
                report.AddError(source, "Wait duration cannot be negative.");
                return null;
            }

            return new StoryStep(
                TrimToNull(node.NodeId),
                StoryStepKind.Wait,
                new StoryStepData(
                    target: FirstOutgoingTarget(storyId, chapterId, node, edges, chapterLookup, nodeLookup, report, "wait") ?? StoryTarget.StoryEnd(),
                    waitSeconds: waitSeconds,
                    tags: tags));
        }

        private static StoryStep BuildRedirectStep(
            string storyId,
            string chapterId,
            StoryAuthoringNode node,
            IReadOnlyList<StoryAuthoringEdge> edges,
            IReadOnlyDictionary<string, StoryAuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            StoryValidationReport report,
            IReadOnlyList<string> tags)
        {
            var target = FirstOutgoingTarget(storyId, chapterId, node, edges, chapterLookup, nodeLookup, report, "routing");
            return target == null
                ? new StoryStep(TrimToNull(node.NodeId), StoryStepKind.Start, new StoryStepData(tags: tags))
                : new StoryStep(TrimToNull(node.NodeId), StoryStepKind.Jump, new StoryStepData(target: target, tags: tags));
        }

        private static StoryStep BuildCommandStep(
            string storyId,
            string chapterId,
            StoryAuthoringNode node,
            IReadOnlyList<StoryAuthoringEdge> edges,
            IReadOnlyDictionary<string, StoryAuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            List<StoryCommandDefinition> commandDefinitions,
            ISet<string> commandNames,
            StoryValidationReport report,
            IReadOnlyList<string> tags)
        {
            var schema = NodeSchemaRegistry.Get(node.NodeKind);
            var commandName = GetCommandName(node);
            var arguments = BuildArguments(storyId, chapterId, node, schema, report);
            var argumentDefinitions = BuildArgumentDefinitions(schema);
            var outcomePorts = BuildOutcomePorts(edges);
            var waitForCompletion = GetBoolean(node.Parameters, "wait") ||
                                    outcomePorts.Count > 0 ||
                                    node.NodeKind == NodeKind.PlayVideo ||
                                    node.NodeKind == NodeKind.MiniGame;

            RegisterCommandSchema(
                commandDefinitions,
                commandNames,
                commandName,
                TrimToNull(node.Title) ?? commandName,
                waitForCompletion,
                argumentDefinitions,
                outcomePorts);

            var outcomeTargets = BuildOutcomeTargets(storyId, chapterId, node, edges, chapterLookup, nodeLookup, report);
            var command = new StoryCommand(
                TrimToNull(node.NodeId),
                commandName,
                new StoryArgumentBag(arguments),
                waitForCompletion,
                outcomePorts,
                outcomeTargets);

            return new StoryStep(
                TrimToNull(node.NodeId),
                StoryStepKind.Command,
                new StoryStepData(
                    command: command,
                    target: FirstOutcomeTarget(outcomeTargets) ?? (edges.Count == 0 ? StoryTarget.StoryEnd() : null),
                    tags: tags));
        }

        private static Dictionary<string, StoryValue> BuildArguments(
            string storyId,
            string chapterId,
            StoryAuthoringNode node,
            NodeParameterSchema schema,
            StoryValidationReport report)
        {
            var arguments = new Dictionary<string, StoryValue>(StringComparer.Ordinal);
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
                var source = $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/field:{parameter.Key}";
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (parameter.Required)
                    {
                        report.AddError(source, "Required command field is missing.");
                    }

                    continue;
                }

                if (TryBuildArgumentValue(parameter, value, source, report, out var storyValue))
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
            StoryValidationReport report,
            out StoryValue storyValue)
        {
            switch (parameter.ValueType)
            {
                case ParameterValueType.Number:
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numberValue))
                    {
                        storyValue = StoryValue.FromNumber(numberValue);
                        return true;
                    }

                    report.AddError(source, "Command field must be a number.");
                    storyValue = StoryValue.Null;
                    return false;
                case ParameterValueType.Boolean:
                    if (bool.TryParse(value, out var booleanValue))
                    {
                        storyValue = StoryValue.FromBoolean(booleanValue);
                        return true;
                    }

                    report.AddError(source, "Command field must be a boolean.");
                    storyValue = StoryValue.Null;
                    return false;
                case ParameterValueType.AssetReference:
                    if (IsProjectAssetReference(value) is false)
                    {
                        report.AddWarning(source, "Asset reference uses a manual string fallback.");
                    }

                    storyValue = StoryValue.FromString(value);
                    return true;
                default:
                    storyValue = StoryValue.FromString(value);
                    return true;
            }
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

        private static IReadOnlyDictionary<string, StoryTarget> BuildOutcomeTargets(
            string storyId,
            string chapterId,
            StoryAuthoringNode node,
            IReadOnlyList<StoryAuthoringEdge> edges,
            IReadOnlyDictionary<string, StoryAuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            StoryValidationReport report)
        {
            var targets = new Dictionary<string, StoryTarget>(StringComparer.Ordinal);
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
                    chapterId,
                    edge,
                    chapterLookup,
                    nodeLookup,
                    report,
                    $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/outcome:{portId}");
                if (target == null)
                {
                    continue;
                }

                if (targets.ContainsKey(portId))
                {
                    report.AddError($"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/outcome:{portId}", "Duplicate outcome port.");
                    continue;
                }

                targets[portId] = target;
            }

            return targets;
        }

        private static StoryTarget FirstDirectTarget(
            StoryAuthoringNode node,
            IReadOnlyDictionary<string, List<StoryAuthoringEdge>> outgoingEdges,
            IReadOnlyDictionary<string, StoryAuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            StoryValidationReport report)
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
                    FindChapterId(chapterLookup, node),
                    edge,
                    chapterLookup,
                    nodeLookup,
                    report,
                    $"node:{node.NodeId}/port:{edge.FromPortId}");
            }

            return null;
        }

        private static StoryTarget FirstOutgoingTarget(
            string storyId,
            string chapterId,
            StoryAuthoringNode node,
            IReadOnlyList<StoryAuthoringEdge> edges,
            IReadOnlyDictionary<string, StoryAuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            StoryValidationReport report,
            string label)
        {
            for (var i = 0; i < edges.Count; i++)
            {
                var target = BuildTarget(
                    storyId,
                    chapterId,
                    edges[i],
                    chapterLookup,
                    nodeLookup,
                    report,
                    $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/{label}:{i}");
                if (target != null)
                {
                    return target;
                }
            }

            return null;
        }

        private static StoryTarget BuildTarget(
            string storyId,
            string currentChapterId,
            StoryAuthoringEdge edge,
            IReadOnlyDictionary<string, StoryAuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            StoryValidationReport report,
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
                        var targetChapterId = TrimToNull(edge.TargetChapterId) ?? currentChapterId;
                        var targetNodeId = TrimToNull(edge.TargetNodeId);
                        if (string.IsNullOrWhiteSpace(targetChapterId) || string.IsNullOrWhiteSpace(targetNodeId))
                        {
                            report.AddError(source, "Step target is invalid.");
                            return null;
                        }

                        if (chapterLookup.ContainsKey(targetChapterId) is false)
                        {
                            report.AddError(source, $"Target chapter does not exist. chapter:{targetChapterId}");
                            return null;
                        }

                        if (nodeLookup.ContainsKey(targetNodeId) is false && string.Equals(targetChapterId, currentChapterId, StringComparison.Ordinal))
                        {
                            report.AddError(source, $"Target step does not exist. chapter:{targetChapterId} step:{targetNodeId}");
                            return null;
                        }

                        return StoryTarget.Step(targetChapterId, targetNodeId);
                    }
                case TransitionTargetKind.Chapter:
                    {
                        var targetChapterId = TrimToNull(edge.TargetChapterId);
                        if (string.IsNullOrWhiteSpace(targetChapterId) || chapterLookup.ContainsKey(targetChapterId) is false)
                        {
                            report.AddError(source, $"Target chapter does not exist. chapter:{targetChapterId}");
                            return null;
                        }

                        return StoryTarget.Chapter(targetChapterId);
                    }
                case TransitionTargetKind.StoryEnd:
                    return StoryTarget.StoryEnd();
                default:
                    report.AddError(source, $"Unsupported target kind '{edge.TargetKind}'.");
                    return null;
            }
        }

        private static IReadOnlyDictionary<string, StoryAuthoringChapter> BuildChapterLookup(StoryAuthoringAsset asset, StoryValidationReport report)
        {
            var chapters = new Dictionary<string, StoryAuthoringChapter>(StringComparer.Ordinal);
            for (var i = 0; i < asset.Chapters.Count; i++)
            {
                var chapter = asset.Chapters[i];
                if (chapter == null)
                {
                    report.AddError($"story:{asset.StoryId}/chapter[{i}]", "Chapter cannot be null.");
                    continue;
                }

                var chapterId = TrimToNull(chapter.ChapterId);
                if (string.IsNullOrWhiteSpace(chapterId))
                {
                    report.AddError($"story:{asset.StoryId}/chapter[{i}]", "Chapter id cannot be empty.");
                    continue;
                }

                if (chapters.ContainsKey(chapterId))
                {
                    report.AddError($"story:{asset.StoryId}/chapter:{chapterId}", "Duplicate chapter id.");
                    continue;
                }

                chapters.Add(chapterId, chapter);
            }

            return chapters;
        }

        private static IReadOnlyDictionary<string, StoryAuthoringNode> BuildNodeLookup(
            string storyId,
            StoryAuthoringChapter chapter,
            StoryValidationReport report)
        {
            var nodes = new Dictionary<string, StoryAuthoringNode>(StringComparer.Ordinal);
            for (var i = 0; i < chapter.Nodes.Count; i++)
            {
                var node = chapter.Nodes[i];
                if (node == null)
                {
                    report.AddError($"story:{storyId}/chapter:{chapter.ChapterId}/node[{i}]", "Node cannot be null.");
                    continue;
                }

                var nodeId = TrimToNull(node.NodeId);
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    report.AddError($"story:{storyId}/chapter:{chapter.ChapterId}/node[{i}]", "Node id cannot be empty.");
                    continue;
                }

                if (nodes.ContainsKey(nodeId))
                {
                    report.AddError($"story:{storyId}/chapter:{chapter.ChapterId}/node:{nodeId}", "Duplicate node id.");
                    continue;
                }

                nodes.Add(nodeId, node);
            }

            return nodes;
        }

        private static IReadOnlyDictionary<string, List<StoryAuthoringEdge>> BuildOutgoingEdgeLookup(
            string storyId,
            StoryAuthoringChapter chapter,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            StoryValidationReport report)
        {
            var lookup = new Dictionary<string, List<StoryAuthoringEdge>>(StringComparer.Ordinal);
            for (var i = 0; i < chapter.Edges.Count; i++)
            {
                var edge = chapter.Edges[i];
                if (edge == null)
                {
                    report.AddError($"story:{storyId}/chapter:{chapter.ChapterId}/edge[{i}]", "Edge cannot be null.");
                    continue;
                }

                var fromNodeId = TrimToNull(edge.FromNodeId);
                if (string.IsNullOrWhiteSpace(fromNodeId))
                {
                    report.AddError($"story:{storyId}/chapter:{chapter.ChapterId}/edge:{edge.EdgeId}", "Edge from node id cannot be empty.");
                    continue;
                }

                if (nodeLookup.ContainsKey(fromNodeId) is false)
                {
                    report.AddError($"story:{storyId}/chapter:{chapter.ChapterId}/edge:{edge.EdgeId}", "Edge source node does not exist.");
                    continue;
                }

                if (!lookup.TryGetValue(fromNodeId, out var edges))
                {
                    edges = new List<StoryAuthoringEdge>();
                    lookup.Add(fromNodeId, edges);
                }

                edges.Add(edge);
            }

            return lookup;
        }

        private static ParallelCompileContext BuildParallelContext(
            string storyId,
            string chapterId,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<StoryAuthoringEdge>> outgoingEdges,
            StoryValidationReport report)
        {
            var context = new ParallelCompileContext();
            foreach (var pair in nodeLookup)
            {
                var node = pair.Value;
                if (node == null || node.NodeKind != NodeKind.Parallel)
                {
                    continue;
                }

                var block = BuildParallelBlock(storyId, chapterId, node, nodeLookup, outgoingEdges, context, report);
                if (block != null)
                {
                    context.Blocks[node.NodeId] = block;
                }
            }

            return context;
        }

        private static ParallelBlockInfo BuildParallelBlock(
            string storyId,
            string chapterId,
            StoryAuthoringNode parallelNode,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<StoryAuthoringEdge>> outgoingEdges,
            ParallelCompileContext context,
            StoryValidationReport report)
        {
            var branches = new List<StoryParallelBranch>();
            var branchIds = new HashSet<string>(StringComparer.Ordinal);
            var waitNodeId = string.Empty;
            var edges = GetOutgoingEdges(outgoingEdges, parallelNode.NodeId);

            if (edges.Count < 2)
            {
                report.AddError(
                    $"story:{storyId}/chapter:{chapterId}/node:{parallelNode.NodeId}/port:branch",
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

                if (IsParallelBranchPort(edge.FromPortId) is false)
                {
                    report.AddError(
                        $"story:{storyId}/chapter:{chapterId}/node:{parallelNode.NodeId}/port:{edge.FromPortId}",
                        "Parallel output must use a branch port.");
                    continue;
                }

                var branchId = NormalizeParallelBranchId(edge.FromPortId, i);
                if (branchIds.Add(branchId) is false)
                {
                    report.AddError(
                        $"story:{storyId}/chapter:{chapterId}/node:{parallelNode.NodeId}/port:{edge.FromPortId}",
                        "Parallel branch port id must be unique.");
                    continue;
                }

                if (edge.TargetKind != TransitionTargetKind.Node ||
                    string.IsNullOrWhiteSpace(edge.TargetNodeId) ||
                    (string.IsNullOrWhiteSpace(edge.TargetChapterId) is false &&
                     string.Equals(edge.TargetChapterId, chapterId, StringComparison.Ordinal) is false) ||
                    nodeLookup.ContainsKey(edge.TargetNodeId) is false)
                {
                    report.AddError(
                        $"story:{storyId}/chapter:{chapterId}/node:{parallelNode.NodeId}/port:{edge.FromPortId}",
                        "Parallel branch must target a node in the same chapter.");
                    continue;
                }

                var result = ResolveParallelBranchMerge(
                    storyId,
                    chapterId,
                    parallelNode.NodeId,
                    branchId,
                    edge.TargetNodeId,
                    nodeLookup,
                    outgoingEdges,
                    new HashSet<string>(StringComparer.Ordinal),
                    report);
                if (result.IsValid is false)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(result.MergeNodeId) is false &&
                    string.IsNullOrWhiteSpace(waitNodeId))
                {
                    waitNodeId = result.MergeNodeId;
                }
                else if (string.IsNullOrWhiteSpace(result.MergeNodeId) is false &&
                         string.Equals(waitNodeId, result.MergeNodeId, StringComparison.Ordinal) is false)
                {
                    report.AddError(
                        $"story:{storyId}/chapter:{chapterId}/node:{parallelNode.NodeId}/port:{edge.FromPortId}",
                        $"Parallel branch must wait on the same Merge node. expected:{waitNodeId} actual:{result.MergeNodeId}");
                    continue;
                }

                branches.Add(new StoryParallelBranch(
                    branchId,
                    TrimToNull(edge.FromPortLabel) ?? branchId,
                    StoryTarget.Step(chapterId, edge.TargetNodeId)));
            }

            if (branches.Count < 2)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(waitNodeId))
            {
                return new ParallelBlockInfo(parallelNode.NodeId, null, branches);
            }

            if (context.MergeOwners.TryGetValue(waitNodeId, out var owner) &&
                string.Equals(owner, parallelNode.NodeId, StringComparison.Ordinal) is false)
            {
                report.AddError(
                    $"story:{storyId}/chapter:{chapterId}/node:{waitNodeId}",
                    $"Merge node cannot belong to multiple Parallel blocks. first:{owner} second:{parallelNode.NodeId}");
                return null;
            }

            context.MergeOwners[waitNodeId] = parallelNode.NodeId;
            return new ParallelBlockInfo(parallelNode.NodeId, waitNodeId, branches);
        }

        private static ParallelBranchMergeResult ResolveParallelBranchMerge(
            string storyId,
            string chapterId,
            string parallelNodeId,
            string branchId,
            string nodeId,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<StoryAuthoringEdge>> outgoingEdges,
            ISet<string> visited,
            StoryValidationReport report)
        {
            nodeId = TrimToNull(nodeId);
            if (string.IsNullOrWhiteSpace(nodeId) || nodeLookup.TryGetValue(nodeId, out var node) is false)
            {
                report.AddError(
                    $"story:{storyId}/chapter:{chapterId}/node:{parallelNodeId}/port:{branchId}",
                    "Parallel branch target does not exist.");
                return ParallelBranchMergeResult.Invalid;
            }

            if (visited.Add(nodeId) is false)
            {
                report.AddError(
                    $"story:{storyId}/chapter:{chapterId}/node:{nodeId}",
                    "Parallel branch contains a cycle before Merge.");
                return ParallelBranchMergeResult.Invalid;
            }

            switch (node.NodeKind)
            {
                case NodeKind.Merge:
                    return new ParallelBranchMergeResult(node.NodeId);
                case NodeKind.End:
                    return ParallelBranchMergeResult.NaturalEnd;
                case NodeKind.Parallel:
                    report.AddError(
                        $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}",
                        "Nested Parallel blocks are not supported.");
                    return ParallelBranchMergeResult.Invalid;
                case NodeKind.Choice:
                    return ParallelBranchMergeResult.NaturalEnd;
                case NodeKind.JumpChapter:
                    report.AddError(
                        $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}",
                        "Parallel branch cannot jump to another chapter before Merge.");
                    return ParallelBranchMergeResult.Invalid;
            }

            var edges = GetOutgoingEdges(outgoingEdges, node.NodeId);
            if (edges.Count == 0)
            {
                return ParallelBranchMergeResult.NaturalEnd;
            }

            var hasErrors = false;
            var mergeNodeId = string.Empty;
            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge == null)
                {
                    continue;
                }

                if (edge.TargetKind == TransitionTargetKind.StoryEnd)
                {
                    report.AddError(
                        $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/port:{edge.FromPortId}",
                        "Parallel branch cannot end the story before Merge.");
                    hasErrors = true;
                    continue;
                }

                if (edge.TargetKind == TransitionTargetKind.Chapter ||
                    (string.IsNullOrWhiteSpace(edge.TargetChapterId) is false &&
                     string.Equals(edge.TargetChapterId, chapterId, StringComparison.Ordinal) is false))
                {
                    report.AddError(
                        $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/port:{edge.FromPortId}",
                        "Parallel branch must stay in the same chapter.");
                    hasErrors = true;
                    continue;
                }

                if (edge.TargetKind != TransitionTargetKind.Node)
                {
                    report.AddError(
                        $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/port:{edge.FromPortId}",
                        "Parallel branch target is invalid.");
                    hasErrors = true;
                    continue;
                }

                var result = ResolveParallelBranchMerge(
                    storyId,
                    chapterId,
                    parallelNodeId,
                    branchId,
                    edge.TargetNodeId,
                    nodeLookup,
                    outgoingEdges,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    report);
                if (result.IsValid is false)
                {
                    hasErrors = true;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(result.MergeNodeId))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(mergeNodeId))
                {
                    mergeNodeId = result.MergeNodeId;
                }
                else if (string.Equals(mergeNodeId, result.MergeNodeId, StringComparison.Ordinal) is false)
                {
                    report.AddError(
                        $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/port:{edge.FromPortId}",
                        "All paths in a Parallel branch must reach the same Merge node.");
                    hasErrors = true;
                }
            }

            return hasErrors && string.IsNullOrWhiteSpace(mergeNodeId)
                ? ParallelBranchMergeResult.Invalid
                : new ParallelBranchMergeResult(mergeNodeId);
        }

        private static List<StoryAuthoringNode> BuildOrderedRuntimeNodes(
            StoryAuthoringChapter chapter,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<StoryAuthoringEdge>> outgoingEdges)
        {
            var ordered = new List<StoryAuthoringNode>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            VisitNode(chapter.EntryNodeId, nodeLookup, outgoingEdges, visited, ordered);

            for (var i = 0; i < chapter.Nodes.Count; i++)
            {
                var node = chapter.Nodes[i];
                if (node != null && visited.Contains(node.NodeId) is false)
                {
                    VisitNode(node.NodeId, nodeLookup, outgoingEdges, visited, ordered);
                }
            }

            return ordered;
        }

        private static void VisitNode(
            string nodeId,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<StoryAuthoringEdge>> outgoingEdges,
            ISet<string> visited,
            List<StoryAuthoringNode> ordered)
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
            IReadOnlyList<StoryAuthoringNode> orderedNodes,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<StoryAuthoringEdge>> outgoingEdges)
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

        private static List<StoryAuthoringEdge> ExcludeChoiceItemEdges(
            IReadOnlyList<StoryAuthoringEdge> edges,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup)
        {
            var result = new List<StoryAuthoringEdge>();
            if (edges == null)
            {
                return result;
            }

            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge != null && IsChoiceItemEdge(edge, nodeLookup) is false)
                {
                    result.Add(edge);
                }
            }

            return result;
        }

        private static List<StoryAuthoringEdge> GetChoiceItemEdges(
            StoryAuthoringNode node,
            IReadOnlyList<StoryAuthoringEdge> edges,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup)
        {
            var result = new List<StoryAuthoringEdge>();
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

        private static bool IsChoiceItemEdge(StoryAuthoringEdge edge, IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup)
        {
            return edge != null &&
                   edge.TargetKind == TransitionTargetKind.Node &&
                   string.Equals(edge.FromPortId, "completed", StringComparison.Ordinal) &&
                   string.IsNullOrWhiteSpace(edge.TargetNodeId) is false &&
                   nodeLookup.TryGetValue(edge.TargetNodeId, out var target) &&
                   target.NodeKind == NodeKind.Choice;
        }

        private static List<StoryAuthoringEdge> GetChoiceSelectedEdges(
            StoryAuthoringNode node,
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, StoryAuthoringChapter> chapterLookup,
            StoryValidationReport report)
        {
            var result = new List<StoryAuthoringEdge>();
            var chapterId = FindChapterId(chapterLookup, node);
            if (string.IsNullOrWhiteSpace(chapterId) || chapterLookup.TryGetValue(chapterId, out var chapter) is false)
            {
                return result;
            }

            for (var i = 0; i < chapter.Edges.Count; i++)
            {
                var edge = chapter.Edges[i];
                if (edge != null &&
                    string.Equals(edge.FromNodeId, node.NodeId, StringComparison.Ordinal) &&
                    string.Equals(edge.FromPortId, "selected", StringComparison.Ordinal))
                {
                    result.Add(edge);
                }
            }

            return result;
        }

        private static string FindChapterId(IReadOnlyDictionary<string, StoryAuthoringChapter> chapterLookup, StoryAuthoringNode node)
        {
            if (chapterLookup == null || node == null)
            {
                return null;
            }

            foreach (var pair in chapterLookup)
            {
                if (pair.Value != null && pair.Value.Nodes.Contains(node))
                {
                    return pair.Key;
                }
            }

            return null;
        }

        private static List<StoryAuthoringEdge> GetOutgoingEdges(
            IReadOnlyDictionary<string, List<StoryAuthoringEdge>> outgoingEdges,
            string nodeId)
        {
            return outgoingEdges != null && outgoingEdges.TryGetValue(nodeId, out var edges)
                ? edges
                : new List<StoryAuthoringEdge>();
        }

        private static StoryExpression BuildCondition(IReadOnlyList<StoryAuthoringCondition> conditions)
        {
            if (conditions == null || conditions.Count == 0)
            {
                return null;
            }

            var expressions = new List<StoryExpression>();
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

            return expressions.Count == 1 ? expressions[0] : StoryExpression.CreateAnd(expressions.ToArray());
        }

        private static StoryExpression BuildCondition(StoryAuthoringCondition condition)
        {
            if (condition == null || string.IsNullOrWhiteSpace(condition.ConditionId))
            {
                return null;
            }

            var arguments = new List<StoryExpression>();
            for (var i = 0; i < condition.Parameters.Count; i++)
            {
                var parameter = condition.Parameters[i];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Key))
                {
                    continue;
                }

                arguments.Add(StoryExpression.FromLiteral(StoryValue.FromString($"{parameter.Key}={parameter.Value}")));
            }

            return arguments.Count == 0
                ? StoryExpression.FromFunction(condition.ConditionId)
                : StoryExpression.FromFunction(condition.ConditionId, arguments.ToArray());
        }

        private static IReadOnlyDictionary<string, StoryTarget> BuildNoTargets()
        {
            return new Dictionary<string, StoryTarget>(0, StringComparer.Ordinal);
        }

        private static StoryTarget FirstOutcomeTarget(IReadOnlyDictionary<string, StoryTarget> outcomeTargets)
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

        private static StoryExpression CombineConditions(StoryExpression left, StoryExpression right)
        {
            if (left == null)
            {
                return right;
            }

            return right == null ? left : StoryExpression.CreateAnd(left, right);
        }

        private static bool IsLineNode(NodeKind kind)
        {
            return kind == NodeKind.Dialogue || kind == NodeKind.Narration;
        }

        private static bool CanOwnChoiceItems(NodeKind kind)
        {
            return IsLineNode(kind) || kind == NodeKind.Merge;
        }

        private static IReadOnlyList<string> BuildOutcomePorts(IReadOnlyList<StoryAuthoringEdge> edges)
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
            List<StoryCommandDefinition> commandDefinitions,
            ISet<string> commandNames,
            string commandName,
            string displayName,
            bool waitForCompletion,
            IReadOnlyList<StoryCommandArgumentDefinition> argumentDefinitions,
            IReadOnlyList<string> outcomePorts)
        {
            if (string.IsNullOrWhiteSpace(commandName) || commandNames.Contains(commandName))
            {
                return;
            }

            commandNames.Add(commandName);
            commandDefinitions.Add(new StoryCommandDefinition(
                commandName,
                displayName,
                waitForCompletion,
                argumentDefinitions,
                outcomePorts));
        }

        private static IReadOnlyList<StoryCommandArgumentDefinition> BuildArgumentDefinitions(NodeParameterSchema schema)
        {
            if (schema?.Parameters == null || schema.Parameters.Count == 0)
            {
                return Array.Empty<StoryCommandArgumentDefinition>();
            }

            var definitions = new List<StoryCommandArgumentDefinition>();
            for (var i = 0; i < schema.Parameters.Count; i++)
            {
                var parameter = schema.Parameters[i];
                if (string.IsNullOrWhiteSpace(parameter.Key) ||
                    string.Equals(parameter.Key, "wait", StringComparison.Ordinal))
                {
                    continue;
                }

                definitions.Add(new StoryCommandArgumentDefinition(
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

        private static string GetCommandName(StoryAuthoringNode node)
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
                case NodeKind.EmitEvent:
                    return "emit_event";
                case NodeKind.MiniGame:
                    return "mini_game";
                default:
                    return fallback;
            }
        }

        private static IReadOnlyList<string> BuildTags(IReadOnlyList<StoryAuthoringParameter> parameters)
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

        private static string GetString(IReadOnlyList<StoryAuthoringParameter> parameters, string key, string fallback = null)
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

        private static bool GetBoolean(IReadOnlyList<StoryAuthoringParameter> parameters, string key, bool fallback = false)
        {
            var value = GetString(parameters, key);
            return string.IsNullOrWhiteSpace(value) ? fallback : bool.TryParse(value, out var result) && result;
        }

        private static float GetFloat(IReadOnlyList<StoryAuthoringParameter> parameters, string key, float fallback = 0f)
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
            IReadOnlyDictionary<string, StoryAuthoringNode> nodeLookup)
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

        private static void ValidateText(string value, string source, StoryValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                report.AddError(source, "Value cannot be empty.");
            }
        }

        private sealed class ParallelCompileContext
        {
            public readonly Dictionary<string, ParallelBlockInfo> Blocks = new Dictionary<string, ParallelBlockInfo>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> MergeOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        private sealed class ParallelBlockInfo
        {
            public ParallelBlockInfo(string parallelNodeId, string mergeNodeId, IReadOnlyList<StoryParallelBranch> branches)
            {
                ParallelNodeId = parallelNodeId;
                MergeNodeId = mergeNodeId;
                Branches = branches ?? Array.Empty<StoryParallelBranch>();
            }

            public string ParallelNodeId { get; }

            public string MergeNodeId { get; }

            public IReadOnlyList<StoryParallelBranch> Branches { get; }
        }

        private readonly struct ParallelBranchMergeResult
        {
            public static readonly ParallelBranchMergeResult Invalid = new ParallelBranchMergeResult(null, false);
            public static readonly ParallelBranchMergeResult NaturalEnd = new ParallelBranchMergeResult(null, true);

            public ParallelBranchMergeResult(string mergeNodeId)
                : this(mergeNodeId, true)
            {
            }

            private ParallelBranchMergeResult(string mergeNodeId, bool isValid)
            {
                MergeNodeId = mergeNodeId;
                IsValid = isValid;
            }

            public string MergeNodeId { get; }

            public bool IsValid { get; }
        }
    }
}
