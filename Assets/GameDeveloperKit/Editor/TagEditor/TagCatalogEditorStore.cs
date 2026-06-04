using GameDeveloperKit.Config;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.TagEditor
{
    internal static class TagCatalogEditorStore
    {
        private const string ResourcesFolder = "Assets/Resources";
        private const string GameDeveloperKitFolder = "Assets/Resources/GameDeveloperKit";

        public static TagCatalogAsset LoadOrCreate()
        {
            EnsureFolder();

            var asset = AssetDatabase.LoadAssetAtPath<TagCatalogAsset>(TagCatalogAsset.AssetPath);
            if (asset != null)
            {
                asset.EnsureDefaults();
                return asset;
            }

            asset = ScriptableObject.CreateInstance<TagCatalogAsset>();
            asset.EnsureDefaults();
            AssetDatabase.CreateAsset(asset, TagCatalogAsset.AssetPath);
            Save(asset);
            return asset;
        }

        public static void Save(TagCatalogAsset asset)
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

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder(GameDeveloperKitFolder))
            {
                AssetDatabase.CreateFolder(ResourcesFolder, "GameDeveloperKit");
            }
        }
    }
}
