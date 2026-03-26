using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// UI设计基类，提供UI文档节点的访问和管理功能
    /// </summary>
    public abstract class UIDesignBase : IUIDesign
    {
        private readonly Dictionary<string, IUIDesign> _childDesigns = new(StringComparer.Ordinal);

        /// <summary>
        /// 获取UI文档实例
        /// </summary>
        protected UIDocument Document { get; private set; }

        /// <summary>
        /// 加载UI设计
        /// </summary>
        /// <param name="root">根游戏对象</param>
        /// <exception cref="ArgumentNullException">根对象为空</exception>
        /// <exception cref="InvalidOperationException">根对象不包含UIDocument组件</exception>
        public virtual void Load(GameObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            Document = root.GetComponent<UIDocument>();
            if (Document == null)
            {
                throw new InvalidOperationException($"GameObject '{root.name}' does not contain UIDocument.");
            }
        }

        /// <summary>
        /// 清除UI设计
        /// </summary>
        public virtual void Clear()
        {
            foreach (var design in _childDesigns.Values)
            {
                design.Dispose();
            }

            _childDesigns.Clear();
            Document = null;
        }

        /// <summary>
        /// 释放UI设计占用的资源
        /// </summary>
        public void Dispose()
        {
            Clear();
        }

        /// <summary>
        /// 获取子UI设计
        /// </summary>
        /// <typeparam name="TDesign">设计类型</typeparam>
        /// <param name="key">文档节点键</param>
        /// <returns>UI设计实例</returns>
        /// <exception cref="ArgumentException">键为空</exception>
        /// <exception cref="InvalidOperationException">文档节点没有目标游戏对象</exception>
        protected TDesign GetDesign<TDesign>(string key)
            where TDesign : class, IUIDesign, new()
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Design key can not be empty.", nameof(key));
            }

            if (_childDesigns.TryGetValue(key, out var cachedDesign))
            {
                return (TDesign)cachedDesign;
            }

            var target = RequireDocumentNode(key).Target;
            if (target == null)
            {
                throw new InvalidOperationException($"UIDocument node '{key}' has no target GameObject.");
            }

            var design = new TDesign();
            design.Load(target);
            _childDesigns.Add(key, design);
            return design;
        }

        /// <summary>
        /// 获取文档节点
        /// </summary>
        /// <param name="key">节点键</param>
        /// <returns>文档节点</returns>
        /// <exception cref="InvalidOperationException">设计未加载</exception>
        protected UIDocument.Node RequireDocumentNode(string key)
        {
            if (Document == null)
            {
                throw new InvalidOperationException("UIDesign has not been loaded.");
            }

            return Document[key];
        }
    }
}
