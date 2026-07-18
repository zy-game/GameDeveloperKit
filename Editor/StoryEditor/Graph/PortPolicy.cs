using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameDeveloperKit.EditorNodeGraph;
using GameDeveloperKit.Story;
using GameDeveloperKit.StoryEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.StoryEditor.Model;

namespace GameDeveloperKit.StoryEditor.Graph
{
    internal static class PortPolicy
    {
        public static PortPolicyResult CanConnect(
            AuthoringChapter chapter,
            AuthoringNode from,
            string outputPortId,
            AuthoringNode target)
        {
            if (chapter == null)
            {
                return PortPolicyResult.Fail("请先选择章节。");
            }

            if (from == null || target == null || string.IsNullOrWhiteSpace(outputPortId))
            {
                return PortPolicyResult.Fail("端口无效。");
            }

            if (string.Equals(from.NodeId, target.NodeId, StringComparison.Ordinal))
            {
                return PortPolicyResult.Fail("不能把节点连接到自己。");
            }

            if (target.NodeKind == NodeKind.Start)
            {
                return PortPolicyResult.Fail("开始节点不能作为目标。");
            }

            if (from.NodeKind == NodeKind.SettleChapter)
            {
                if (string.Equals(outputPortId, SettlementCommandNames.CompletedOutcome, StringComparison.Ordinal) && target.NodeKind != NodeKind.End)
                {
                    return PortPolicyResult.Fail("章节结算的完成端口必须连接结束节点。");
                }

                if (string.Equals(outputPortId, SettlementCommandNames.FailedOutcome, StringComparison.Ordinal) && target.NodeKind == NodeKind.End)
                {
                    return PortPolicyResult.Fail("章节结算失败不能直接进入结束节点。");
                }
            }

            if (target.NodeKind == NodeKind.End &&
                (from.NodeKind != NodeKind.SettleChapter || string.Equals(outputPortId, SettlementCommandNames.CompletedOutcome, StringComparison.Ordinal) is false))
            {
                return PortPolicyResult.Fail("章节结束只能由章节结算的完成端口进入。");
            }

            if (from.NodeKind == NodeKind.End)
            {
                return PortPolicyResult.Fail("结束节点没有输出端口。");
            }

            if (NodeSchemaRegistry.IsDefaultAuthoringNode(from.NodeKind) is false)
            {
                return PortPolicyResult.Fail("该节点已退出默认作者路径，不能再参与剧情流程连线。");
            }

            if (NodeSchemaRegistry.IsDefaultAuthoringNode(target.NodeKind) is false)
            {
                return PortPolicyResult.Fail("目标节点已退出默认作者路径，请改用内容、媒体、音频、等待、选项或章节跳转节点。");
            }

            if (from.NodeKind == NodeKind.Choice &&
                string.Equals(outputPortId, "selected", StringComparison.Ordinal) is false)
            {
                return PortPolicyResult.Fail("选项节点只能从“选择后”端口连接分支目标。");
            }

            if (target.NodeKind == NodeKind.Choice &&
                (CanOwnChoiceItems(from.NodeKind) is false ||
                 string.Equals(outputPortId, "completed", StringComparison.Ordinal) is false))
            {
                return PortPolicyResult.Fail("选项节点只能接在对白、旁白、等待或等待全部完成的完成端口后。");
            }

            if (!HasDeclaredOutputPort(from.NodeKind, outputPortId))
            {
                return PortPolicyResult.Fail("该节点没有这个输出端口。");
            }

            if (HasDuplicateEdge(chapter, from.NodeId, outputPortId, target.NodeId))
            {
                return PortPolicyResult.Fail("这条连线已经存在。");
            }

            return PortPolicyResult.Success;
        }

        public static bool IsMultipleOutputPort(
            AuthoringNode node,
            string portId,
            AuthoringNode targetNode)
        {
            if (node == null || string.IsNullOrWhiteSpace(portId))
            {
                return false;
            }

            if (IsLineChoicePort(node, portId, targetNode))
            {
                return true;
            }

            var schema = NodeSchemaRegistry.Get(node.NodeKind);
            for (var i = 0; i < schema.Ports.Count; i++)
            {
                var port = schema.Ports[i];
                if (port.Direction == PortDirection.Output &&
                    string.Equals(port.PortId, portId, StringComparison.Ordinal))
                {
                    return port.Multiple;
                }
            }

            return false;
        }

        public static bool HasDeclaredOutputPort(NodeKind kind, string portId)
        {
            if (string.IsNullOrWhiteSpace(portId))
            {
                return false;
            }

            if (IsParallelBranchOutput(kind, portId))
            {
                return true;
            }

            var schema = NodeSchemaRegistry.Get(kind);
            for (var i = 0; i < schema.Ports.Count; i++)
            {
                var port = schema.Ports[i];
                if (port.Direction == PortDirection.Output &&
                    string.Equals(port.PortId, portId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool AllowsRuntimeFlowInput(NodeKind kind)
        {
            return kind != NodeKind.Start;
        }

        public static bool AllowsRuntimeFlowOutput(NodeKind kind)
        {
            return kind != NodeKind.End;
        }

        public static bool IsParallelBranchPort(string portId)
        {
            return !string.IsNullOrWhiteSpace(portId) &&
                   portId.StartsWith("branch_", StringComparison.Ordinal);
        }

        public static bool IsLineChoicePort(AuthoringNode node, string portId, AuthoringNode targetNode)
        {
            return node != null &&
                   targetNode != null &&
                   CanOwnChoiceItems(node.NodeKind) &&
                   targetNode.NodeKind == NodeKind.Choice &&
                   string.Equals(portId, "completed", StringComparison.Ordinal);
        }

        private static bool IsParallelBranchOutput(NodeKind kind, string portId)
        {
            return kind == NodeKind.Parallel &&
                   (string.Equals(portId, "branch", StringComparison.Ordinal) || IsParallelBranchPort(portId));
        }

        private static bool IsLineNode(NodeKind kind)
        {
            return kind == NodeKind.Dialogue || kind == NodeKind.Narration;
        }

        private static bool CanOwnChoiceItems(NodeKind kind)
        {
            return IsLineNode(kind) || kind == NodeKind.Merge || kind == NodeKind.Wait;
        }

        private static bool HasDuplicateEdge(
            AuthoringChapter chapter,
            string fromNodeId,
            string portId,
            string targetNodeId)
        {
            if (chapter == null)
            {
                return false;
            }

            for (var i = 0; i < chapter.Edges.Count; i++)
            {
                var edge = chapter.Edges[i];
                if (edge != null &&
                    edge.TargetKind == TransitionTargetKind.Node &&
                    string.Equals(edge.FromNodeId, fromNodeId, StringComparison.Ordinal) &&
                    string.Equals(edge.FromPortId, portId, StringComparison.Ordinal) &&
                    string.Equals(edge.TargetNodeId, targetNodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static AuthoringNode FindNode(AuthoringChapter chapter, string nodeId)
        {
            if (chapter == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return null;
            }

            for (var i = 0; i < chapter.Nodes.Count; i++)
            {
                var node = chapter.Nodes[i];
                if (node != null && string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
                {
                    return node;
                }
            }

            return null;
        }
    }

    internal readonly struct PortPolicyResult
    {
        private PortPolicyResult(bool allowed, string message)
        {
            Allowed = allowed;
            Message = message;
        }

        public bool Allowed { get; }

        public string Message { get; }

        public static PortPolicyResult Success => new PortPolicyResult(true, null);

        public static PortPolicyResult Fail(string message)
        {
            return new PortPolicyResult(false, message);
        }
    }
}
