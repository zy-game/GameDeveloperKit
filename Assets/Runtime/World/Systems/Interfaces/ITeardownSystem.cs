namespace GameDeveloperKit.World
{
    /// <summary>
    /// 实体清理接口
    /// 必须配合IGroupSystem使用，通过GroupConfig.Include声明关注的组件类型
    /// 当实体移除Include中的组件且在移除前满足Include/Exclude条件时调用
    /// </summary>
    public interface ITeardownSystem : ISystem
    {
        /// <summary>
        /// 实体即将不满足Group条件时调用
        /// 在实体移除Include中的任一组件前，检查移除前是否满足完整的Include和Exclude条件
        /// 此时组件数据仍可访问
        /// </summary>
        /// <param name="world">游戏世界</param>
        /// <param name="entityId">实体ID</param>
        void OnTeardown(GameWorld world, int entityId);
    }
}
