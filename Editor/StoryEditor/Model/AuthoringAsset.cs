using System;
using System.Collections.Generic;
using GameDeveloperKit.Story;
using UnityEngine;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Publishing;

namespace GameDeveloperKit.StoryEditor.Model
{
    /// <summary>
    /// Story Editor authoring asset。
    /// </summary>
    public sealed class AuthoringAsset : ScriptableObject
    {
        [SerializeField] private string m_StoryId = "new_story";
        [SerializeField] private string m_Version = "1.0.0";
        [SerializeField] private string m_RuntimeProgramAssetPath;
        [SerializeField] private List<AuthoringVolumeAsset> m_VolumeAssets = new List<AuthoringVolumeAsset>();
        [SerializeField] private PublishedIdentityBaseline m_PublishedIdentity = new PublishedIdentityBaseline();

        public string StoryId
        {
            get => m_StoryId;
            set => m_StoryId = value;
        }

        public string Version
        {
            get => m_Version;
            set => m_Version = value;
        }

        public string RuntimeProgramAssetPath
        {
            get => m_RuntimeProgramAssetPath;
            set => m_RuntimeProgramAssetPath = value;
        }

        public IReadOnlyList<AuthoringEpisode> Episodes
        {
            get
            {
                var volumes = Volumes;
                var all = new List<AuthoringEpisode>();
                for (var v = 0; v < volumes.Count; v++)
                {
                    var volume = volumes[v];
                    for (var i = 0; i < (volume?.Episodes.Count ?? 0); i++)
                    {
                        if (volume.Episodes[i] != null)
                        {
                            all.Add(volume.Episodes[i]);
                        }
                    }
                }

                return all.AsReadOnly();
            }
        }

        public IReadOnlyList<AuthoringVolume> Volumes
        {
            get
            {
                var volumes = new List<AuthoringVolume>();
                for (var i = 0; i < VolumeAssets.Count; i++)
                {
                    if (VolumeAssets[i]?.Volume != null)
                    {
                        volumes.Add(VolumeAssets[i].Volume);
                    }
                }

                return volumes.AsReadOnly();
            }
        }

        public AuthoringVolume SelectedVolume
        {
            get
            {
                return Volumes.Count == 0 ? null : Volumes[0];
            }
        }

        public IReadOnlyList<AuthoringVolumeAsset> VolumeAssets
        {
            get
            {
                m_VolumeAssets ??= new List<AuthoringVolumeAsset>();
                return m_VolumeAssets;
            }
        }

        internal void ReplaceVolumeAssets(IReadOnlyList<AuthoringVolumeAsset> volumeAssets)
        {
            m_VolumeAssets ??= new List<AuthoringVolumeAsset>();
            m_VolumeAssets.Clear();
            for (var i = 0; i < (volumeAssets?.Count ?? 0); i++)
            {
                m_VolumeAssets.Add(volumeAssets[i]);
            }
        }

        internal AuthoringVolumeAsset FindVolumeAsset(string volumeId)
        {
            for (var i = 0; i < VolumeAssets.Count; i++)
            {
                var volumeAsset = VolumeAssets[i];
                if (volumeAsset != null &&
                    string.Equals(volumeAsset.Volume.VolumeId, volumeId, StringComparison.Ordinal))
                {
                    return volumeAsset;
                }
            }

            return null;
        }

        internal bool TryGetPublishedIdentity(out IdentityManifest manifest, out string error)
        {
            if (m_PublishedIdentity == null)
            {
                manifest = null;
                error = null;
                return false;
            }

            return m_PublishedIdentity.TryGet(out manifest, out error);
        }

        internal void CommitPublishedIdentity(IdentityManifest manifest)
        {
            m_PublishedIdentity ??= new PublishedIdentityBaseline();
            m_PublishedIdentity.Set(manifest);
        }

        internal void RestorePublishedIdentity(IdentityManifest manifest)
        {
            m_PublishedIdentity ??= new PublishedIdentityBaseline();
            if (manifest == null)
            {
                m_PublishedIdentity.Clear();
                return;
            }

            m_PublishedIdentity.Set(manifest);
        }

        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(m_StoryId))
            {
                m_StoryId = "new_story";
            }

            if (string.IsNullOrWhiteSpace(m_Version))
            {
                m_Version = "1.0.0";
            }

