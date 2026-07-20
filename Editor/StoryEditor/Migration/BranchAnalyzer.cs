using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Model;

namespace GameDeveloperKit.StoryEditor.Migration
{
    internal static class BranchAnalyzer
    {
        public static void Extract(
            string storyId,
            AuthoringVolume volume,
            AuthoringEpisode episode,
            IList<AuthoringRouteEdge> pendingRouteEdges,
            MigrationReport report)
        {
            var location = $"story:{storyId}/volume:{volume.VolumeId}/episode:{episode.EpisodeId}";
            var nodes = BuildNodes(episode, location, report);
            if (nodes == null || !nodes.ContainsKey(episode.EntryNodeId))
            {
                report.AddConflict("missing_entry_node", location + "/entry", $"Episode entry node does not exist. node:{episode.EntryNodeId}");
                return;
            }

            var outgoing = BuildOutgoing(episode);
            var main = Collect(
                episode.EntryNodeId,
                nodes,
                outgoing,
                true,
                location,
                report);
            if (main == null)
            {
                return;
            }

            var plans = new List<BranchPlan>();
            var claimed = new HashSet<string>(main, StringComparer.Ordinal);
            foreach (var choice in episode.Nodes
                         .Where(x => x != null && x.NodeKind == NodeKind.Choice && main.Contains(x.NodeId))
                         .OrderBy(x => x.NodeId, StringComparer.Ordinal))
            {
                var selected = GetSelectedEdges(outgoing, choice.NodeId);
                if (selected.Count == 0)
                {
                    continue;
                }

                if (selected.Count != 1)
                {
                    report.AddConflict(
                        "choice_multiple_selected_targets",
                        location + $"/node:{choice.NodeId}",
                        "Legacy Choice must have exactly one selected target to extract a child Episode.");
                    continue;
                }

                var edge = selected[0];
                if (edge.TargetKind != TransitionTargetKind.Node)
                {
                    report.AddConflict(
                        "choice_cross_episode_target",
                        location + $"/edge:{edge.EdgeId}",
                        "Legacy Choice selected target must be a node in the same Episode.");
                    continue;
                }

                var branch = Collect(edge.TargetNodeId, nodes, outgoing, false, location, report);
                if (branch == null)
                {
                    continue;
                }

                var overlap = branch.FirstOrDefault(claimed.Contains);
                if (!string.IsNullOrWhiteSpace(overlap))
                {
                    report.AddConflict(
                        "branch_shared_node",
                        location + $"/node:{overlap}",
                        $"Choice branches share a node or flow back to the parent graph. choice:{choice.NodeId}");
                    continue;
                }

                if (!ValidateIncoming(episode, branch, edge, location, report))
                {
                    continue;
                }

                claimed.UnionWith(branch);
                plans.Add(new BranchPlan(choice, edge, branch));
            }

            if (!report.CanApply)
            {
                return;
            }

            var unclaimed = nodes.Keys.FirstOrDefault(x => !claimed.Contains(x));
            if (!string.IsNullOrWhiteSpace(unclaimed))
            {
                report.AddConflict(
                    "branch_unowned_node",
                    location + $"/node:{unclaimed}",
                    "Node ownership cannot be determined from the Episode entry and Choice branches.");
                return;
            }

            for (var i = 0; i < plans.Count; i++)
            {
                var child = MoveBranch(storyId, volume, episode, plans[i], pendingRouteEdges, report);
                Extract(storyId, volume, child, pendingRouteEdges, report);
            }
        }

        private static Dictionary<string, AuthoringNode> BuildNodes(
            AuthoringEpisode episode,
            string location,
            MigrationReport report)
        {
            var result = new Dictionary<string, AuthoringNode>(StringComparer.Ordinal);
            for (var i = 0; i < episode.Nodes.Count; i++)
            {
                var node = episode.Nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                {
                    report.AddConflict("invalid_node_id", location + $"/node[{i}]", "Node ID cannot be empty.");
                    continue;
                }

                if (!result.TryAdd(node.NodeId, node))
                {
                    report.AddConflict("duplicate_node_id", location + $"/node:{node.NodeId}", "Node ID must be unique in the Episode.");
                }
            }

            return report.CanApply ? result : null;
        }

