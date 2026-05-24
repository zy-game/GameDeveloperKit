using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Resource
{
    public sealed class BundleProvider : ProviderBase
    {
        private BundleHandle _bundle;
        private List<ResourceHandle> _assets;
        private List<ResourceHandle> _pendingUnloadAssets;

        public BundleProvider(BundleInfo bundleInfo) : base(bundleInfo)
        {
            _assets = new List<ResourceHandle>();
            _pendingUnloadAssets = new List<ResourceHandle>();
        }

        public override async UniTask<InitializeBundleOperationHandle> InitializeProviderAsync()
        {
            if (Info is null)
            {
                return InitializeBundleOperationHandle.Failure(new GameException(""));
            }

            var operation = await Super.Operation.WaitCompletionAsync<InitializeBundleOperationHandle>(this, Info);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                return InitializeBundleOperationHandle.Failure(new GameException(""));
            }

            _bundle = operation.Value;
            return operation;
        }

        public override async UniTask<UninitializeBundleOperationHandle> UninitializeProviderAsync()
        {
            if (_bundle is null)
            {
                return UninitializeBundleOperationHandle.Failure(new GameException(""));
            }

            var operation = await Super.Operation.WaitCompletionAsync<UninitializeBundleOperationHandle>(this, Info);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                return UninitializeBundleOperationHandle.Failure(new GameException(""));
            }

            return operation;
        }

        public override bool HasAsset(string location)
        {
            return this.Info.Assets.Any(x => x.Location == location || x.TypeName == location || (x.Labels != null && x.Labels.Contains(location)));
        }

        private bool TryGetAsset<T>(AssetInfo asset, out T assetHandle) where T : ResourceHandle
        {
            var target = _assets.FirstOrDefault(x => x.Info == asset);
            if (target is not null)
            {
                assetHandle = (T)target;
                return assetHandle != null;
            }

            target = _pendingUnloadAssets.FirstOrDefault(x => x.Info == asset);
            if (target is not null)
            {
                _pendingUnloadAssets.Remove(target);
                _assets.Add(target);
            }

            assetHandle = (T)target;
            return target != null;
        }

        public override async UniTask<AssetHandle> LoadAssetAsync(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return AssetHandle.Failure(new GameException("参数不能为空:" + location));
            }

            if (Info is null)
            {
                return AssetHandle.Failure(new GameException("资源包信息为空"));
            }

            var asset = this.Info.Assets.FirstOrDefault(x => x.Location == location || x.TypeName == location || (x.Labels != null && x.Labels.Contains(location)));
            if (asset is null)
            {
                return AssetHandle.Failure(new GameException("没有找到相关的资源信息：" + location));
            }

            if (TryGetAsset<AssetHandle>(asset, out var resource))
            {
                return resource;
            }

            var handle = await Super.Operation.WaitCompletionAsync<LoadingAssetOperationHandle>(asset, asset, _bundle, _assets);
            if (handle.Status is not OperationStatus.Succeeded)
            {
                return AssetHandle.Failure(handle.Error);
            }

            return handle.Value;
        }

        public override async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return new List<AssetHandle>();
            }

            if (Info is null)
            {
                return Array.Empty<AssetHandle>();
            }

            List<AssetHandle> handles = new List<AssetHandle>();
            var assets = this.Info.Assets.Where(x => x.Labels != null && x.Labels.Contains(label));
            foreach (var asset in assets)
            {
                if (TryGetAsset<AssetHandle>(asset, out var resource))
                {
                    handles.Add(resource);
                }
                else
                {
                    var handle = await Super.Operation.WaitCompletionAsync<LoadingAssetOperationHandle>(asset, asset, _bundle, _assets);
                    if (handle.Status is OperationStatus.Succeeded)
                    {
                        handles.Add(handle.Value);
                    }
                }
            }

            return handles;
        }

        public override async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByTypeAsync<T>()
        {
            if (Info is null)
            {
                return Array.Empty<AssetHandle>();
            }

            string typeName = typeof(T).Name;
            List<AssetHandle> handles = new List<AssetHandle>();
            var assets = this.Info.Assets.Where(x => x.TypeName == typeName);
            foreach (var asset in assets)
            {
                if (TryGetAsset<AssetHandle>(asset, out var resource))
                {
                    handles.Add(resource);
                }
                else
                {
                    var handle = await Super.Operation.WaitCompletionAsync<LoadingAssetOperationHandle>(asset, asset, _bundle, _assets);
                    if (handle.Status is OperationStatus.Succeeded)
                    {
                        handles.Add(handle.Value);
                    }
                }
            }

            return handles;
        }

        public override async UniTask<RawAssetHandle> LoadRawAssetAsync(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return RawAssetHandle.Failure(new ArgumentNullException("location"));
            }

            if (Info is null)
            {
                return RawAssetHandle.Failure(new ArgumentNullException("Info"));
            }

            var asset = this.Info.Assets.FirstOrDefault(x => x.Location == location);
            if (asset is null)
            {
                return RawAssetHandle.Failure(new GameException("未找到相关资源:" + location));
            }

            if (TryGetAsset<RawAssetHandle>(asset, out var resource))
            {
                return resource;
            }

            var operation = await Super.Operation.WaitCompletionAsync<LoadingRawAssetOperationHandle>(asset, asset, _bundle, _assets);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                return RawAssetHandle.Failure(operation.Error);
            }

            return operation.Value;
        }

        public override async UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return Array.Empty<RawAssetHandle>();
            }

            if (Info is null)
            {
                return Array.Empty<RawAssetHandle>();
            }

            List<RawAssetHandle> handles = new List<RawAssetHandle>();
            var assets = this.Info.Assets.Where(x => x.Labels != null && x.Labels.Contains(label));
            foreach (var asset in assets)
            {
                if (TryGetAsset<RawAssetHandle>(asset, out var resource))
                {
                    handles.Add(resource);
                }
                else
                {
                    var operation = await Super.Operation.WaitCompletionAsync<LoadingRawAssetOperationHandle>(asset, asset, _bundle, _assets);
                    if (operation.Status is OperationStatus.Succeeded)
                    {
                        handles.Add(operation.Value);
                    }
                }
            }

            return handles;
        }

        public override async UniTask<SceneAssetHandle> LoadSceneAssetAsync(string name)
        {
            if (Info is null)
            {
                return SceneAssetHandle.Failure(new ArgumentNullException("Info"));
            }

            var asset = this.Info.Assets.FirstOrDefault(x => x.Location == name);
            if (asset is null)
            {
                return SceneAssetHandle.Failure(new GameException(""));
            }

            if (TryGetAsset<SceneAssetHandle>(asset, out var resource))
            {
                return resource;
            }

            var operation = await Super.Operation.WaitCompletionAsync<LoadingSceneAssetOperationHandle>(asset, asset, _bundle, _assets);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                return SceneAssetHandle.Failure(new GameException(""));
            }

            return operation.Value;
        }

        public override async UniTask UnloadUnusedAssetAsync()
        {
            await Resources.UnloadUnusedAssets();
        }

        public override async UniTask UnloadAsset(AssetHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (_assets.Contains(handle))
            {
                _assets.Remove(handle);
                _pendingUnloadAssets.Add(handle);
            }

            handle.Release();
            await UniTask.CompletedTask;
        }

        public override void Release()
        {
            if (_bundle == null)
            {
                return;
            }

            _bundle.Release();
            _bundle = null;
        }
    }
}