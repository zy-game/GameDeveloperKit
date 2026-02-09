namespace GameDeveloperKit.World
{
    /// <summary>
    /// 组系统接口
    /// 用于声明System对特定组件组合感兴趣
    /// GameWorld可以基于此配置进行优化（例如缓存Filter）
    /// </summary>
    public interface IGroupSystem : ISystem
    {
        /// <summary>
        /// 获取组配置
        /// 声明此System关注的组件Include/Exclude条件
        /// </summary>
        /// <returns>组配置</returns>
        GroupConfig GetGroupConfig();
    }
}
