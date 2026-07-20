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
            string assetPath,
            string storyId,
            string episodeId,
            string nodeId,
            string nodeTitle)
        {
            AssetPath = assetPath ?? string.Empty;
            StoryId = storyId ?? string.Empty;
            EpisodeId = episodeId ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            NodeTitle = nodeTitle ?? string.Empty;
        }

        public string AssetPath { get; }
        public string StoryId { get; }
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
            if (asset?.Episodes == null)
            {
                return;
            }

            for (var episodeIndex = 0; episodeIndex < asset.Episodes.Count; episodeIndex++)
            {
                var episode = asset.Episodes[episodeIndex];
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
                        assetPath,
                        asset.StoryId,
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
