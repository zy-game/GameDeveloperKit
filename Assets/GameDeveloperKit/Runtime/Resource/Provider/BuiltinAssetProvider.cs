using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;

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
        public override UniTask<OperationHandle> UninitializeProviderAsync()
        {
            _bundle?.Release();
            Status = ResourceStatus.Released;
            _bundle = null;
            return UniTask.FromResult<OperationHandle>(UninitializeBundleOperationHandle.Sucecess());
        }

        /// <inheritdoc/>
        protected override async UniTask<AssetHandle> LoadAssetInternalAsync(AssetInfo asset)
        {
            if (_bundle == null)
            {
                return AssetHandle.Failure(new GameException("Bundle is not initialized."));
            }

            var operation = await App.Operation.WaitCompletionWithKeyAsync<LoadingAssetOperationHandle>(asset, asset, _bundle);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                return AssetHandle.Failure(operation.Error ?? new GameException($"Asset load failed: {asset.Location}"));
            }

            return operation.Value;
        }

        /// <inheritdoc/>
        protected override async UniTask<RawAssetHandle> LoadRawAssetInternalAsync(AssetInfo asset)
        {
            if (_bundle == null)
            {
                return RawAssetHandle.Failure(new GameException("Bundle is not initialized."));
            }

            var operation = await App.Operation.WaitCompletionWithKeyAsync<LoadingRawAssetOperationHandle>(asset, asset, _bundle);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                return RawAssetHandle.Failure(operation.Error ?? new GameException($"Raw asset load failed: {asset.Location}"));
            }

            return operation.Value;
        }

        /// <inheritdoc/>
        protected override async UniTask<SceneAssetHandle> LoadSceneAssetInternalAsync(AssetInfo asset)
        {
            if (_bundle == null)
            {
                return SceneAssetHandle.Failure(new GameException("Bundle is not initialized."));
            }

            var operation = await App.Operation.WaitCompletionWithKeyAsync<LoadingSceneAssetOperationHandle>(asset, asset, _bundle);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                return SceneAssetHandle.Failure(operation.Error ?? new GameException($"Scene load failed: {asset.Location}"));
            }

            return operation.Value;
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
