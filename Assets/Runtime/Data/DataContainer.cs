using System;
using System.Collections.Generic;
using ZLinq;

namespace GameDeveloperKit.Data
{
    /// <summary>
    /// 数据容器（内存管理），使用泛型避免装箱拆箱
    /// 所有操作在主线程执行（UniTask模型），不需要lock
    /// </summary>
    internal class DataContainer
    {
        // 按类型存储，避免装箱
        private readonly Dictionary<Type, object> _runtimeContainers = new Dictionary<Type, object>();
        private readonly Dictionary<Type, object> _persistentContainers = new Dictionary<Type, object>();
        private readonly HashSet<string> _dirtyKeys = new HashSet<string>();
        private readonly Dictionary<string, (Type type, bool isPersistent)> _keyIndex = new Dictionary<string, (Type, bool)>();

        public void SetRuntime<T>(string key, T value)
        {
            var container = GetOrCreateContainer<T>(_runtimeContainers);
            container.Set(key, value);
            _keyIndex[key] = (typeof(T), false);
        }

        public void SetPersistent<T>(string key, T value)
        {
            var container = GetOrCreateContainer<T>(_persistentContainers);
            container.Set(key, value);
            _keyIndex[key] = (typeof(T), true);
            _dirtyKeys.Add(key);
        }

        public bool TryGet<T>(string key, out T value)
        {
            // 优先运行时
            if (_runtimeContainers.TryGetValue(typeof(T), out var runtimeObj))
            {
                var runtimeContainer = (TypedDataContainer<T>)runtimeObj;
                if (runtimeContainer.TryGet(key, out value))
                    return true;
            }

            // 其次持久化
            if (_persistentContainers.TryGetValue(typeof(T), out var persistObj))
            {
                var persistContainer = (TypedDataContainer<T>)persistObj;
                return persistContainer.TryGet(key, out value);
            }

            value = default;
            return false;
        }

        public bool Has(string key)
        {
            return _keyIndex.ContainsKey(key);
        }

        public bool IsPersistent(string key)
        {
            return _keyIndex.TryGetValue(key, out var info) && info.isPersistent;
        }

        public void Delete(string key)
        {
            if (!_keyIndex.TryGetValue(key, out var info))
                return;
            
            var containers = info.isPersistent ? _persistentContainers : _runtimeContainers;
            
            if (containers.TryGetValue(info.type, out var containerObj))
            {
                var container = containerObj as IDataContainerBase;
                container?.Remove(key);
            }
            
            _keyIndex.Remove(key);
            if (info.isPersistent)
                _dirtyKeys.Add(key);
        }

        public void ClearRuntime()
        {
            var keysToRemove = new List<string>(_keyIndex.Count);
            foreach (var kvp in _keyIndex.AsValueEnumerable())
            {
                if (!kvp.Value.isPersistent)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToRemove.AsValueEnumerable())
            {
                _keyIndex.Remove(key);
            }
            
            _runtimeContainers.Clear();
        }

        public void ClearPersistent()
        {
            var keysToRemove = new List<string>(_keyIndex.Count);
            foreach (var kvp in _keyIndex.AsValueEnumerable())
            {
                if (kvp.Value.isPersistent)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToRemove.AsValueEnumerable())
            {
                _keyIndex.Remove(key);
            }
            
            _persistentContainers.Clear();
            _dirtyKeys.Clear();
        }

        public string[] GetAllKeys()
        {
            return _keyIndex.Keys.AsValueEnumerable().ToArray();
        }

        public string[] GetRuntimeKeys()
        {
            var keys = new List<string>(_keyIndex.Count);
            foreach (var kvp in _keyIndex.AsValueEnumerable())
            {
                if (!kvp.Value.isPersistent)
                {
                    keys.Add(kvp.Key);
                }
            }
            return keys.ToArray();
        }

        public string[] GetPersistentKeys()
        {
            var keys = new List<string>(_keyIndex.Count);
            foreach (var kvp in _keyIndex.AsValueEnumerable())
            {
                if (kvp.Value.isPersistent)
                {
                    keys.Add(kvp.Key);
                }
            }
            return keys.ToArray();
        }

        public Dictionary<string, object> GetDirtyData()
        {
            var dirtyData = new Dictionary<string, object>();

            foreach (var key in _dirtyKeys)
            {
                // 尝试从所有持久化容器获取
                foreach (var kvp in _persistentContainers)
                {
                    var method = kvp.Value.GetType().GetMethod("TryGet");
                    var parameters = new object[] { key, null };
                    var found = (bool)method.Invoke(kvp.Value, parameters);

                    if (found)
                    {
                        dirtyData[key] = parameters[1];
                        break;
                    }
                }
            }

            _dirtyKeys.Clear();
            return dirtyData;
        }

        private TypedDataContainer<T> GetOrCreateContainer<T>(Dictionary<Type, object> containers)
        {
            var type = typeof(T);
            if (!containers.TryGetValue(type, out var containerObj))
            {
                containerObj = new TypedDataContainer<T>();
                containers[type] = containerObj;
            }
            return (TypedDataContainer<T>)containerObj;
        }
    }
}
