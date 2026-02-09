using System;

namespace GameDeveloperKit.Config
{
    /// <summary>
    /// 配置表接口（配置集合容器）
    /// </summary>
    /// <typeparam name="T">配置项类型</typeparam>
    public interface IConfig<T> where T : IConfigData
    {
        /// <summary>
        /// 所有配置项数组
        /// </summary>
        T[] Datas { get; }

        /// <summary>
        /// 配置项数量
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 根据ID获取配置项
        /// </summary>
        T GetById(string id);

        /// <summary>
        /// 尝试根据ID获取配置项
        /// </summary>
        bool TryGetById(string id, out T data);

        /// <summary>
        /// 检查是否包含指定ID的配置项
        /// </summary>
        bool ContainsId(string id);

        /// <summary>
        /// 根据条件过滤配置项
        /// </summary>
        T[] Where(Func<T, bool> predicate);

        /// <summary>
        /// 查找第一个满足条件的配置项
        /// </summary>
        T FirstOrDefault(Func<T, bool> predicate);
    }
}