        private static Dictionary<string, List<AuthoringEdge>> BuildOutgoing(AuthoringEpisode episode)
        {
            var result = new Dictionary<string, List<AuthoringEdge>>(StringComparer.Ordinal);
            for (var i = 0; i < episode.Edges.Count; i++)
            {
                var edge = episode.Edges[i];
                if (edge == null || string.IsNullOrWhiteSpace(edge.FromNodeId))
                {
                    continue;
                }

                if (!result.TryGetValue(edge.FromNodeId, out var edges))
                {
                    edges = new List<AuthoringEdge>();
                    result.Add(edge.FromNodeId, edges);
                }

                edges.Add(edge);
            }

            return result;
        }

        private static HashSet<string> Collect(
            string rootNodeId,
            IReadOnlyDictionary<string, AuthoringNode> nodes,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoing,
            bool stopAtSelected,
            string location,
            MigrationReport report)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            return Visit(rootNodeId, nodes, outgoing, stopAtSelected, location, result, visiting, report)
                ? result
                : null;
        }

        private static bool Visit(
            string nodeId,
            IReadOnlyDictionary<string, AuthoringNode> nodes,
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoing,
            bool stopAtSelected,
            string location,
            ISet<string> result,
            ISet<string> visiting,
            MigrationReport report)
        {
            if (!nodes.TryGetValue(nodeId ?? string.Empty, out var node))
            {
                report.AddConflict("missing_target_node", location + $"/node:{nodeId}", "Edge target node does not exist.");
                return false;
            }

            if (visiting.Contains(nodeId))
            {
                report.AddConflict("branch_cycle", location + $"/node:{nodeId}", "Choice branch contains a cycle.");
                return false;
            }

            if (!result.Add(nodeId))
            {
                return true;
            }

            visiting.Add(nodeId);
            if (outgoing.TryGetValue(nodeId, out var edges))
            {
                for (var i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    if ((int)edge.TargetKind == LegacyNodeKinds.TargetEpisode)
                    {
                        if ((int)node.NodeKind != LegacyNodeKinds.JumpEpisode)
                        {
                            report.AddConflict(
                                "cross_episode_step_target",
                                location + $"/edge:{edge.EdgeId}",
                                "Only a legacy Jump node may target another Episode during migration.");
                            visiting.Remove(nodeId);
                            return false;
                        }

                        continue;
                    }

                    if (edge.TargetKind == TransitionTargetKind.StoryEnd ||
                        stopAtSelected && node.NodeKind == NodeKind.Choice &&
                        string.Equals(edge.FromPortId, "selected", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!Visit(edge.TargetNodeId, nodes, outgoing, stopAtSelected, location, result, visiting, report))
                    {
                        visiting.Remove(nodeId);
                        return false;
                    }
                }
            }

            visiting.Remove(nodeId);
            return true;
        }

        private static List<AuthoringEdge> GetSelectedEdges(
            IReadOnlyDictionary<string, List<AuthoringEdge>> outgoing,
            string choiceId)
        {
            if (!outgoing.TryGetValue(choiceId, out var edges))
            {
                return new List<AuthoringEdge>();
            }

            return edges
                .Where(x => x != null && string.Equals(x.FromPortId, "selected", StringComparison.Ordinal))
                .OrderBy(x => x.EdgeId, StringComparer.Ordinal)
                .ToList();
        }

        private static bool ValidateIncoming(
            AuthoringEpisode episode,
            ISet<string> branch,
            AuthoringEdge rootEdge,
            string location,
            MigrationReport report)
        {
            var incoming = branch.ToDictionary(x => x, _ => 0, StringComparer.Ordinal);
            for (var i = 0; i < episode.Edges.Count; i++)
            {
                var edge = episode.Edges[i];
                if (edge?.TargetKind != TransitionTargetKind.Node || !branch.Contains(edge.TargetNodeId))
                {
                    continue;
                }

                incoming[edge.TargetNodeId]++;
                if (!branch.Contains(edge.FromNodeId) && !ReferenceEquals(edge, rootEdge))
                {
                    report.AddConflict(
                        "branch_backflow",
                        location + $"/edge:{edge.EdgeId}",
                        "Choice branch has an incoming edge from outside its owning branch.");
                    return false;
                }
            }

            foreach (var pair in incoming.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (pair.Value > 1)
                {
                    report.AddConflict(
                        "branch_multiple_incoming",
                        location + $"/node:{pair.Key}",
                        "Choice branch node has multiple incoming edges and cannot be extracted without copying or merging.");
                    return false;
                }
            }

            return true;
        }

        private static AuthoringEpisode MoveBranch(
            string storyId,
            AuthoringVolume volume,
            AuthoringEpisode parent,
            BranchPlan plan,
            IList<AuthoringRouteEdge> pendingRouteEdges,
            MigrationReport report)
        {
            var childId = IdentityId.New();
            var startId = IdentityId.New();
            var child = new AuthoringEpisode
            {
                EpisodeId = childId,
                Title = string.IsNullOrWhiteSpace(plan.Choice.Title) ? childId : plan.Choice.Title,
                EntryNodeId = startId
            };
            child.Nodes.Add(new AuthoringNode
            {
                NodeId = startId,
                Title = "开始",
                NodeKind = NodeKind.Start
            });
            child.Edges.Add(new AuthoringEdge
            {
                EdgeId = IdentityId.New(),
                FromNodeId = startId,
                FromPortId = "completed",
                FromPortLabel = "完成",
                TargetKind = TransitionTargetKind.Node,
                TargetNodeId = plan.RootEdge.TargetNodeId
            });

            for (var i = parent.Nodes.Count - 1; i >= 0; i--)
            {
                if (parent.Nodes[i] != null && plan.NodeIds.Contains(parent.Nodes[i].NodeId))
                {
                    child.Nodes.Insert(1, parent.Nodes[i]);
                    parent.Nodes.RemoveAt(i);
                }
            }

            for (var i = parent.Edges.Count - 1; i >= 0; i--)
            {
                var edge = parent.Edges[i];
                if (edge != null && plan.NodeIds.Contains(edge.FromNodeId))
                {
                    child.Edges.Add(edge);
                    parent.Edges.RemoveAt(i);
                }
                else if (ReferenceEquals(edge, plan.RootEdge))
                {
                    parent.Edges.RemoveAt(i);
                }
            }

            MovePlacements(parent, child, plan.NodeIds, startId, plan.RootEdge.TargetNodeId);
            volume.Episodes.Add(child);
            pendingRouteEdges.Add(new AuthoringRouteEdge
            {
                SourceKind = RouteEdgeSourceKind.EpisodeExit,
                FromEpisodeId = parent.EpisodeId,
                FromExitId = plan.Choice.NodeId,
                ToEpisodeId = childId
            });
            report.AddChange(
                MigrationChangeKind.Split,
                $"story:{storyId}/volume:{volume.VolumeId}/episode:{parent.EpisodeId}/node:{plan.Choice.NodeId}",
                $"Choice branch -> child Episode:{childId}");
            return child;
        }

        private static void MovePlacements(
            AuthoringEpisode parent,
            AuthoringEpisode child,
            ISet<string> nodeIds,
            string startId,
            string branchRootId)
        {
            var rootPosition = default(UnityEngine.Vector2);
            for (var i = parent.DetailLayout.Nodes.Count - 1; i >= 0; i--)
            {
                var placement = parent.DetailLayout.Nodes[i];
                if (placement != null && nodeIds.Contains(placement.NodeId))
                {
                    if (string.Equals(placement.NodeId, branchRootId, StringComparison.Ordinal))
                    {
                        rootPosition = placement.Position;
                    }

                    child.DetailLayout.Nodes.Add(placement);
                    parent.DetailLayout.Nodes.RemoveAt(i);
                }
            }

            child.DetailLayout.Nodes.Add(new EpisodeNodePlacement
            {
                NodeId = startId,
                Position = rootPosition + new UnityEngine.Vector2(-220f, 0f)
            });
        }

        private sealed class BranchPlan
        {
            public BranchPlan(AuthoringNode choice, AuthoringEdge rootEdge, HashSet<string> nodeIds)
            {
                Choice = choice;
                RootEdge = rootEdge;
                NodeIds = nodeIds;
            }

            public AuthoringNode Choice { get; }

            public AuthoringEdge RootEdge { get; }

            public HashSet<string> NodeIds { get; }
        }
    }

}
