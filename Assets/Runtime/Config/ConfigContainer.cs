using System;
using System.Collections.Generic;
using UnityEngine;
using ZLinq;

namespace GameDeveloperKit.Config
{
    /// <summary>
    /// 配置容器实现
    /// </summary>
    public class ConfigContainer<T> : IConfig<T> where T : IConfigData
    {
        private readonly T[] _datas;
        private readonly Dictionary<string, T> _dataDict;

        public T[] Datas => _datas;
        public int Count => _datas.Length;

        public ConfigContainer(T[] datas)
        {
            _datas = datas ?? Array.Empty<T>();
            _dataDict = new Dictionary<string, T>(_datas.Length);

            foreach (var data in _datas)
            {
                if (data == null) continue;
                
                var id = data.Id;
                if (string.IsNullOrEmpty(id))
                {
                    Game.Debug.Warning($"[Config] Empty Id in {typeof(T).Name}");
                    continue;
                }
                
                if (_dataDict.ContainsKey(id))
                {
                    Game.Debug.Warning($"[Config] Duplicate Id: {id} in {typeof(T).Name}");
                    continue;
                }
                _dataDict[id] = data;
            }
        }

        public T GetById(string id)
        {
            if (!_dataDict.TryGetValue(id, out var data))
            {
                throw new KeyNotFoundException($"[Config] Id '{id}' not found in {typeof(T).Name}");
            }
            return data;
        }

        public bool TryGetById(string id, out T data) => _dataDict.TryGetValue(id, out data);

        public bool ContainsId(string id) => _dataDict.ContainsKey(id);

        public T[] Where(Func<T, bool> predicate)
        {
            return _datas.AsValueEnumerable().Where(predicate).ToArray();
        }

        public T FirstOrDefault(Func<T, bool> predicate)
        {
            return _datas.AsValueEnumerable().FirstOrDefault(predicate);
        }
    }
}
