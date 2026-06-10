using GameDeveloperKit.Config;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.TagEditor
{
    /// <summary>
    /// 定义 Tag Catalog Editor Store 类型。
    /// </summary>
    internal static class TagCatalogEditorStore
    {
        /// <summary>
        /// 定义 Resources Folder 常量。
        /// </summary>
        private const string ResourcesFolder = "Assets/Resources";
        /// <summary>
        /// 定义 Game Developer Kit Folder 常量。
        /// </summary>
        private const string GameDeveloperKitFolder = "Assets/Resources/GameDeveloperKit";

        /// <summary>
        /// 加载 Or Create。
        /// </summary>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 保存 member。
        /// </summary>
        /// <param name="asset">asset 参数。</param>
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

        /// <summary>
        /// 确保 Folder。
        /// </summary>
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
