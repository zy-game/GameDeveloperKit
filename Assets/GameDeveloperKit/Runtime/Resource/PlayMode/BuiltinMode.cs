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
        private BuiltinProvider _provider;
        public const string BUILTIN_PACKAGE_NAME = "BUILTIN";

        public BuiltinMode(ManifestInfo manifest) : base(manifest)
        {
        }

        /// <summary>
        /// 查询资源是否存在
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public override bool HasAsset(string location)
        {
            if (this._provider == null)
            {
                return false;
            }

            return this._provider.HasAsset(location);
        }

        public override bool HasPackage(string package)
        {
            return package == BUILTIN_PACKAGE_NAME;
        }

        public override async UniTask<OperationHandle> InitializePackageAsync(string package)
        {
            if (package is not BUILTIN_PACKAGE_NAME)
            {
                throw new ArgumentException($"Invalid package: {package}");
            }

            return await Super.Operation.WaitCompletionAsync<InitializePackageOperationHandle>(this, package, this, Manifest);
        }

        public override async UniTask<OperationHandle> UninitializePackageAsync(string package)
        {
            if (package is not BUILTIN_PACKAGE_NAME)
            {
                throw new ArgumentException($"Invalid package: {package}");
            }

            var operation = await Super.Operation.WaitCompletionAsync<UninitializePackageOperationHandle>(this, package, _provider);
            if (operation.Status is OperationStatus.Succeeded)
            {
                _provider = null;
            }

            return operation;
        }

        public override async UniTask<AssetHandle> LoadAssetAsync(string location)
        {
            if (this._provider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            if (this._provider.HasAsset(location) is false)
            {
                return default;
            }

            return await this._provider.LoadAssetAsync(location);
        }

        public override async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label)
        {
            if (this._provider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            if (this._provider.HasAsset(label) == false)
            {
                throw new GameException($"Asset Label {label} not found");
            }

            return await this._provider.LoadAssetsByLabelAsync(label);
        }

        public override async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByTypeAsync<T>()
        {
            var assetTypeName = typeof(T).Name;
            if (this._provider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            if (this._provider.HasAsset(assetTypeName) == false)
            {
                throw new GameException($"Asset Type {assetTypeName} not found");
            }

            return await this._provider.LoadAssetsByTypeAsync<T>();
        }

        public override async UniTask<RawAssetHandle> LoadRawAssetAsync(string location)
        {
            if (this._provider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            if (this._provider.HasAsset(location) == false)
            {
                throw new GameException($"Asset {location} not found");
            }

            return await this._provider.LoadRawAssetAsync(location);
        }

        public override async UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label)
        {
            if (this._provider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            if (this._provider.HasAsset(label) == false)
            {
                throw new GameException($"Asset Label {label} not found");
            }

            return await this._provider.LoadRawAssetsByLabelAsync(label);
        }

        public override async UniTask<SceneAssetHandle> LoadSceneAssetAsync(string name)
        {
            if (this._provider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            if (this._provider.HasAsset(name) == false)
            {
                throw new GameException($"Asset {name} not found");
            }

            return await this._provider.LoadSceneAssetAsync(name);
        }

        public override async UniTask UnloadUnusedAssetAsync()
        {
            if (this._provider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            await this._provider.UnloadUnusedAssetAsync();
        }

        public override async UniTask UnloadAsset(AssetHandle handle)
        {
            if (this._provider == null)
            {
                throw new GameException($"{BUILTIN_PACKAGE_NAME} not initialized");
            }

            await this._provider.UnloadAsset(handle);
        }

        public override async void Release()
        {
            await _provider.UninitializeProviderAsync();
            _provider = null;
        }
    }
}
