using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using SceneHandle = GameDeveloperKit.Resource.SceneHandle;

namespace GameDeveloperKit.World
{
    /// <summary>
    /// World场景管理器
    /// 管理场景加载、卸载、导航和回调通知
    /// </summary>
    public sealed class WorldSceneManager
    {
        private readonly GameWorld _world;

        private readonly Stack<string> _sceneHistory;
        private readonly HashSet<string> _loadedScenes;
        private readonly Dictionary<string, SceneHandle> _sceneHandles;


        public WorldSceneManager(GameWorld world)
        {
            _world = world;
            _sceneHistory = new Stack<string>();
            _loadedScenes = new HashSet<string>();
            _sceneHandles = new Dictionary<string, SceneHandle>();
        }

        #region Scene Loading

        /// <summary>
        /// 加载场景（异步）
        /// </summary>
        public async UniTask<SceneHandle> AddScene(string sceneName, LoadSceneMode mode, Action<float> onProgress = null, Action onComplete = null)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Game.Debug.Error("[WorldSceneManager] Scene name cannot be null or empty");
                onComplete?.Invoke();
                return default;
            }

            if (_loadedScenes.Contains(sceneName))
            {
                Game.Debug.Warning($"[WorldSceneManager] Scene '{sceneName}' is already loaded");
                onComplete?.Invoke();
                return default;
            }

            // 使用Game.Resource.LoadSceneAsync加载场景
            SceneHandle handle = null;
            try
            {
                // 包装进度回调，同时通知外部和System
                Action<float> progressCallback = (progress) =>
                {
                    onProgress?.Invoke(progress);
                };

                handle = await Game.Resource.LoadSceneAsync(sceneName, mode, progressCallback);
            }
            catch (Exception ex)
            {
                Game.Debug.Error($"[WorldSceneManager] Failed to load scene '{sceneName}': {ex.Message}");
                onComplete?.Invoke();
                return default;
            }

            if (handle == null || handle.Scene.isLoaded is false)
            {
                Game.Debug.Error($"[WorldSceneManager] Scene '{sceneName}' loaded but handle is null");
                onComplete?.Invoke();
                return default;
            }

            // 记录场景
            _sceneHandles[sceneName] = handle;
            _loadedScenes.Add(sceneName);
            _sceneHistory.Push(sceneName);
            onComplete?.Invoke();
            return handle;
        }

        #endregion

        #region Scene Unloading

        /// <summary>
        /// 卸载场景（异步）
        /// </summary>
        public void RemoveScene(string sceneName, Action onComplete = null)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Game.Debug.Error("[WorldSceneManager] Scene name cannot be null or empty");
                onComplete?.Invoke();
                return;
            }

            if (!_loadedScenes.Contains(sceneName))
            {
                Game.Debug.Warning($"[WorldSceneManager] Scene '{sceneName}' is not loaded");
                onComplete?.Invoke();
                return;
            }

            // 获取SceneHandle
            if (!_sceneHandles.TryGetValue(sceneName, out var handle))
            {
                Game.Debug.Warning($"[WorldSceneManager] Scene '{sceneName}' handle not found");
                onComplete?.Invoke();
                return;
            }

            // 清理记录
            _loadedScenes.Remove(sceneName);
            _sceneHandles.Remove(sceneName);

            // 从历史栈中移除（如果存在）
            var tempStack = new Stack<string>();
            while (_sceneHistory.Count > 0)
            {
                var name = _sceneHistory.Pop();
                if (name != sceneName)
                {
                    tempStack.Push(name);
                }
            }
            while (tempStack.Count > 0)
            {
                _sceneHistory.Push(tempStack.Pop());
            }

            onComplete?.Invoke();
        }

        #endregion

        #region Navigation

        /// <summary>
        /// 返回上一个场景
        /// </summary>
        public void Back(Action<float> onProgress = null)
        {
            if (_sceneHistory.Count < 2)
            {
                Game.Debug.Warning("[WorldSceneManager] No previous scene to go back to");
                return;
            }

            // 弹出当前场景
            var current = _sceneHistory.Pop();

            // 获取上一个场景
            var previous = _sceneHistory.Peek();

            // 卸载当前场景，加载上一个场景
            RemoveScene(current, () => { AddScene(previous, LoadSceneMode.Additive, onProgress).Forget(); });
        }

        /// <summary>
        /// 返回到指定场景
        /// </summary>
        public void BackTo(string sceneName, Action<float> onProgress = null)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Game.Debug.Error("[WorldSceneManager] Scene name cannot be null or empty");
                return;
            }

            // 查找目标场景在历史栈中的位置
            var tempStack = new Stack<string>();
            bool found = false;

            while (_sceneHistory.Count > 0)
            {
                var name = _sceneHistory.Pop();
                tempStack.Push(name);

                if (name == sceneName)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Game.Debug.Warning($"[WorldSceneManager] Scene '{sceneName}' not found in history");
                // 恢复历史栈
                while (tempStack.Count > 0)
                {
                    _sceneHistory.Push(tempStack.Pop());
                }
                return;
            }

            // 重建历史栈（只保留到目标场景）
            _sceneHistory.Push(sceneName);

            // 卸载所有中间场景
            var scenesToUnload = new List<string>();
            while (tempStack.Count > 0)
            {
                var name = tempStack.Pop();
                if (name != sceneName && _loadedScenes.Contains(name))
                {
                    scenesToUnload.Add(name);
                }
            }

            // 卸载中间场景
            foreach (var name in scenesToUnload)
            {
                RemoveScene(name);
            }

            // 如果目标场景未加载，则加载它
            if (!_loadedScenes.Contains(sceneName))
            {
                AddScene(sceneName, LoadSceneMode.Additive, onProgress).Forget();
            }
        }

        #endregion

        #region Query

        /// <summary>
        /// 检查场景是否已加载
        /// </summary>
        public bool HasScene(string sceneName)
        {
            return _loadedScenes.Contains(sceneName);
        }

        /// <summary>
        /// 获取当前场景（栈顶）
        /// </summary>
        public string GetCurrentScene()
        {
            return _sceneHistory.Count > 0 ? _sceneHistory.Peek() : null;
        }

        /// <summary>
        /// 获取场景历史
        /// </summary>
        public IReadOnlyList<string> GetSceneHistory()
        {
            return _sceneHistory.ToArray();
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// 清理所有场景
        /// </summary>
        public void Clear()
        {
            _sceneHistory.Clear();
            _loadedScenes.Clear();
            _sceneHandles.Clear();
        }

        #endregion
    }
}