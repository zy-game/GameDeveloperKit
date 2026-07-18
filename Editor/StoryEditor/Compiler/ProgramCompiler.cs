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

namespace GameDeveloperKit.StoryEditor.Compiler
{
    /// <summary>
    /// 将 Story Editor authoring 数据编译为 Program。
    /// </summary>
    public static partial class ProgramCompiler
    {
        public static Program Compile(AuthoringAsset asset, out ValidationReport report)
        {
            report = new ValidationReport();
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

            var commandDefinitions = new List<CommandDefinition>();
            var commandNames = new HashSet<string>(StringComparer.Ordinal);
            var chapters = new List<Chapter>();
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

            return new Program(
                TrimToNull(asset.StoryId),
                TrimToNull(asset.Version),
                TrimToNull(asset.EntryChapterId),
                chapters,
                new VariableSchema(),
                new CommandSchema(commandDefinitions));
        }

        public static ValidationReport Validate(AuthoringAsset asset)
        {
            Compile(asset, out var report);
            return report;
        }

        private static Chapter CompileChapter(
            string storyId,
            AuthoringChapter chapter,
            IReadOnlyDictionary<string, AuthoringChapter> chapterLookup,
            List<CommandDefinition> commandDefinitions,
            ISet<string> commandNames,
            ValidationReport report)
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
                    var choiceStep = BuildOwnedChoiceStep(
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

            var previewImagePath = GetPreviewImagePath(chapter);

            return new Chapter(
                chapterId,
                TrimToNull(chapter.Title) ?? chapterId,
                TrimToNull(chapter.EntryNodeId),
                steps,
                previewImagePath,
                TrimToNull(chapter.Description));
        }

        private static Step CompileNode(
            string storyId,
            string chapterId,
            AuthoringNode node,
            IReadOnlyDictionary<string, AuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            ParallelCompileContext parallelContext,
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
                    $"story:{storyId}/chapter:{chapterId}/node:{nodeId}",
                    "Node kind is no longer supported in Story default authoring path.");
                return null;
            }

            switch (node.NodeKind)
            {
                case NodeKind.Start:
                    return new Step(nodeId, StepKind.Start, new StepData(tags: tags));
                case NodeKind.End:
                    return new Step(nodeId, StepKind.End, new StepData(tags: tags));
                case NodeKind.Dialogue:
                case NodeKind.Narration:
                    return BuildLineStep(node, nodeId, outgoingEdges, chapterLookup, nodeLookup, report, existingStepIds, tags);
                case NodeKind.Choice:
                    return BuildChoiceStep(storyId, chapterId, node, edges, chapterLookup, nodeLookup, report, existingStepIds, tags);
                case NodeKind.PlayVideo:
                case NodeKind.ShowImage:
                case NodeKind.PlayAudio:
                case NodeKind.EmitEvent:
                case NodeKind.MiniGame:
                case NodeKind.Qte:
                case NodeKind.Unlock:
                    return BuildCommandStep(
                        storyId,
                        chapterId,
                        node,
                        edges,
                        outgoingEdges,
                        chapterLookup,
                        nodeLookup,
                        parallelContext,
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
                    return BuildWaitStep(storyId, chapterId, node, edges, chapterLookup, nodeLookup, report, existingStepIds, tags);
                default:
                    report.AddError($"story:{storyId}/chapter:{chapterId}/node:{nodeId}", $"Unsupported node kind '{node.NodeKind}'.");
                    return null;
            }
        }

