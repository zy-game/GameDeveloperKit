using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Logger
{
    /// <summary>
    /// 定义 Debug Profile Registry 类型。
    /// </summary>
    public sealed class DebugProfileRegistry
    {
        /// <summary>
        /// 存储 Handles。
        /// </summary>
        private readonly List<ProfileHandle> m_Handles = new List<ProfileHandle>();

        /// <summary>
        /// 注册 member。
        /// </summary>
        /// <param name="handle">handle 参数。</param>
        public void Register(ProfileHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            foreach (var registered in m_Handles)
            {
                if (ReferenceEquals(registered, handle))
                {
                    return;
                }
            }

            m_Handles.Add(handle);
        }

        /// <summary>
        /// 注销 member。
        /// </summary>
        /// <param name="handle">handle 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        public bool Unregister(ProfileHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            for (var i = 0; i < m_Handles.Count; i++)
            {
                if (!ReferenceEquals(m_Handles[i], handle))
                {
                    continue;
                }

                m_Handles.RemoveAt(i);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 清理 member。
        /// </summary>
        public void Clear()
        {
            m_Handles.Clear();
        }

        /// <summary>
        /// 执行 Snapshot。
        /// </summary>
        /// <returns>执行结果。</returns>
        public IReadOnlyList<ProfileHandle> Snapshot()
        {
            return m_Handles.ToArray();
        }

        /// <summary>
        /// 绘制 member。
        /// </summary>
        internal void Draw()
        {
            foreach (var handle in m_Handles)
            {
                DrawProfile(handle);
                GUILayout.Space(8f);
            }
        }

        /// <summary>
        /// 获取 Display Name。
        /// </summary>
        /// <param name="handle">handle 参数。</param>
        /// <returns>执行结果。</returns>
        internal static string GetDisplayName(ProfileHandle handle)
        {
            try
            {
                var name = handle.Name;
                return string.IsNullOrWhiteSpace(name) ? handle.GetType().Name : name;
            }
            catch
            {
                return handle.GetType().Name;
            }
        }

        /// <summary>
        /// 绘制 Profile。
        /// </summary>
        /// <param name="handle">handle 参数。</param>
        private static void DrawProfile(ProfileHandle handle)
        {
            Exception nameException = null;
            var name = handle.GetType().Name;

            try
            {
                name = GetDisplayName(handle);
            }
            catch (Exception exception)
            {
                nameException = exception;
            }

            GUILayout.Label(name);
            if (nameException != null)
            {
                GUILayout.Label($"Error: {nameException.Message}");
            }

            try
            {
                handle.Draw();
            }
            catch (Exception exception)
            {
                GUILayout.Label($"Error: {exception.Message}");
            }
        }
    }
}
