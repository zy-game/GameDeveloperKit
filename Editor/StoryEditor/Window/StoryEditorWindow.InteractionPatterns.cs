using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.EditorNodeGraph;
using GameDeveloperKit.Story;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor
{
    public sealed partial class StoryEditorWindow
    {
        internal void AddInteractionPatternFromGraph(string templateId, Vector2 position, EditorGraphPortRef connectFrom)
        {
            if (m_Asset == null || m_SelectedChapter == null)
            {
                return;
            }

            if (templateId != StoryEditorGraphAdapter.VideoWaitChoiceTemplateId &&
                templateId != StoryEditorGraphAdapter.VideoWaitQteTemplateId &&
                templateId != StoryEditorGraphAdapter.VideoWaitUnlockTemplateId)
            {
                return;
            }

            RecordStoryUndo("Add Story Interaction Pattern");

            var fromNode = connectFrom.IsValid ? FindNode(connectFrom.NodeId) : null;
            var fromPortId = connectFrom.IsValid ? connectFrom.PortId : null;
            var fromPortLabel = fromNode == null ? null : ResolveOutputPortLabel(fromNode, fromPortId);
            IReadOnlyList<StoryAuthoringNode> nodes;
            switch (templateId)
            {
                case StoryEditorGraphAdapter.VideoWaitChoiceTemplateId:
                    nodes = AddVideoWaitChoicePattern(position);
                    break;
                case StoryEditorGraphAdapter.VideoWaitQteTemplateId:
                    nodes = AddVideoWaitQtePattern(position);
                    break;
                case StoryEditorGraphAdapter.VideoWaitUnlockTemplateId:
                    nodes = AddVideoWaitUnlockPattern(position);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported interaction template '{templateId}'.");
            }

            if (nodes.Count == 0)
            {
                return;
            }

            if (fromNode != null)
            {
                AddInteractionPatternEdge(fromNode, fromPortId, fromPortLabel, nodes[0]);
            }

            SelectInteractionPatternNodes(nodes);
            MarkDirty();
            RefreshAll("已添加互动模板。");
        }

        private IReadOnlyList<StoryAuthoringNode> AddVideoWaitChoicePattern(Vector2 position)
        {
            var parallel = AddInteractionPatternNode("video_wait_choice_parallel", "视频中途选项", NodeKind.Parallel, position);
            var video = AddInteractionPatternNode("video_wait_choice_video", "播放视频", NodeKind.PlayVideo, position + new Vector2(300f, -120f));
            var wait = AddInteractionPatternNode("video_wait_choice_wait", "等待选项出现", NodeKind.Wait, position + new Vector2(300f, 120f));
            var choiceA = AddInteractionPatternNode("video_wait_choice_option_a", "选项 A", NodeKind.Choice, position + new Vector2(600f, 60f));
            var choiceB = AddInteractionPatternNode("video_wait_choice_option_b", "选项 B", NodeKind.Choice, position + new Vector2(600f, 180f));
            var targetA = AddInteractionPatternNode("video_wait_choice_option_a_target", "选项 A 后续", NodeKind.Narration, position + new Vector2(900f, 60f));
            var targetB = AddInteractionPatternNode("video_wait_choice_option_b_target", "选项 B 后续", NodeKind.Narration, position + new Vector2(900f, 180f));

            SetVideoDefaults(video);
            SetParameterValue(wait, "duration", "3");
            SetParameterValue(choiceA, "textKey", "choice.option_a");
            SetParameterValue(choiceB, "textKey", "choice.option_b");
            SetParameterValue(targetA, "textKey", "choice.option_a.after");
            SetParameterValue(targetB, "textKey", "choice.option_b.after");

            AddInteractionPatternEdge(parallel, "branch_1", "视频轨", video);
            AddInteractionPatternEdge(parallel, "branch_2", "交互轨", wait);
            AddInteractionPatternEdge(wait, "completed", "完成", choiceA);
            AddInteractionPatternEdge(wait, "completed", "完成", choiceB);
            AddInteractionPatternEdge(choiceA, "selected", "选择后", targetA);
            AddInteractionPatternEdge(choiceB, "selected", "选择后", targetB);

            return new[] { parallel, video, wait, choiceA, choiceB, targetA, targetB };
        }

        private IReadOnlyList<StoryAuthoringNode> AddVideoWaitQtePattern(Vector2 position)
        {
            var parallel = AddInteractionPatternNode("video_wait_qte_parallel", "视频中途 QTE", NodeKind.Parallel, position);
            var video = AddInteractionPatternNode("video_wait_qte_video", "播放视频", NodeKind.PlayVideo, position + new Vector2(300f, -120f));
            var wait = AddInteractionPatternNode("video_wait_qte_wait", "等待 QTE 出现", NodeKind.Wait, position + new Vector2(300f, 120f));
            var qte = AddInteractionPatternNode("video_wait_qte", "QTE", NodeKind.Qte, position + new Vector2(600f, 120f));
            var success = AddInteractionPatternNode("video_wait_qte_success", "QTE 成功后续", NodeKind.Narration, position + new Vector2(900f, 60f));
            var fail = AddInteractionPatternNode("video_wait_qte_fail", "QTE 失败后续", NodeKind.Narration, position + new Vector2(900f, 180f));

            SetVideoDefaults(video);
            SetParameterValue(wait, "duration", "3");
            SetParameterValue(qte, StoryInteractionCommandNames.InputActionIdArgument, "space");
            SetParameterValue(qte, StoryInteractionCommandNames.DurationSecondsArgument, "3");
            SetParameterValue(qte, StoryInteractionCommandNames.RequiredCountArgument, "1");
            SetParameterValue(qte, StoryInteractionCommandNames.PromptTextKeyArgument, "qte.prompt");
            SetParameterValue(success, "textKey", "qte.success.after");
            SetParameterValue(fail, "textKey", "qte.fail.after");

            AddInteractionPatternEdge(parallel, "branch_1", "视频轨", video);
            AddInteractionPatternEdge(parallel, "branch_2", "交互轨", wait);
            AddInteractionPatternEdge(wait, "completed", "完成", qte);
            AddInteractionPatternEdge(qte, StoryInteractionCommandNames.SuccessOutcome, "成功", success);
            AddInteractionPatternEdge(qte, StoryInteractionCommandNames.FailOutcome, "失败", fail);

            return new[] { parallel, video, wait, qte, success, fail };
        }

        private IReadOnlyList<StoryAuthoringNode> AddVideoWaitUnlockPattern(Vector2 position)
        {
            var parallel = AddInteractionPatternNode("video_wait_unlock_parallel", "视频中途 Unlock", NodeKind.Parallel, position);
            var video = AddInteractionPatternNode("video_wait_unlock_video", "播放视频", NodeKind.PlayVideo, position + new Vector2(300f, -120f));
            var wait = AddInteractionPatternNode("video_wait_unlock_wait", "等待 Unlock 出现", NodeKind.Wait, position + new Vector2(300f, 120f));
            var unlock = AddInteractionPatternNode("video_wait_unlock", "Unlock", NodeKind.Unlock, position + new Vector2(600f, 120f));
            var success = AddInteractionPatternNode("video_wait_unlock_success", "Unlock 成功后续", NodeKind.Narration, position + new Vector2(900f, 60f));
            var fail = AddInteractionPatternNode("video_wait_unlock_fail", "Unlock 失败后续", NodeKind.Narration, position + new Vector2(900f, 180f));

            SetVideoDefaults(video);
            SetParameterValue(wait, "duration", "3");
            SetParameterValue(unlock, StoryInteractionCommandNames.UnlockIdArgument, $"{unlock.NodeId}.unlock");
            SetParameterValue(unlock, StoryInteractionCommandNames.PuzzleTypeArgument, StoryInteractionCommandNames.PuzzleTypeNodeUnlock);
            SetParameterValue(unlock, StoryInteractionCommandNames.PromptTextKeyArgument, "unlock.prompt");
            SetParameterValue(success, "textKey", "unlock.success.after");
            SetParameterValue(fail, "textKey", "unlock.fail.after");

            AddInteractionPatternEdge(parallel, "branch_1", "视频轨", video);
            AddInteractionPatternEdge(parallel, "branch_2", "交互轨", wait);
            AddInteractionPatternEdge(wait, "completed", "完成", unlock);
            AddInteractionPatternEdge(unlock, StoryInteractionCommandNames.SuccessOutcome, "成功", success);
            AddInteractionPatternEdge(unlock, StoryInteractionCommandNames.FailOutcome, "失败", fail);

            return new[] { parallel, video, wait, unlock, success, fail };
        }

        private StoryAuthoringNode AddInteractionPatternNode(string baseId, string title, NodeKind kind, Vector2 position)
        {
            var schema = NodeSchemaRegistry.Get(kind);
            var node = new StoryAuthoringNode
            {
                NodeId = MakeUnique(baseId, m_SelectedChapter.Nodes.Select(x => x.NodeId)),
                Title = title,
                NodeKind = kind
            };
            AddDefaultParameters(node, schema);
            m_SelectedChapter.Nodes.Add(node);
            GetLayout(node).Position = position;
            return node;
        }

        private void AddInteractionPatternEdge(StoryAuthoringNode fromNode, string portId, string portLabel, StoryAuthoringNode targetNode)
        {
            if (fromNode == null ||
                targetNode == null ||
                StoryEditorPortPolicy.CanConnect(m_SelectedChapter, fromNode, portId, targetNode).Allowed is false)
            {
                return;
            }

            var edge = CreateEdge(fromNode, portId, portLabel, TransitionTargetKind.Node, targetNode.NodeId, null);
            AddEdgeToChapter(fromNode, edge);
        }

        private static void SetVideoDefaults(StoryAuthoringNode video)
        {
            SetParameterValue(video, StoryMediaCommandNames.VideoSourceArgument, StoryMediaCommandNames.VideoSourceStreamingAssets);
            SetParameterValue(video, StoryMediaCommandNames.ClipArgument, string.Empty);
            SetParameterValue(video, "wait", "true");
            SetParameterValue(video, "loop", "false");
        }

        private void SelectInteractionPatternNodes(IReadOnlyList<StoryAuthoringNode> nodes)
        {
            m_SelectedNodeIds.Clear();
            m_SelectedEdge = null;
            m_SelectedNode = null;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                {
                    continue;
                }

                m_SelectedNodeIds.Add(node.NodeId);
                m_SelectedNode = node;
            }

            m_SelectionKind = SelectionKind.Node;
        }
    }
}