        private static Step BuildLineStep(
            AuthoringNode node,
            string nodeId,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            IReadOnlyDictionary<string, AuthoringChapter> chapterLookup,
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

                target = Target.Step(
                    FindChapterId(chapterLookup, node),
                    MakeSyntheticChoiceStepId(nodeId, existingStepIds, nodeLookup));
            }
            else
            {
                target = FirstDirectTarget(node, outgoingEdges, chapterLookup, nodeLookup, report);
                if (target == null)
                {
                    target = Target.StoryEnd();
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
            string chapterId,
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, AuthoringChapter> chapterLookup,
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

                    var textKey = GetString(optionNode.Parameters, "textKey");
                    if (string.IsNullOrWhiteSpace(textKey))
                    {
                        report.AddWarning(
                            $"story:{storyId}/chapter:{chapterId}/node:{optionNode.NodeId}/field:textKey",
                            "Choice item textKey is missing; node title is used as fallback.");
                        textKey = TrimToNull(optionNode.Title) ?? optionNode.NodeId;
                    }
                    ValidateLocalizedText(
                        textKey,
                        $"story:{storyId}/chapter:{chapterId}/node:{optionNode.NodeId}/field:textKey",
                        report);
                    choices.Add(new Choice(
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

                    choices.Add(new Choice(choiceId, TrimToNull(edge.FromPortLabel) ?? choiceId, BuildCondition(edge.Conditions), target));
                }
            }

            if (choices.Count == 0)
            {
                report.AddError($"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}", "Choice node has no outgoing choices.");
                return null;
            }

            return new Step(
                TrimToNull(node.NodeId),
                StepKind.Choice,
                new StepData(choices: choices, tags: tags));
        }

        private static Step BuildOwnedChoiceStep(
            string storyId,
            string chapterId,
            AuthoringNode ownerNode,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            IReadOnlyDictionary<string, AuthoringChapter> chapterLookup,
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
            return BuildChoiceStep(storyId, chapterId, choiceNode, choiceItemEdges, chapterLookup, nodeLookup, report, existingStepIds, tags);
        }

        private static Step BuildParallelStep(
            string storyId,
            string chapterId,
            AuthoringNode node,
            ParallelCompileContext parallelContext,
            ValidationReport report,
            IReadOnlyList<string> tags)
        {
            var nodeId = TrimToNull(node.NodeId);
            if (parallelContext == null ||
                parallelContext.Blocks.TryGetValue(nodeId, out var block) is false)
            {
                report.AddError($"story:{storyId}/chapter:{chapterId}/node:{nodeId}/port:branch", "Parallel node must define a valid branch block.");
                return null;
            }

            return new Step(
                nodeId,
                StepKind.Parallel,
                new StepData(
                    tags: tags,
                    branches: block.Branches,
                    mergePolicy: MergePolicy.All));
        }

        private static Step BuildMergeStep(
            string storyId,
            string chapterId,
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, AuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ParallelCompileContext parallelContext,
            ValidationReport report,
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

            Target target = null;
            var choiceItemEdges = GetChoiceItemEdges(node, edges, nodeLookup);
            var directEdges = ExcludeChoiceItemEdges(node, edges, nodeLookup);
            if (choiceItemEdges.Count > 0)
            {
                if (directEdges.Count > 0)
                {
                    report.AddError(
                        $"story:{storyId}/chapter:{chapterId}/node:{nodeId}/port:completed",
                        "Merge completed port cannot connect choices and ordinary targets at the same time.");
                }

                target = Target.Step(chapterId, MakeSyntheticChoiceStepId(nodeId, existingStepIds, nodeLookup));
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

            return new Step(
                    nodeId,
                    StepKind.Merge,
                    new StepData(
                        target: target,
                        tags: tags,
                        mergePolicy: MergePolicy.All,
                        parallelStepId: parallelStepId));
        }

        private static Step BuildBranchStep(
            string storyId,
            string chapterId,
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, AuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ValidationReport report,
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

            return new Step(
                TrimToNull(node.NodeId),
                StepKind.Branch,
                new StepData(condition: condition, target: target, tags: tags));
        }

        private static Step BuildJumpChapterStep(
            string storyId,
            string chapterId,
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, AuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ValidationReport report,
            IReadOnlyList<string> tags)
        {
            var targetChapterId = GetString(node.Parameters, "chapterId");
            Target target = null;
            if (string.IsNullOrWhiteSpace(targetChapterId) is false)
            {
                if (chapterLookup.ContainsKey(targetChapterId))
                {
                    target = Target.Chapter(targetChapterId);
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
                : new Step(TrimToNull(node.NodeId), StepKind.Jump, new StepData(target: target, tags: tags));
        }

        private static Step BuildWaitStep(
            string storyId,
            string chapterId,
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, AuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ValidationReport report,
            ISet<string> existingStepIds,
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
                        $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/port:completed",
                        "Wait completed output cannot mix choice items and direct flow targets.");
                    return null;
                }

                target = Target.Step(chapterId, MakeSyntheticChoiceStepId(node.NodeId, existingStepIds, nodeLookup));
            }
            else
            {
                target = FirstOutgoingTarget(storyId, chapterId, node, edges, chapterLookup, nodeLookup, report, "wait") ?? Target.StoryEnd();
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
            string chapterId,
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, AuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ValidationReport report,
            IReadOnlyList<string> tags)
        {
            var target = FirstOutgoingTarget(storyId, chapterId, node, edges, chapterLookup, nodeLookup, report, "routing");
            return target == null
                ? new Step(TrimToNull(node.NodeId), StepKind.Start, new StepData(tags: tags))
                : new Step(TrimToNull(node.NodeId), StepKind.Jump, new StepData(target: target, tags: tags));
        }

        private static Step BuildCommandStep(
            string storyId,
            string chapterId,
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            IReadOnlyDictionary<string, AuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ParallelCompileContext parallelContext,
            List<CommandDefinition> commandDefinitions,
            ISet<string> commandNames,
            ValidationReport report,
            IReadOnlyList<string> tags)
        {
            var schema = NodeSchemaRegistry.Get(node.NodeKind);
            var commandName = GetCommandName(node);
            var arguments = BuildArguments(storyId, chapterId, node, schema, report);
            var argumentDefinitions = node.NodeKind == NodeKind.PlayVideo
                ? BuildVideoArgumentDefinitions()
                : node.NodeKind == NodeKind.PlayAudio
                    ? BuildAudioArgumentDefinitions()
                    : BuildArgumentDefinitions(schema);
            var outcomePorts = BuildOutcomePorts(edges);
            var outcomeTargets = BuildOutcomeTargets(storyId, chapterId, node, edges, chapterLookup, nodeLookup, report);
            ValidateQteCommand(storyId, chapterId, node, arguments, outcomePorts, outcomeTargets, report);
            ValidateUnlockCommand(storyId, chapterId, node, outcomePorts, outcomeTargets, report);
            var waitForCompletion = GetBoolean(node.Parameters, "wait") ||
                                    outcomePorts.Count > 0 ||
                                    node.NodeKind == NodeKind.PlayVideo ||
                                    node.NodeKind == NodeKind.MiniGame ||
                                    node.NodeKind == NodeKind.Qte ||
                                    node.NodeKind == NodeKind.Unlock;

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
                    target: FirstOutcomeTarget(outcomeTargets) ?? (edges.Count == 0 ? Target.StoryEnd() : null),
                    tags: tags));
        }

