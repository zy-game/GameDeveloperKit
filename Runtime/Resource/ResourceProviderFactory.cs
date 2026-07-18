using System;

namespace GameDeveloperKit.Resource
{
    internal static class ResourceProviderFactory
    {
        /// <summary>
        /// 根据 bundle 的 provider 标识与当前资源模式创建对应 provider。
        /// </summary>
        /// <param name="bundle">资源包信息。</param>
        /// <param name="mode">当前资源模式，决定 AssetBundle 系 bundle 用哪种 provider。</param>
        /// <returns>资源 provider。</returns>
        public static ProviderBase Create(
            BundleInfo bundle,
            ResourceMode mode,
            string manifestVersion,
            bool isRemote)
        {
            if (bundle == null)
            {
                throw new ArgumentNullException(nameof(bundle));
            }

            if (string.Equals(bundle.ProviderId, ResourceProviderIds.Resources, StringComparison.Ordinal))
            {
                return new BuiltinAssetProvider(bundle);
            }

            if (string.Equals(bundle.ProviderId, ResourceProviderIds.AssetBundle, StringComparison.Ordinal))
            {
                return mode == ResourceMode.EditorSimulator
                    ? new EditorAssetProvider(bundle)
                    : new BundleAssetProvider(bundle, mode, manifestVersion, isRemote);
            }

            throw new GameException($"Unsupported resource provider: {bundle.ProviderId}");
        }
    }
}
