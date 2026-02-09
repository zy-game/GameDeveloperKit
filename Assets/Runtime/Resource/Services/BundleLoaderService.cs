using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// Bundle 加载服务
    /// 全局单例，负责 AssetBundle 的加载和依赖管理
    /// </summary>
    public class BundleLoaderService
    {
        private readonly BundleCache _cache;
        private readonly DependencyResolver _dependencyResolver;
        private readonly HashSet<string> _loadingBundles = new HashSet<string>();
        private readonly Dictionary<string, UniTaskCompletionSource<AssetBundle>> _loadingTasks 
            = new Dictionary<string, UniTaskCompletionSource<AssetBundle>>();
        
        public int LoadedBundleCount => _cache.Count;
        
        public BundleLoaderService()
        {
            _cache = new BundleCache();
            _dependencyResolver = new DependencyResolver();
        }
        
        /// <summary>
        /// 初始化依赖图
        /// </summary>
        public void InitializeDependencies(PackageManifest manifest)
        {
            _dependencyResolver.BuildDependencyGraph(manifest);
        }
        
        /// <summary>
        /// 异步加载 Bundle（含依赖）
        /// </summary>
        public async UniTask<AssetBundle> LoadBundleAsync(string bundleName)
        {
            // 1. 检查缓存
            if (_cache.TryGet(bundleName, out var cachedBundle))
            {
                _cache.IncrementRef(bundleName);
                return cachedBundle;
            }
            
            // 2. 等待正在加载的 Bundle
            if (_loadingTasks.TryGetValue(bundleName, out var loadingTask))
            {
                var bundle = await loadingTask.Task;
                if (bundle != null)
                {
                    _cache.IncrementRef(bundleName);
                }
                return bundle;
            }
            
            // 3. 开始加载
            var completionSource = new UniTaskCompletionSource<AssetBundle>();
            _loadingTasks[bundleName] = completionSource;
            _loadingBundles.Add(bundleName);
            
            try
            {
                // 4. 加载依赖
                var dependencies = _dependencyResolver.GetDependencies(bundleName);
                if (dependencies.Length > 0)
                {
                    var depTasks = new List<UniTask<AssetBundle>>();
                    foreach (var dep in dependencies)
                    {
                        depTasks.Add(LoadBundleAsync(dep));
                    }
                    await UniTask.WhenAll(depTasks);
                }
                
                // 5. 加载 Bundle
                var loadedBundle = await LoadBundleFromVFSAsync(bundleName);
                
                if (loadedBundle == null)
                {
                    Game.Debug.Error($"Failed to load bundle: {bundleName}");
                    completionSource.TrySetResult(null);
                    return null;
                }
                
                // 6. 缓存
                _cache.Add(bundleName, loadedBundle);
                completionSource.TrySetResult(loadedBundle);
                
                return loadedBundle;
            }
            catch (System.Exception ex)
            {
                Game.Debug.Error($"Load bundle exception '{bundleName}': {ex.Message}");
                completionSource.TrySetException(ex);
                return null;
            }
            finally
            {
                _loadingBundles.Remove(bundleName);
                _loadingTasks.Remove(bundleName);
            }
        }
        
        /// <summary>
        /// 从 VFS 加载 Bundle
        /// </summary>
        private async UniTask<AssetBundle> LoadBundleFromVFSAsync(string bundleName)
        {
            // 1. 从 VFS 读取 Bundle 数据
            var bundleHandle = await Game.File.ReadHandleAsync(bundleName);
            if (bundleHandle == null)
            {
                Game.Debug.Error($"Bundle '{bundleName}' not found in VFS");
                return null;
            }
            
            var bundleData = bundleHandle.Bytes;
            if (bundleData == null || bundleData.Length == 0)
            {
                Game.Debug.Error($"Bundle '{bundleName}' data is empty");
                return null;
            }
            
            // 2. 从内存加载 AssetBundle
            var loadRequest = AssetBundle.LoadFromMemoryAsync(bundleData);
            await loadRequest;
            
            var bundle = loadRequest.assetBundle;
            if (bundle == null)
            {
                Game.Debug.Error($"Failed to load AssetBundle '{bundleName}' from memory");
                return null;
            }
            
            Game.Debug.Debug($"Bundle loaded: {bundleName}");
            return bundle;
        }
        
        /// <summary>
        /// 卸载 Bundle
        /// </summary>
        public void UnloadBundle(string bundleName, bool unloadAllLoadedObjects = false)
        {
            _cache.DecrementRef(bundleName);
            
            if (_cache.GetRefCount(bundleName) <= 0)
            {
                // 递归减少此 Bundle 依赖的其他 Bundle 的引用计数
                var dependencies = _dependencyResolver.GetDependencies(bundleName);
                foreach (var dep in dependencies)
                {
                    UnloadBundle(dep, unloadAllLoadedObjects);
                }
                
                _cache.Unload(bundleName, unloadAllLoadedObjects);
            }
        }
        
        /// <summary>
        /// 卸载未使用的 Bundle
        /// </summary>
        public void UnloadUnusedBundles()
        {
            _cache.UnloadUnused(false);
        }
        
        /// <summary>
        /// 是否正在加载
        /// </summary>
        public bool IsLoading(string bundleName)
        {
            return _loadingBundles.Contains(bundleName);
        }
        
        /// <summary>
        /// 清理
        /// </summary>
        public void Clear()
        {
            _cache.Clear(true);
            _dependencyResolver.Clear();
            _loadingBundles.Clear();
            _loadingTasks.Clear();
        }
    }
}
