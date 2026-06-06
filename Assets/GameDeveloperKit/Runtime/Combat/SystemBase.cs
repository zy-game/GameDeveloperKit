namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 战斗系统基类。
    /// </summary>
    public abstract class SystemBase
    {
        /// <summary>
        /// 系统匹配的实体查询条件。
        /// </summary>
        public virtual Queryable Query => Queryable.All;

        /// <summary>
        /// 初始化系统。
        /// </summary>
        /// <param name="world">战斗世界。</param>
        public virtual void Initialize(World world)
        {
        }

        /// <summary>
        /// 实体进入系统匹配集合。
        /// </summary>
        /// <param name="entity">实体。</param>
        public virtual void OnCreate(Entity entity)
        {
        }

        /// <summary>
        /// 实体离开系统匹配集合。
        /// </summary>
        /// <param name="entity">实体。</param>
        public virtual void OnDestroy(Entity entity)
        {
        }

        /// <summary>
        /// 固定帧更新实体。
        /// </summary>
        /// <param name="entity">实体。</param>
        public virtual void OnUpdate(Entity entity)
        {
        }
    }
}
