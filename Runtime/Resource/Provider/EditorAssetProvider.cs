using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 编辑器资源提供者，用于通过Unity编辑器AssetDatabase加载资源。
    /// </summary>
    public sealed partial class EditorAssetProvider : ProviderBase
    {
        private BundleHandle _bundle;

        public bool CanLoadAssets => _bundle != null;

        /// <summary>
        /// 初始化编辑器资源提供者。
        /// </summary>
        /// <param name="bundleInfo">资源包信息。</param>
        public EditorAssetProvider(BundleInfo bundleInfo) : base(bundleInfo)
        {
        }

        /// <summary>
        /// 初始化编辑器资源提供者。
        /// </summary>
        /// <returns>资源包加载操作句柄。</returns>
        public override async UniTask<OperationHandle<BundleHandle>> InitializeProviderAsync()
        {
            if (Info is null)
            {
                return InitializeBundleOperationHandle.Failure(new GameException("Bundle info is null."));
            }

            Status = ResourceStatus.Loading;
            var operation = await App.Operation.WaitCompletionWithKeyAsync<InitializeBundleOperationHandle>(this, Info);
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
        /// 卸载编辑器资源提供者。
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

        /// <summary>
        /// 在编辑器中按资源路径和类型加载Unity资源。
        /// </summary>
        /// <param name="assetPath">资源路径。</param>
        /// <param name="assetType">资源类型。</param>
        /// <returns>Unity资源对象。</returns>
        /// <exception cref="ArgumentException">资源路径为空时抛出。</exception>
        /// <exception cref="ArgumentNullException">资源类型为空时抛出。</exception>
        /// <exception cref="GameException">非编辑器环境调用时抛出。</exception>
        private static UnityEngine.Object LoadAssetAtPath(string assetPath, Type assetType)
        {
#if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new ArgumentException("Asset path cannot be empty.", nameof(assetPath));
            }

            if (assetType == null)
            {
                throw new ArgumentNullException(nameof(assetType));
            }

            return UnityEditor.AssetDatabase.LoadAssetAtPath(assetPath, assetType);
#else
            throw new GameException("EditorProvider is only available in Unity Editor.");
#endif
        }

        private static string ResolveEditorAssetPath(AssetInfo assetInfo)
        {
            if (assetInfo == null)
            {
                throw new ArgumentNullException(nameof(assetInfo));
            }

            return string.IsNullOrWhiteSpace(assetInfo.AssetPath) ? assetInfo.Location : assetInfo.AssetPath;
        }

        /// <inheritdoc/>
        protected override UniTask<AssetHandle> LoadAssetInternalAsync(AssetInfo asset)
        {
            if (_bundle == null)
            {
                return UniTask.FromResult(AssetHandle.Failure(new GameException("Bundle is not initialized.")));
            }

            ValidateLoad(asset, "Asset");
            var loadedAsset = LoadAssetAtPath(ResolveEditorAssetPath(asset), typeof(UnityEngine.Object));
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

            ValidateLoad(asset, "Raw asset");
            var textAsset = LoadAssetAtPath(ResolveEditorAssetPath(asset), typeof(TextAsset)) as TextAsset;
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

            ValidateLoad(asset, "Scene");
            var assetPath = ResolveEditorAssetPath(asset);
            if (LoadAssetAtPath(assetPath, typeof(UnityEngine.Object)) == null)
            {
                return SceneAssetHandle.Failure(new GameException($"Scene asset load failed: {asset.Location}"));
            }

            var sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
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

        private static void ValidateLoad(AssetInfo asset, string assetType)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (string.IsNullOrWhiteSpace(asset.Location))
            {
                throw new ArgumentException($"{assetType} location cannot be empty.", nameof(asset));
            }
        }

        /// <summary>
        /// 释放编辑器资源提供者，并释放资源包句柄。
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
