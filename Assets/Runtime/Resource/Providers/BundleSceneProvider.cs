using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// AssetBundle 场景提供者
    /// 从 AssetBundle 加载场景
    /// </summary>
    public class BundleSceneProvider : ISceneProvider
    {
        private readonly BundleLoaderService _bundleService;
        private readonly Dictionary<string, SceneHandle> _cachedScenes = new Dictionary<string, SceneHandle>();

        public BundleSceneProvider(BundleLoaderService bundleService)
        {
            _bundleService = bundleService;
        }

        /// <summary>
        /// 异步加载场景
        /// </summary>
        public async UniTask<SceneHandle> LoadSceneAsync(ResourceLocation location, LoadSceneMode mode, Action<float> progressHandler = null)
        {
            if (location == null || string.IsNullOrEmpty(location.BundleName))
            {
                Game.Debug.Error("Invalid scene location");
                return default;
            }

            var cacheKey = location.AssetInfo.guid;

            // 检查缓存
            if (_cachedScenes.TryGetValue(cacheKey, out var cachedHandle))
            {
                return cachedHandle;
            }

            try
            {
                // 1. 加载 Bundle（含依赖）
                var bundle = await _bundleService.LoadBundleAsync(location.BundleName);
                if (bundle == null)
                {
                    Game.Debug.Error($"Failed to load bundle for scene: {location.BundleName}");
                    return default;
                }

                // 2. 加载场景
                var sceneName = location.AssetPath;
                var loadOperation = SceneManager.LoadSceneAsync(sceneName, mode);
                if (loadOperation == null)
                {
                    Game.Debug.Error($"Failed to start loading scene: {sceneName}");
                    return default;
                }

                await loadOperation.ToUniTask(Progress.Create<float>(x => progressHandler?.Invoke(x)));

                if (!loadOperation.isDone)
                {
                    Game.Debug.Error($"Scene loading incomplete: {sceneName}");
                    return default;
                }

                // 3. 获取场景
                var scene = SceneManager.GetSceneByName(sceneName);
                if (!scene.IsValid())
                {
                    Game.Debug.Error($"Loaded scene is invalid: {sceneName}");
                    return default;
                }

                // 4. 创建句柄并缓存
                var handle = new SceneHandle(scene, sceneName);
                _cachedScenes[cacheKey] = handle;
                
                progressHandler?.Invoke(1f);
                Game.Debug.Debug($"Scene loaded: {sceneName}");
                return handle;
            }
            catch (System.Exception ex)
            {
                Game.Debug.Error($"Load scene exception '{location.AssetPath}': {ex.Message}");
                return default;
            }
        }
    }
}