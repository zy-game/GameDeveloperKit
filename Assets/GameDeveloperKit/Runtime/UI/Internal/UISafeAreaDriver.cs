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
        /// 存储 Safe Area Root。
        /// </summary>
        private RectTransform m_SafeAreaRoot;
        /// <summary>
        /// 存储 Canvas。
        /// </summary>
        private Canvas m_Canvas;
        /// <summary>
        /// 存储 Canvas Scaler。
        /// </summary>
        private UnityEngine.UI.CanvasScaler m_CanvasScaler;

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
        /// 初始化 member。
        /// </summary>
        /// <param name="safeAreaRoot">safe Area Root 参数。</param>
        /// <param name="canvas">canvas 参数。</param>
        /// <param name="canvasScaler">canvas Scaler 参数。</param>
        public void Initialize(RectTransform safeAreaRoot, Canvas canvas, UnityEngine.UI.CanvasScaler canvasScaler)
        {
            m_SafeAreaRoot = safeAreaRoot;
            m_Canvas = canvas;
            m_CanvasScaler = canvasScaler;
            RefreshAll();
        }

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
            ApplyFullScreenBackground(document);
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
            m_SafeAreaRoot = null;
            m_Canvas = null;
            m_CanvasScaler = null;
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

            ApplySafeArea();
            for (var i = m_Documents.Count - 1; i >= 0; i--)
            {
                var document = m_Documents[i];
                if (document == null)
                {
                    m_Documents.RemoveAt(i);
                    continue;
                }

                ApplyFullScreenBackground(document);
            }
        }

        /// <summary>
        /// 执行 Apply Safe Area。
        /// </summary>
        private void ApplySafeArea()
        {
            if (m_SafeAreaRoot == null)
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

            m_SafeAreaRoot.anchorMin = anchorMin;
            m_SafeAreaRoot.anchorMax = anchorMax;
            m_SafeAreaRoot.offsetMin = Vector2.zero;
            m_SafeAreaRoot.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 执行 Apply Full Screen Background。
        /// </summary>
        /// <param name="document">document 参数。</param>
        private void ApplyFullScreenBackground(UIDocument document)
        {
            if (document == null)
            {
                return;
            }

            var rectTransform = document.FullScreenRoot;
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;

            var safeArea = Screen.safeArea;
            var screenSize = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            var scaleFactor = GetScaleFactor(screenSize);
            var leftExtend = safeArea.xMin / scaleFactor;
            var rightExtend = (screenSize.x - safeArea.xMax) / scaleFactor;
            var bottomExtend = safeArea.yMin / scaleFactor;
            var topExtend = (screenSize.y - safeArea.yMax) / scaleFactor;

            rectTransform.offsetMin = new Vector2(-leftExtend, -bottomExtend);
            rectTransform.offsetMax = new Vector2(rightExtend, topExtend);
        }

        /// <summary>
        /// 获取 Scale Factor。
        /// </summary>
        /// <param name="screenSize">screen Size 参数。</param>
        /// <returns>执行结果。</returns>
        private float GetScaleFactor(Vector2 screenSize)
        {
            if (m_CanvasScaler != null &&
                m_CanvasScaler.uiScaleMode == UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                var referenceResolution = m_CanvasScaler.referenceResolution;
                var logWidth = Mathf.Log(screenSize.x / Mathf.Max(1f, referenceResolution.x), 2f);
                var logHeight = Mathf.Log(screenSize.y / Mathf.Max(1f, referenceResolution.y), 2f);
                var logScale = Mathf.Lerp(logWidth, logHeight, m_CanvasScaler.matchWidthOrHeight);
                return Mathf.Max(0.0001f, Mathf.Pow(2f, logScale));
            }

            return Mathf.Max(0.0001f, m_Canvas == null ? 1f : m_Canvas.scaleFactor);
        }
    }
}
