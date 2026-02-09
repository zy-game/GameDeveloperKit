using System;

namespace GameDeveloperKit
{
    /// <summary>
    /// 引用句柄基类
    /// </summary>
    public abstract class BaseHandle : IReference, IDisposable
    {
        private int _referenceCount;

        /// <summary>
        /// 资源名称
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// 资源地址
        /// </summary>
        public abstract string Address { get; }

        /// <summary>
        /// 资源GUID
        /// </summary>
        public abstract string GUID { get; }

        /// <summary>
        /// 引用计数
        /// </summary>
        public int ReferenceCount => _referenceCount;

        /// <summary>
        /// 保留引用
        /// </summary>
        internal void Retain()
        {
            _referenceCount++;
        }

        /// <summary>
        /// 释放引用
        /// </summary>
        internal void Release()
        {
            _referenceCount--;
        }

        /// <summary>
        /// 释放资源（子类实现具体释放逻辑）
        /// </summary>
        void IDisposable.Dispose() => OnDispose();

        /// <summary>
        /// 释放资源的具体实现（子类覆盖）
        /// </summary>
        protected abstract void OnDispose();

        /// <summary>
        /// 清理
        /// </summary>
        public virtual void OnClearup()
        {
            _referenceCount = 0;
        }
    }
}
