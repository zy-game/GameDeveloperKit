namespace GameDeveloperKit.World
{
    /// <summary>
    /// 系统更新接口
    /// 每帧由GameWorld调用，用于执行游戏逻辑
    /// 如果实现了IGroupSystem，则会对每个匹配的实体调用OnUpdate
    /// </summary>
    public interface IUpdateSystem : ISystem
    {
        /// <summary>
        /// 系统更新方法
        /// </summary>
        /// <param name="world">游戏世界</param>
        /// <param name="entityId">实体ID（如果是GroupSystem则为匹配的实体，否则为-1）</param>
        /// <param name="deltaTime">时间间隔（秒）</param>
        void OnUpdate(GameWorld world, int entityId, float deltaTime);
    }
}
