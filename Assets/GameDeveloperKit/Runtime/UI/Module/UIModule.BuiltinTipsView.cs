using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class UIModule
    {
        /// <summary>
        /// 内置提示视图，用于展示简单的文本提示。
        /// </summary>
        private sealed class BuiltinTipsView : MonoBehaviour, IGameObjectPoolable
        {
            private CanvasGroup _canvasGroup;
            private Image _background;
            private Text _messageText;

            /// <summary>
            /// 获取提示视图的矩形变换组件。
            /// </summary>
            public RectTransform RectTransform => (RectTransform)transform;

            /// <summary>
            /// 初始化提示视图依赖的组件引用。
            /// </summary>
            /// <param name="canvasGroup">控制透明度与交互的画布组。</param>
            /// <param name="background">提示背景图像。</param>
            /// <param name="messageText">提示文本组件。</param>
            public void Initialize(CanvasGroup canvasGroup, Image background, Text messageText)
            {
                _canvasGroup = canvasGroup;
                _background = background;
                _messageText = messageText;
            }

            /// <summary>
            /// 设置提示内容。
            /// </summary>
            /// <param name="message">要显示的提示文本。</param>
            public void SetMessage(string message)
            {
                if (_messageText != null)
                {
                    _messageText.text = message ?? string.Empty;
                }
            }

            /// <summary>
            /// 在对象从池中取出时重置显示状态。
            /// </summary>
            public void OnSpawnedFromPool()
            {
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 1f;
                    _canvasGroup.interactable = false;
                    _canvasGroup.blocksRaycasts = false;
                }

                if (_background != null)
                {
                    _background.color = new Color(0f, 0f, 0f, 0.78f);
                }

                var rectTransform = RectTransform;
                rectTransform.localScale = Vector3.one;
                rectTransform.localRotation = Quaternion.identity;
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = new Vector2(460f, 72f);
            }

            /// <summary>
            /// 在对象回收到池中时清理临时状态。
            /// </summary>
            public void OnDespawnedToPool()
            {
                if (_messageText != null)
                {
                    _messageText.text = string.Empty;
                }

                var rectTransform = RectTransform;
                rectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }
}
