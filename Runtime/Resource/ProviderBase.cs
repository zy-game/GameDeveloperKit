using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Resource
{
    public abstract class ProviderBase : IResourceHandleOwner
    {
        private readonly List<ResourceHandle> _assets;
        private readonly List<ResourceHandle> _pendingUnloadAssets;
        private readonly Dictionary<SceneAssetHandle, SceneUnloadEntry> _sceneUnloadEntries;
        private readonly Dictionary<string, PendingLoadEntry<AssetHandle>> _pendingAssetLoads;
        private readonly Dictionary<string, PendingLoadEntry<RawAssetHandle>> _pendingRawAssetLoads;
        private readonly Dictionary<string, PendingLoadEntry<SceneAssetHandle>> _pendingSceneAssetLoads;
        private int _referenceCount = 1;
        private bool _acceptLoads = true;

        /// <summary>
        /// 资源包信息。
        /// </summary>
        public BundleInfo Info { get; }

        /// <summary>
        /// 资源状态
        /// </summary>
        public ResourceStatus Status { get; protected set; } = ResourceStatus.None;

        /// <summary>
        /// 被 package 持有的引用数量。
        /// </summary>
        public int ReferenceCount => _referenceCount;

        /// <summary>
        /// 是否仍被至少一个 package 持有。
        /// </summary>
        public bool IsReferenced => _referenceCount > 0;

        /// <summary>
        /// 是否仍持有已加载资源句柄。
        /// </summary>
        public bool HasLoadedAssets =>
            _assets.Count > 0 ||
            _pendingUnloadAssets.Count > 0 ||
            HasPendingLoads;

        private bool HasPendingLoads =>
            _pendingAssetLoads.Count > 0 ||
            _pendingRawAssetLoads.Count > 0 ||
            _pendingSceneAssetLoads.Count > 0;

        /// <summary>
        /// 初始化资源提供者。
        /// </summary>
        /// <param name="info">资源包信息。</param>
        public ProviderBase(BundleInfo info)
        {
            Info = info;
            _assets = new List<ResourceHandle>();
            _pendingUnloadAssets = new List<ResourceHandle>();
            _sceneUnloadEntries = new Dictionary<SceneAssetHandle, SceneUnloadEntry>();
            _pendingAssetLoads = new Dictionary<string, PendingLoadEntry<AssetHandle>>(StringComparer.Ordinal);
            _pendingRawAssetLoads = new Dictionary<string, PendingLoadEntry<RawAssetHandle>>(StringComparer.Ordinal);
            _pendingSceneAssetLoads = new Dictionary<string, PendingLoadEntry<SceneAssetHandle>>(StringComparer.Ordinal);
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
        /// 增加 package 引用。
        /// </summary>
        public int RetainReference()
        {
            if (Status is ResourceStatus.Failed or ResourceStatus.Released)
            {
                throw new GameException($"Cannot retain a {Status.ToString().ToLowerInvariant()} resource provider: {Info?.Name}");
            }

            if (_referenceCount == 0)
            {
                _acceptLoads = true;
            }

            _referenceCount++;
            return _referenceCount;
        }

        /// <summary>
        /// 减少 package 引用。
        /// </summary>
        public int ReleaseReference()
        {
            if (_referenceCount <= 0)
            {
                return 0;
            }

            _referenceCount--;
            if (_referenceCount == 0)
            {
                _acceptLoads = false;
            }

            return _referenceCount;
        }

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

            return await LoadAssetByInfoAsync(asset);
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

            return await LoadRawAssetByInfoAsync(asset);
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

            var pendingHandle = _pendingUnloadAssets
                .OfType<SceneAssetHandle>()
                .FirstOrDefault(candidate => candidate.Info == asset);
            if (pendingHandle != null && _sceneUnloadEntries.TryGetValue(pendingHandle, out var unloadEntry))
            {
                await unloadEntry.Task;
            }

            return await LoadSceneAssetByInfoAsync(asset);
        }

        /// <summary>
        /// 检查资源提供者是否包含指定 Location。
        /// </summary>
        /// <param name="location">资源 Location。</param>
        /// <returns>如果包含资源，则返回true；否则返回false。</returns>
        public bool HasAsset(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return false;
            }

            return GetAssets().Any(x => string.Equals(x.Location, location, StringComparison.Ordinal));
        }

        /// <summary>
        /// 加载 Asset By Info Async。
        /// </summary>
        internal UniTask<AssetHandle> LoadAssetByInfoAsync(AssetInfo asset)
        {
            return LoadByInfoAsync(
                asset,
                _pendingAssetLoads,
                LoadAssetInternalAsync,
                AssetHandle.Failure,
                "Asset");
        }

        /// <summary>
        /// 加载 Raw Asset By Info Async。
        /// </summary>
        internal UniTask<RawAssetHandle> LoadRawAssetByInfoAsync(AssetInfo asset)
        {
            return LoadByInfoAsync(
                asset,
                _pendingRawAssetLoads,
                LoadRawAssetInternalAsync,
                RawAssetHandle.Failure,
                "Raw asset");
        }

        /// <summary>
        /// 按资源信息加载场景并合并同 Location 的首次并发请求。
        /// </summary>
        private UniTask<SceneAssetHandle> LoadSceneAssetByInfoAsync(AssetInfo asset)
        {
            return LoadByInfoAsync(
                asset,
                _pendingSceneAssetLoads,
                LoadSceneAssetInternalAsync,
                SceneAssetHandle.Failure,
                "Scene");
        }

        private async UniTask<THandle> LoadByInfoAsync<THandle>(
            AssetInfo asset,
            Dictionary<string, PendingLoadEntry<THandle>> pendingLoads,
            Func<AssetInfo, UniTask<THandle>> loader,
            Func<Exception, THandle> failureFactory,
            string assetTypeLabel)
            where THandle : ResourceHandle
        {
            if (_referenceCount <= 0)
            {
                return failureFactory(new GameException($"{assetTypeLabel} provider is not owned by an initialized package: {Info?.Name}"));
            }

            if (_acceptLoads is false)
            {
                return failureFactory(new GameException($"{assetTypeLabel} provider is shutting down: {Info?.Name}"));
            }

            if (TryGetAsset<THandle>(asset, out var cachedHandle))
            {
                return cachedHandle;
            }

            var ownsInitialReference = false;
            if (pendingLoads.TryGetValue(asset.Location, out var entry) is false)
            {
                entry = new PendingLoadEntry<THandle>();
                pendingLoads.Add(asset.Location, entry);
                ownsInitialReference = true;
                ExecutePendingLoadAsync(
                    asset,
                    pendingLoads,
                    entry,
                    loader,
                    assetTypeLabel).Forget(UnityEngine.Debug.LogException);
            }

            var result = await entry.Task;
            if (result.Handle == null)
            {
                return failureFactory(result.Error);
            }

            if (ownsInitialReference is false)
            {
                result.Handle.Retain();
            }

            return result.Handle;
        }

        private async UniTask ExecutePendingLoadAsync<THandle>(
            AssetInfo asset,
            Dictionary<string, PendingLoadEntry<THandle>> pendingLoads,
            PendingLoadEntry<THandle> entry,
            Func<AssetInfo, UniTask<THandle>> loader,
            string assetTypeLabel)
            where THandle : ResourceHandle
        {
            PendingLoadResult<THandle> result;
            try
            {
                var handle = await loader(asset);
                if (handle != null && handle.Status == ResourceStatus.Succeeded)
                {
                    AddAsset(handle);
                    result = PendingLoadResult<THandle>.Success(handle);
                }
                else
                {
                    var exception = handle?.Error ?? new GameException($"{assetTypeLabel} load failed: {asset.Location}");
                    handle?.ReleaseInternal();
                    result = PendingLoadResult<THandle>.Failure(exception);
                }
            }
            catch (Exception exception)
            {
                result = PendingLoadResult<THandle>.Failure(exception);
            }

            if (pendingLoads.TryGetValue(asset.Location, out var currentEntry) &&
                ReferenceEquals(currentEntry, entry))
            {
                pendingLoads.Remove(asset.Location);
            }

            entry.SetResult(result);
        }

        /// <summary>
        /// 获取 Assets。
        /// </summary>
        private IEnumerable<AssetInfo> GetAssets()
        {
            return Info?.Assets?.Where(x => x != null) ?? Enumerable.Empty<AssetInfo>();
        }

        /// <summary>
        /// 获取 Assets By Label。
        /// </summary>
        internal IReadOnlyList<AssetInfo> GetAssetsByLabel(string label)
        {
            return GetAssets().Where(x => x.Labels != null && x.Labels.Contains(label)).ToArray();
        }

        /// <summary>
        /// 获取 Assets By Type。
        /// </summary>
        /// <param name="typeName">type Name 参数。</param>
        internal IReadOnlyList<AssetInfo> GetAssetsByType(string typeName)
        {
            return GetAssets().Where(x => x.TypeName == typeName).ToArray();
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

            handle.AttachOwner(this);
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

            if (handle.Owner != null && ReferenceEquals(handle.Owner, this) is false)
            {
                return false;
            }

            if (handle.ReleaseReference() > 0)
            {
                return false;
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
        /// 释放一个资源句柄引用。
        /// </summary>
        /// <param name="handle">资源句柄。</param>
        bool IResourceHandleOwner.ReleaseHandle<TInfo>(ResourceHandle<TInfo> handle)
        {
            if (handle is not ResourceHandle resourceHandle)
            {
                return false;
            }

            return RemoveAsset(resourceHandle);
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
                target.Retain();
                handle = target;
                return true;
            }

            target = _pendingUnloadAssets.OfType<T>().FirstOrDefault(x => x.Info == info);
            if (target is not null)
            {
                _pendingUnloadAssets.Remove(target);
                target.Retain();
                _assets.Add(target);
            }

            handle = target;
            return target != null;
        }

        /// <summary>
        /// 卸载待释放资源。
        /// </summary>
        /// <returns>卸载任务。</returns>
        public virtual async UniTask UnloadUnusedAssetAsync()
        {
            foreach (var handle in _pendingUnloadAssets.ToArray())
            {
                if (handle is SceneAssetHandle sceneHandle)
                {
                    await EnsureSceneUnloadAsync(sceneHandle);
                    continue;
                }

                _pendingUnloadAssets.Remove(handle);
                handle.ReleaseInternal();
            }
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
        public virtual async UniTask UnloadSceneAsset(SceneAssetHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (_sceneUnloadEntries.TryGetValue(handle, out var unloadEntry))
            {
                await unloadEntry.Task;
                return;
            }

            if (MoveSceneToPending(handle, false) is false)
            {
                return;
            }

            await EnsureSceneUnloadAsync(handle);
        }

        internal void ValidateCanUnloadAllScenes()
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var handle in EnumerateSceneHandles())
            {
                if (handle.Asset.IsValid() && handle.Asset.isLoaded && handle.Asset == activeScene)
                {
                    throw new GameException($"Cannot unload active scene: {handle.SceneName}");
                }
            }
        }

        internal async UniTask UnloadAllSceneAssetsAsync()
        {
            ValidateCanUnloadAllScenes();
            foreach (var handle in _assets.OfType<SceneAssetHandle>().ToArray())
            {
                MoveSceneToPending(handle, true);
            }

            foreach (var handle in _pendingUnloadAssets.OfType<SceneAssetHandle>().ToArray())
            {
                await EnsureSceneUnloadAsync(handle);
            }
        }

        /// <summary>
        /// 释放资源提供者。
        /// </summary>
        public virtual void Release()
        {
            _acceptLoads = false;
            if (HasPendingLoads)
            {
                throw new GameException("Cannot release a resource provider while resource loads are pending. Await provider teardown first.");
            }

            if (_sceneUnloadEntries.Count > 0 ||
                EnumerateSceneHandles().Any(handle => handle.Asset.IsValid() && handle.Asset.isLoaded))
            {
                throw new GameException("Cannot release a resource provider while Unity scenes are loaded. Await scene unload first.");
            }

            _referenceCount = 0;
            foreach (var handle in _assets.ToArray())
            {
                handle.ReleaseInternal();
            }

            foreach (var handle in _pendingUnloadAssets.ToArray())
            {
                handle.ReleaseInternal();
            }

            _assets.Clear();
            _pendingUnloadAssets.Clear();
        }

        /// <summary>
        /// 停止接收新加载并等待 provider 当前所有底层加载完成。
        /// </summary>
        protected async UniTask PrepareForUninitializeAsync()
        {
            await StopAndDrainPendingLoadsAsync();
            if (_assets.Count > 0 || _pendingUnloadAssets.Count > 0 || _sceneUnloadEntries.Count > 0)
            {
                throw new GameException("Cannot uninitialize a resource provider while loaded handles are still retained.");
            }
        }

        internal async UniTask StopAndDrainPendingLoadsAsync()
        {
            _acceptLoads = false;
            await WaitForPendingEntriesAsync(_pendingAssetLoads.Values.ToArray());
            await WaitForPendingEntriesAsync(_pendingRawAssetLoads.Values.ToArray());
            await WaitForPendingEntriesAsync(_pendingSceneAssetLoads.Values.ToArray());
        }

        internal void ResumeLoadsAfterTeardownFailure()
        {
            if (Status != ResourceStatus.Released)
            {
                _acceptLoads = true;
            }
        }

        private static async UniTask WaitForPendingEntriesAsync<THandle>(
            IReadOnlyList<PendingLoadEntry<THandle>> entries)
            where THandle : ResourceHandle
        {
            foreach (var entry in entries)
            {
                await entry.WaitAsync();
            }
        }

        private bool MoveSceneToPending(SceneAssetHandle handle, bool releaseAllReferences)
        {
            if (handle.Owner != null && ReferenceEquals(handle.Owner, this) is false)
            {
                throw new GameException($"Scene handle belongs to another provider: {handle.SceneName}");
            }

            if (_pendingUnloadAssets.Contains(handle))
            {
                return true;
            }

            if (_assets.Contains(handle) is false)
            {
                return false;
            }

            if (releaseAllReferences)
            {
                while (handle.ReleaseReference() > 0)
                {
                }
            }
            else if (handle.ReleaseReference() > 0)
            {
                return false;
            }

            _assets.Remove(handle);
            _pendingUnloadAssets.Add(handle);
            return true;
        }

        private UniTask EnsureSceneUnloadAsync(SceneAssetHandle handle)
        {
            if (_sceneUnloadEntries.TryGetValue(handle, out var existingEntry))
            {
                return existingEntry.Task;
            }

            var entry = new SceneUnloadEntry();
            _sceneUnloadEntries.Add(handle, entry);
            ExecuteSceneUnloadAsync(handle, entry).Forget(UnityEngine.Debug.LogException);
            return entry.Task;
        }

        private async UniTask ExecuteSceneUnloadAsync(
            SceneAssetHandle handle,
            SceneUnloadEntry entry)
        {
            try
            {
                var scene = handle.Asset;
                if (scene.IsValid() && scene.isLoaded)
                {
                    if (scene == SceneManager.GetActiveScene())
                    {
                        throw new GameException($"Cannot unload active scene: {handle.SceneName}");
                    }

                    handle.SetStatus(ResourceStatus.Unloading);
                    var operation = SceneManager.UnloadSceneAsync(scene);
                    if (operation == null)
                    {
                        throw new GameException($"Scene unload operation could not start: {handle.SceneName}");
                    }

                    await operation;
                    if (scene.isLoaded)
                    {
                        throw new GameException($"Scene unload did not complete: {handle.SceneName}");
                    }
                }

                _pendingUnloadAssets.Remove(handle);
                handle.ReleaseInternal();
                _sceneUnloadEntries.Remove(handle);
                entry.SetResult();
            }
            catch (Exception exception)
            {
                if (handle.Status is ResourceStatus.Unloading)
                {
                    handle.SetStatus(ResourceStatus.Succeeded);
                }

                _sceneUnloadEntries.Remove(handle);
                entry.SetException(exception);
            }
        }

        private IEnumerable<SceneAssetHandle> EnumerateSceneHandles()
        {
            return _assets
                .Concat(_pendingUnloadAssets)
                .OfType<SceneAssetHandle>()
                .Distinct();
        }

        private sealed class SceneUnloadEntry
        {
            private readonly UniTaskCompletionSource m_Completion = new UniTaskCompletionSource();

            public UniTask Task => m_Completion.Task;

            public void SetResult()
            {
                m_Completion.TrySetResult();
            }

            public void SetException(Exception exception)
            {
                m_Completion.TrySetException(exception);
            }
        }

        private sealed class PendingLoadEntry<THandle> where THandle : ResourceHandle
        {
            private readonly UniTaskCompletionSource<PendingLoadResult<THandle>> m_Completion =
                new UniTaskCompletionSource<PendingLoadResult<THandle>>();

            public UniTask<PendingLoadResult<THandle>> Task => m_Completion.Task;

            public async UniTask WaitAsync()
            {
                await m_Completion.Task;
            }

            public void SetResult(PendingLoadResult<THandle> result)
            {
                m_Completion.TrySetResult(result);
            }
        }

        private readonly struct PendingLoadResult<THandle> where THandle : ResourceHandle
        {
            public THandle Handle { get; }
            public Exception Error { get; }

            private PendingLoadResult(THandle handle, Exception error)
            {
                Handle = handle;
                Error = error;
            }

            public static PendingLoadResult<THandle> Success(THandle handle)
            {
                return new PendingLoadResult<THandle>(handle, null);
            }

            public static PendingLoadResult<THandle> Failure(Exception exception)
            {
                return new PendingLoadResult<THandle>(null, exception);
            }
        }

        /// <summary>
        /// 解析 Bundle File Name。
        /// </summary>
        /// <param name="bundleInfo">bundle Info 参数。</param>
        internal static string ResolveBundleFileName(BundleInfo bundleInfo)
        {
            if (bundleInfo == null)
            {
                throw new ArgumentNullException(nameof(bundleInfo));
            }

            var fileName = bundleInfo.Name;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("Bundle file name cannot be empty.", nameof(bundleInfo));
            }

            return fileName;
        }
    }

    internal readonly struct ResourceBatchItem
    {
        public ResourceBatchItem(ProviderBase provider, AssetInfo asset)
        {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Asset = asset ?? throw new ArgumentNullException(nameof(asset));
        }

        public ProviderBase Provider { get; }

        public AssetInfo Asset { get; }
    }

    internal static class ResourceBatchLoader
    {
        public static async UniTask<IReadOnlyList<THandle>> LoadAsync<THandle>(
            IReadOnlyList<ResourceBatchItem> items,
            int maxConcurrency,
            Func<ResourceBatchItem, UniTask<THandle>> loader,
            string assetKind)
            where THandle : ResourceHandle
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            if (items.Count == 0)
            {
                return Array.Empty<THandle>();
            }

            ResourceSettings.ValidateBatchLoadConcurrency(maxConcurrency);
            var state = new BatchState<THandle>(items.Count);
            var workers = new UniTask[Math.Min(maxConcurrency, items.Count)];
            for (var index = 0; index < workers.Length; index++)
            {
                workers[index] = RunWorkerAsync(items, loader, state);
            }

            await UniTask.WhenAll(workers);
            for (var index = 0; index < state.Errors.Length; index++)
            {
                if (state.Errors[index] == null)
                {
                    continue;
                }

                ReleaseBatch(state.Results);
                throw new GameException(
                    $"Failed to load {assetKind} batch item: {items[index].Asset.Location}",
                    state.Errors[index]);
            }

            return state.Results;
        }

        private static async UniTask RunWorkerAsync<THandle>(
            IReadOnlyList<ResourceBatchItem> items,
            Func<ResourceBatchItem, UniTask<THandle>> loader,
            BatchState<THandle> state)
            where THandle : ResourceHandle
        {
            while (Volatile.Read(ref state.FailureSignaled) == 0)
            {
                var index = Interlocked.Increment(ref state.NextIndex) - 1;
                if (index >= items.Count)
                {
                    return;
                }

                THandle handle;
                try
                {
                    handle = await loader(items[index]);
                }
                catch (Exception exception)
                {
                    state.Errors[index] = exception;
                    Interlocked.Exchange(ref state.FailureSignaled, 1);
                    continue;
                }

                if (handle == null || handle.Status is not ResourceStatus.Succeeded)
                {
                    state.Errors[index] = handle?.Error ??
                        new GameException($"Provider returned no {typeof(THandle).Name} for: {items[index].Asset.Location}");
                    handle?.Release();
                    Interlocked.Exchange(ref state.FailureSignaled, 1);
                    continue;
                }

                state.Results[index] = handle;
            }
        }

        private static void ReleaseBatch<THandle>(IReadOnlyList<THandle> handles)
            where THandle : ResourceHandle
        {
            for (var index = handles.Count - 1; index >= 0; index--)
            {
                handles[index]?.Release();
            }
        }

        private sealed class BatchState<THandle>
            where THandle : ResourceHandle
        {
            public BatchState(int count)
            {
                Results = new THandle[count];
                Errors = new Exception[count];
            }

            public readonly THandle[] Results;
            public readonly Exception[] Errors;
            public int NextIndex;
            public int FailureSignaled;
        }
    }
}
