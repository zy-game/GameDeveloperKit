using System;

namespace GameDeveloperKit.Resource
{
    internal enum ResourceAssetBundleProviderKind
    {
        Bundle,
        Web,
        Editor
    }

    internal static class ResourceProviderFactory
    {
        public static ProviderBase Create(BundleInfo bundle, ResourceAssetBundleProviderKind assetBundleProviderKind)
        {
            if (bundle == null)
            {
                throw new ArgumentNullException(nameof(bundle));
            }

            if (ResourceProviderIds.IsResources(bundle.EffectiveProviderId))
            {
                return new BuiltinAssetProvider(bundle);
            }

            if (ResourceProviderIds.IsAssetBundle(bundle.EffectiveProviderId))
            {
                return assetBundleProviderKind switch
                {
                    ResourceAssetBundleProviderKind.Web => new WebAssetProvider(bundle),
                    ResourceAssetBundleProviderKind.Editor => new EditorAssetProvider(bundle),
                    _ => new BundleAssetProvider(bundle),
                };
            }

            throw new GameException($"Unsupported resource provider: {bundle.EffectiveProviderId}");
        }
    }
}
