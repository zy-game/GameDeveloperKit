using UnityEditor;

namespace GameDeveloperKit
{
    internal static class GameDeveloperKitEditorPaths
    {
        private const string AssetsRoot = "Assets/GameDeveloperKit";
        private const string PackageRoot = "Packages/com.gamedeveloperkit.framework";

        private static string s_Root;

        public static string PackageAssetPath(string relativePath)
        {
            return $"{Root}/{NormalizeRelativePath(relativePath)}";
        }

        public static T LoadPackageAsset<T>(string relativePath) where T : UnityEngine.Object
        {
            var normalizedPath = NormalizeRelativePath(relativePath);

            var asset = LoadPackageAssetAtRoot<T>(Root, normalizedPath);
            if (asset != null)
            {
                return asset;
            }

            if (string.Equals(Root, PackageRoot, System.StringComparison.Ordinal) is false)
            {
                asset = LoadPackageAssetAtRoot<T>(PackageRoot, normalizedPath);
                if (asset != null)
                {
                    return asset;
                }
            }

            if (string.Equals(Root, AssetsRoot, System.StringComparison.Ordinal) is false)
            {
                asset = LoadPackageAssetAtRoot<T>(AssetsRoot, normalizedPath);
                if (asset != null)
                {
                    return asset;
                }
            }

            return null;
        }

        private static string Root
        {
            get
            {
                if (string.IsNullOrWhiteSpace(s_Root))
                {
                    s_Root = ResolveRoot();
                }

                return s_Root;
            }
        }

        private static string ResolveRoot()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(GameDeveloperKitEditorPaths).Assembly);
            if (string.IsNullOrWhiteSpace(packageInfo?.assetPath) is false)
            {
                return NormalizePath(packageInfo.assetPath);
            }

            if (AssetDatabase.IsValidFolder(PackageRoot))
            {
                return PackageRoot;
            }

            return AssetsRoot;
        }

        private static T LoadPackageAssetAtRoot<T>(string root, string relativePath) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>($"{root}/{relativePath}");
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            var normalizedPath = NormalizePath(relativePath).Trim('/');
            if (normalizedPath.StartsWith(AssetsRoot + "/", System.StringComparison.Ordinal))
            {
                return normalizedPath.Substring(AssetsRoot.Length + 1);
            }

            if (normalizedPath.StartsWith(PackageRoot + "/", System.StringComparison.Ordinal))
            {
                return normalizedPath.Substring(PackageRoot.Length + 1);
            }

            return normalizedPath;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }
}
