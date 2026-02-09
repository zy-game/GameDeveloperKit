using System.Collections.Generic;

namespace GameDeveloperKit.World
{
    /// <summary>
    /// 世界管理器接口
    /// </summary>
    public interface IWorldManager : IModule
    {
        /// <summary>
        /// 默认World
        /// </summary>
        GameWorld DefaultWorld { get; }

        /// <summary>
        /// 创建一个新的World
        /// </summary>
        /// <param name="name">World名称</param>
        /// <returns>创建的World实例</returns>
        GameWorld CreateWorld(string name);

        /// <summary>
        /// 创建一个新的World，带时间配置
        /// </summary>
        /// <param name="name">World名称</param>
        /// <param name="timeConfig">时间配置</param>
        /// <returns>创建的World实例</returns>
        GameWorld CreateWorld(string name, WorldTimeConfig timeConfig);

        /// <summary>
        /// 获取指定名称的World
        /// </summary>
        /// <param name="name">World名称</param>
        /// <returns>World实例，不存在则返回null</returns>
        GameWorld GetWorld(string name);

        /// <summary>
        /// 销毁指定名称的World
        /// </summary>
        /// <param name="name">World名称</param>
        /// <returns>是否成功销毁</returns>
        bool DestroyWorld(string name);

        /// <summary>
        /// 检查是否存在指定名称的World
        /// </summary>
        /// <param name="name">World名称</param>
        /// <returns>是否存在</returns>
        bool HasWorld(string name);

        /// <summary>
        /// 获取所有World
        /// </summary>
        /// <returns>所有World的只读集合</returns>
        IReadOnlyCollection<GameWorld> GetAllWorlds();
    }
}