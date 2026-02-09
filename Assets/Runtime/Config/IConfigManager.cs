using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Config
{
    /// <summary>
    /// 配置管理器接口
    /// </summary>
    public interface IConfigManager : IModule
    {
        /// <summary>
        /// 异步加载配置表（从 JSON）
        /// </summary>
        UniTask<IConfig<T>> LoadConfigAsync<T>(string address) where T : IConfigData;

        /// <summary>
        /// 异步加载配置表（从 ScriptableObject，通过类型批量加载）
        /// </summary>
        UniTask<IConfig<T>> LoadConfigByTypeAsync<T>() where T : UnityEngine.ScriptableObject, IConfigData;

        /// <summary>
        /// 获取已加载的配置表
        /// </summary>
        IConfig<T> GetConfig<T>() where T : IConfigData;

        /// <summary>
        /// 根据ID查找指定配置项（快捷方法）
        /// </summary>
        T Find<T>(string id) where T : IConfigData;

        /// <summary>
        /// 尝试根据ID查找指定配置项
        /// </summary>
        bool TryFind<T>(string id, out T data) where T : IConfigData;

        /// <summary>
        /// 检查配置表是否已加载
        /// </summary>
        bool IsConfigLoaded<T>() where T : IConfigData;

        /// <summary>
        /// 卸载配置表
        /// </summary>
        void UnloadConfig<T>() where T : IConfigData;

        /// <summary>
        /// 卸载所有配置表
        /// </summary>
        void UnloadAllConfigs();

        /// <summary>
        /// 根据条件过滤配置项
        /// </summary>
        T[] Where<T>(System.Func<T, bool> predicate) where T : IConfigData;

        /// <summary>
        /// 查找第一个满足条件的配置项
        /// </summary>
        T FirstOrDefault<T>(System.Func<T, bool> predicate) where T : IConfigData;

        /// <summary>
        /// 获取配置项数量
        /// </summary>
        int Count<T>() where T : IConfigData;

        /// <summary>
        /// 获取符合条件的配置项数量
        /// </summary>
        int Count<T>(System.Func<T, bool> predicate) where T : IConfigData;

        /// <summary>
        /// 获取所有配置项
        /// </summary>
        T[] All<T>() where T : IConfigData;

        /// <summary>
        /// 检查是否包含指定ID的配置项
        /// </summary>
        bool ContainsId<T>(string id) where T : IConfigData;
    }
}
