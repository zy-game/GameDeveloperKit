using System;
using System.Collections.Generic;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class BundleProvider
    {
        public sealed class LoadingAssetOperationHandle : OperationHandle<AssetHandle>
        {
            public override void Execute(params object[] args)
            {
                try
                {
                    var assetInfo = args[0] as AssetInfo;
                    var bundle = args[1] as BundleHandle;
                    var assets = args.Length > 2 ? args[2] as List<ResourceHandle> : null;
                    Validate(assetInfo, bundle);

                    var asset = bundle.Asset.LoadAsset(assetInfo.Location);
                    if (asset == null)
                    {
                        SetException(new GameException($"Asset load failed: {assetInfo.Location}"));
                        return;
                    }

                    var handle = AssetHandle.Success(assetInfo, asset);
                    assets?.Add(handle);
                    SetResult(handle);
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }

            private static void Validate(AssetInfo assetInfo, BundleHandle bundle)
            {
                if (assetInfo == null)
                {
                    throw new ArgumentNullException(nameof(assetInfo));
                }

                if (string.IsNullOrWhiteSpace(assetInfo.Location))
                {
                    throw new ArgumentException("Asset location cannot be empty.", nameof(assetInfo));
                }

                if (bundle == null || bundle.Asset == null)
                {
                    throw new ArgumentNullException(nameof(bundle));
                }
            }
        }
    }
}
