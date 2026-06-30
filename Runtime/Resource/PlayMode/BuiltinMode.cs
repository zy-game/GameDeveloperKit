using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 针对Unity内部资源的加载模式
    /// </summary>
    public sealed partial class BuiltinMode : ModeBase
    {
        private readonly List<ProviderBase> _providers = new List<ProviderBase>();

        /// <summary>
        /// 内置资源包名称。
        /// </summary>
        public const string BUILTIN_PACKAGE_NAME = "BUILTIN";

        /// <summary>
        /// 初始化内置资源模式。
        /// </summary>
        /// <param name="manifest">资源清单。</param>
        public BuiltinMode(ManifestInfo manifest) : base(manifest)
        {
        }

        /// <summary>
        /// 查询资源是否存在
        /// </summary>
        /// <param name="location">资源地址、类型名或标签。</param>
        /// <returns>如果内置资源包包含该资源，则返回true；否则返回false。</returns>
        public override bool HasAsset(string location)
        {
            return this._providers.Any(provider => provider.HasAsset(location));
        }

        /// <summary>
        /// 检查是否为内置资源包。
        /// </summary>
        /// <param name="package">资源包名。</param>
        /// <returns>如果资源包名为内置资源包，则返回true；否则返回false。</returns>
        public override bool HasPackage(string package)
        {
            return package == BUILTIN_PACKAGE_NAME;
        }

        /// <summary>
        /// 初始化内置资源包。
        /// </summary>
        /// <param name="package">资源包名。</param>
        /// <returns>资源包初始化操作句柄。</returns>
        /// <exception cref="ArgumentException">资源包名不是内置资源包时抛出。</exception>
        public override async UniTask<OperationHandle> InitializePackageAsync(string package)
        {
            if (package is not BUILTIN_PACKAGE_NAME)
            {
                throw new ArgumentException($"Invalid package: {package}");
            }

            Status = ResourceStatus.Loading;
            var operation = await App.Operation.WaitCompletionWithKeyAsync<InitializePackageOperationHandle>(package, package, this);
            Status = operation.Status is not OperationStatus.Succeeded ? ResourceStatus.Failed : ResourceStatus.Succeeded;
            return operation;
        }

        /// <summary>
        /// 卸载内置资源包。
        /// </summary>
        /// <param name="package">资源包名。</param>
        /// <returns>资源包卸载操作句柄。</returns>
        /// <exception cref="ArgumentException">资源包名不是内置资源包时抛出。</exception>
        public override async UniTask<OperationHandle> UninitializePackageAsync(string package)
        {
            if (package is not BUILTIN_PACKAGE_NAME)
            {
                throw new ArgumentException($"Invalid package: {package}");
            }

            Status = ResourceStatus.Unloading;
            var operation = await App.Operation.WaitCompletionWithKeyAsync<UninitializePackageOperationHandle>(package, package, this);

            Status = ResourceStatus.Released;
            return operation;
        }

        /// <summary>
        /// 从内置资源包异步加载资源。
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <returns>资源加载句柄。</returns>
        /// <exception cref="GameException">内置资源包未初始化时抛出。</exception>
        public override async UniTask<AssetHandle> LoadAssetAsync(string location)
        {
            var provider = GetProvider(location);
            if (provider == null)
            {
                return default;
            }

            return await provider.LoadAssetAsync(location);
        }

        /// <summary>
        /// 从内置资源包按标签异步加载资源列表。
        /// </summary>
        /// <param name="label">资源标签。</param>
        /// <returns>资源加载句柄列表。</returns>
        /// <exception cref="GameException">内置资源包未初始化或标签不存在时抛出。</exception>
        public override async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label)
        {
            var handles = new List<AssetHandle>();
            foreach (var provider in this._providers.Where(provider => provider.HasAsset(label)))
            {
                handles.AddRange(await provider.LoadAssetsByLabelAsync(label));
            }

            return handles;
        }

        /// <summary>
        /// 从内置资源包按资源类型异步加载资源列表。
        /// </summary>
        /// <typeparam name="T">Unity资源类型。</typeparam>
        /// <returns>资源加载句柄列表。</returns>
        /// <exception cref="GameException">内置资源包未初始化或资源类型不存在时抛出。</exception>
        public override async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByTypeAsync<T>()
        {
            var assetTypeName = typeof(T).Name;
            var handles = new List<AssetHandle>();
            foreach (var provider in this._providers.Where(provider => provider.HasAsset(assetTypeName)))
            {
                handles.AddRange(await provider.LoadAssetsByTypeAsync<T>());
            }

            return handles;
        }

        /// <summary>
        /// 从内置资源包异步加载二进制资源。
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <returns>二进制资源句柄。</returns>
        /// <exception cref="GameException">内置资源包未初始化或资源不存在时抛出。</exception>
        public override async UniTask<RawAssetHandle> LoadRawAssetAsync(string location)
        {
            var provider = GetProvider(location);
            if (provider == null)
            {
                throw new GameException($"Asset {location} not found");
            }

            return await provider.LoadRawAssetAsync(location);
        }

        /// <summary>
        /// 从内置资源包按标签异步加载二进制资源列表。
        /// </summary>
        /// <param name="label">资源标签。</param>
        /// <returns>二进制资源句柄列表。</returns>
        /// <exception cref="GameException">内置资源包未初始化或标签不存在时抛出。</exception>
        public override async UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label)
        {
            var handles = new List<RawAssetHandle>();
            foreach (var provider in this._providers.Where(provider => provider.HasAsset(label)))
            {
                handles.AddRange(await provider.LoadRawAssetsByLabelAsync(label));
            }

            return handles;
        }

        /// <summary>
        /// 从内置资源包异步加载场景资源。
        /// </summary>
        /// <param name="name">场景资源地址或名称。</param>
        /// <returns>场景资源句柄。</returns>
        /// <exception cref="GameException">内置资源包未初始化或场景资源不存在时抛出。</exception>
        public override async UniTask<SceneAssetHandle> LoadSceneAssetAsync(string name)
        {
            var provider = GetProvider(name);
            if (provider == null)
            {
                throw new GameException($"Asset {name} not found");
            }

            return await provider.LoadSceneAssetAsync(name);
        }

        /// <summary>
        /// 卸载内置资源包中未使用的资源。
        /// </summary>
        /// <returns>卸载任务。</returns>
        /// <exception cref="GameException">内置资源包未初始化时抛出。</exception>
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
        /// 卸载内置资源句柄。
        /// </summary>
        /// <param name="handle">资源句柄。</param>
        /// <returns>卸载任务。</returns>
        /// <exception cref="GameException">内置资源包未初始化时抛出。</exception>
        public override async UniTask UnloadAsset(AssetHandle handle)
        {
            var provider = GetProvider(handle?.Info?.Location);
            if (provider != null)
            {
                await provider.UnloadAsset(handle);
            }
        }

        /// <summary>
        /// 卸载内置二进制资源句柄。
        /// </summary>
        /// <param name="handle">二进制资源句柄。</param>
        /// <returns>卸载任务。</returns>
        /// <exception cref="GameException">内置资源包未初始化时抛出。</exception>
        public override async UniTask UnloadRawAsset(RawAssetHandle handle)
        {
            var provider = GetProvider(handle?.Info?.Location);
            if (provider != null)
            {
                await provider.UnloadRawAsset(handle);
            }
        }

        /// <summary>
        /// 卸载内置场景资源句柄。
        /// </summary>
        /// <param name="handle">场景资源句柄。</param>
        /// <returns>卸载任务。</returns>
        /// <exception cref="GameException">内置资源包未初始化时抛出。</exception>
        public override async UniTask UnloadSceneAsset(SceneAssetHandle handle)
        {
            var provider = GetProvider(handle?.Info?.Location);
            if (provider != null)
            {
                await provider.UnloadSceneAsset(handle);
            }
        }

        /// <summary>
        /// 释放内置资源模式。
        /// </summary>
        public override void Release()
        {
            foreach (var provider in _providers.ToArray())
            {
                provider.Release();
            }

            _providers.Clear();
        }

        private ProviderBase GetProvider(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            return this._providers.FirstOrDefault(provider => provider.HasAsset(location));
        }
    }
}
