using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// UI数据基类，提供UI数据的管理和生命周期功能
    /// </summary>
    public abstract class UIDataBase : IDisposable
    {
        /// <summary>
        /// 初始化UI数据
        /// </summary>
        public virtual void OnInitialize()
        {
        }

        /// <summary>
        /// 清理UI数据
        /// </summary>
        public virtual void OnClearup()
        {
        }

        /// <summary>
        /// 释放UI数据占用的资源
        /// </summary>
        public void Dispose()
        {
            OnClearup();
        }
    }
}
