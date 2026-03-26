using System;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 定义 UI 设计资源的加载与清理能力。
    /// </summary>
    public interface IUIDesign : IDisposable
    {
        /// <summary>
        /// 加载并绑定指定的 UI 根节点。
        /// </summary>
        /// <param name="root">要绑定的 UI 根对象。</param>
        void Load(GameObject root);

        /// <summary>
        /// 清理当前已加载的 UI 设计资源。
        /// </summary>
        void Clear();
    }
}
