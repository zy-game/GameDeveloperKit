using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 远程资源提供者
    /// 从网络加载资源（HTTP/HTTPS）
    /// </summary>
    public class RemoteAssetProvider : IAssetProvider
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
            
            var url = location.AssetInfo.address;
            
            // 1. 检查缓存
            if (_cachedAssets.TryGetValue(url, out var cachedHandle))
            {
                cachedHandle.Retain();
                return (AssetHandle<T>)cachedHandle;
            }
            
            // 2. 等待正在加载的资源
            if (_loadingAssets.TryGetValue(url, out var loadingTask))
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
            _loadingAssets[url] = completionSource;
            
            try
            {
                BaseHandle newHandle = null;
                
                // 根据类型加载不同的资源
                if (typeof(T) == typeof(Texture2D))
                {
                    newHandle = await LoadTextureAsync(url);
                }
                else if (typeof(T) == typeof(Sprite))
                {
                    newHandle = await LoadSpriteAsync(url);
                }
                else if (typeof(T) == typeof(AudioClip))
                {
                    newHandle = await LoadAudioClipAsync(url);
                }
                else if (typeof(T) == typeof(TextAsset))
                {
                    newHandle = await LoadTextAssetAsync(url);
                }
                else
                {
                    Game.Debug.Error($"Unsupported type for remote loading: {typeof(T).Name}");
                    completionSource.TrySetResult(null);
                    return default;
                }
                
                if (newHandle == null)
                {
                    Game.Debug.Error($"Failed to load remote asset: {url}");
                    completionSource.TrySetResult(null);
                    return default;
                }
                
                newHandle.Retain();
                
                // 4. 缓存
                _cachedAssets[url] = newHandle;
                completionSource.TrySetResult(newHandle);
                
                return (AssetHandle<T>)newHandle;
            }
            catch (System.Exception ex)
            {
                Game.Debug.Error($"Load remote asset exception '{url}': {ex.Message}");
                completionSource.TrySetException(ex);
                return default;
            }
            finally
            {
                _loadingAssets.Remove(url);
            }
        }
        
        /// <summary>
        /// 使用 UnityWebRequestTexture 加载纹理
        /// </summary>
        private async UniTask<BaseHandle> LoadTextureAsync(string url)
        {
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                await request.SendWebRequest();
                
#if UNITY_2020_2_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isHttpError || request.isNetworkError)
#endif
                {
                    Game.Debug.Error($"Failed to load texture from {url}: {request.error}");
                    return null;
                }
                
                var texture = DownloadHandlerTexture.GetContent(request);
                if (texture == null)
                {
                    Game.Debug.Error($"Failed to get texture content from {url}");
                    return null;
                }
                
                var assetInfo = new AssetInfo { name = url, address = url, guid = url };
                return AssetHandle<Texture2D>.Success(texture, assetInfo);
            }
        }
        
        /// <summary>
        /// 加载 Sprite（先加载 Texture2D 再转换）
        /// </summary>
        private async UniTask<BaseHandle> LoadSpriteAsync(string url)
        {
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                await request.SendWebRequest();
                
#if UNITY_2020_2_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isHttpError || request.isNetworkError)
#endif
                {
                    Game.Debug.Error($"Failed to load texture for sprite from {url}: {request.error}");
                    return null;
                }
                
                var texture = DownloadHandlerTexture.GetContent(request);
                if (texture == null)
                {
                    Game.Debug.Error($"Failed to get texture content from {url}");
                    return null;
                }
                
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
                var assetInfo = new AssetInfo { name = url, address = url, guid = url };
                return AssetHandle<Sprite>.Success(sprite, assetInfo);
            }
        }
        
        /// <summary>
        /// 使用 UnityWebRequestMultimedia 加载音频
        /// </summary>
        private async UniTask<BaseHandle> LoadAudioClipAsync(string url)
        {
            // 根据 URL 后缀判断音频类型
            AudioType audioType = GetAudioType(url);
            
            using (var request = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
            {
                await request.SendWebRequest();
                
#if UNITY_2020_2_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isHttpError || request.isNetworkError)
#endif
                {
                    Game.Debug.Error($"Failed to load audio from {url}: {request.error}");
                    return null;
                }
                
                var audio = DownloadHandlerAudioClip.GetContent(request);
                if (audio == null)
                {
                    Game.Debug.Error($"Failed to get audio content from {url}");
                    return null;
                }
                
                var assetInfo = new AssetInfo { name = url, address = url, guid = url };
                return AssetHandle<AudioClip>.Success(audio, assetInfo);
            }
        }
        
        /// <summary>
        /// 使用 UnityWebRequest 加载文本
        /// </summary>
        private async UniTask<BaseHandle> LoadTextAssetAsync(string url)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                await request.SendWebRequest();
                
#if UNITY_2020_2_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isHttpError || request.isNetworkError)
#endif
                {
                    Game.Debug.Error($"Failed to load text from {url}: {request.error}");
                    return null;
                }
                
                var text = request.downloadHandler.text;
                if (string.IsNullOrEmpty(text))
                {
                    Game.Debug.Warning($"Text content is empty from {url}");
                }
                
                var textAsset = new TextAsset(text);
                var assetInfo = new AssetInfo { name = url, address = url, guid = url };
                return AssetHandle<TextAsset>.Success(textAsset, assetInfo);
            }
        }
        
        /// <summary>
        /// 根据 URL 后缀判断音频类型
        /// </summary>
        private AudioType GetAudioType(string url)
        {
            var extension = System.IO.Path.GetExtension(url).ToLower();
            return extension switch
            {
                ".mp3" => AudioType.MPEG,
                ".ogg" => AudioType.OGGVORBIS,
                ".wav" => AudioType.WAV,
                ".aiff" => AudioType.AIFF,
                ".mod" => AudioType.MOD,
                ".it" => AudioType.IT,
                ".s3m" => AudioType.S3M,
                ".xm" => AudioType.XM,
                _ => AudioType.UNKNOWN
            };
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
                _cachedAssets.Remove(handle.Address);
                handle.OnClearup();
            }
        }
        
        /// <summary>
        /// 异步加载子资源（远程资源不支持子资源，返回主资源）
        /// </summary>
        public async UniTask<AssetHandle<T>> LoadSubAssetAsync<T>(ResourceLocation location, string subAssetName) 
            where T : UnityEngine.Object
        {
            Game.Debug.Warning("[RemoteAssetProvider] Remote resources do not support sub-asset loading, returning main asset");
            return await LoadAsync<T>(location);
        }
        
        /// <summary>
        /// 异步加载所有资源（远程资源不支持，仅返回主资源）
        /// </summary>
        public async UniTask<List<AssetHandle<T>>> LoadAllAsync<T>(ResourceLocation location) 
            where T : UnityEngine.Object
        {
            Game.Debug.Warning("[RemoteAssetProvider] Remote resources do not support loading all assets, returning main asset only");
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
        }
    }
}
