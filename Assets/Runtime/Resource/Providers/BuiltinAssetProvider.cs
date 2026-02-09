using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 内置资源提供者
    /// 从 Resources 文件夹加载资源
    /// </summary>
    public class BuiltinAssetProvider : IAssetProvider
    {
        private readonly Dictionary<string, BaseHandle> _cachedAssets = new Dictionary<string, BaseHandle>();
        private readonly Dictionary<string, UniTaskCompletionSource<BaseHandle>> _loadingAssets 
            = new Dictionary<string, UniTaskCompletionSource<BaseHandle>>();
        
        /// <summary>
        /// 异步加载资源
        /// </summary>
        public async UniTask<AssetHandle<T>> LoadAsync<T>(ResourceLocation location) where T : UnityEngine.Object
        {
            if (location == null)
            {
                Game.Debug.Error("Invalid resource location");
                return default;
            }
            
            var address = location.AssetInfo.address;
            var normalizedPath = NormalizePath(address);
            
            // 1. 检查缓存
            if (_cachedAssets.TryGetValue(normalizedPath, out var cachedHandle))
            {
                cachedHandle.Retain();
                return (AssetHandle<T>)cachedHandle;
            }
            
            // 2. 等待正在加载的资源
            if (_loadingAssets.TryGetValue(normalizedPath, out var loadingTask))
            {
                var handle = await loadingTask.Task;
                if (handle != null)
                {
                    handle.Retain();
                    return (AssetHandle<T>)handle;
                }
            }
            
            // 3. 开始加载
            var completionSource = new UniTaskCompletionSource<BaseHandle>();
            _loadingAssets[normalizedPath] = completionSource;
            
            try
            {
                // 4. 使用 Resources.LoadAsync 加载资源
                var resourceRequest = Resources.LoadAsync<T>(normalizedPath);
                await resourceRequest;
                
                var asset = resourceRequest.asset as T;
                if (asset == null)
                {
                    Game.Debug.Error($"Failed to load asset from Resources: {normalizedPath}");
                    completionSource.TrySetResult(null);
                    return default;
                }
                
                // 5. 创建句柄
                var assetInfo = new AssetInfo
                {
                    name = Path.GetFileNameWithoutExtension(normalizedPath),
                    address = address,
                    guid = normalizedPath
                };
                
                var newHandle = AssetHandle<T>.Success(asset, assetInfo);
                newHandle.Retain();
                
                // 6. 缓存
                _cachedAssets[normalizedPath] = newHandle;
                completionSource.TrySetResult(newHandle);
                
                return newHandle;
            }
            catch (System.Exception ex)
            {
                Game.Debug.Error($"Load builtin asset exception '{address}': {ex.Message}");
                completionSource.TrySetException(ex);
                return default;
            }
            finally
            {
                _loadingAssets.Remove(normalizedPath);
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
                var normalizedPath = NormalizePath(handle.Address);
                _cachedAssets.Remove(normalizedPath);
                
                // 卸载 Resources 资源
                if (handle is AssetHandle<UnityEngine.Object> resourceHandle)
                {
                    if (resourceHandle.Asset != null && !(resourceHandle.Asset is GameObject))
                    {
                        Resources.UnloadAsset(resourceHandle.Asset);
                    }
                }
                
                handle.OnClearup();
            }
        }
        
        /// <summary>
        /// 规范化资源路径
        /// 移除 "Resources/" 前缀和文件扩展名
        /// </summary>
        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            
            // 移除 "Resources/" 前缀
            if (path.StartsWith("Resources/", System.StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring("Resources/".Length);
            }
            
            // 移除文件扩展名
            var extension = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(extension))
            {
                path = path.Substring(0, path.Length - extension.Length);
            }
            
            return path;
        }
        
        /// <summary>
        /// 异步加载子资源（Resources 不支持子资源，返回主资源）
        /// </summary>
        public async UniTask<AssetHandle<T>> LoadSubAssetAsync<T>(ResourceLocation location, string subAssetName) 
            where T : UnityEngine.Object
        {
            Game.Debug.Warning("[BuiltinAssetProvider] Resources folder does not support sub-asset loading, returning main asset");
            return await LoadAsync<T>(location);
        }
        
        /// <summary>
        /// 异步加载所有资源（Resources 不支持，仅返回主资源）
        /// </summary>
        public async UniTask<List<AssetHandle<T>>> LoadAllAsync<T>(ResourceLocation location) 
            where T : UnityEngine.Object
        {
            Game.Debug.Warning("[BuiltinAssetProvider] Resources folder does not support loading all assets, returning main asset only");
            var handle = await LoadAsync<T>(location);
            var result = new List<AssetHandle<T>>();
            if (handle != null)
            {
                result.Add(handle);
            }
            return result;
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
            
            Resources.UnloadUnusedAssets();
        }
    }
}
