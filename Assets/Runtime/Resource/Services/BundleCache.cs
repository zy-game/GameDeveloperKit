using System.Collections.Generic;
using UnityEngine;
using ZLinq;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// Bundle 缓存管理
    /// 管理已加载的 AssetBundle 及其引用计数
    /// 注意：所有方法必须在Unity主线程调用
    /// </summary>
    public class BundleCache
    {
        private readonly Dictionary<string, AssetBundle> _loadedBundles = new Dictionary<string, AssetBundle>();
        private readonly Dictionary<string, int> _referenceCounts = new Dictionary<string, int>();
        
        /// <summary>
        /// 已加载的 Bundle 数量
        /// </summary>
        public int Count => _loadedBundles.Count;
        
        /// <summary>
        /// 尝试获取 Bundle
        /// </summary>
        public bool TryGet(string bundleName, out AssetBundle bundle)
        {
            return _loadedBundles.TryGetValue(bundleName, out bundle);
        }
        
        /// <summary>
        /// 添加 Bundle
        /// </summary>
        public void Add(string bundleName, AssetBundle bundle)
        {
            if (_loadedBundles.ContainsKey(bundleName))
            {
                Game.Debug.Warning($"Bundle '{bundleName}' already exists in cache");
                return;
            }
            
            _loadedBundles[bundleName] = bundle;
            _referenceCounts[bundleName] = 1;
        }
        
        /// <summary>
        /// 增加引用计数
        /// </summary>
        public void IncrementRef(string bundleName)
        {
            if (_referenceCounts.ContainsKey(bundleName))
            {
                _referenceCounts[bundleName]++;
            }
        }
        
        /// <summary>
        /// 减少引用计数
        /// </summary>
        public void DecrementRef(string bundleName)
        {
            if (_referenceCounts.ContainsKey(bundleName))
            {
                _referenceCounts[bundleName]--;
                
                if (_referenceCounts[bundleName] < 0)
                {
                    Game.Debug.Warning($"Bundle '{bundleName}' reference count is negative");
                    _referenceCounts[bundleName] = 0;
                }
            }
        }
        
        /// <summary>
        /// 获取引用计数
        /// </summary>
        public int GetRefCount(string bundleName)
        {
            return _referenceCounts.TryGetValue(bundleName, out var count) ? count : 0;
        }
        
        /// <summary>
        /// 卸载未使用的 Bundle
        /// </summary>
        public void UnloadUnused(bool unloadAllLoadedObjects = false)
        {
            var bundlesToUnload = new List<string>(_referenceCounts.Count);
            
            foreach (var kvp in _referenceCounts.AsValueEnumerable())
            {
                if (kvp.Value <= 0)
                {
                    bundlesToUnload.Add(kvp.Key);
                }
            }
            
            foreach (var bundleName in bundlesToUnload.AsValueEnumerable())
            {
                Unload(bundleName, unloadAllLoadedObjects);
            }
            
            if (bundlesToUnload.Count > 0)
            {
                Game.Debug.Info($"Unloaded {bundlesToUnload.Count} unused bundles");
            }
        }
        
        /// <summary>
        /// 卸载指定 Bundle
        /// </summary>
        public void Unload(string bundleName, bool unloadAllLoadedObjects = false)
        {
            if (_loadedBundles.TryGetValue(bundleName, out var bundle))
            {
                bundle?.Unload(unloadAllLoadedObjects);
                _loadedBundles.Remove(bundleName);
                _referenceCounts.Remove(bundleName);
                
                Game.Debug.Info($"Bundle '{bundleName}' unloaded");
            }
        }
        
        /// <summary>
        /// 是否包含 Bundle
        /// </summary>
        public bool Contains(string bundleName)
        {
            return _loadedBundles.ContainsKey(bundleName);
        }
        
        /// <summary>
        /// 清空缓存
        /// </summary>
        public void Clear(bool unloadAllLoadedObjects = true)
        {
            foreach (var bundle in _loadedBundles.Values)
            {
                bundle?.Unload(unloadAllLoadedObjects);
            }
            
            _loadedBundles.Clear();
            _referenceCounts.Clear();
        }
    }
}
