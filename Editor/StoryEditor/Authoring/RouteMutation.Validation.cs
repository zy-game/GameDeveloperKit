using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Model;

namespace GameDeveloperKit.StoryEditor.Authoring
{
    public sealed partial class RouteMutation
    {
        private RouteMutationResult ValidateCandidate(
            AuthoringVolume volume,
            IReadOnlyList<AuthoringEpisode> episodes,
            AuthoringRoute route)
        {
            var episodeLookup = new Dictionary<string, AuthoringEpisode>(StringComparer.Ordinal);
            var incoming = new Dictionary<string, int>(StringComparer.Ordinal);
            var outgoing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (var i = 0; i < (episodes?.Count ?? 0); i++)
            {
                var episode = episodes[i];
                if (episode == null || string.IsNullOrWhiteSpace(episode.EpisodeId))
                {
                    return Fail(UnknownEpisode, "剧情段或剧情段 ID 不能为空。");
                }

                if (episodeLookup.ContainsKey(episode.EpisodeId))
                {
                    return Fail(MultipleIncoming, $"剧情段 ID 重复：{episode.EpisodeId}");
                }

                episodeLookup.Add(episode.EpisodeId, episode);
                incoming.Add(episode.EpisodeId, 0);
                outgoing.Add(episode.EpisodeId, new List<string>());
            }

            if (episodeLookup.Count == 0)
            {
                return RouteMutationResult.Success("空卷路线有效。");
            }

            var edgeIds = new HashSet<string>(StringComparer.Ordinal);
            var boundExits = new HashSet<string>(StringComparer.Ordinal);
            var roots = new List<string>();
            for (var i = 0; i < (route?.Edges.Count ?? 0); i++)
            {
                var edge = route.Edges[i];
                if (edge == null || string.IsNullOrWhiteSpace(edge.EdgeId))
                {
                    return Fail(MultipleIncoming, "路线边或路线边 ID 不能为空。");
                }

                if (edgeIds.Add(edge.EdgeId) is false)
                {
                    return Fail(MultipleIncoming, $"路线边 ID 重复：{edge.EdgeId}");
                }

                if (episodeLookup.ContainsKey(edge.ToEpisodeId) is false)
                {
                    return Fail(UnknownEpisode, $"路线目标剧情段不存在：{edge.ToEpisodeId}");
                }

                incoming[edge.ToEpisodeId]++;
                if (incoming[edge.ToEpisodeId] > 1)
                {
                    return Fail(MultipleIncoming, $"剧情段不能有多条入边：{edge.ToEpisodeId}");
                }

                if (edge.SourceKind == RouteEdgeSourceKind.Root)
                {
                    if (string.IsNullOrWhiteSpace(edge.FromEpisodeId) is false ||
                        string.IsNullOrWhiteSpace(edge.FromExitId) is false)
                    {
                        return Fail(RootImmutable, "虚拟根路线边不能声明来源剧情段或出口。");
                    }

                    roots.Add(edge.ToEpisodeId);
                    continue;
                }

                if (edge.SourceKind != RouteEdgeSourceKind.EpisodeExit)
                {
                    return Fail(UnknownExit, $"路线边来源类型无效：{edge.SourceKind}");
                }

                if (episodeLookup.TryGetValue(edge.FromEpisodeId, out var sourceEpisode) is false)
                {
                    return Fail(UnknownEpisode, $"路线来源剧情段不存在：{edge.FromEpisodeId}");
                }

                if (DeclaresExit(sourceEpisode, edge.FromExitId) is false)
                {
                    return Fail(UnknownExit, $"路线来源出口不存在：{edge.FromEpisodeId}/{edge.FromExitId}");
                }

                var exitKey = edge.FromEpisodeId + "\n" + edge.FromExitId;
                if (boundExits.Add(exitKey) is false)
                {
                    return Fail(ExitAlreadyBound, $"剧情段出口已绑定：{edge.FromEpisodeId}/{edge.FromExitId}");
                }

                outgoing[edge.FromEpisodeId].Add(edge.ToEpisodeId);
            }

            foreach (var pair in incoming)
            {
                if (pair.Value != 1)
                {
                    return Fail(MultipleIncoming, $"剧情段必须恰好有一条入边：{pair.Key}");
                }
            }

            if (roots.Count == 0)
            {
                return Fail(MultipleIncoming, $"卷必须至少有一个首层剧情段：{volume?.VolumeId}");
            }

            var states = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var episodeId in episodeLookup.Keys)
            {
                if (ContainsCycle(episodeId, outgoing, states))
                {
                    return Fail(RouteCycle, $"卷路线不能包含循环：{episodeId}");
                }
            }

            var reachable = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < roots.Count; i++)
            {
                MarkReachable(roots[i], outgoing, reachable);
            }

            foreach (var episodeId in episodeLookup.Keys)
            {
                if (reachable.Contains(episodeId) is false)
                {
                    return Fail(MultipleIncoming, $"剧情段无法从虚拟根到达：{episodeId}");
                }
            }

            return RouteMutationResult.Success("路线结构有效。");
        }

        private bool RemovesPublishedIdentity(
            AuthoringEpisode episode,
            AuthoringRouteEdge incoming,
            out string message)
        {
            if (m_Asset.TryGetPublishedIdentity(out var baseline, out var baselineError) is false)
            {
                message = string.IsNullOrWhiteSpace(baselineError)
                    ? null
                    : $"发布身份基线无效：{baselineError}";
                return string.IsNullOrWhiteSpace(message) is false;
            }

            if (baseline == null)
            {
                message = null;
                return false;
            }

            if (Contains(baseline.EpisodeIds, episode.EpisodeId) ||
                Contains(baseline.EdgeIds, incoming.EdgeId) ||
                ContainsEpisodeExit(baseline.Exits, episode.EpisodeId))
            {
                message = $"剧情段包含已发布身份，删除可能使外部状态失效：{episode.EpisodeId}";
                return true;
            }

            message = null;
            return false;
        }

        private static bool DeclaresExit(AuthoringEpisode episode, string exitId)
        {
            if (episode == null || string.IsNullOrWhiteSpace(exitId))
            {
                return false;
            }

            for (var i = 0; i < episode.Nodes.Count; i++)
            {
                var node = episode.Nodes[i];
                if (node != null &&
                    string.Equals(node.NodeId, exitId, StringComparison.Ordinal) &&
                    (node.NodeKind == NodeKind.Choice || node.NodeKind == NodeKind.End))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsCycle(
            string episodeId,
            IReadOnlyDictionary<string, List<string>> outgoing,
            IDictionary<string, int> states)
        {
            if (states.TryGetValue(episodeId, out var state))
            {
                return state == 1;
            }

            states[episodeId] = 1;
            var children = outgoing[episodeId];
            for (var i = 0; i < children.Count; i++)
            {
                if (ContainsCycle(children[i], outgoing, states))
                {
                    return true;
                }
            }

            states[episodeId] = 2;
            return false;
        }

        private static void MarkReachable(
            string episodeId,
            IReadOnlyDictionary<string, List<string>> outgoing,
            ISet<string> reachable)
        {
            if (reachable.Add(episodeId) is false)
            {
                return;
            }

            var children = outgoing[episodeId];
            for (var i = 0; i < children.Count; i++)
            {
                MarkReachable(children[i], outgoing, reachable);
            }
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (var i = 0; i < (values?.Count ?? 0); i++)
            {
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsEpisodeExit(
            IReadOnlyList<ExitIdentity> exits,
            string episodeId)
        {
            for (var i = 0; i < (exits?.Count ?? 0); i++)
            {
                if (string.Equals(exits[i].EpisodeId, episodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
