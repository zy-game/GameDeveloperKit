using System;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 基础资源句柄
    /// </summary>
    /// <typeparam name="T">信息数据</typeparam>
    public class ResourceHandle<T> : IReference where T : class
    {
        /// <summary>
        /// 资源信息
        /// </summary>
        public T Info { get; protected set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public Exception Error { get; protected set; }

        /// <summary>
        /// 资源是否有效
        /// </summary>
        /// <returns></returns>
        public bool IsValid => Error == null;

        /// <summary>
        /// 释放资源
        /// </summary>
        public virtual void Release()
        {
            Info = null;
            Error = null;
        }
    }

    /// <summary>
    /// 基础资源句柄
    /// </summary>
    public class ResourceHandle : ResourceHandle<AssetInfo>
    {
    }
}