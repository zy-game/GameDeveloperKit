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
        /// <summary>
        /// 存储 asset Provider。
        /// </summary>
        private BuiltinAssetProvider assetProvider;

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
            if (this.assetProvider == null)
            {
                return false;
            }

            return this.assetProvider.HasAsset(location);
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
            if (operation.Status is OperationStatus.Succeeded)
            {
                assetProvider = null;
            }

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
            if (this.assetProvider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            if (this.assetProvider.HasAsset(location) is false)
            {
                return default;
            }

            return await this.assetProvider.LoadAssetAsync(location);
        }

        /// <summary>
        /// 从内置资源包按标签异步加载资源列表。
        /// </summary>
        /// <param name="label">资源标签。</param>
        /// <returns>资源加载句柄列表。</returns>
        /// <exception cref="GameException">内置资源包未初始化或标签不存在时抛出。</exception>
        public override async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label)
        {
            if (this.assetProvider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            if (this.assetProvider.HasAsset(label) == false)
            {
                throw new GameException($"Asset Label {label} not found");
            }

            return await this.assetProvider.LoadAssetsByLabelAsync(label);
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
            if (this.assetProvider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            if (this.assetProvider.HasAsset(assetTypeName) == false)
            {
                throw new GameException($"Asset Type {assetTypeName} not found");
            }

            return await this.assetProvider.LoadAssetsByTypeAsync<T>();
        }

        /// <summary>
        /// 从内置资源包异步加载二进制资源。
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <returns>二进制资源句柄。</returns>
        /// <exception cref="GameException">内置资源包未初始化或资源不存在时抛出。</exception>
        public override async UniTask<RawAssetHandle> LoadRawAssetAsync(string location)
        {
            if (this.assetProvider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            if (this.assetProvider.HasAsset(location) == false)
            {
                throw new GameException($"Asset {location} not found");
            }

            return await this.assetProvider.LoadRawAssetAsync(location);
        }

        /// <summary>
        /// 从内置资源包按标签异步加载二进制资源列表。
        /// </summary>
        /// <param name="label">资源标签。</param>
        /// <returns>二进制资源句柄列表。</returns>
        /// <exception cref="GameException">内置资源包未初始化或标签不存在时抛出。</exception>
        public override async UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label)
        {
            if (this.assetProvider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            if (this.assetProvider.HasAsset(label) == false)
            {
                throw new GameException($"Asset Label {label} not found");
            }

            return await this.assetProvider.LoadRawAssetsByLabelAsync(label);
        }

        /// <summary>
        /// 从内置资源包异步加载场景资源。
        /// </summary>
        /// <param name="name">场景资源地址或名称。</param>
        /// <returns>场景资源句柄。</returns>
        /// <exception cref="GameException">内置资源包未初始化或场景资源不存在时抛出。</exception>
        public override async UniTask<SceneAssetHandle> LoadSceneAssetAsync(string name)
        {
            if (this.assetProvider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            if (this.assetProvider.HasAsset(name) == false)
            {
                throw new GameException($"Asset {name} not found");
            }

            return await this.assetProvider.LoadSceneAssetAsync(name);
        }

        /// <summary>
        /// 卸载内置资源包中未使用的资源。
        /// </summary>
        /// <returns>卸载任务。</returns>
        /// <exception cref="GameException">内置资源包未初始化时抛出。</exception>
        public override async UniTask UnloadUnusedAssetAsync()
        {
            if (this.assetProvider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            await this.assetProvider.UnloadUnusedAssetAsync();
        }

        /// <summary>
        /// 卸载内置资源句柄。
        /// </summary>
        /// <param name="handle">资源句柄。</param>
        /// <returns>卸载任务。</returns>
        /// <exception cref="GameException">内置资源包未初始化时抛出。</exception>
        public override async UniTask UnloadAsset(AssetHandle handle)
        {
            if (this.assetProvider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            await this.assetProvider.UnloadAsset(handle);
        }

        /// <summary>
        /// 卸载内置二进制资源句柄。
        /// </summary>
        /// <param name="handle">二进制资源句柄。</param>
        /// <returns>卸载任务。</returns>
        /// <exception cref="GameException">内置资源包未初始化时抛出。</exception>
        public override async UniTask UnloadRawAsset(RawAssetHandle handle)
        {
            if (this.assetProvider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            await this.assetProvider.UnloadRawAsset(handle);
        }

        /// <summary>
        /// 卸载内置场景资源句柄。
        /// </summary>
        /// <param name="handle">场景资源句柄。</param>
        /// <returns>卸载任务。</returns>
        /// <exception cref="GameException">内置资源包未初始化时抛出。</exception>
        public override async UniTask UnloadSceneAsset(SceneAssetHandle handle)
        {
            if (this.assetProvider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            await this.assetProvider.UnloadSceneAsset(handle);
        }

        /// <summary>
        /// 释放内置资源模式。
        /// </summary>
        public override async void Release()
        {
            if (assetProvider == null)
            {
                return;
            }

            await assetProvider.UninitializeProviderAsync();
            assetProvider = null;
        }
    }
}
