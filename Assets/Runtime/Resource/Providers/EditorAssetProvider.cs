#if UNITY_EDITOR
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 编辑器资源提供者
    /// 使用 AssetDatabase 直接加载资源，无需打包
    /// </summary>
    public class EditorAssetProvider : IAssetProvider
    {
        private readonly Dictionary<string, BaseHandle> _cachedAssets = new Dictionary<string, BaseHandle>();
        
        public async UniTask<AssetHandle<T>> LoadAsync<T>(ResourceLocation location) where T : UnityEngine.Object
        {
            var cacheKey = location.AssetInfo.guid;
            
            // 检查缓存
            if (_cachedAssets.TryGetValue(cacheKey, out var cachedHandle))
            {
                cachedHandle.Retain();
                return (AssetHandle<T>)cachedHandle;
            }
            
            // 优先使用 path 字段加载
            var assetPath = location.AssetInfo.path;
            
            if (!string.IsNullOrEmpty(assetPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                {
                    Game.Debug.Debug($"[EditorAssetProvider] Loaded asset from path: {assetPath}");
                    var newHandle = AssetHandle<T>.Success(asset, location.AssetInfo);
                    newHandle.Retain();
                    _cachedAssets[cacheKey] = newHandle;
                    await UniTask.Yield();
                    return newHandle;
                }
            }
            
            // 降级：使用 address
            assetPath = location.AssetInfo.address;
            
            // 如果 address 不是完整路径，尝试查找
            if (!assetPath.StartsWith("Assets/"))
            {
                // 通过名称查找资源
                var guids = AssetDatabase.FindAssets($"{location.AssetInfo.name} t:{typeof(T).Name}");
                if (guids.Length > 0)
                {
                    assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                }
                else
                {
                    Game.Debug.Error($"[EditorAssetProvider] Asset not found: {location.AssetInfo.name}");
                    return AssetHandle<T>.Failure(location.AssetInfo.address);
                }
            }
            
            var fallbackAsset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            
            if (fallbackAsset == null)
            {
                Game.Debug.Error($"[EditorAssetProvider] Failed to load asset: {assetPath}");
                return AssetHandle<T>.Failure(location.AssetInfo.address);
            }
            
            Game.Debug.Debug($"[EditorAssetProvider] Loaded asset from address: {assetPath}");
            
            var handle = AssetHandle<T>.Success(fallbackAsset, location.AssetInfo);
            handle.Retain();
            
            _cachedAssets[cacheKey] = handle;
            
            // 模拟异步加载
            await UniTask.Yield();
            
            return handle;
        }
        
        public void Unload(BaseHandle handle)
        {
            if (handle == null)
                return;
            
            handle.Release();
            
            if (handle.ReferenceCount <= 0)
            {
                _cachedAssets.Remove(handle.GUID);
                // 编辑器模式不需要真正卸载资源
            }
        }
        
        /// <summary>
        /// 异步加载子资源（如 Atlas 中的 Sprite）
        /// </summary>
        public async UniTask<AssetHandle<T>> LoadSubAssetAsync<T>(ResourceLocation location, string subAssetName) 
            where T : UnityEngine.Object
        {
            var mainAssetPath = location.AssetInfo.path;
            
            if (string.IsNullOrEmpty(mainAssetPath))
                mainAssetPath = location.AssetInfo.address;
            
            // 加载所有子资源
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(mainAssetPath);
            
            foreach (var asset in allAssets)
            {
                if (asset.name == subAssetName && asset is T typedAsset)
                {
                    var handle = AssetHandle<T>.Success(typedAsset, location.AssetInfo);
                    handle.Retain();
                    
                    Game.Debug.Debug($"[EditorAssetProvider] Loaded sub-asset: {subAssetName} from {mainAssetPath}");
                    await UniTask.Yield();
                    return handle;
                }
            }
            
            Game.Debug.Error($"[EditorAssetProvider] Sub-asset '{subAssetName}' not found in '{mainAssetPath}'");
            return AssetHandle<T>.Failure(location.AssetInfo.address);
        }
        
        /// <summary>
        /// 异步加载资源中的所有子对象
        /// </summary>
        public async UniTask<List<AssetHandle<T>>> LoadAllAsync<T>(ResourceLocation location) 
            where T : UnityEngine.Object
        {
            var mainAssetPath = location.AssetInfo.path;
            
            if (string.IsNullOrEmpty(mainAssetPath))
                mainAssetPath = location.AssetInfo.address;
            
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(mainAssetPath);
            
            var handles = new List<AssetHandle<T>>();
            foreach (var asset in allAssets)
            {
                if (asset is T typedAsset)
                {
                    var handle = AssetHandle<T>.Success(typedAsset, location.AssetInfo);
                    handle.Retain();
                    handles.Add(handle);
                }
            }
            
            Game.Debug.Debug($"[EditorAssetProvider] Loaded {handles.Count} assets from {mainAssetPath}");
            await UniTask.Yield();
            return handles;
        }
    }
}
#endif
