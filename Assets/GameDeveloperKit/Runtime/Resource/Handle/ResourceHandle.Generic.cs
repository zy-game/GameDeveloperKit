using System;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 基础资源句柄
    /// </summary>
    /// <typeparam name="T">资源信息类型。</typeparam>
    public class ResourceHandle<T> : IReference where T : class
    {
        private int m_ReferenceCount = 1;
        private ProviderBase m_Owner;

        /// <summary>
        /// 资源信息
        /// </summary>
        public T Info { get; protected set; }

        /// <summary>
        /// 资源状态
        /// </summary>
        public ResourceStatus Status { get; protected set; } = ResourceStatus.None;

        /// <summary>
        /// 错误信息
        /// </summary>
        public Exception Error { get; protected set; }

        /// <summary>
        /// 当前引用计数。
        /// </summary>
        public int ReferenceCount => m_ReferenceCount;

        /// <summary>
        /// 是否还有引用持有该资源。
        /// </summary>
        public bool IsReferenced => m_ReferenceCount > 0;

        /// <summary>
        /// 持有该句柄生命周期的资源提供者。
        /// </summary>
        internal ProviderBase Owner => m_Owner;

        /// <summary>
        /// 增加引用计数。
        /// </summary>
        public int Retain()
        {
            if (Status is ResourceStatus.Released)
            {
                throw new GameException("Cannot retain a released resource handle.");
            }

            m_ReferenceCount++;
            return m_ReferenceCount;
        }

        /// <summary>
        /// 减少引用计数。
        /// </summary>
        public int ReleaseReference()
        {
            if (m_ReferenceCount <= 0)
            {
                return 0;
            }

            m_ReferenceCount--;
            return m_ReferenceCount;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public virtual void Release()
        {
            if (Status is ResourceStatus.Released)
            {
                return;
            }

            if (m_Owner != null)
            {
                m_Owner.ReleaseHandle(this);
                return;
            }

            if (ReleaseReference() > 0)
            {
                return;
            }

            ReleaseCore();
        }

        /// <summary>
        /// 执行最终资源释放。
        /// </summary>
        protected virtual void ReleaseCore()
        {
            Info = null;
            Error = null;
            Status = ResourceStatus.Released;
            m_ReferenceCount = 0;
            m_Owner = null;
        }

        /// <summary>
        /// 设置资源状态
        /// </summary>
        /// <param name="status">资源状态</param>
        public void SetStatus(ResourceStatus status)
        {
            Status = status;
        }

        /// <summary>
        /// 重置引用计数。
        /// </summary>
        protected void ResetReferenceCount()
        {
            m_ReferenceCount = 1;
        }

        /// <summary>
        /// 绑定资源提供者。
        /// </summary>
        internal void AttachOwner(ProviderBase owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (m_Owner != null && ReferenceEquals(m_Owner, owner) is false)
            {
                throw new GameException("Resource handle already belongs to another provider.");
            }

            m_Owner = owner;
        }

        /// <summary>
        /// 解除资源提供者绑定。
        /// </summary>
        internal void DetachOwner(ProviderBase owner)
        {
            if (ReferenceEquals(m_Owner, owner))
            {
                m_Owner = null;
            }
        }

        /// <summary>
        /// 由资源提供者执行最终释放，不再递减外部引用计数。
        /// </summary>
        internal void ReleaseInternal()
        {
            if (Status is ResourceStatus.Released)
            {
                return;
            }

            ReleaseCore();
        }
    }
}
