using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Resource
{
    public sealed partial class BundleProvider
    {
        public sealed class LoadingSceneAssetOperationHandle : OperationHandle<SceneAssetHandle>
        {
            public override async void Execute(params object[] args)
            {
                try
                {
                    var assetInfo = args[0] as AssetInfo;
                    var bundle = args[1] as BundleHandle;
                    var assets = args.Length > 2 ? args[2] as List<ResourceHandle> : null;
                    Validate(assetInfo, bundle);

                    var operation = SceneManager.LoadSceneAsync(assetInfo.Location, LoadSceneMode.Additive);
                    if (operation == null)
                    {
                        SetException(new GameException($"Scene load failed: {assetInfo.Location}"));
                        return;
                    }

                    await operation;
                    var scene = SceneManager.GetSceneByName(assetInfo.Location);
                    if (scene.IsValid() is false)
                    {
                        SetException(new GameException($"Scene load failed: {assetInfo.Location}"));
                        return;
                    }

                    var handle = SceneAssetHandle.Success(assetInfo, scene);
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
                    throw new ArgumentException("Scene location cannot be empty.", nameof(assetInfo));
                }

                if (bundle == null || bundle.Asset == null)
                {
                    throw new ArgumentNullException(nameof(bundle));
                }
            }
        }
    }
}