        private static Dictionary<string, Value> BuildArguments(
            string storyId,
            string chapterId,
            AuthoringNode node,
            NodeSchema schema,
            ValidationReport report)
        {
            if (node.NodeKind == NodeKind.PlayVideo)
            {
                return BuildVideoArguments(storyId, chapterId, node, report);
            }

            if (node.NodeKind == NodeKind.PlayAudio)
            {
                return BuildAudioArguments(storyId, chapterId, node, report);
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
                var source = $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/field:{parameter.Key}";
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
                    if (string.Equals(parameter.Key, InteractionCommandNames.PromptTextKeyArgument, StringComparison.Ordinal))
                    {
                        ValidateLocalizedText(value, source, report);
                    }
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

        private static void ValidateQteCommand(
            string storyId,
            string chapterId,
            AuthoringNode node,
            IReadOnlyDictionary<string, Value> arguments,
            IReadOnlyList<string> outcomePorts,
            IReadOnlyDictionary<string, Target> outcomeTargets,
            ValidationReport report)
        {
            if (node.NodeKind != NodeKind.Qte)
            {
                return;
            }

            if (arguments != null &&
                arguments.TryGetValue(InteractionCommandNames.DurationSecondsArgument, out var durationValue) &&
                durationValue.TryGetNumber(out var durationSeconds) &&
                TimeRules.IsFinitePositive(durationSeconds) is false)
            {
                report.AddError(
                    $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/field:{InteractionCommandNames.DurationSecondsArgument}",
                    "QTE durationSeconds must be finite and greater than zero.");
            }

            if (arguments != null &&
                arguments.TryGetValue(InteractionCommandNames.RequiredCountArgument, out var requiredCountValue) &&
                requiredCountValue.TryGetNumber(out var requiredCount) &&
                TimeRules.IsFinitePositive(requiredCount) is false)
            {
                report.AddError(
                    $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/field:{InteractionCommandNames.RequiredCountArgument}",
                    "QTE requiredCount must be finite and greater than zero.");
            }

            ValidateQteOutcomePort(storyId, chapterId, node, InteractionCommandNames.SuccessOutcome, outcomeTargets, report);
            ValidateQteOutcomePort(storyId, chapterId, node, InteractionCommandNames.FailOutcome, outcomeTargets, report);
            for (var i = 0; i < outcomePorts.Count; i++)
            {
                var outcomePort = outcomePorts[i];
                if (string.Equals(outcomePort, InteractionCommandNames.SuccessOutcome, StringComparison.Ordinal) ||
                    string.Equals(outcomePort, InteractionCommandNames.FailOutcome, StringComparison.Ordinal))
                {
                    continue;
                }

                report.AddError(
                    $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/outcome:{outcomePort}",
                    "QTE command only supports success and fail outcomes.");
            }
        }

        private static void ValidateQteOutcomePort(
            string storyId,
            string chapterId,
            AuthoringNode node,
            string outcomePort,
            IReadOnlyDictionary<string, Target> outcomeTargets,
            ValidationReport report)
        {
            if (outcomeTargets != null && outcomeTargets.ContainsKey(outcomePort))
            {
                return;
            }

            report.AddError(
                $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/outcome:{outcomePort}",
                "QTE command must target both success and fail outcomes.");
        }

        private static void ValidateUnlockCommand(
            string storyId,
            string chapterId,
            AuthoringNode node,
            IReadOnlyList<string> outcomePorts,
            IReadOnlyDictionary<string, Target> outcomeTargets,
            ValidationReport report)
        {
            if (node.NodeKind != NodeKind.Unlock)
            {
                return;
            }

            ValidateUnlockOutcomePort(storyId, chapterId, node, InteractionCommandNames.SuccessOutcome, outcomeTargets, report);
            ValidateUnlockOutcomePort(storyId, chapterId, node, InteractionCommandNames.FailOutcome, outcomeTargets, report);
            for (var i = 0; i < outcomePorts.Count; i++)
            {
                var outcomePort = outcomePorts[i];
                if (string.Equals(outcomePort, InteractionCommandNames.SuccessOutcome, StringComparison.Ordinal) ||
                    string.Equals(outcomePort, InteractionCommandNames.FailOutcome, StringComparison.Ordinal))
                {
                    continue;
                }

                report.AddError(
                    $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/outcome:{outcomePort}",
                    "Unlock command only supports success and fail outcomes.");
            }
        }

        private static void ValidateUnlockOutcomePort(
            string storyId,
            string chapterId,
            AuthoringNode node,
            string outcomePort,
            IReadOnlyDictionary<string, Target> outcomeTargets,
            ValidationReport report)
        {
            if (outcomeTargets != null && outcomeTargets.ContainsKey(outcomePort))
            {
                return;
            }

            report.AddError(
                $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/outcome:{outcomePort}",
                "Unlock command must target both success and fail outcomes.");
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
            string chapterId,
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, AuthoringChapter> chapterLookup,
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

        private static Target FirstDirectTarget(
            AuthoringNode node,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            IReadOnlyDictionary<string, AuthoringChapter> chapterLookup,
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
                    FindChapterId(chapterLookup, node),
                    edge,
                    chapterLookup,
                    nodeLookup,
                    report,
                    $"node:{node.NodeId}/port:{edge.FromPortId}");
            }

            return null;
        }

        private static Target FirstOutgoingTarget(
            string storyId,
            string chapterId,
            AuthoringNode node,
            IReadOnlyList<AuthoringEdge> edges,
            IReadOnlyDictionary<string, AuthoringChapter> chapterLookup,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ValidationReport report,
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

        private static Target BuildTarget(
            string storyId,
            string currentChapterId,
            AuthoringEdge edge,
            IReadOnlyDictionary<string, AuthoringChapter> chapterLookup,
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

                        return Target.Step(targetChapterId, targetNodeId);
                    }
                case TransitionTargetKind.Chapter:
                    {
                        var targetChapterId = TrimToNull(edge.TargetChapterId);
                        if (string.IsNullOrWhiteSpace(targetChapterId) || chapterLookup.ContainsKey(targetChapterId) is false)
                        {
                            report.AddError(source, $"Target chapter does not exist. chapter:{targetChapterId}");
                            return null;
                        }

                        return Target.Chapter(targetChapterId);
                    }
                case TransitionTargetKind.StoryEnd:
                    return Target.StoryEnd();
                default:
                    report.AddError(source, $"Unsupported target kind '{edge.TargetKind}'.");
                    return null;
            }
        }

