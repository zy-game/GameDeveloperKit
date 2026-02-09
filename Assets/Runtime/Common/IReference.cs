using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace GameDeveloperKit
{
    /// <summary>
    /// 引用接口，支持对象池管理
    /// </summary>
    public interface IReference 
    {
        /// <summary>
        /// 清理对象状态，准备回池
        /// </summary>
        void OnClearup();
    }
}