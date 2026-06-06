using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源包资源模式
    /// </summary>
    public sealed partial class BundleMode : ModeBase
    {
        private readonly List<ProviderBase> _providers;

        /// <summary>
        /// 初始化资源包资源模式。
        /// </summary>
        /// <param name="manifest">资源清单。</param>
        public BundleMode(ManifestInfo manifest) : base(manifest)
        {
            _providers = new List<ProviderBase>();
        }

        /// <summary>
        /// 检查是否存在资源
        /// </summary>
        /// <param name="location">资源定位</param>
        /// <returns>是否存在资源</returns>
        public override bool HasAsset(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return false;
            }

            return this._providers.Any(x => x.HasAsset(location));
        }

        /// <summary>
        /// 是否存在资源包
        /// </summary>
        /// <param name="package">资源包名</param>
        /// <returns>是否存在资源包</returns>
        public override bool HasPackage(string package)
        {
            if (string.IsNullOrWhiteSpace(package))
            {
                return false;
            }

            var packageInfo = Manifest.Packages.FirstOrDefault(x => x != null && x.Name == package);
            if (packageInfo?.Bundles == null)
            {
                return false;
            }

            return packageInfo.Bundles.Any(bundle => bundle != null && this._providers.Any(provider => provider.Info != null && provider.Info.Name == bundle.Name));
        }

        /// <summary>
        /// 初始化资源包
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="GameException"></exception>
        public override async UniTask<OperationHandle> InitializePackageAsync(string package)
        {
            ValidateKey(package, nameof(package));
            if (package == BuiltinMode.BUILTIN_PACKAGE_NAME)
            {
                return InitializePackageOperationHandle.Failure(new GameException($"Package not found: {BuiltinMode.BUILTIN_PACKAGE_NAME}"));
            }

            Status = ResourceStatus.Loading;
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
        /// 从已初始化资源包异步加载资源。
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
        /// 从已初始化资源包按标签异步加载资源列表。
        /// </summary>
        /// <param name="label">资源标签。</param>
        /// <returns>资源加载句柄列表。</returns>
        public override async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label)
        {
            ValidateKey(label, nameof(label));
            var handles = new List<AssetHandle>();
            foreach (var provider in this._providers.Where(x => x.Info?.Assets != null && x.Info.Assets.Any(asset => asset.Labels != null && asset.Labels.Contains(label))))
            {
                handles.AddRange(await provider.LoadAssetsByLabelAsync(label));
            }

            return handles;
        }

        /// <summary>
        /// 从已初始化资源包按资源类型异步加载资源列表。
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
        /// 从已初始化资源包异步加载二进制资源。
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
        /// 从已初始化资源包按标签异步加载二进制资源列表。
        /// </summary>
        /// <param name="label">资源标签。</param>
        /// <returns>二进制资源句柄列表。</returns>
        public override async UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label)
        {
            ValidateKey(label, nameof(label));
            var handles = new List<RawAssetHandle>();
            foreach (var provider in this._providers.Where(x => x.Info?.Assets != null && x.Info.Assets.Any(asset => asset.Labels != null && asset.Labels.Contains(label))))
            {
                handles.AddRange(await provider.LoadRawAssetsByLabelAsync(label));
            }

            return handles;
        }

        /// <summary>
        /// 从已初始化资源包异步加载场景资源。
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
        /// 卸载所有资源提供者中未使用的资源。
        /// </summary>
        /// <returns>卸载任务。</returns>
        public override async UniTask UnloadUnusedAssetAsync()
        {
            var tasks = this._providers.Select(provider => provider.UnloadUnusedAssetAsync());
            await UniTask.WhenAll(tasks);
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
        /// 释放资源包模式，并释放所有资源提供者。
        /// </summary>
        public override void Release()
        {
            foreach (var provider in _providers.ToArray())
            {
                provider.Release();
            }

            _providers.Clear();
        }

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