using System.IO;
using UnityEditor;
using UnityEngine;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Publishing;

namespace GameDeveloperKit.StoryEditor.Model
{
    /// <summary>
    /// Story authoring asset 存取。
    /// </summary>
    internal static class AuthoringAssetStore
    {
        private const string DefaultFolder = "Assets/Bundles/Story";
        private const string DefaultAssetPath = DefaultFolder + "/NewStoryAuthoring.asset";

        public static AuthoringAsset LoadOrCreate()
        {
            EnsureFolder(DefaultFolder);

            var asset = AssetDatabase.LoadAssetAtPath<AuthoringAsset>(DefaultAssetPath);
            if (asset != null)
            {
                asset.EnsureDefaults();
                return asset;
            }

            asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            asset.EnsureDefaults();
            InitializeCurrentRoute(asset);
            AssetDatabase.CreateAsset(asset, DefaultAssetPath);
            Save(asset);
            return asset;
        }

        public static AuthoringAsset CreateAtPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            EnsureFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            asset.EnsureDefaults();
            InitializeCurrentRoute(asset);
            AssetDatabase.CreateAsset(asset, assetPath);
            Save(asset);
            return asset;
        }

        public static void Save(AuthoringAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            asset.EnsureDefaults();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var name = Path.GetFileName(folder);
            EnsureFolder(parent);
            if (string.IsNullOrWhiteSpace(parent) is false && string.IsNullOrWhiteSpace(name) is false && AssetDatabase.IsValidFolder(folder) is false)
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void InitializeCurrentRoute(AuthoringAsset asset)
        {
            for (var i = 0; i < asset.Volumes.Count; i++)
            {
                var volume = asset.Volumes[i];
                if (volume == null || volume.Episodes.Count == 0)
                {
                    continue;
                }

                var root = volume.Episodes[0];
                volume.Route = new AuthoringRoute();
                volume.Route.Edges.Add(new AuthoringRouteEdge
                {
                    EdgeId = IdentityId.RootEdge(root.EpisodeId),
                    SourceKind = RouteEdgeSourceKind.Root,
                    ToEpisodeId = root.EpisodeId
                });
            }
        }
    }
}
