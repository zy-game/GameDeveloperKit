using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Resource
{
    public sealed partial class EditorAssetProvider
    {
        /// <summary>
        /// 场景资源加载操作句柄。
        /// </summary>
        public sealed class LoadingSceneAssetOperationHandle : OperationHandle<SceneAssetHandle>
        {
            /// <summary>
            /// 执行操作句柄逻辑。
            /// </summary>
            /// <param name="args">操作参数。</param>
            public override async void Execute(params object[] args)
            {
                try
                {
                    var assetInfo = args.Length > 0 ? args[0] as AssetInfo : null;
                    Validate(assetInfo);

                    var sceneAsset = LoadAssetAtPath(assetInfo.Location, typeof(UnityEngine.Object));
                    if (sceneAsset == null)
                    {
                        SetException(new GameException($"Scene asset load failed: {assetInfo.Location}"));
                        return;
                    }

                    var sceneName = System.IO.Path.GetFileNameWithoutExtension(assetInfo.Location);
                    var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                    if (operation == null)
                    {
                        SetException(new GameException($"Scene load failed: {assetInfo.Location}"));
                        return;
                    }

                    await operation;
                    var scene = SceneManager.GetSceneByName(sceneName);
                    if (scene.IsValid() is false)
                    {
                        SetException(new GameException($"Scene load failed: {assetInfo.Location}"));
                        return;
                    }

                    var handle = SceneAssetHandle.Success(assetInfo, scene);
                    SetResult(handle);
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }

            private static void Validate(AssetInfo assetInfo)
            {
                if (assetInfo == null)
                {
                    throw new ArgumentNullException(nameof(assetInfo));
                }

                if (string.IsNullOrWhiteSpace(assetInfo.Location))
                {
                    throw new ArgumentException("Scene location cannot be empty.", nameof(assetInfo));
                }
            }
        }
    }
}
