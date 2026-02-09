using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Log;
using UnityEngine;

namespace GameDeveloperKit.Data
{
    /// <summary>
    /// 数据模块
    /// </summary>
    public sealed class DataModule : IModule, IDataManager
    {
        private readonly DataContainer _container = new DataContainer();
        private string _persistentFilePath;
        private bool _autoSave = true;
        private float _autoSaveInterval = 60f;
        private float _autoSaveTimer = 0f;

        public void OnStartup()
        {
            _persistentFilePath = Path.Combine(Application.persistentDataPath, "gamedata.json");

            // 注册数据调试面板
            DebugConsole.Instance?.RegisterPanel(new DataDebugPanel());

            LoadPersistentData().SafeForget("DataModule.LoadPersistentData");

            Game.Debug.Debug("DataModule started");
        }

        public void OnUpdate(float elapseSeconds)
        {
            if (!_autoSave) return;

            _autoSaveTimer += elapseSeconds;
            if (_autoSaveTimer >= _autoSaveInterval)
            {
                _autoSaveTimer = 0f;
                SaveDirtyDataAsync().SafeForget("DataModule.SaveDirtyData");
            }
        }

        public void OnClearup()
        {
            SaveDirtyDataAsync().GetAwaiter().GetResult();

            _container.ClearRuntime();
            Game.Debug.Debug("DataModule cleared");
        }

        public void SetData<T>(string key, T value)
        {
            ValidateKey(key);
            _container.SetRuntime(key, value);
        }

        public void Save<T>(string key, T value)
        {
            ValidateKey(key);
            _container.SetPersistent(key, value);
            SaveDirtyDataAsync().SafeForget("DataModule.SaveDirtyData");
        }

        public async UniTask SaveAsync<T>(string key, T value)
        {
            ValidateKey(key);
            _container.SetPersistent(key, value);
            await SaveDirtyDataAsync();
        }

        public T Get<T>(string key, T defaultValue = default)
        {
            ValidateKey(key);
            return _container.TryGet<T>(key, out var value) ? value : defaultValue;
        }

        public bool TryGet<T>(string key, out T value)
        {
            ValidateKey(key);
            return _container.TryGet(key, out value);
        }

        public UniTask<T> GetAsync<T>(string key, T defaultValue = default)
        {
            ValidateKey(key);
            return UniTask.FromResult(Get(key, defaultValue));
        }

        public bool Has(string key)
        {
            ValidateKey(key);
            return _container.Has(key);
        }

        public bool IsPersistent(string key)
        {
            ValidateKey(key);
            return _container.IsPersistent(key);
        }

        public void Delete(string key)
        {
            ValidateKey(key);
            _container.Delete(key);
            SaveDirtyDataAsync().SafeForget("DataModule.SaveDirtyData");
        }

        public void Clear()
        {
            _container.ClearRuntime();
        }

        public void ClearPersistent()
        {
            _container.ClearPersistent();
            SaveAllDataAsync().SafeForget("DataModule.SaveAllData");
        }

        public void ClearAll()
        {
            _container.ClearRuntime();
            _container.ClearPersistent();
            SaveAllDataAsync().SafeForget("DataModule.SaveAllData");
        }

        public async UniTask SaveBatchAsync(Dictionary<string, object> data)
        {
            foreach (var kvp in data)
            {
                _container.SetPersistent(kvp.Key, kvp.Value);
            }
            await SaveDirtyDataAsync();
        }

        public string[] GetAllKeys()
        {
            return _container.GetAllKeys();
        }

        public string[] GetAllPersistentKeys()
        {
            return _container.GetPersistentKeys();
        }

        private async UniTask LoadPersistentData()
        {
            try
            {
                var serializedData = await DataSerializer.LoadFromFileAsync(_persistentFilePath);
                
                foreach (var kvp in serializedData)
                {
                    _container.SetPersistent(kvp.Key, kvp.Value);
                }

                Game.Debug.Debug($"Loaded {serializedData.Count} persistent data entries");
            }
            catch (Exception ex)
            {
                Game.Debug.Error($"Failed to load persistent data: {ex.Message}");
            }
        }

        private async UniTask SaveDirtyDataAsync()
        {
            var dirtyData = _container.GetDirtyData();
            if (dirtyData.Count == 0) return;

            try
            {
                var existingData = await DataSerializer.LoadFromFileAsync(_persistentFilePath);

                foreach (var kvp in dirtyData)
                {
                    var json = DataSerializer.SerializeToJson(kvp.Value);
                    existingData[kvp.Key] = json;
                }

                await DataSerializer.SaveToFileAsync(_persistentFilePath, existingData);
            }
            catch (Exception ex)
            {
                Game.Debug.Error($"Failed to save dirty data: {ex.Message}");
            }
        }

        private async UniTask SaveAllDataAsync()
        {
            try
            {
                var allData = new Dictionary<string, string>();
                var persistentKeys = _container.GetPersistentKeys();

                foreach (var key in persistentKeys)
                {
                    if (_container.TryGet<object>(key, out var value))
                    {
                        var json = DataSerializer.SerializeToJson(value);
                        allData[key] = json;
                    }
                }

                await DataSerializer.SaveToFileAsync(_persistentFilePath, allData);
            }
            catch (Exception ex)
            {
                Game.Debug.Error($"Failed to save all data: {ex.Message}");
            }
        }

        private void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            }
        }
    }
}
