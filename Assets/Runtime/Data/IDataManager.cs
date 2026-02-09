using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Data
{
    /// <summary>
    /// 数据管理器接口
    /// </summary>
    public interface IDataManager : IModule
    {
        /// <summary>
        /// 设置运行时数据（不持久化）
        /// </summary>
        void SetData<T>(string key, T value);

        /// <summary>
        /// 保存持久化数据（立即写入磁盘）
        /// </summary>
        void Save<T>(string key, T value);

        /// <summary>
        /// 异步保存持久化数据
        /// </summary>
        UniTask SaveAsync<T>(string key, T value);

        /// <summary>
        /// 获取数据（优先从运行时数据获取，如果不存在则从持久化数据加载）
        /// </summary>
        T Get<T>(string key, T defaultValue = default);

        /// <summary>
        /// 尝试获取数据
        /// </summary>
        bool TryGet<T>(string key, out T value);

        /// <summary>
        /// 异步获取数据
        /// </summary>
        UniTask<T> GetAsync<T>(string key, T defaultValue = default);

        /// <summary>
        /// 检查数据是否存在（运行时或持久化）
        /// </summary>
        bool Has(string key);

        /// <summary>
        /// 检查是否为持久化数据
        /// </summary>
        bool IsPersistent(string key);

        /// <summary>
        /// 删除数据（运行时+持久化）
        /// </summary>
        void Delete(string key);

        /// <summary>
        /// 删除所有运行时数据
        /// </summary>
        void Clear();

        /// <summary>
        /// 删除所有持久化数据
        /// </summary>
        void ClearPersistent();

        /// <summary>
        /// 删除所有数据（运行时+持久化）
        /// </summary>
        void ClearAll();

        /// <summary>
        /// 批量保存（提高性能，减少IO）
        /// </summary>
        UniTask SaveBatchAsync(Dictionary<string, object> data);

        /// <summary>
        /// 获取所有数据的键
        /// </summary>
        string[] GetAllKeys();

        /// <summary>
        /// 获取所有持久化数据的键
        /// </summary>
        string[] GetAllPersistentKeys();
    }
}
