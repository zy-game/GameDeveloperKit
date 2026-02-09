#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using Progress = Cysharp.Threading.Tasks.Progress;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 编辑器场景提供者
    /// 使用 AssetDatabase 直接加载场景
    /// </summary>
    public class EditorSceneProvider : ISceneProvider
    {
        private readonly Dictionary<string, SceneHandle> _cachedScenes = new Dictionary<string, SceneHandle>();

        public async UniTask<SceneHandle> LoadSceneAsync(ResourceLocation location, LoadSceneMode mode, Action<float> progressHandler = null)
        {
            var scenePath = location.AssetInfo.address;
            var cacheKey = location.AssetInfo.guid;

            // 检查缓存
            if (_cachedScenes.TryGetValue(cacheKey, out var cachedHandle))
            {
                return cachedHandle;
            }

            // 如果不是完整路径，尝试查找
            if (!scenePath.StartsWith("Assets/"))
            {
                var guids = AssetDatabase.FindAssets($"{location.AssetInfo.name} t:Scene");
                if (guids.Length > 0)
                {
                    scenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
                }
                else
                {
                    Game.Debug.Error($"[EditorSceneProvider] Scene not found: {location.AssetInfo.name}");
                    return default;
                }
            }

            Game.Debug.Debug($"[EditorSceneProvider] Loading scene: {scenePath}");

            var loadOperation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                scenePath,
                new LoadSceneParameters(mode)
            );

            await loadOperation.ToUniTask(Progress.Create<float>(x => progressHandler?.Invoke(x)));

            if (!loadOperation.isDone)
            {
                Game.Debug.Error($"[EditorSceneProvider] Scene loading incomplete: {scenePath}");
                return default;
            }

            var sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            var scene = SceneManager.GetSceneByName(sceneName);

            if (!scene.IsValid())
            {
                Game.Debug.Error($"[EditorSceneProvider] Loaded scene is invalid: {sceneName}");
                return default;
            }

            var handle = new SceneHandle(scene, sceneName);
            _cachedScenes[cacheKey] = handle;
            
            progressHandler?.Invoke(1f);
            Game.Debug.Debug($"[EditorSceneProvider] Scene loaded: {sceneName}");
            return handle;
        }
    }
}
#endif