using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// Web资源提供者，用于通过UnityWebRequest加载远端AssetBundle资源。
    /// </summary>
    public sealed partial class WebAssetProvider : ProviderBase
    {
        private BundleHandle _bundle;

        public WebAssetProvider(BundleInfo info) : base(info)
        {
        }

        /// <summary>
        /// 初始化资源包。
        /// </summary>
        /// <returns>资源包加载操作句柄。</returns>
        public override async UniTask<OperationHandle<BundleHandle>> InitializeProviderAsync()
        {
            if (Info is null)
            {
                return InitializeBundleOperationHandle.Failure(new GameException("Bundle info is null."));
            }

            Status = ResourceStatus.Loading;
            var operation = await Super.Operation.WaitCompletionAsync<InitializeBundleOperationHandle>(this, Info);
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
        /// 卸载资源包。
        /// </summary>
        /// <returns>资源包卸载操作句柄。</returns>
        public override async UniTask<OperationHandle> UninitializeProviderAsync()
        {
            if (_bundle is null)
            {
                return UninitializeBundleOperationHandle.Failure(new GameException("Bundle is not initialized."));
            }

            Status = ResourceStatus.Unloading;
            var operation = await Super.Operation.WaitCompletionAsync<UninitializeBundleOperationHandle>(this, Info, _bundle);
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

            var operation = await Super.Operation.WaitCompletionAsync<LoadingAssetOperationHandle>(asset, asset, _bundle);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                return AssetHandle.Failure(operation.Error ?? new GameException($"Asset load failed: {asset.Location}"));
            }

            return operation.Value;
        }

        /// <inheritdoc/>
        protected override async UniTask<RawAssetHandle> LoadRawAssetInternalAsync(AssetInfo asset)
        {
            if (_bundle?.Asset == null)
            {
                return RawAssetHandle.Failure(new GameException("Bundle is not initialized."));
            }

            var operation = await Super.Operation.WaitCompletionAsync<LoadingRawAssetOperationHandle>(asset, asset, _bundle);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                return RawAssetHandle.Failure(operation.Error ?? new GameException($"Raw asset load failed: {asset.Location}"));
            }

            return operation.Value;
        }

        /// <inheritdoc/>
        protected override async UniTask<SceneAssetHandle> LoadSceneAssetInternalAsync(AssetInfo asset)
        {
            if (_bundle?.Asset == null)
            {
                return SceneAssetHandle.Failure(new GameException("Bundle is not initialized."));
            }

            var operation = await Super.Operation.WaitCompletionAsync<LoadingSceneAssetOperationHandle>(asset, asset, _bundle);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                return SceneAssetHandle.Failure(operation.Error ?? new GameException($"Scene load failed: {asset.Location}"));
            }

            return operation.Value;
        }

        /// <summary>
        /// 释放Web资源提供者，并卸载已加载的资源包。
        /// </summary>
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
