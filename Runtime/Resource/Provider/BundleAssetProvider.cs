using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// AssetBundle资源提供者，用于从已加载的AssetBundle中加载资源。
    /// </summary>
    public sealed partial class BundleAssetProvider : ProviderBase
    {
        private BundleHandle _bundle;
        private readonly ResourceMode _mode;
        private readonly string _manifestVersion;
        private readonly bool _isRemote;

        public bool CanLoadAssets => _bundle != null;

        /// <summary>
        /// 当前资源模式，决定 bundle 字节来源（Offline/Online 走本地/持久化目录，Web 走 UnityWebRequest）。
        /// </summary>
        internal ResourceMode Mode => _mode;

        internal bool IsRemote => _isRemote;

        /// <summary>
        /// 初始化AssetBundle资源提供者。
        /// </summary>
        /// <param name="bundleInfo">资源包信息。</param>
        /// <param name="mode">资源模式，决定 bundle 字节来源。</param>
        public BundleAssetProvider(
            BundleInfo bundleInfo,
            ResourceMode mode,
            string manifestVersion,
            bool isRemote) : base(bundleInfo)
        {
            _mode = mode;
            _manifestVersion = manifestVersion;
            _isRemote = isRemote;
        }

        /// <summary>
        /// 初始化AssetBundle并保存资源包句柄。
        /// </summary>
        /// <returns>资源包加载操作句柄。</returns>
        public override async UniTask<OperationHandle<BundleHandle>> InitializeProviderAsync()
        {
            if (Info is null)
            {
                return InitializeBundleOperationHandle.Failure(new GameException("Bundle info is null."));
            }

            Status = ResourceStatus.Loading;
            var operation = await App.Operation.WaitCompletionWithKeyAsync<InitializeBundleOperationHandle>(
                this,
                Info,
                _mode,
                _manifestVersion,
                _isRemote);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                Status = ResourceStatus.Failed;
                return InitializeBundleOperationHandle.Failure(operation.Error ?? new GameException($"Bundle initialize failed: {Info.Name}"));
            }

            Status = ResourceStatus.Succeeded;
            _bundle = operation.Value;
            return operation;
        }

        /// <summary>
        /// 卸载AssetBundle并清理资源包句柄。
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

            if (_bundle is null)
            {
                return UninitializeBundleOperationHandle.Failure(new GameException("Bundle is not initialized."));
            }

            Status = ResourceStatus.Unloading;
            var operation = await App.Operation.WaitCompletionWithKeyAsync<UninitializeBundleOperationHandle>(this, Info, _bundle);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                Status = ResourceStatus.Failed;
                return UninitializeBundleOperationHandle.Failure(operation.Error ?? new GameException($"Bundle uninitialize failed: {Info.Name}"));
            }

            Status = ResourceStatus.Released;
            _bundle = null;
            return operation;
        }

        /// <inheritdoc/>
        protected override async UniTask<AssetHandle> LoadAssetInternalAsync(AssetInfo asset)
        {
            if (_bundle?.Asset == null)
            {
                return AssetHandle.Failure(new GameException("Bundle is not initialized."));
            }

            ValidateLoad(asset);
            var request = _bundle.Asset.LoadAssetAsync(asset.Location);
            await request;
            if (request.asset == null)
            {
                return AssetHandle.Failure(new GameException($"Asset load failed: {asset.Location}"));
            }

            return AssetHandle.Success(asset, request.asset, _bundle);
        }

        /// <inheritdoc/>
        protected override async UniTask<RawAssetHandle> LoadRawAssetInternalAsync(AssetInfo asset)
        {
            if (_bundle?.Asset == null)
            {
                return RawAssetHandle.Failure(new GameException("Bundle is not initialized."));
            }

            ValidateLoad(asset);
            var request = _bundle.Asset.LoadAssetAsync<TextAsset>(asset.Location);
            await request;
            var textAsset = request.asset as TextAsset;
            if (textAsset == null)
            {
                return RawAssetHandle.Failure(new GameException($"Raw asset load failed: {asset.Location}"));
            }

            return RawAssetHandle.Success(asset, textAsset.bytes, _bundle);
        }

        /// <inheritdoc/>
        protected override async UniTask<SceneAssetHandle> LoadSceneAssetInternalAsync(AssetInfo asset)
        {
            if (_bundle?.Asset == null)
            {
                return SceneAssetHandle.Failure(new GameException("Bundle is not initialized."));
            }

            ValidateLoad(asset);
            var operation = SceneManager.LoadSceneAsync(asset.Location, LoadSceneMode.Additive);
            if (operation == null)
            {
                return SceneAssetHandle.Failure(new GameException($"Scene load failed: {asset.Location}"));
            }

            await operation;
            var scene = SceneManager.GetSceneByName(asset.Location);
            return scene.IsValid()
                ? SceneAssetHandle.Success(asset, scene, _bundle)
                : SceneAssetHandle.Failure(new GameException($"Scene load failed: {asset.Location}"));
        }

        private static void ValidateLoad(AssetInfo asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (string.IsNullOrWhiteSpace(asset.Location))
            {
                throw new ArgumentException("Asset location cannot be empty.", nameof(asset));
            }
        }

        /// <summary>
        /// 释放AssetBundle资源提供者，并卸载已加载的资源包。
        /// </summary>
        public override void Release()
        {
            base.Release();
            if (_bundle == null)
            {
                return;
            }

            _bundle.Release();
            _bundle = null;
        }
    }
}
