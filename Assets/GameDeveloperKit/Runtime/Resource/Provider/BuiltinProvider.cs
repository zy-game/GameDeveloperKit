using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    public sealed partial class BuiltinProvider : ProviderBase
    {
        private List<ResourceHandle> _assets;
        private List<ResourceHandle> _pendingUnloadingAssets;

        public BuiltinProvider(BundleInfo bundleInfo) : base(bundleInfo)
        {
            _assets = new List<ResourceHandle>();
            _pendingUnloadingAssets = new List<ResourceHandle>();
        }

        public override UniTask<OperationHandle<BundleHandle>> InitializeProviderAsync()
        {
            return UniTask.FromResult<OperationHandle<BundleHandle>>(InitializeBundleOperationHandle.Success(Info));
        }

        public override UniTask<OperationHandle> UninitializeProviderAsync()
        {
            return UniTask.FromResult<OperationHandle>(UninitializeBundleOperationHandle.Sucecess());
        }

        public override bool HasAsset(string location)
        {
            return Info.Assets.Any(x => x.Location == location || x.TypeName == location || (x.Labels != null && x.Labels.Contains(location)));
        }

        private bool TryGetAsset<T>(AssetInfo info, out T handle) where T : ResourceHandle
        {
            var target = _assets.FirstOrDefault(x => x.Info == info);
            if (target is not null)
            {
                handle = (T)target;
                return handle != null;
            }

            target = _pendingUnloadingAssets.FirstOrDefault(x => x.Info == info);
            if (target is not null)
            {
                _pendingUnloadingAssets.Remove(target);
                _assets.Add(target);
            }

            handle = (T)target;
            return target != null;
        }

        public override async UniTask<AssetHandle> LoadAssetAsync(string location)
        {
            if (Info is null)
            {
                return AssetHandle.Failure(new GameException("Cannot find asset"));
            }

            var asset = Info.Assets.FirstOrDefault(x => x.Location == location || x.TypeName == location || (x.Labels != null && x.Labels.Contains(location)));
            if (asset is null)
            {
                return AssetHandle.Failure(new GameException("Cannot find asset"));
            }

            if (TryGetAsset<AssetHandle>(asset, out var handle))
            {
                return handle;
            }

            var operation = await Super.Operation.WaitCompletionAsync<LoadingAssetOperationHandle>(asset, asset, _assets);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                return AssetHandle.Failure(new GameException("Cannot load asset"));
            }

            _assets.Add(operation.Value);
            return operation.Value;
        }

        public override async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label)
        {
            if (Info is null)
            {
                return Array.Empty<AssetHandle>();
            }

            var assets = Info.Assets.Where(x => x.Labels.Contains(label));
            if (assets.Count() == 0)
            {
                return Array.Empty<AssetHandle>();
            }

            List<AssetHandle> handles = new List<AssetHandle>();
            foreach (var asset in assets)
            {
                if (TryGetAsset<AssetHandle>(asset, out var handle))
                {
                    handles.Add(handle);
                }
                else
                {
                    var operation = await Super.Operation.WaitCompletionAsync<LoadingAssetOperationHandle>(asset, asset, _assets);
                    if (operation.Status is OperationStatus.Succeeded)
                    {
                        handles.Add(operation.Value);
                    }
                }
            }

            _assets.AddRange(handles);
            return handles;
        }

        public override async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByTypeAsync<T>()
        {
            if (Info is null)
            {
                return Array.Empty<AssetHandle>();
            }

            string typeName = typeof(T).Name;
            var assets = Info.Assets.Where(x => x.TypeName == typeName);
            if (assets.Count() == 0)
            {
                return Array.Empty<AssetHandle>();
            }

            List<AssetHandle> handles = new List<AssetHandle>();
            foreach (var asset in assets)
            {
                if (TryGetAsset<AssetHandle>(asset, out var handle))
                {
                    handles.Add(handle);
                }
                else
                {
                    var operation = await Super.Operation.WaitCompletionAsync<LoadingAssetOperationHandle>(asset, asset, _assets);
                    if (operation.Status is OperationStatus.Succeeded)
                    {
                        handles.Add(operation.Value);
                    }
                }
            }

            _assets.AddRange(handles);
            return handles;
        }

        public override async UniTask<RawAssetHandle> LoadRawAssetAsync(string location)
        {
            if (Info is null)
            {
                return RawAssetHandle.Failure(new GameException("Cannot find asset"));
            }

            var asset = Info.Assets.FirstOrDefault(x => x.Location == location || x.TypeName == location || (x.Labels != null && x.Labels.Contains(location)));
            if (asset is null)
            {
                return RawAssetHandle.Failure(new GameException("Cannot find asset"));
            }

            if (TryGetAsset<RawAssetHandle>(asset, out var handle))
            {
                return handle;
            }

            var operation = await Super.Operation.WaitCompletionAsync<LoadingRawAssetOperationHandle>(asset, asset, _assets);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                return RawAssetHandle.Failure(new GameException("Cannot load asset"));
            }

            _assets.Add(operation.Value);
            return operation.Value;
        }

        public override async UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label)
        {
            if (Info is null)
            {
                return Array.Empty<RawAssetHandle>();
            }

            var assets = Info.Assets.Where(x => x.Labels.Contains(label));
            if (assets.Count() == 0)
            {
                return Array.Empty<RawAssetHandle>();
            }

            List<RawAssetHandle> handles = new List<RawAssetHandle>();
            foreach (var asset in assets)
            {
                if (TryGetAsset<RawAssetHandle>(asset, out var handle))
                {
                    handles.Add(handle);
                }
                else
                {
                    var operation = await Super.Operation.WaitCompletionAsync<LoadingRawAssetOperationHandle>(asset, asset);
                    if (operation.Status is OperationStatus.Succeeded)
                    {
                        handles.Add(operation.Value);
                    }
                }
            }

            _assets.AddRange(handles);
            return handles;
        }


        public override async UniTask<SceneAssetHandle> LoadSceneAssetAsync(string location)
        {
            if (Info is null)
            {
                return SceneAssetHandle.Failure(new GameException("Cannot find asset"));
            }

            var asset = Info.Assets.FirstOrDefault(x => x.Location == location || x.TypeName == location || (x.Labels != null && x.Labels.Contains(location)));
            if (asset is null)
            {
                return SceneAssetHandle.Failure(new GameException("Cannot find asset"));
            }

            if (TryGetAsset<SceneAssetHandle>(asset, out var handle))
            {
                return handle;
            }

            var operation = await Super.Operation.WaitCompletionAsync<LoadingSceneAssetOperationHandle>(asset, asset);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                return SceneAssetHandle.Failure(new GameException("Cannot load asset"));
            }

            _assets.Add(operation.Value);
            return operation.Value;
        }

        public override async UniTask UnloadUnusedAssetAsync()
        {
            await Resources.UnloadUnusedAssets();
        }

        public override UniTask UnloadAsset(AssetHandle handle)
        {
            if (_assets is null || _assets.Count == 0)
            {
                return UniTask.CompletedTask;
            }

            if (_assets.Contains(handle))
            {
                _assets.Remove(handle);
            }

            _pendingUnloadingAssets.Add(handle);
            return UniTask.CompletedTask;
        }
    }
}
