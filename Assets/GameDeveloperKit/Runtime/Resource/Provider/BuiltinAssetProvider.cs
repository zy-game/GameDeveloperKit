using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 内置资源提供者，用于加载Unity Resources中的资源。
    /// </summary>
    public sealed partial class BuiltinAssetProvider : ProviderBase
    {
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
            return UniTask.FromResult<OperationHandle<BundleHandle>>(InitializeBundleOperationHandle.Success(Info));
        }

        /// <summary>
        /// 卸载内置资源提供者。
        /// </summary>
        /// <returns>资源包卸载操作句柄。</returns>
        public override UniTask<OperationHandle> UninitializeProviderAsync()
        {
            Status = ResourceStatus.Released;
            return UniTask.FromResult<OperationHandle>(UninitializeBundleOperationHandle.Sucecess());
        }

        /// <inheritdoc/>
        protected override async UniTask<AssetHandle> LoadAssetInternalAsync(AssetInfo asset)
        {
            var operation = await Super.Operation.WaitCompletionAsync<LoadingAssetOperationHandle>(asset, asset);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                return AssetHandle.Failure(operation.Error ?? new GameException($"Asset load failed: {asset.Location}"));
            }

            return operation.Value;
        }

        /// <inheritdoc/>
        protected override async UniTask<RawAssetHandle> LoadRawAssetInternalAsync(AssetInfo asset)
        {
            var operation = await Super.Operation.WaitCompletionAsync<LoadingRawAssetOperationHandle>(asset, asset);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                return RawAssetHandle.Failure(operation.Error ?? new GameException($"Raw asset load failed: {asset.Location}"));
            }

            return operation.Value;
        }

        /// <inheritdoc/>
        protected override async UniTask<SceneAssetHandle> LoadSceneAssetInternalAsync(AssetInfo asset)
        {
            var operation = await Super.Operation.WaitCompletionAsync<LoadingSceneAssetOperationHandle>(asset, asset);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                return SceneAssetHandle.Failure(operation.Error ?? new GameException($"Scene load failed: {asset.Location}"));
            }

            return operation.Value;
        }
    }
}
