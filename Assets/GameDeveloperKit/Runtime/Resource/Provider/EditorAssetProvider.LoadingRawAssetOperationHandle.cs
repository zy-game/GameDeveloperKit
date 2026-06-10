using System;
using GameDeveloperKit.Operation;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 定义 Editor Asset Provider 类型。
    /// </summary>
    public sealed partial class EditorAssetProvider
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
                    Validate(assetInfo);

                    var textAsset = LoadAssetAtPath(assetInfo.Location, typeof(TextAsset)) as TextAsset;
                    if (textAsset == null)
                    {
                        SetException(new GameException($"Raw asset load failed: {assetInfo.Location}"));
                        return;
                    }

                    var handle = RawAssetHandle.Success(assetInfo, textAsset.bytes);
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
                    throw new ArgumentException("Raw asset location cannot be empty.", nameof(assetInfo));
                }
            }
        }
    }
}
