using System;
using System.Collections.Generic;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// 红点模块（GDK 公用）。
    /// 数据层：key 注册（支持父级聚合）、计数增减、变化通知。
    /// UI 层：<see cref="RedDotBadge"/> 组件挂到任意节点自动显隐。
    /// 用法：App.RedDot.Register("main.mail") → App.RedDot.SetCount("main.mail", 1)
    /// 父级 key 在子级变化时自动聚合（任一子级红点 &gt; 0 则父级显示），无需手动维护父级计数。
    /// </summary>
    public sealed class RedDotModule : GameModuleBase
    {
        private readonly Dictionary<string, RedDotNode> m_Nodes = new Dictionary<string, RedDotNode>();
        private readonly List<Action<string>> m_GlobalListeners = new List<Action<string>>();
        private readonly Dictionary<string, List<Action<string>>> m_KeyListeners = new Dictionary<string, List<Action<string>>>();

        private const char PathSeparator = '.';

        public override void Startup()
        {
        }

        public override void Shutdown()
        {
            m_Nodes.Clear();
            m_GlobalListeners.Clear();
            m_KeyListeners.Clear();
        }

        /// <summary>
        /// 注册红点 key（幂等）。支持层级路径（如 "main.mail"），自动建立父级聚合关系。
        /// </summary>
        public void Register(string key)
        {
            var normalized = NormalizeKey(key);
            if (string.IsNullOrEmpty(normalized) || m_Nodes.ContainsKey(normalized))
            {
                return;
            }

            var node = new RedDotNode { Key = normalized };
            m_Nodes.Add(normalized, node);

            var parentKey = GetParentKey(normalized);
            if (parentKey != null)
            {
                Register(parentKey);
                if (m_Nodes.TryGetValue(parentKey, out var parent))
                {
                    parent.Children.Add(node);
                }
            }
        }

        /// <summary>
        /// 设置红点计数。count &gt; 0 显示，否则隐藏。父级自动聚合刷新。
        /// </summary>
        public void SetCount(string key, int count)
        {
            var normalized = NormalizeKey(key);
            if (!m_Nodes.TryGetValue(normalized, out var node))
            {
                Register(normalized);
                node = m_Nodes[normalized];
            }

            if (node.Count == count)
            {
                return;
            }

            node.Count = count;
            RefreshActive(node);
            Notify(node);
            PropagateParent(node);
        }

        /// <summary>
        /// 增减红点计数（可传负数）。父级自动聚合刷新。
        /// </summary>
        public void AddCount(string key, int delta)
        {
            var normalized = NormalizeKey(key);
            if (!m_Nodes.TryGetValue(normalized, out var node))
            {
                Register(normalized);
                node = m_Nodes[normalized];
            }

            SetCount(normalized, node.Count + delta);
        }

        /// <summary>
        /// 清空红点（计数归零）。
        /// </summary>
        public void Clear(string key)
        {
            SetCount(key, 0);
        }

        /// <summary>
        /// 获取红点是否显示（自身或任一子级 &gt; 0）。
        /// </summary>
        public bool IsActive(string key)
        {
            var normalized = NormalizeKey(key);
            return m_Nodes.TryGetValue(normalized, out var node) && node.IsActive;
        }

        /// <summary>
        /// 获取自身计数（不含子级聚合）。
        /// </summary>
        public int GetCount(string key)
        {
            var normalized = NormalizeKey(key);
            return m_Nodes.TryGetValue(normalized, out var node) ? node.Count : 0;
        }

        /// <summary>
        /// 订阅指定 key 的变化（自身变化或子级聚合变化都会触发）。
        /// 返回的 Action 用于取消订阅。
        /// </summary>
        public Action Subscribe(string key, Action<string> listener)
        {
            var normalized = NormalizeKey(key);
            if (!m_KeyListeners.TryGetValue(normalized, out var listeners))
            {
                listeners = new List<Action<string>>();
                m_KeyListeners.Add(normalized, listeners);
            }

            if (!listeners.Contains(listener))
            {
                listeners.Add(listener);
            }

            return () => Unsubscribe(normalized, listener);
        }

        /// <summary>
        /// 订阅所有红点变化。
        /// </summary>
        public Action SubscribeAll(Action<string> listener)
        {
            if (!m_GlobalListeners.Contains(listener))
            {
                m_GlobalListeners.Add(listener);
            }

            return () => m_GlobalListeners.Remove(listener);
        }

        private void Unsubscribe(string key, Action<string> listener)
        {
            if (m_KeyListeners.TryGetValue(key, out var listeners))
            {
                listeners.Remove(listener);
                if (listeners.Count == 0)
                {
                    m_KeyListeners.Remove(key);
                }
            }
        }

        /// <summary>
        /// 刷新自身激活状态，并沿父链向上重算（子级聚合）。被改动的节点全部触发通知。
        /// </summary>
        private void RefreshActive(RedDotNode node)
        {
            node.IsActive = node.Count > 0 || AnyChildActive(node);
            Notify(node);
        }

        private void PropagateParent(RedDotNode node)
        {
            var parent = FindParent(node);
            while (parent != null)
            {
                var changed = RefreshParentActive(parent);
                if (!changed)
                {
                    return;
                }

                Notify(parent);
                parent = FindParent(parent);
            }
        }

        private bool RefreshParentActive(RedDotNode parent)
        {
            var previous = parent.IsActive;
            parent.IsActive = parent.Count > 0 || AnyChildActive(parent);
            return previous != parent.IsActive;
        }

        private static bool AnyChildActive(RedDotNode node)
        {
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (node.Children[i].IsActive)
                {
                    return true;
                }
            }

            return false;
        }

        private RedDotNode FindParent(RedDotNode node)
        {
            var parentKey = GetParentKey(node.Key);
            return parentKey != null && m_Nodes.TryGetValue(parentKey, out var parent) ? parent : null;
        }

        private void Notify(RedDotNode node)
        {
            for (var i = m_GlobalListeners.Count - 1; i >= 0; i--)
            {
                m_GlobalListeners[i]?.Invoke(node.Key);
            }

            if (m_KeyListeners.TryGetValue(node.Key, out var listeners))
            {
                for (var i = listeners.Count - 1; i >= 0; i--)
                {
                    listeners[i]?.Invoke(node.Key);
                }
            }
        }

        private static string GetParentKey(string key)
        {
            var lastSeparator = key.LastIndexOf(PathSeparator);
            return lastSeparator > 0 ? key.Substring(0, lastSeparator) : null;
        }

        private static string NormalizeKey(string key)
        {
            return key?.Trim().Trim(PathSeparator) ?? string.Empty;
        }

        private sealed class RedDotNode
        {
            public string Key;
            public int Count;
            public bool IsActive;
            public readonly List<RedDotNode> Children = new List<RedDotNode>();
        }
    }
}