        private static IReadOnlyDictionary<string, AuthoringChapter> BuildChapterLookup(AuthoringAsset asset, ValidationReport report)
        {
            var chapters = new Dictionary<string, AuthoringChapter>(StringComparer.Ordinal);
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

        private static IReadOnlyDictionary<string, AuthoringNode> BuildNodeLookup(
            string storyId,
            AuthoringChapter chapter,
            ValidationReport report)
        {
            var nodes = new Dictionary<string, AuthoringNode>(StringComparer.Ordinal);
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

        private static IReadOnlyDictionary<string, List<AuthoringEdge>> BuildOutgoingEdgeLookup(
            string storyId,
            AuthoringChapter chapter,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            ValidationReport report)
        {
            var lookup = new Dictionary<string, List<AuthoringEdge>>(StringComparer.Ordinal);
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
                    edges = new List<AuthoringEdge>();
                    lookup.Add(fromNodeId, edges);
                }

                edges.Add(edge);
            }

            return lookup;
        }

        private static ParallelCompileContext BuildParallelContext(
            string storyId,
            string chapterId,
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
            AuthoringNode parallelNode,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            ParallelCompileContext context,
            ValidationReport report)
        {
            var branches = new List<ParallelBranch>();
            var branchIds = new HashSet<string>(StringComparer.Ordinal);
            var waitNodeId = string.Empty;
            var edges = GetParallelBranchEdges(GetOutgoingEdges(outgoingEdges, parallelNode.NodeId));

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

                branches.Add(new ParallelBranch(
                    branchId,
                    TrimToNull(edge.FromPortLabel) ?? branchId,
                    Target.Step(chapterId, edge.TargetNodeId)));
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
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            ISet<string> visited,
            ValidationReport report)
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
                    return ParallelBranchMergeResult.NaturalEnd;
                case NodeKind.Choice:
                    return ParallelBranchMergeResult.NaturalEnd;
                case NodeKind.JumpChapter:
                    return ParallelBranchMergeResult.NaturalEnd;
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
                    continue;
                }

                if (edge.TargetKind == TransitionTargetKind.Chapter ||
                    (string.IsNullOrWhiteSpace(edge.TargetChapterId) is false &&
                     string.Equals(edge.TargetChapterId, chapterId, StringComparison.Ordinal) is false))
                {
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

        private static List<AuthoringNode> BuildOrderedRuntimeNodes(
            AuthoringChapter chapter,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges)
        {
            var ordered = new List<AuthoringNode>();
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

        private static List<AuthoringEdge> GetChoiceSelectedEdges(
            AuthoringNode node,
            IReadOnlyDictionary<string, AuthoringNode> nodeLookup,
            IReadOnlyDictionary<string, AuthoringChapter> chapterLookup,
            ValidationReport report)
        {
            var result = new List<AuthoringEdge>();
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

        private static string FindChapterId(IReadOnlyDictionary<string, AuthoringChapter> chapterLookup, AuthoringNode node)
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

        private static List<AuthoringEdge> GetOutgoingEdges(
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoingEdges,
            string nodeId)
        {
            return outgoingEdges != null && outgoingEdges.TryGetValue(nodeId, out var edges)
                ? edges
                : new List<AuthoringEdge>();
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
            return IsLineNode(kind) || kind == NodeKind.Merge || kind == NodeKind.Wait;
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
                case NodeKind.EmitEvent:
                    return "emit_event";
                case NodeKind.MiniGame:
                    return "mini_game";
                case NodeKind.Qte:
                    return InteractionCommandNames.Qte;
                case NodeKind.Unlock:
                    return InteractionCommandNames.Unlock;
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

        private static string GetPreviewImagePath(AuthoringChapter chapter)
        {
            if (chapter?.PreviewImage == null)
            {
                return null;
            }

#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(chapter.PreviewImage);
            return string.IsNullOrWhiteSpace(path) ? null : path;
#else
            return null;
#endif
        }

        private sealed class ParallelCompileContext
        {
            public readonly Dictionary<string, ParallelBlockInfo> Blocks = new Dictionary<string, ParallelBlockInfo>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> MergeOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        private sealed class ParallelBlockInfo
        {
            public ParallelBlockInfo(string parallelNodeId, string mergeNodeId, IReadOnlyList<ParallelBranch> branches)
            {
                ParallelNodeId = parallelNodeId;
                MergeNodeId = mergeNodeId;
                Branches = branches ?? Array.Empty<ParallelBranch>();
            }

            public string ParallelNodeId { get; }

            public string MergeNodeId { get; }

            public IReadOnlyList<ParallelBranch> Branches { get; }
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
