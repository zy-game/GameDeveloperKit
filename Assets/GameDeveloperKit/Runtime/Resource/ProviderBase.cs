using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Resource
{
    public abstract class ProviderBase
    {
        public BundleInfo Info { get; }

        public ProviderBase(BundleInfo info)
        {
            Info = info;
        }

        public abstract UniTask<InitializeBundleOperationHandle> InitializeProviderAsync();

        public abstract UniTask<UninitializeBundleOperationHandle> UninitializeProviderAsync();

        public abstract bool HasAsset(string location);

        public abstract UniTask<AssetHandle> LoadAssetAsync(string location);

        public abstract UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label);

        public abstract UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByTypeAsync<T>() where T : UnityEngine.Object;

        public abstract UniTask<RawAssetHandle> LoadRawAssetAsync(string location);

        public abstract UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label);

        public abstract UniTask<SceneAssetHandle> LoadSceneAssetAsync(string name);

        public abstract UniTask UnloadUnusedAssetAsync();

        public abstract UniTask UnloadAsset(AssetHandle handle);

        public virtual void Release()
        {
        }
    }
}
