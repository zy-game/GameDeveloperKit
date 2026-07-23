using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.StoryEditor.Model;
using UnityEditor;

namespace GameDeveloperKit.StoryEditor.Media
{
    public readonly struct MediaUsage
    {
        public MediaUsage(
            string projectAssetPath,
            string volumeAssetPath,
            string storyId,
            string volumeId,
            string episodeId,
            string nodeId,
            string nodeTitle)
        {
            ProjectAssetPath = projectAssetPath ?? string.Empty;
            VolumeAssetPath = volumeAssetPath ?? string.Empty;
            StoryId = storyId ?? string.Empty;
            VolumeId = volumeId ?? string.Empty;
            EpisodeId = episodeId ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            NodeTitle = nodeTitle ?? string.Empty;
        }

        public string AssetPath => VolumeAssetPath;
        public string ProjectAssetPath { get; }
        public string VolumeAssetPath { get; }
        public string StoryId { get; }
        public string VolumeId { get; }
        public string EpisodeId { get; }
        public string NodeId { get; }
        public string NodeTitle { get; }
    }

    public interface IUsageIndex
    {
        IReadOnlyList<MediaUsage> Find(MediaReference reference);
        void Rebuild();
    }

    internal sealed class UsageIndex : IUsageIndex
    {
        private readonly Func<IReadOnlyList<(string Path, AuthoringAsset Asset)>> m_LoadAssets;
        private readonly Dictionary<string, List<MediaUsage>> m_Usages =
            new Dictionary<string, List<MediaUsage>>(StringComparer.Ordinal);

        public UsageIndex(Func<IReadOnlyList<(string Path, AuthoringAsset Asset)>> loadAssets = null)
        {
            m_LoadAssets = loadAssets ?? LoadAllAssets;
        }

        public bool IsAvailable { get; private set; }

        public string ErrorMessage { get; private set; }

        public IReadOnlyList<MediaUsage> Find(MediaReference reference)
        {
            if (IsAvailable is false)
            {
                throw new InvalidOperationException(ErrorMessage ?? "Media usage index is unavailable.");
            }

            return m_Usages.TryGetValue(Identity(reference), out var result)
                ? result
                : Array.Empty<MediaUsage>();
        }

        public void Rebuild()
        {
            m_Usages.Clear();
            IsAvailable = false;
            ErrorMessage = null;
            try
            {
                var assets = m_LoadAssets() ?? Array.Empty<(string, AuthoringAsset)>();
                for (var i = 0; i < assets.Count; i++)
                {
                    Scan(assets[i].Path, assets[i].Asset);
                }

                IsAvailable = true;
            }
            catch (Exception exception)
            {
                m_Usages.Clear();
                ErrorMessage = exception.Message;
            }
        }

        private void Scan(string assetPath, AuthoringAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            if (asset.VolumeAssets.Count == 0)
            {
                ScanEpisodes(assetPath, assetPath, asset.StoryId, string.Empty, asset.Episodes);
                return;
            }

            for (var volumeIndex = 0; volumeIndex < asset.VolumeAssets.Count; volumeIndex++)
            {
                var volumeAsset = asset.VolumeAssets[volumeIndex];
                if (volumeAsset != null)
                {
                    ScanEpisodes(
                        assetPath,
                        AssetDatabase.GetAssetPath(volumeAsset),
                        asset.StoryId,
                        volumeAsset.Volume.VolumeId,
                        volumeAsset.Volume.Episodes);
                }
            }
        }

        private void ScanEpisodes(
            string projectAssetPath,
            string volumeAssetPath,
            string storyId,
            string volumeId,
            IReadOnlyList<AuthoringEpisode> episodes)
        {
            for (var episodeIndex = 0; episodeIndex < (episodes?.Count ?? 0); episodeIndex++)
            {
                var episode = episodes[episodeIndex];
                if (episode?.Nodes == null)
                {
                    continue;
                }

                for (var nodeIndex = 0; nodeIndex < episode.Nodes.Count; nodeIndex++)
                {
                    var node = episode.Nodes[nodeIndex];
                    if (node?.NodeKind != NodeKind.PlayVideo)
                    {
                        continue;
                    }

                    var value = GetParameter(node, MediaCommandNames.ClipArgument);
                    if (VideoReferenceCodec.TryDeserialize(value, out var reference, out _) is false)
                    {
                        continue;
                    }

                    var identity = Identity(reference.Primary);
                    if (m_Usages.TryGetValue(identity, out var usages) is false)
                    {
                        usages = new List<MediaUsage>();
                        m_Usages.Add(identity, usages);
                    }

                    usages.Add(new MediaUsage(
                        projectAssetPath,
                        volumeAssetPath,
                        storyId,
                        volumeId,
                        episode.EpisodeId,
                        node.NodeId,
                        node.Title));
                }
            }
        }

        private static string Identity(MediaReference reference)
        {
            return reference.Source == MediaSource.Cdn
                ? $"cdn:{reference.MediaId}"
                : $"{reference.Source}:{reference.Location}";
        }

        private static string GetParameter(AuthoringNode node, string key)
        {
            for (var i = 0; i < node.Parameters.Count; i++)
            {
                if (string.Equals(node.Parameters[i]?.Key, key, StringComparison.Ordinal))
                {
                    return node.Parameters[i].Value;
                }
            }

            return null;
        }

        private static IReadOnlyList<(string Path, AuthoringAsset Asset)> LoadAllAssets()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(AuthoringAsset)}");
            var result = new List<(string, AuthoringAsset)>(guids.Length);
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<AuthoringAsset>(path);
                if (asset != null)
                {
                    result.Add((path, asset));
                }
            }

            return result;
        }
    }
}
