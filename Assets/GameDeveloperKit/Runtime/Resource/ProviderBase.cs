using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源提供者基类，定义了资源提供者的基本接口和功能，包括初始化、卸载、加载和卸载资源等方法。它包含一个BundleInfo属性，用于存储资源包的信息，并且提供了一系列抽象方法，要求具体的资源提供者类必须实现这些方法来完成资源的加载和管理逻辑。通过继承ProviderBase类，开发者可以创建不同类型的资源提供者，以适应不同的资源加载需求和场景，从而提高游戏的性能和用户体验。
    /// </summary>
    public abstract class ProviderBase
    {
        /// <summary>
        /// 存储 assets。
        /// </summary>
        private readonly List<ResourceHandle> _assets;
        /// <summary>
        /// 存储 pending Unload Assets。
        /// </summary>
        private readonly List<ResourceHandle> _pendingUnloadAssets;

        /// <summary>
        /// 资源包信息。
        /// </summary>
        public BundleInfo Info { get; }

        /// <summary>
        /// 资源状态
        /// </summary>
        public ResourceStatus Status { get; protected set; } = ResourceStatus.None;

        /// <summary>
        /// 初始化资源提供者。
        /// </summary>
        /// <param name="info">资源包信息。</param>
        public ProviderBase(BundleInfo info)
        {
            Info = info;
            _assets = new List<ResourceHandle>();
            _pendingUnloadAssets = new List<ResourceHandle>();
        }

        /// <summary>
        /// 初始化资源提供者。
        /// </summary>
        /// <returns>资源包加载操作句柄。</returns>
        public abstract UniTask<OperationHandle<BundleHandle>> InitializeProviderAsync();

        /// <summary>
        /// 卸载资源提供者。
        /// </summary>
        /// <returns>资源包卸载操作句柄。</returns>
        public abstract UniTask<OperationHandle> UninitializeProviderAsync();

        /// <summary>
        /// 执行具体资源加载。
        /// </summary>
        /// <param name="asset">资源信息。</param>
        /// <returns>资源句柄。</returns>
        protected abstract UniTask<AssetHandle> LoadAssetInternalAsync(AssetInfo asset);

        /// <summary>
        /// 执行具体二进制资源加载。
        /// </summary>
        /// <param name="asset">资源信息。</param>
        /// <returns>二进制资源句柄。</returns>
        protected abstract UniTask<RawAssetHandle> LoadRawAssetInternalAsync(AssetInfo asset);

        /// <summary>
        /// 执行具体场景资源加载。
        /// </summary>
        /// <param name="asset">资源信息。</param>
        /// <returns>场景资源句柄。</returns>
        protected abstract UniTask<SceneAssetHandle> LoadSceneAssetInternalAsync(AssetInfo asset);

        /// <summary>
        /// 异步加载资源。
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <returns>资源加载句柄。</returns>
        public async UniTask<AssetHandle> LoadAssetAsync(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return AssetHandle.Failure(new ArgumentException("Asset location cannot be empty.", nameof(location)));
            }

            if (Info == null || Info.TryGetAsset(location, out var asset) is false)
            {
                return AssetHandle.Failure(new GameException($"Asset not found: {location}"));
            }

            if (TryGetAsset<AssetHandle>(asset, out var handle))
            {
                return handle;
            }

            handle = await LoadAssetInternalAsync(asset);
            if (handle != null && handle.Status is ResourceStatus.Succeeded)
            {
                AddAsset(handle);
            }

            return handle ?? AssetHandle.Failure(new GameException($"Asset load failed: {location}"));
        }

        /// <summary>
        /// 根据标签异步加载资源列表。
        /// </summary>
        /// <param name="label">资源标签。</param>
        /// <returns>资源加载句柄列表。</returns>
        public async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return Array.Empty<AssetHandle>();
            }

            var handles = new List<AssetHandle>();
            foreach (var asset in GetAssetsByLabel(label))
            {
                var handle = await LoadAssetByInfoAsync(asset);
                if (handle != null && handle.Status is ResourceStatus.Succeeded)
                {
                    handles.Add(handle);
                }
            }

            return handles;
        }

        /// <summary>
        /// 根据Unity资源类型异步加载资源列表。
        /// </summary>
        /// <typeparam name="T">Unity资源类型。</typeparam>
        /// <returns>资源加载句柄列表。</returns>
        public async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByTypeAsync<T>() where T : UnityEngine.Object
        {
            var handles = new List<AssetHandle>();
            foreach (var asset in GetAssetsByType(typeof(T).Name))
            {
                var handle = await LoadAssetByInfoAsync(asset);
                if (handle != null && handle.Status is ResourceStatus.Succeeded)
                {
                    handles.Add(handle);
                }
            }

            return handles;
        }

        /// <summary>
        /// 异步加载二进制资源。
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <returns>二进制资源句柄。</returns>
        public async UniTask<RawAssetHandle> LoadRawAssetAsync(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return RawAssetHandle.Failure(new ArgumentException("Raw asset location cannot be empty.", nameof(location)));
            }

            if (Info == null || Info.TryGetAsset(location, out var asset) is false)
            {
                return RawAssetHandle.Failure(new GameException($"Raw asset not found: {location}"));
            }

            if (TryGetAsset<RawAssetHandle>(asset, out var handle))
            {
                return handle;
            }

            handle = await LoadRawAssetInternalAsync(asset);
            if (handle != null && handle.Status is ResourceStatus.Succeeded)
            {
                AddAsset(handle);
            }

            return handle ?? RawAssetHandle.Failure(new GameException($"Raw asset load failed: {location}"));
        }

        /// <summary>
        /// 根据标签异步加载二进制资源列表。
        /// </summary>
        /// <param name="label">资源标签。</param>
        /// <returns>二进制资源句柄列表。</returns>
        public async UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return Array.Empty<RawAssetHandle>();
            }

            var handles = new List<RawAssetHandle>();
            foreach (var asset in GetAssetsByLabel(label))
            {
                var handle = await LoadRawAssetByInfoAsync(asset);
                if (handle != null && handle.Status is ResourceStatus.Succeeded)
                {
                    handles.Add(handle);
                }
            }

            return handles;
        }

        /// <summary>
        /// 异步加载场景资源。
        /// </summary>
        /// <param name="name">场景资源地址或名称。</param>
        /// <returns>场景资源句柄。</returns>
        public async UniTask<SceneAssetHandle> LoadSceneAssetAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return SceneAssetHandle.Failure(new ArgumentException("Scene location cannot be empty.", nameof(name)));
            }

            if (Info == null || Info.TryGetAsset(name, out var asset) is false)
            {
                return SceneAssetHandle.Failure(new GameException($"Scene not found: {name}"));
            }

            if (TryGetAsset<SceneAssetHandle>(asset, out var handle))
            {
                return handle;
            }

            handle = await LoadSceneAssetInternalAsync(asset);
            if (handle != null && handle.Status is ResourceStatus.Succeeded)
            {
                AddAsset(handle);
            }

            return handle ?? SceneAssetHandle.Failure(new GameException($"Scene load failed: {name}"));
        }

        /// <summary>
        /// 检查AssetBundle资源提供者是否包含指定资源。
        /// </summary>
        /// <param name="location">资源地址、类型名或标签。</param>
        /// <returns>如果包含资源，则返回true；否则返回false。</returns>
        public bool HasAsset(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return false;
            }

            return GetAssets().Any(x => x.Location == location || x.TypeName == location || (x.Labels != null && x.Labels.Contains(location)));
        }

        /// <summary>
        /// 加载 Asset By Info Async。
        /// </summary>
        /// <param name="asset">asset 参数。</param>
        /// <returns>操作完成任务。</returns>
        private async UniTask<AssetHandle> LoadAssetByInfoAsync(AssetInfo asset)
        {
            if (TryGetAsset<AssetHandle>(asset, out var handle))
            {
                return handle;
            }

            handle = await LoadAssetInternalAsync(asset);
            if (handle != null && handle.Status is ResourceStatus.Succeeded)
            {
                AddAsset(handle);
            }

            return handle;
        }

        /// <summary>
        /// 加载 Raw Asset By Info Async。
        /// </summary>
        /// <param name="asset">asset 参数。</param>
        /// <returns>操作完成任务。</returns>
        private async UniTask<RawAssetHandle> LoadRawAssetByInfoAsync(AssetInfo asset)
        {
            if (TryGetAsset<RawAssetHandle>(asset, out var handle))
            {
                return handle;
            }

            handle = await LoadRawAssetInternalAsync(asset);
            if (handle != null && handle.Status is ResourceStatus.Succeeded)
            {
                AddAsset(handle);
            }

            return handle;
        }

        /// <summary>
        /// 获取 Assets。
        /// </summary>
        /// <returns>执行结果。</returns>
        private IEnumerable<AssetInfo> GetAssets()
        {
            return Info?.Assets?.Where(x => x != null) ?? Enumerable.Empty<AssetInfo>();
        }

        /// <summary>
        /// 获取 Assets By Label。
        /// </summary>
        /// <param name="label">label 参数。</param>
        /// <returns>执行结果。</returns>
        private IEnumerable<AssetInfo> GetAssetsByLabel(string label)
        {
            return GetAssets().Where(x => x.Labels != null && x.Labels.Contains(label));
        }

        /// <summary>
        /// 获取 Assets By Type。
        /// </summary>
        /// <param name="typeName">type Name 参数。</param>
        /// <returns>执行结果。</returns>
        private IEnumerable<AssetInfo> GetAssetsByType(string typeName)
        {
            return GetAssets().Where(x => x.TypeName == typeName);
        }

        /// <summary>
        /// 记录已加载的资源句柄。
        /// </summary>
        /// <param name="handle">资源句柄。</param>
        /// <exception cref="ArgumentNullException">资源句柄为空时抛出。</exception>
        protected void AddAsset(ResourceHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            _pendingUnloadAssets.Remove(handle);
            if (_assets.Contains(handle))
            {
                return;
            }

            _assets.Add(handle);
        }

        /// <summary>
        /// 将资源句柄移入待卸载列表。
        /// </summary>
        /// <param name="handle">资源句柄。</param>
        /// <returns>如果资源句柄已从活动列表移除，则返回true；否则返回false。</returns>
        /// <exception cref="ArgumentNullException">资源句柄为空时抛出。</exception>
        protected bool RemoveAsset(ResourceHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (_assets.Remove(handle) is false)
            {
                return false;
            }

            if (_pendingUnloadAssets.Contains(handle) is false)
            {
                _pendingUnloadAssets.Add(handle);
            }

            return true;
        }

        /// <summary>
        /// 尝试获取已加载或待卸载的资源句柄。
        /// </summary>
        /// <typeparam name="T">资源句柄类型。</typeparam>
        /// <param name="info">资源信息。</param>
        /// <param name="handle">输出资源句柄。</param>
        /// <returns>如果找到资源句柄，则返回true；否则返回false。</returns>
        protected bool TryGetAsset<T>(AssetInfo info, out T handle) where T : ResourceHandle
        {
            var target = _assets.OfType<T>().FirstOrDefault(x => x.Info == info);
            if (target is not null)
            {
                handle = target;
                return true;
            }

            target = _pendingUnloadAssets.OfType<T>().FirstOrDefault(x => x.Info == info);
            if (target is not null)
            {
                _pendingUnloadAssets.Remove(target);
                _assets.Add(target);
            }

            handle = target;
            return target != null;
        }

        /// <summary>
        /// 卸载待释放资源。
        /// </summary>
        /// <returns>卸载任务。</returns>
        public virtual UniTask UnloadUnusedAssetAsync()
        {
            foreach (var handle in _pendingUnloadAssets.ToArray())
            {
                handle.Release();
            }

            _pendingUnloadAssets.Clear();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 标记资源句柄为待卸载。
        /// </summary>
        /// <param name="handle">资源句柄。</param>
        /// <returns>卸载任务。</returns>
        /// <exception cref="ArgumentNullException">资源句柄为空时抛出。</exception>
        public virtual UniTask UnloadAsset(AssetHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            RemoveAsset(handle);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 标记二进制资源句柄为待卸载。
        /// </summary>
        /// <param name="handle">二进制资源句柄。</param>
        /// <returns>卸载任务。</returns>
        /// <exception cref="ArgumentNullException">资源句柄为空时抛出。</exception>
        public virtual UniTask UnloadRawAsset(RawAssetHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            RemoveAsset(handle);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 标记场景资源句柄为待卸载。
        /// </summary>
        /// <param name="handle">场景资源句柄。</param>
        /// <returns>卸载任务。</returns>
        /// <exception cref="ArgumentNullException">资源句柄为空时抛出。</exception>
        public virtual UniTask UnloadSceneAsset(SceneAssetHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            RemoveAsset(handle);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 释放资源提供者。
        /// </summary>
        public virtual void Release()
        {
        }

        /// <summary>
        /// 解析 Bundle File Name。
        /// </summary>
        /// <param name="bundleInfo">bundle Info 参数。</param>
        /// <returns>执行结果。</returns>
        internal static string ResolveBundleFileName(BundleInfo bundleInfo)
        {
            if (bundleInfo == null)
            {
                throw new ArgumentNullException(nameof(bundleInfo));
            }

            var fileName = string.IsNullOrWhiteSpace(bundleInfo.Hash)
                ? bundleInfo.Name
                : $"{bundleInfo.Hash}.bundle";
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("Bundle file name cannot be empty.", nameof(bundleInfo));
            }

            return fileName;
        }
    }
}
