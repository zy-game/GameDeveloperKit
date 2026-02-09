using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Log;
using UnityEngine;

namespace GameDeveloperKit.Config
{
    /// <summary>
    /// 配置模块
    /// </summary>
    public sealed class ConfigModule : IModule, IConfigManager
    {
        private readonly Dictionary<Type, object> _configs = new();

        public void OnStartup()
        {
            Log.DebugConsole.Instance?.RegisterPanel(new ConfigDebugPanel());
            Game.Debug.Debug("ConfigModule started");
        }

        public void OnUpdate(float elapseSeconds)
        {
        }

        public void OnClearup()
        {
            _configs.Clear();
            Game.Debug.Debug("ConfigModule cleared");
        }

        /// <summary>
        /// 异步加载配置表（从 JSON）
        /// </summary>
        public async UniTask<IConfig<T>> LoadConfigAsync<T>(string address) where T : IConfigData
        {
            var type = typeof(T);

            if (_configs.TryGetValue(type, out var cached))
            {
                Game.Debug.Warning($"[Config] {type.Name} is already loaded");
                return cached as IConfig<T>;
            }

            var datas = await ConfigLoader.LoadFromJsonAsync<T>(address);

            if (datas == null || datas.Length == 0)
            {
                Game.Debug.Error($"[Config] Failed to load: {address}");
                return null;
            }

            var config = new ConfigContainer<T>(datas);
            _configs[type] = config;

            Game.Debug.Debug($"[Config] Loaded: {type.Name} ({datas.Length} items) from {address}");
            return config;
        }

        /// <summary>
        /// 异步加载配置表（从 ScriptableObject，通过类型批量加载）
        /// </summary>
        public async UniTask<IConfig<T>> LoadConfigByTypeAsync<T>() where T : ScriptableObject, IConfigData
        {
            var type = typeof(T);

            if (_configs.TryGetValue(type, out var cached))
            {
                Game.Debug.Warning($"[Config] {type.Name} is already loaded");
                return cached as IConfig<T>;
            }

            var handles = await Game.Resource.LoadAssetsByTypeAsync<T>();
            var datas = new List<T>();

            foreach (var handle in handles)
            {
                if (handle.Asset != null)
                {
                    datas.Add(handle.Asset);
                }
            }

            if (datas.Count == 0)
            {
                Game.Debug.Warning($"[Config] No {type.Name} found");
                return null;
            }

            var config = new ConfigContainer<T>(datas.ToArray());
            _configs[type] = config;

            Game.Debug.Debug($"[Config] Loaded: {type.Name} ({datas.Count} items)");
            return config;
        }

        public IConfig<T> GetConfig<T>() where T : IConfigData
        {
            return _configs.TryGetValue(typeof(T), out var config) ? config as IConfig<T> : null;
        }

        public T Find<T>(string id) where T : IConfigData
        {
            var config = GetConfig<T>();
            if (config == null)
            {
                throw new InvalidOperationException(
                    $"[Config] {typeof(T).Name} is not loaded. Call LoadConfigAsync/LoadSOConfigAsync first.");
            }

            return config.GetById(id);
        }

        public bool TryFind<T>(string id, out T data) where T : IConfigData
        {
            data = default;

            var config = GetConfig<T>();
            if (config == null)
            {
                return false;
            }

            return config.TryGetById(id, out data);
        }

        public bool IsConfigLoaded<T>() where T : IConfigData
        {
            return _configs.ContainsKey(typeof(T));
        }

        public void UnloadConfig<T>() where T : IConfigData
        {
            if (_configs.Remove(typeof(T)))
            {
                Game.Debug.Debug($"[Config] Unloaded: {typeof(T).Name}");
            }
        }

        public void UnloadAllConfigs()
        {
            _configs.Clear();
            Game.Debug.Debug("[Config] All configs unloaded");
        }

        public T[] Where<T>(Func<T, bool> predicate) where T : IConfigData
        {
            var config = GetConfig<T>();
            if (config == null)
            {
                throw new InvalidOperationException(
                    $"[Config] {typeof(T).Name} is not loaded.");
            }

            return config.Where(predicate);
        }

        public T FirstOrDefault<T>(Func<T, bool> predicate) where T : IConfigData
        {
            var config = GetConfig<T>();
            if (config == null)
            {
                throw new InvalidOperationException(
                    $"[Config] {typeof(T).Name} is not loaded.");
            }

            return config.FirstOrDefault(predicate);
        }

        public int Count<T>() where T : IConfigData
        {
            var config = GetConfig<T>();
            if (config == null)
            {
                throw new InvalidOperationException(
                    $"[Config] {typeof(T).Name} is not loaded.");
            }

            return config.Count;
        }

        public int Count<T>(Func<T, bool> predicate) where T : IConfigData
        {
            return Where(predicate).Length;
        }

        public T[] All<T>() where T : IConfigData
        {
            var config = GetConfig<T>();
            if (config == null)
            {
                throw new InvalidOperationException(
                    $"[Config] {typeof(T).Name} is not loaded.");
            }

            return config.Datas;
        }

        public bool ContainsId<T>(string id) where T : IConfigData
        {
            var config = GetConfig<T>();
            if (config == null)
            {
                throw new InvalidOperationException(
                    $"[Config] {typeof(T).Name} is not loaded.");
            }

            return config.ContainsId(id);
        }
    }
}
