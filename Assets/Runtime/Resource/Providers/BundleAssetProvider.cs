using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// AssetBundle 资源提供者
    /// 从 AssetBundle 加载资源
    /// </summary>
    public class BundleAssetProvider : IAssetProvider
    {
        private readonly BundleLoaderService _bundleService;
        private readonly Dictionary<string, BaseHandle> _cachedAssets = new Dictionary<string, BaseHandle>();
        private readonly Dictionary<string, UniTaskCompletionSource<BaseHandle>> _loadingAssets 
            = new Dictionary<string, UniTaskCompletionSource<BaseHandle>>();
        
        public BundleAssetProvider(BundleLoaderService bundleService)
        {
            _bundleService = bundleService;
        }
        
        /// <summary>
        /// 异步加载资源
        /// </summary>
        public async UniTask<AssetHandle<T>> LoadAsync<T>(ResourceLocation location) where T : UnityEngine.Object
        {
            if (location == null || string.IsNullOrEmpty(location.BundleName))
            {
                Game.Debug.Error("[BundleAssetProvider] Invalid resource location");
                return AssetHandle<T>.Failure("unknown");
            }
            
            var cacheKey = location.AssetInfo.guid;
            var address = location.AssetInfo.address;
            
            // 1. 检查缓存
            if (_cachedAssets.TryGetValue(cacheKey, out var cachedHandle))
            {
                cachedHandle.Retain();
                return (AssetHandle<T>)cachedHandle;
            }
            
            // 2. 等待正在加载的资源
            if (_loadingAssets.TryGetValue(cacheKey, out var loadingTask))
            {
                var handle = await loadingTask.Task;
                if (handle != null)
                {
                    handle.Retain();
                    return (AssetHandle<T>)handle;
                }
                Game.Debug.Warning($"[BundleAssetProvider] Previous load failed for: {address}");
                return AssetHandle<T>.Failure(address);
            }
            
            // 3. 开始加载
            var completionSource = new UniTaskCompletionSource<BaseHandle>();
            _loadingAssets[cacheKey] = completionSource;
            
            try
            {
                // 4. 加载 Bundle（含依赖）
                var bundle = await _bundleService.LoadBundleAsync(location.BundleName);
                if (bundle == null)
                {
                    Game.Debug.Error($"[BundleAssetProvider] Failed to load bundle: {location.BundleName} for asset: {address}");
                    completionSource.TrySetResult(null);
                    return AssetHandle<T>.Failure(address);
                }
                
                // 5. 从 Bundle 加载资源
                var loadRequest = bundle.LoadAssetAsync<T>(location.AssetPath);
                await loadRequest;
                
                var asset = loadRequest.asset as T;
                if (asset == null)
                {
                    Game.Debug.Error($"[BundleAssetProvider] Failed to load asset '{location.AssetPath}' from bundle '{location.BundleName}'");
                    completionSource.TrySetResult(null);
                    return AssetHandle<T>.Failure(address);
                }
                
                // 6. 创建成功句柄
                var newHandle = AssetHandle<T>.Success(asset, location.AssetInfo);
                newHandle.Retain();
                
                // 7. 缓存
                _cachedAssets[cacheKey] = newHandle;
                completionSource.TrySetResult(newHandle);
                
                return newHandle;
            }
            catch (System.Exception ex)
            {
                Game.Debug.Error($"[BundleAssetProvider] Exception loading asset '{address}': {ex.Message}");
                completionSource.TrySetResult(null);
                return AssetHandle<T>.Failure(address);
            }
            finally
            {
                _loadingAssets.Remove(cacheKey);
            }
        }
        
        /// <summary>
        /// 卸载资源
        /// </summary>
        public void Unload(BaseHandle handle)
        {
            if (handle == null)
                return;
            
            handle.Release();
            
            if (handle.ReferenceCount <= 0)
            {
                var cacheKey = handle.GUID;
                _cachedAssets.Remove(cacheKey);
                handle.OnClearup();
                
                // 注意：这里不卸载 Bundle，由 BundleLoaderService 统一管理
            }
        }
        
        /// <summary>
        /// 卸载未使用的资源
        /// </summary>
        public void UnloadUnusedAssets()
        {
            var keysToRemove = new List<string>();
            
            foreach (var kvp in _cachedAssets)
            {
                if (kvp.Value.ReferenceCount <= 0)
                {
                    keysToRemove.Add(kvp.Key);
                    kvp.Value.OnClearup();
                }
            }
            
            foreach (var key in keysToRemove)
            {
                _cachedAssets.Remove(key);
            }
            
            if (keysToRemove.Count > 0)
            {
                Game.Debug.Info($"BundleAssetProvider: Unloaded {keysToRemove.Count} unused assets");
            }
        }
        
        /// <summary>
        /// 异步加载子资源（如 Atlas 中的 Sprite）
        /// </summary>
        public async UniTask<AssetHandle<T>> LoadSubAssetAsync<T>(ResourceLocation location, string subAssetName) 
            where T : UnityEngine.Object
        {
            var bundle = await _bundleService.LoadBundleAsync(location.BundleName);
            
            if (bundle == null)
            {
                Game.Debug.Error($"Failed to load bundle: {location.BundleName}");
                return default;
            }
            
            // 从 Bundle 加载指定名称的子资源
            var asset = bundle.LoadAsset<T>(subAssetName);
            
            if (asset == null)
            {
                Game.Debug.Error($"Sub-asset '{subAssetName}' not found in bundle '{location.BundleName}'");
                return default;
            }
            
            var handle = AssetHandle<T>.Success(asset, location.AssetInfo);
            handle.Retain();
            
            Game.Debug.Info($"[BundleAssetProvider] Loaded sub-asset: {subAssetName}");
            return handle;
        }
        
        /// <summary>
        /// 异步加载 Bundle 中所有指定类型的资源
        /// </summary>
        public async UniTask<List<AssetHandle<T>>> LoadAllAsync<T>(ResourceLocation location) 
            where T : UnityEngine.Object
        {
            var bundle = await _bundleService.LoadBundleAsync(location.BundleName);
            
            if (bundle == null)
            {
                Game.Debug.Error($"Failed to load bundle: {location.BundleName}");
                return new List<AssetHandle<T>>();
            }
            
            // 加载 Bundle 中所有指定类型的资源
            var allAssets = bundle.LoadAllAssets<T>();
            
            var handles = new List<AssetHandle<T>>();
            foreach (var asset in allAssets)
            {
                var handle = AssetHandle<T>.Success(asset, location.AssetInfo);
                handle.Retain();
                handles.Add(handle);
            }
            
            Game.Debug.Debug($"[BundleAssetProvider] Loaded {handles.Count} assets from bundle '{location.BundleName}'");
            return handles;
        }
        
        /// <summary>
        /// 清理
        /// </summary>
        public void Clear()
        {
            foreach (var handle in _cachedAssets.Values)
            {
                handle.OnClearup();
            }
            
            _cachedAssets.Clear();
            _loadingAssets.Clear();
        }
    }
}
