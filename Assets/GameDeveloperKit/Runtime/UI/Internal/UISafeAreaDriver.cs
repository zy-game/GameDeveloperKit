using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.UI.Internal
{
    /// <summary>
    /// 定义 UI Safe Area Driver 类型。
    /// </summary>
    internal sealed class UISafeAreaDriver
    {
        /// <summary>
        /// 存储 Documents。
        /// </summary>
        private readonly List<UIDocument> m_Documents = new List<UIDocument>();

        /// <summary>
        /// 存储 Last Safe Area。
        /// </summary>
        private Rect m_LastSafeArea;
        /// <summary>
        /// 存储 Last Width。
        /// </summary>
        private int m_LastWidth;
        /// <summary>
        /// 存储 Last Height。
        /// </summary>
        private int m_LastHeight;

        /// <summary>
        /// 添加 member。
        /// </summary>
        /// <param name="document">document 参数。</param>
        public void Add(UIDocument document)
        {
            if (document == null || m_Documents.Contains(document))
            {
                return;
            }

            m_Documents.Add(document);
            Apply(document);
        }

        /// <summary>
        /// 移除 member。
        /// </summary>
        /// <param name="document">document 参数。</param>
        public void Remove(UIDocument document)
        {
            if (document == null)
            {
                return;
            }

            m_Documents.Remove(document);
        }

        /// <summary>
        /// 清理 member。
        /// </summary>
        public void Clear()
        {
            m_Documents.Clear();
            m_LastSafeArea = default;
            m_LastWidth = 0;
            m_LastHeight = 0;
        }

        /// <summary>
        /// 刷新 If Changed。
        /// </summary>
        public void RefreshIfChanged()
        {
            if (m_LastSafeArea == Screen.safeArea &&
                m_LastWidth == Screen.width &&
                m_LastHeight == Screen.height)
            {
                return;
            }

            RefreshAll();
        }

        /// <summary>
        /// 刷新 All。
        /// </summary>
        public void RefreshAll()
        {
            m_LastSafeArea = Screen.safeArea;
            m_LastWidth = Screen.width;
            m_LastHeight = Screen.height;

            for (var i = m_Documents.Count - 1; i >= 0; i--)
            {
                var document = m_Documents[i];
                if (document == null)
                {
                    m_Documents.RemoveAt(i);
                    continue;
                }

                Apply(document);
            }
        }

        /// <summary>
        /// 执行 Apply。
        /// </summary>
        /// <param name="document">document 参数。</param>
        public static void Apply(UIDocument document)
        {
            if (document == null)
            {
                return;
            }

            ApplyFullScreen(document.FullScreenRoot);
            ApplySafeArea(document.SafeAreaRoot);
        }

        /// <summary>
        /// 执行 Apply Full Screen。
        /// </summary>
        /// <param name="rectTransform">rect Transform 参数。</param>
        private static void ApplyFullScreen(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 执行 Apply Safe Area。
        /// </summary>
        /// <param name="rectTransform">rect Transform 参数。</param>
        private static void ApplySafeArea(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            var width = Mathf.Max(1, Screen.width);
            var height = Mathf.Max(1, Screen.height);
            var safeArea = Screen.safeArea;
            var anchorMin = safeArea.position;
            var anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= width;
            anchorMin.y /= height;
            anchorMax.x /= width;
            anchorMax.y /= height;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
