using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// Unity的StreamingAssets资源模式
    /// </summary>
    public sealed partial class StreamingAssetMode : ModeBase
    {
        private List<ProviderBase> _providers = new List<ProviderBase>();

        /// <summary>
        /// 初始化StreamingAssets资源模式。
        /// </summary>
        /// <param name="manifest">资源清单。</param>
        public StreamingAssetMode(ManifestInfo manifest) : base(manifest)
        {
        }

        /// <summary>
        /// 检查是否存在资源。
        /// </summary>
        /// <param name="location">资源地址、类型名或标签。</param>
        /// <returns>如果存在资源，则返回true；否则返回false。</returns>
        public override bool HasAsset(string location)
        {
            return this._providers.Any(p => p.HasAsset(location));
        }

        /// <summary>
        /// 检查资源包是否已经初始化。
        /// </summary>
        /// <param name="package">资源包名。</param>
        /// <returns>如果资源包已经初始化，则返回true；否则返回false。</returns>
        public override bool HasPackage(string package)
        {
            var bundleNames = GetPackageBundleNames(package);
            return bundleNames.Count > 0 && this._providers.Any(x => x.Info != null && bundleNames.Contains(x.Info.Name));
        }

        /// <summary>
        /// 初始化资源包。
        /// </summary>
        /// <param name="package">资源包名。</param>
        /// <returns>资源包初始化操作句柄。</returns>
        public override async UniTask<OperationHandle> InitializePackageAsync(string package)
        {
            ValidateKey(package, nameof(package));
            if (package == BuiltinMode.BUILTIN_PACKAGE_NAME)
            {
                return InitializePackageOperationHandle.Failure(new GameException($"Package not found: {BuiltinMode.BUILTIN_PACKAGE_NAME}"));
            }

            var operation = await App.Operation.WaitCompletionWithKeyAsync<InitializePackageOperationHandle>(package, package, this);
            Status = operation.Status is not OperationStatus.Succeeded ? ResourceStatus.Failed : ResourceStatus.Succeeded;
            return operation;
        }

        /// <summary>
        /// 卸载资源包。
        /// </summary>
        /// <param name="package">资源包名。</param>
        /// <returns>资源包卸载操作句柄。</returns>
        public override async UniTask<OperationHandle> UninitializePackageAsync(string package)
        {
            ValidateKey(package, nameof(package));
            if (package == BuiltinMode.BUILTIN_PACKAGE_NAME || HasPackage(package) is false)
            {
                return UninitializePackageOperationHandle.Failure(new GameException($"Package not found: {package}"));
            }

            Status = ResourceStatus.Unloading;
            var operation = await App.Operation.WaitCompletionWithKeyAsync<UninitializePackageOperationHandle>(package, package, this);
            Status = ResourceStatus.Released;
            return operation;
        }

        /// <summary>
        /// 从StreamingAssets资源包异步加载资源。
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <returns>资源加载句柄。</returns>
        public override async UniTask<AssetHandle> LoadAssetAsync(string location)
        {
            ValidateKey(location, nameof(location));

            var provider = this._providers.FirstOrDefault(x => x.HasAsset(location));
            if (provider == null)
            {
                return AssetHandle.Failure(new GameException($"Asset not found: {location}"));
            }

            return await provider.LoadAssetAsync(location);
        }

        /// <summary>
        /// 从StreamingAssets资源包按标签异步加载资源列表。
        /// </summary>
        /// <param name="label">资源标签。</param>
        /// <returns>资源加载句柄列表。</returns>
        public override async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label)
        {
            ValidateKey(label, nameof(label));
            var handles = new List<AssetHandle>();
            foreach (var provider in this._providers.Where(x => x.HasAsset(label)))
            {
                handles.AddRange(await provider.LoadAssetsByLabelAsync(label));
            }

            return handles;
        }

        /// <summary>
        /// 从StreamingAssets资源包按资源类型异步加载资源列表。
        /// </summary>
        /// <typeparam name="T">Unity资源类型。</typeparam>
        /// <returns>资源加载句柄列表。</returns>
        public override async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByTypeAsync<T>()
        {
            var typeName = typeof(T).Name;
            var handles = new List<AssetHandle>();
            foreach (var provider in this._providers.Where(x => x.HasAsset(typeName)))
            {
                handles.AddRange(await provider.LoadAssetsByTypeAsync<T>());
            }

            return handles;
        }

        /// <summary>
        /// 从StreamingAssets资源包异步加载二进制资源。
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <returns>二进制资源句柄。</returns>
        public override async UniTask<RawAssetHandle> LoadRawAssetAsync(string location)
        {
            ValidateKey(location, nameof(location));
            var provider = this._providers.FirstOrDefault(x => x.HasAsset(location));
            if (provider == null)
            {
                return RawAssetHandle.Failure(new GameException($"Raw asset not found: {location}"));
            }

            return await provider.LoadRawAssetAsync(location);
        }

        /// <summary>
        /// 从StreamingAssets资源包按标签异步加载二进制资源列表。
        /// </summary>
        /// <param name="label">资源标签。</param>
        /// <returns>二进制资源句柄列表。</returns>
        public override async UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label)
        {
            ValidateKey(label, nameof(label));
            var handles = new List<RawAssetHandle>();
            foreach (var provider in this._providers.Where(x => x.HasAsset(label)))
            {
                handles.AddRange(await provider.LoadRawAssetsByLabelAsync(label));
            }

            return handles;
        }

        /// <summary>
        /// 从StreamingAssets资源包异步加载场景资源。
        /// </summary>
        /// <param name="name">场景资源地址或名称。</param>
        /// <returns>场景资源句柄。</returns>
        public override async UniTask<SceneAssetHandle> LoadSceneAssetAsync(string name)
        {
            ValidateKey(name, nameof(name));
            var provider = this._providers.FirstOrDefault(x => x.HasAsset(name));
            if (provider == null)
            {
                return SceneAssetHandle.Failure(new GameException($"Scene not found: {name}"));
            }

            return await provider.LoadSceneAssetAsync(name);
        }

        /// <summary>
        /// 卸载所有StreamingAssets提供者中未使用的资源。
        /// </summary>
        /// <returns>卸载任务。</returns>
        public override async UniTask UnloadUnusedAssetAsync()
        {
            foreach (var provider in this._providers.ToArray())
            {
                await provider.UnloadUnusedAssetAsync();
                if (provider.IsReferenced || provider.HasLoadedAssets)
                {
                    continue;
                }

                var operation = await provider.UninitializeProviderAsync();
                if (operation.Status is not OperationStatus.Succeeded)
                {
                    throw new GameException($"Bundle uninitialize failed: {provider.Info?.Name}", operation.Error);
                }

                provider.Release();
                this._providers.Remove(provider);
            }
        }

        /// <summary>
        /// 卸载资源句柄。
        /// </summary>
        /// <param name="handle">资源句柄。</param>
        /// <returns>卸载任务。</returns>
        /// <exception cref="ArgumentNullException">资源句柄为空时抛出。</exception>
        public override async UniTask UnloadAsset(AssetHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (handle.Info == null)
            {
                handle.Release();
                return;
            }

            var provider = this._providers.FirstOrDefault(x => x.HasAsset(handle.Info.Location));
            if (provider == null)
            {
                return;
            }

            await provider.UnloadAsset(handle);
        }

        /// <summary>
        /// 卸载二进制资源句柄。
        /// </summary>
        /// <param name="handle">二进制资源句柄。</param>
        /// <returns>卸载任务。</returns>
        /// <exception cref="ArgumentNullException">资源句柄为空时抛出。</exception>
        public override async UniTask UnloadRawAsset(RawAssetHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (handle.Info == null)
            {
                handle.Release();
                return;
            }

            var provider = this._providers.FirstOrDefault(x => x.HasAsset(handle.Info.Location));
            if (provider == null)
            {
                return;
            }

            await provider.UnloadRawAsset(handle);
        }

        /// <summary>
        /// 卸载场景资源句柄。
        /// </summary>
        /// <param name="handle">场景资源句柄。</param>
        /// <returns>卸载任务。</returns>
        /// <exception cref="ArgumentNullException">资源句柄为空时抛出。</exception>
        public override async UniTask UnloadSceneAsset(SceneAssetHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (handle.Info == null)
            {
                handle.Release();
                return;
            }

            var provider = this._providers.FirstOrDefault(x => x.HasAsset(handle.Info.Location));
            if (provider == null)
            {
                return;
            }

            await provider.UnloadSceneAsset(handle);
        }

        /// <summary>
        /// 释放StreamingAssets资源模式，并释放所有资源提供者。
        /// </summary>
        public override void Release()
        {
            foreach (var provider in _providers.ToArray())
            {
                provider.Release();
            }

            _providers.Clear();
        }

        /// <summary>
        /// 校验 Key。
        /// </summary>
        /// <param name="parameterName">parameter Name 参数。</param>
        private static void ValidateKey(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }
    }
}
