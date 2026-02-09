using System.Collections.Generic;
using ZLinq;

namespace GameDeveloperKit.Data
{
    /// <summary>
    /// 泛型数据容器，避免装箱拆箱
    /// </summary>
    internal class TypedDataContainer<T> : IDataContainerBase
    {
        private readonly Dictionary<string, T> _data = new Dictionary<string, T>();

        public void Set(string key, T value) => _data[key] = value;

        public bool TryGet(string key, out T value) => _data.TryGetValue(key, out value);

        public bool Has(string key) => _data.ContainsKey(key);

        public void Remove(string key) => _data.Remove(key);

        public void Clear() => _data.Clear();

        public string[] GetKeys() => _data.Keys.AsValueEnumerable().ToArray();
    }
}
