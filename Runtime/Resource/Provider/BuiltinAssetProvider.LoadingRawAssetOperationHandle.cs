using System;
using GameDeveloperKit.Operation;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    public sealed partial class BuiltinAssetProvider
    {
        /// <summary>
        /// 二进制资源加载操作句柄。
        /// </summary>
        public sealed class LoadingRawAssetOperationHandle : OperationHandle<RawAssetHandle>
        {
            /// <summary>
            /// 执行操作句柄逻辑。
            /// </summary>
            /// <param name="args">操作参数。</param>
            public override void Execute(params object[] args)
            {
                try
                {
                    var assetInfo = args.Length > 0 ? args[0] as AssetInfo : null;
                    var bundle = args.Length > 1 ? args[1] as BundleHandle : null;
                    var location = NormalizeResourcesLocation(assetInfo);

                    var textAsset = Resources.Load<TextAsset>(location);
                    if (textAsset == null)
                    {
                        SetException(new GameException($"Raw asset load failed: {assetInfo.Location}"));
                        return;
                    }

                    var handle = RawAssetHandle.Success(assetInfo, textAsset.bytes, bundle);
                    SetResult(handle);
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }

            /// <summary>
            /// 执行 Normalize Resources Location。
            /// </summary>
            /// <param name="assetInfo">asset Info 参数。</param>
            private static string NormalizeResourcesLocation(AssetInfo assetInfo)
            {
                if (assetInfo == null)
                {
                    throw new ArgumentNullException(nameof(assetInfo));
                }

                if (string.IsNullOrWhiteSpace(assetInfo.Location))
                {
                    throw new ArgumentException("Raw asset location cannot be empty.", nameof(assetInfo));
                }

                const string resourcesPrefix = "Resources/";
                if (assetInfo.Location.StartsWith(resourcesPrefix, StringComparison.Ordinal) is false)
                {
                    throw new GameException($"Builtin raw asset location must start with '{resourcesPrefix}': {assetInfo.Location}");
                }

                var location = assetInfo.Location.Substring(resourcesPrefix.Length);
                if (string.IsNullOrWhiteSpace(location))
                {
                    throw new ArgumentException("Raw asset location cannot be empty after Resources/ prefix.", nameof(assetInfo));
                }

                return location;
            }
        }
    }
}
