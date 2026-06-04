using System;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 战斗系统基类。
    /// </summary>
    public abstract class SystemBase
    {
        /// <summary>
        /// 必须包含的组件类型。
        /// </summary>
        public virtual ComponentType[] Include { get; } = Array.Empty<ComponentType>();

        /// <summary>
        /// 必须不包含的组件类型。
        /// </summary>
        public virtual ComponentType[] Exclude { get; } = Array.Empty<ComponentType>();

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
        protected virtual void OnCreate(Entity entity)
        {
        }

        /// <summary>
        /// 实体离开系统匹配集合。
        /// </summary>
        /// <param name="entity">实体。</param>
        protected virtual void OnDestroy(Entity entity)
        {
        }

        /// <summary>
        /// 固定帧更新实体。
        /// </summary>
        /// <param name="entity">实体。</param>
        protected virtual void OnUpdate(Entity entity)
        {
        }

        internal void InvokeOnCreate(Entity entity)
        {
            OnCreate(entity);
        }

        internal void InvokeOnDestroy(Entity entity)
        {
            OnDestroy(entity);
        }

        internal void InvokeOnUpdate(Entity entity)
        {
            OnUpdate(entity);
        }
    }
}
