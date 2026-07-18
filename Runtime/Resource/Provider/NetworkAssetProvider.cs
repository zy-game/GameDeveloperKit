using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 网络散资源提供者，用于加载 http/https 资源并管理 URL 级句柄生命周期。
    /// </summary>
    public sealed class NetworkAssetProvider : IResourceHandleOwner
    {
        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg" };

        private readonly Dictionary<string, AssetHandle> m_Assets =
            new Dictionary<string, AssetHandle>(StringComparer.Ordinal);
        private readonly Dictionary<string, RawAssetHandle> m_RawAssets =
            new Dictionary<string, RawAssetHandle>(StringComparer.Ordinal);
        private readonly Dictionary<string, AssetHandle> m_PendingUnloadAssets =
            new Dictionary<string, AssetHandle>(StringComparer.Ordinal);
        private readonly Dictionary<string, RawAssetHandle> m_PendingUnloadRawAssets =
            new Dictionary<string, RawAssetHandle>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingLoadEntry<AssetHandle>> m_PendingAssetLoads =
            new Dictionary<string, PendingLoadEntry<AssetHandle>>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingLoadEntry<RawAssetHandle>> m_PendingRawAssetLoads =
            new Dictionary<string, PendingLoadEntry<RawAssetHandle>>(StringComparer.Ordinal);
        private bool m_AcceptLoads = true;

        public bool HasLoadedAssets =>
            m_Assets.Count > 0 ||
            m_RawAssets.Count > 0 ||
            m_PendingUnloadAssets.Count > 0 ||
            m_PendingUnloadRawAssets.Count > 0 ||
            HasPendingLoads;

        private bool HasPendingLoads => m_PendingAssetLoads.Count > 0 || m_PendingRawAssetLoads.Count > 0;

        /// <summary>
        /// 判断资源地址是否为网络散资源。
        /// </summary>
        public static bool IsNetworkLocation(string location)
        {
            return string.IsNullOrWhiteSpace(location) is false &&
                   (location.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    location.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 异步加载网络资源，图片地址返回 Texture2D，其余返回 TextAsset。
        /// </summary>
        public UniTask<AssetHandle> LoadAssetAsync(string location)
        {
            if (IsNetworkLocation(location) is false)
            {
                return UniTask.FromResult(AssetHandle.Failure(
                    new ArgumentException("Network asset location must start with http or https.", nameof(location))));
            }

            return LoadAsync(
                location,
                m_Assets,
                m_PendingUnloadAssets,
                m_PendingAssetLoads,
                LoadAssetInternalAsync,
                AssetHandle.Failure,
                "Network asset");
        }

        /// <summary>
        /// 异步加载网络二进制资源。
        /// </summary>
        public UniTask<RawAssetHandle> LoadRawAssetAsync(string location)
        {
            if (IsNetworkLocation(location) is false)
            {
                return UniTask.FromResult(RawAssetHandle.Failure(
                    new ArgumentException("Network asset location must start with http or https.", nameof(location))));
            }

            return LoadAsync(
                location,
                m_RawAssets,
                m_PendingUnloadRawAssets,
                m_PendingRawAssetLoads,
                LoadRawAssetInternalAsync,
                RawAssetHandle.Failure,
                "Network raw asset");
        }

        public UniTask UnloadAsset(AssetHandle handle)
        {
            ReleaseOwnedHandle(handle, m_Assets, m_PendingUnloadAssets, "Network asset");
            return UniTask.CompletedTask;
        }

        public UniTask UnloadRawAsset(RawAssetHandle handle)
        {
            ReleaseOwnedHandle(handle, m_RawAssets, m_PendingUnloadRawAssets, "Network raw asset");
            return UniTask.CompletedTask;
        }

        public UniTask UnloadUnusedAssetAsync()
        {
            foreach (var pair in m_PendingUnloadAssets.ToArray())
            {
                if (pair.Value.IsReferenced)
                {
                    continue;
                }

                m_PendingUnloadAssets.Remove(pair.Key);
                ReleaseAssetHandle(pair.Value);
            }

            foreach (var pair in m_PendingUnloadRawAssets.ToArray())
            {
                if (pair.Value.IsReferenced)
                {
                    continue;
                }

                m_PendingUnloadRawAssets.Remove(pair.Key);
                ReleaseHandle(pair.Value);
            }

            return UniTask.CompletedTask;
        }

        internal async UniTask StopAndDrainPendingLoadsAsync()
        {
            m_AcceptLoads = false;
            foreach (var entry in m_PendingAssetLoads.Values.ToArray())
            {
                await entry.WaitAsync();
            }

            foreach (var entry in m_PendingRawAssetLoads.Values.ToArray())
            {
                await entry.WaitAsync();
            }
        }

        internal void ResumeLoadsAfterTeardownFailure()
        {
            m_AcceptLoads = true;
        }

        /// <summary>
        /// 释放所有网络句柄和已创建的Unity资源对象。
        /// </summary>
        public void Release()
        {
            m_AcceptLoads = false;
            if (HasPendingLoads)
            {
                throw new GameException("Cannot release network resources while requests are pending. Await network teardown first.");
            }

            foreach (var handle in m_Assets.Values.Concat(m_PendingUnloadAssets.Values).Distinct().ToArray())
            {
                ReleaseAssetHandle(handle);
            }

            foreach (var handle in m_RawAssets.Values.Concat(m_PendingUnloadRawAssets.Values).Distinct().ToArray())
            {
                ReleaseHandle(handle);
            }

            m_Assets.Clear();
            m_RawAssets.Clear();
            m_PendingUnloadAssets.Clear();
            m_PendingUnloadRawAssets.Clear();
            m_AcceptLoads = true;
        }

        bool IResourceHandleOwner.ReleaseHandle<TInfo>(ResourceHandle<TInfo> handle)
        {
            if (handle is AssetHandle assetHandle)
            {
                return ReleaseOwnedHandle(assetHandle, m_Assets, m_PendingUnloadAssets, "Network asset");
            }

            if (handle is RawAssetHandle rawAssetHandle)
            {
                return ReleaseOwnedHandle(rawAssetHandle, m_RawAssets, m_PendingUnloadRawAssets, "Network raw asset");
            }

            return false;
        }

        private async UniTask<THandle> LoadAsync<THandle>(
            string location,
            IDictionary<string, THandle> activeHandles,
            IDictionary<string, THandle> pendingUnloadHandles,
            IDictionary<string, PendingLoadEntry<THandle>> pendingLoads,
            Func<string, UniTask<THandle>> loader,
            Func<Exception, THandle> failureFactory,
            string assetTypeLabel)
            where THandle : ResourceHandle
        {
            if (m_AcceptLoads is false)
            {
                return failureFactory(new GameException($"{assetTypeLabel} provider is shutting down."));
            }

            if (TryGetCached(location, activeHandles, pendingUnloadHandles, out var cachedHandle))
            {
                return cachedHandle;
            }

            var ownsInitialReference = false;
            if (pendingLoads.TryGetValue(location, out var entry) is false)
            {
                entry = new PendingLoadEntry<THandle>();
                pendingLoads.Add(location, entry);
                ownsInitialReference = true;
                ExecuteLoadAsync(location, activeHandles, pendingLoads, entry, loader)
                    .Forget(UnityEngine.Debug.LogException);
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

        private async UniTask ExecuteLoadAsync<THandle>(
            string location,
            IDictionary<string, THandle> activeHandles,
            IDictionary<string, PendingLoadEntry<THandle>> pendingLoads,
            PendingLoadEntry<THandle> entry,
            Func<string, UniTask<THandle>> loader)
            where THandle : ResourceHandle
        {
            LoadResult<THandle> result;
            try
            {
                var handle = await loader(location);
                if (handle == null || handle.Status is not ResourceStatus.Succeeded)
                {
                    var error = handle?.Error ?? new GameException($"Network asset load failed: {location}");
                    handle?.ReleaseInternal();
                    result = LoadResult<THandle>.Failure(error);
                }
                else
                {
                    handle.AttachOwner(this);
                    activeHandles.Add(location, handle);
                    result = LoadResult<THandle>.Success(handle);
                }
            }
            catch (Exception exception)
            {
                result = LoadResult<THandle>.Failure(exception);
            }

            if (pendingLoads.TryGetValue(location, out var current) && ReferenceEquals(current, entry))
            {
                pendingLoads.Remove(location);
            }

            entry.SetResult(result);
        }

        private static bool TryGetCached<THandle>(
            string location,
            IDictionary<string, THandle> activeHandles,
            IDictionary<string, THandle> pendingUnloadHandles,
            out THandle handle)
            where THandle : ResourceHandle
        {
            if (activeHandles.TryGetValue(location, out handle))
            {
                handle.Retain();
                return true;
            }

            if (pendingUnloadHandles.TryGetValue(location, out handle))
            {
                pendingUnloadHandles.Remove(location);
                activeHandles.Add(location, handle);
                handle.Retain();
                return true;
            }

            handle = null;
            return false;
        }

        private bool ReleaseOwnedHandle<THandle>(
            THandle handle,
            IDictionary<string, THandle> activeHandles,
            IDictionary<string, THandle> pendingUnloadHandles,
            string assetTypeLabel)
            where THandle : ResourceHandle
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (handle.Status is ResourceStatus.Released)
            {
                return false;
            }

            if (ReferenceEquals(handle.Owner, this) is false || handle.Info == null)
            {
                throw new GameException($"{assetTypeLabel} handle belongs to another provider.");
            }

            var location = handle.Info.Location;
            var isActive = activeHandles.TryGetValue(location, out var active) && ReferenceEquals(active, handle);
            var isPending = pendingUnloadHandles.TryGetValue(location, out var pending) && ReferenceEquals(pending, handle);
            if (isActive is false && isPending is false)
            {
                throw new GameException($"{assetTypeLabel} handle is not tracked: {location}");
            }

            if (handle.ReleaseReference() > 0 || isPending)
            {
                return false;
            }

            activeHandles.Remove(location);
            pendingUnloadHandles.Add(location, handle);
            return true;
        }

        private static void ReleaseAssetHandle(AssetHandle handle)
        {
            var asset = handle.Asset;
            handle.DetachOwner(handle.Owner);
            handle.ReleaseInternal();
            if (asset != null)
            {
                UnityEngine.Object.Destroy(asset);
            }
        }

        private static void ReleaseHandle(ResourceHandle handle)
        {
            handle.DetachOwner(handle.Owner);
            handle.ReleaseInternal();
        }

        private static bool IsImageLocation(string location)
        {
            var path = Uri.TryCreate(location, UriKind.Absolute, out var uri) ? uri.AbsolutePath : location;
            return ImageExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        }

        private static async UniTask<AssetHandle> LoadAssetInternalAsync(string location)
        {
            var info = new AssetInfo { Location = location };
            if (IsImageLocation(location))
            {
                return AssetHandle.Success(info, await DownloadTextureAsync(location));
            }

            var bytes = await DownloadBytesAsync(location);
            return AssetHandle.Success(info, new TextAsset(System.Text.Encoding.UTF8.GetString(bytes)));
        }

        private static async UniTask<RawAssetHandle> LoadRawAssetInternalAsync(string location)
        {
            return RawAssetHandle.Success(new AssetInfo { Location = location }, await DownloadBytesAsync(location));
        }

        private static async UniTask<Texture2D> DownloadTextureAsync(string location)
        {
            using (var request = UnityWebRequestTexture.GetTexture(location))
            {
                await request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new GameException(request.error ?? $"Network asset load failed: {location}");
                }

                return DownloadHandlerTexture.GetContent(request);
            }
        }

        private static async UniTask<byte[]> DownloadBytesAsync(string location)
        {
            using (var request = UnityWebRequest.Get(location))
            {
                await request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new GameException(request.error ?? $"Network asset load failed: {location}");
                }

                return request.downloadHandler.data ?? Array.Empty<byte>();
            }
        }

        private sealed class PendingLoadEntry<THandle> where THandle : ResourceHandle
        {
            private readonly UniTaskCompletionSource<LoadResult<THandle>> m_Completion =
                new UniTaskCompletionSource<LoadResult<THandle>>();

            public UniTask<LoadResult<THandle>> Task => m_Completion.Task;

            public async UniTask WaitAsync()
            {
                await m_Completion.Task;
            }

            public void SetResult(LoadResult<THandle> result)
            {
                m_Completion.TrySetResult(result);
            }
        }

        private readonly struct LoadResult<THandle> where THandle : ResourceHandle
        {
            public THandle Handle { get; }
            public Exception Error { get; }

            private LoadResult(THandle handle, Exception error)
            {
                Handle = handle;
                Error = error;
            }

            public static LoadResult<THandle> Success(THandle handle)
            {
                return new LoadResult<THandle>(handle, null);
            }

            public static LoadResult<THandle> Failure(Exception exception)
            {
                return new LoadResult<THandle>(null, exception);
            }
        }
    }
}
