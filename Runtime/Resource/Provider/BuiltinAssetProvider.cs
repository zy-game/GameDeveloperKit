using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 内置资源提供者，用于加载Unity Resources中的资源。
    /// </summary>
    public sealed partial class BuiltinAssetProvider : ProviderBase
    {
        private BundleHandle _bundle;

        public bool CanLoadAssets => _bundle != null;

        /// <summary>
        /// 初始化内置资源提供者。
        /// </summary>
        /// <param name="bundleInfo">资源包信息。</param>
        public BuiltinAssetProvider(BundleInfo bundleInfo) : base(bundleInfo)
        {
        }

        /// <summary>
        /// 初始化内置资源提供者。
        /// </summary>
        /// <returns>资源包加载操作句柄。</returns>
        public override UniTask<OperationHandle<BundleHandle>> InitializeProviderAsync()
        {
            Status = ResourceStatus.Succeeded;
            var operation = InitializeBundleOperationHandle.Success(Info);
            _bundle = operation.Value;
            return UniTask.FromResult<OperationHandle<BundleHandle>>(operation);
        }

        /// <summary>
        /// 卸载内置资源提供者。
        /// </summary>
        /// <returns>资源包卸载操作句柄。</returns>
        public override async UniTask<OperationHandle> UninitializeProviderAsync()
        {
            try
            {
                await PrepareForUninitializeAsync();
            }
            catch (System.Exception exception)
            {
                Status = ResourceStatus.Failed;
                return UninitializeBundleOperationHandle.Failure(exception);
            }

            _bundle?.Release();
            Status = ResourceStatus.Released;
            _bundle = null;
            return UninitializeBundleOperationHandle.Sucecess();
        }

        /// <inheritdoc/>
        protected override UniTask<AssetHandle> LoadAssetInternalAsync(AssetInfo asset)
        {
            if (_bundle == null)
            {
                return UniTask.FromResult(AssetHandle.Failure(new GameException("Bundle is not initialized.")));
            }

            var loadedAsset = Resources.Load(NormalizeResourcesLocation(asset, "Asset"));
            if (loadedAsset == null)
            {
                return UniTask.FromResult(AssetHandle.Failure(new GameException($"Asset load failed: {asset.Location}")));
            }

            return UniTask.FromResult(AssetHandle.Success(asset, loadedAsset, _bundle));
        }

        /// <inheritdoc/>
        protected override UniTask<RawAssetHandle> LoadRawAssetInternalAsync(AssetInfo asset)
        {
            if (_bundle == null)
            {
                return UniTask.FromResult(RawAssetHandle.Failure(new GameException("Bundle is not initialized.")));
            }

            var textAsset = Resources.Load<TextAsset>(NormalizeResourcesLocation(asset, "Raw asset"));
            if (textAsset == null)
            {
                return UniTask.FromResult(RawAssetHandle.Failure(new GameException($"Raw asset load failed: {asset.Location}")));
            }

            return UniTask.FromResult(RawAssetHandle.Success(asset, textAsset.bytes, _bundle));
        }

        /// <inheritdoc/>
        protected override async UniTask<SceneAssetHandle> LoadSceneAssetInternalAsync(AssetInfo asset)
        {
            if (_bundle == null)
            {
                return SceneAssetHandle.Failure(new GameException("Bundle is not initialized."));
            }

            var sceneName = NormalizeResourcesLocation(asset, "Scene");
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (operation == null)
            {
                return SceneAssetHandle.Failure(new GameException($"Scene load failed: {asset.Location}"));
            }

            await operation;
            var scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid()
                ? SceneAssetHandle.Success(asset, scene, _bundle)
                : SceneAssetHandle.Failure(new GameException($"Scene load failed: {asset.Location}"));
        }

        private static string NormalizeResourcesLocation(AssetInfo asset, string assetType)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (string.IsNullOrWhiteSpace(asset.Location))
            {
                throw new ArgumentException($"{assetType} location cannot be empty.", nameof(asset));
            }

            const string resourcesPrefix = "Resources/";
            if (asset.Location.StartsWith(resourcesPrefix, StringComparison.Ordinal) is false)
            {
                throw new GameException($"Builtin {assetType.ToLowerInvariant()} location must start with '{resourcesPrefix}': {asset.Location}");
            }

            var location = asset.Location.Substring(resourcesPrefix.Length);
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException($"{assetType} location cannot be empty after Resources/ prefix.", nameof(asset));
            }

            return location;
        }

        /// <summary>
        /// 释放内置资源提供者。
        /// </summary>
        public override void Release()
        {
            base.Release();
            _bundle?.Release();
            _bundle = null;
        }
    }
}
