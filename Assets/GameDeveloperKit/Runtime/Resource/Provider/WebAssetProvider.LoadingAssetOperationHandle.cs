using System;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class WebAssetProvider
    {
        /// <summary>
        /// 资源加载操作句柄。
        /// </summary>
        public sealed class LoadingAssetOperationHandle : OperationHandle<AssetHandle>
        {
            /// <summary>
            /// 执行操作句柄逻辑。
            /// </summary>
            /// <param name="args">操作参数。</param>
            public override void Execute(params object[] args)
            {
                try
                {
                    var assetInfo = args[0] as AssetInfo;
                    var bundle = args[1] as BundleHandle;
                    Validate(assetInfo, bundle);

                    var asset = bundle.Asset.LoadAsset(assetInfo.Location);
                    if (asset == null)
                    {
                        SetException(new GameException($"Asset load failed: {assetInfo.Location}"));
                        return;
                    }

                    var handle = AssetHandle.Success(assetInfo, asset);
                    SetResult(handle);
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }

            /// <summary>
            /// 校验 member。
            /// </summary>
            /// <param name="assetInfo">asset Info 参数。</param>
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