            m_VolumeAssets ??= new List<AuthoringVolumeAsset>();

            var allEpisodes = new List<AuthoringEpisode>();
            for (var v = 0; v < Volumes.Count; v++)
            {
                var volume = Volumes[v];
                if (volume == null)
                {
                    continue;
                }

                allEpisodes.AddRange(volume.Episodes);
            }

            for (var i = 0; i < allEpisodes.Count; i++)
            {
                EnsureEpisode(allEpisodes[i], i);
            }

            for (var i = 0; i < Volumes.Count; i++)
            {
                EnsureExplicitRoute(Volumes[i]);
            }
        }

        private static void EnsureExplicitRoute(AuthoringVolume volume)
        {
            if (volume?.Route == null)
            {
                return;
            }

            var incoming = new HashSet<string>(StringComparer.Ordinal);
            var edgeIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < volume.Route.Edges.Count; i++)
            {
                var edge = volume.Route.Edges[i];
                if (edge == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(edge.ToEpisodeId) is false)
                {
                    incoming.Add(edge.ToEpisodeId);
                }

                if (string.IsNullOrWhiteSpace(edge.EdgeId) is false)
                {
                    edgeIds.Add(edge.EdgeId);
                }
            }

            for (var i = 0; i < volume.Episodes.Count; i++)
            {
                var episode = volume.Episodes[i];
                if (episode == null ||
                    string.IsNullOrWhiteSpace(episode.EpisodeId) ||
                    incoming.Contains(episode.EpisodeId))
                {
                    continue;
                }

                var edgeId = IdentityId.RootEdge(episode.EpisodeId);
                if (edgeIds.Add(edgeId) is false)
                {
                    edgeId = NewId();
                    edgeIds.Add(edgeId);
                }

                volume.Route.Edges.Add(new AuthoringRouteEdge
                {
                    EdgeId = edgeId,
                    SourceKind = GameDeveloperKit.Story.Model.RouteEdgeSourceKind.Root,
                    ToEpisodeId = episode.EpisodeId
                });
                incoming.Add(episode.EpisodeId);
                EnsureRouteLayoutPlacement(volume, episode.EpisodeId, edgeId);
            }
        }

        private static void EnsureRouteLayoutPlacement(AuthoringVolume volume, string episodeId, string edgeId)
        {
            for (var i = 0; i < volume.Layouts.Count; i++)
            {
                var layout = volume.Layouts[i];
                if (layout == null)
                {
                    continue;
                }

                var hasEpisode = false;
                for (var placementIndex = 0; placementIndex < layout.Episodes.Count; placementIndex++)
                {
                    if (string.Equals(layout.Episodes[placementIndex]?.EpisodeId, episodeId, StringComparison.Ordinal))
                    {
                        hasEpisode = true;
                        break;
                    }
                }

                if (hasEpisode is false)
                {
                    var origin = layout.RootPlacement?.Position ?? Vector2.zero;
                    const float offsetX = 0.18f;
                    var offsetY = ((layout.Episodes.Count % 5) - 2) * 0.075f;
                    layout.Episodes.Add(new AuthoringEpisodePlacement
                    {
                        EpisodeId = episodeId,
                        Position = new AuthoringPlacement
                        {
                            Position = layout.Orientation == LayoutOrientation.Portrait
                                ? new Vector2(Mathf.Clamp01(origin.x + offsetY), origin.y + offsetX)
                                : layout.Orientation == LayoutOrientation.Landscape
                                    ? new Vector2(origin.x + offsetX, Mathf.Clamp01(origin.y + offsetY))
                                    : new Vector2(origin.x + offsetX, origin.y + offsetY)
                        }
                    });
                }

                var hasEdge = false;
                for (var edgeIndex = 0; edgeIndex < layout.Edges.Count; edgeIndex++)
                {
                    if (string.Equals(layout.Edges[edgeIndex]?.EdgeId, edgeId, StringComparison.Ordinal))
                    {
                        hasEdge = true;
                        break;
                    }
                }

                if (hasEdge is false)
                {
                    layout.Edges.Add(new AuthoringRouteEdgePlacement { EdgeId = edgeId });
                }
            }
        }

        internal AuthoringEpisode FindDefaultEpisode()
        {
            for (var volumeIndex = 0; volumeIndex < Volumes.Count; volumeIndex++)
            {
                var root = FindRootEpisode(Volumes[volumeIndex]);
                if (root != null)
                {
                    return root;
                }
            }

            return Episodes.Count == 0 ? null : Episodes[0];
        }

        internal AuthoringEpisode FindRootEpisode(AuthoringVolume volume)
        {
            if (volume?.Route == null)
            {
                return null;
            }

            for (var i = 0; i < volume.Route.Edges.Count; i++)
            {
                var edge = volume.Route.Edges[i];
                if (edge != null && edge.SourceKind == GameDeveloperKit.Story.Model.RouteEdgeSourceKind.Root)
                {
                    return FindEpisode(edge.ToEpisodeId);
                }
            }

            return null;
        }

        public AuthoringEpisode FindEpisode(string episodeId)
        {
            for (var v = 0; v < Volumes.Count; v++)
            {
                var volume = Volumes[v];
                if (volume?.Episodes == null)
                {
                    continue;
                }

                for (var i = 0; i < volume.Episodes.Count; i++)
                {
                    var episode = volume.Episodes[i];
                    if (episode != null && string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal))
                    {
                        return episode;
                    }
                }
            }

            return null;
        }

        private static void EnsureEpisode(AuthoringEpisode episode, int index)
        {
            if (episode == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(episode.EpisodeId))
            {
                episode.EpisodeId = NewId();
            }

            if (string.IsNullOrWhiteSpace(episode.Title))
            {
                episode.Title = episode.EpisodeId;
            }

            EnsureEpisodeStartNode(episode);
        }

        private static void EnsureEpisodeStartNode(AuthoringEpisode episode)
        {
            if (episode == null)
            {
                return;
            }

            var start = FindFirstNodeByKind(episode, NodeKind.Start);
            if (start == null)
            {
                var preferredStartId = string.IsNullOrWhiteSpace(episode.EntryNodeId) ? NewId() : episode.EntryNodeId;
                start = new AuthoringNode
                {
                    NodeId = MakeUniqueNodeId(episode, preferredStartId),
                    Title = "开始",
                    NodeKind = NodeKind.Start
                };
                episode.Nodes.Insert(0, start);
            }

            episode.EntryNodeId = start.NodeId;
            RemoveDuplicateBoundaryNodes(episode, NodeKind.Start, start.NodeId);
        }

        private static void RemoveDuplicateBoundaryNodes(AuthoringEpisode episode, NodeKind kind, string keepNodeId)
        {
            if (episode == null || string.IsNullOrWhiteSpace(keepNodeId))
            {
                return;
            }

            for (var i = episode.Nodes.Count - 1; i >= 0; i--)
            {
                var node = episode.Nodes[i];
                if (node == null ||
                    node.NodeKind != kind ||
                    string.Equals(node.NodeId, keepNodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                var nodeId = node.NodeId;
                episode.Nodes.RemoveAt(i);
                episode.Edges.RemoveAll(edge =>
                    edge != null &&
                    (string.Equals(edge.FromNodeId, nodeId, StringComparison.Ordinal) ||
                     string.Equals(edge.TargetNodeId, nodeId, StringComparison.Ordinal)));
            }
        }

        private static AuthoringNode FindFirstNodeByKind(AuthoringEpisode episode, NodeKind kind)
        {
            for (var i = 0; i < episode.Nodes.Count; i++)
            {
                var node = episode.Nodes[i];
                if (node != null && node.NodeKind == kind)
                {
                    return node;
                }
            }

            return null;
        }

        private static string MakeUniqueNodeId(AuthoringEpisode episode, string preferredId)
        {
            var baseId = string.IsNullOrWhiteSpace(preferredId) ? "node" : preferredId;
            var candidate = baseId;
            var index = 2;
            while (ContainsNode(episode, candidate))
            {
                candidate = $"{baseId}_{index}";
                index++;
            }

            return candidate;
        }

        private static bool ContainsNode(AuthoringEpisode episode, string nodeId)
        {
            if (episode == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            for (var i = 0; i < episode.Nodes.Count; i++)
            {
                var node = episode.Nodes[i];
                if (node != null && string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NewId()
        {
            return IdentityId.New();
        }
    }
}
