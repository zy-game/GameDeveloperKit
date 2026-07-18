using System;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源加载 Provider 标识。
    /// </summary>
    public static class ResourceProviderIds
    {
        public const string Resources = "resources";

        public const string AssetBundle = "asset-bundle";

        public static string Normalize(string providerId)
        {
            return string.IsNullOrWhiteSpace(providerId) ? string.Empty : providerId.Trim();
        }

        public static bool IsResources(string providerId)
        {
            return string.Equals(Normalize(providerId), Resources, StringComparison.Ordinal);
        }

        public static bool IsAssetBundle(string providerId)
        {
            return string.Equals(Normalize(providerId), AssetBundle, StringComparison.Ordinal);
        }
    }
}
