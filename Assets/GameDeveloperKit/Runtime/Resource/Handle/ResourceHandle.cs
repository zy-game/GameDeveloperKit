using System;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 基础资源句柄
    /// </summary>
    public class ResourceHandle : ResourceHandle<AssetInfo>
    {
    }

    /// <summary>
    /// 基础资源句柄
    /// </summary>
    /// <typeparam name="T">资源信息类型。</typeparam>
    public class ResourceHandle<T> : IReference where T : class
    {
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
        /// 释放资源
        /// </summary>
        public virtual void Release()
        {
            Info = null;
            Error = null;
            Status = ResourceStatus.Released;
        }

        /// <summary>
        /// 设置资源状态
        /// </summary>
        /// <param name="status">资源状态</param>
        public void SetStatus(ResourceStatus status)
        {
            Status = status;
        }
    }
}
