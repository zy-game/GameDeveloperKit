namespace GameDeveloperKit.World
{
    /// <summary>
    /// 烘焙接口，用于将数据转换为实体和组件
    /// </summary>
    public interface IBaker
    {
        /// <summary>
        /// 将数据烘焙到指定实体
        /// </summary>
        /// <param name="world">游戏世界</param>
        /// <param name="entityId">实体ID</param>
        void Bake(GameWorld world, int entityId);
    }
}