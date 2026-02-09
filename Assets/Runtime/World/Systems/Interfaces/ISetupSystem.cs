namespace GameDeveloperKit.World
{
    /// <summary>
    /// 实体设置接口
    /// 必须配合IGroupSystem使用，通过GroupConfig.Include声明关注的组件类型
    /// 当实体添加Include中的组件且满足Include/Exclude条件时调用
    /// </summary>
    public interface ISetupSystem : ISystem
    {
        /// <summary>
        /// 实体满足Group条件时调用
        /// 在实体添加Include中的任一组件后，检查是否满足完整的Include和Exclude条件
        /// </summary>
        /// <param name="world">游戏世界</param>
        /// <param name="entityId">实体ID</param>
        void OnSetup(GameWorld world, int entityId);
    }
}
