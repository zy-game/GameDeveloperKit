using System;
using GameDeveloperKit.Operation;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    public sealed partial class EditorAssetProvider
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
                    var assetInfo = args.Length > 0 ? args[0] as AssetInfo : null;
                    Validate(assetInfo);

                    var asset = LoadAssetAtPath(assetInfo.Location, typeof(UnityEngine.Object));
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
            private static void Validate(AssetInfo assetInfo)
            {
                if (assetInfo == null)
                {
                    throw new ArgumentNullException(nameof(assetInfo));
                }

                if (string.IsNullOrWhiteSpace(assetInfo.Location))
                {
                    throw new ArgumentException("Asset location cannot be empty.", nameof(assetInfo));
                }
            }
        }
    }
}
