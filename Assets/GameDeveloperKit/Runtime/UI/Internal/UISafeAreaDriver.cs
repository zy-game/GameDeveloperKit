using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.UI.Internal
{
    internal sealed class UISafeAreaDriver
    {
        private readonly List<UIDocument> m_Documents = new List<UIDocument>();

        private Rect m_LastSafeArea;
        private int m_LastWidth;
        private int m_LastHeight;

        public void Add(UIDocument document)
        {
            if (document == null || m_Documents.Contains(document))
            {
                return;
            }

            m_Documents.Add(document);
            Apply(document);
        }

        public void Remove(UIDocument document)
        {
            if (document == null)
            {
                return;
            }

            m_Documents.Remove(document);
        }

        public void Clear()
        {
            m_Documents.Clear();
            m_LastSafeArea = default;
            m_LastWidth = 0;
            m_LastHeight = 0;
        }

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

        public static void Apply(UIDocument document)
        {
            if (document == null)
            {
                return;
            }

            ApplyFullScreen(document.FullScreenRoot);
            ApplySafeArea(document.SafeAreaRoot);
        }

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
