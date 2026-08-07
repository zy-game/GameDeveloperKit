using System;
using GameDeveloperKit.UI;
using TMPro;
using UnityEngine;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// 红点角标组件（GDK 公用）。挂到任意 UI 节点上，绑定红点 key 后自动显隐。
    /// 自身计数 &gt; 0 时显示（子级聚合也会触发），计数归零时隐藏。
    /// 可选：绑定的 TextMeshProUGUI 显示计数数字（挂到同一物体或子物体）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RedDotBadge : MonoBehaviour
    {
        [Header("RedDot")]
        [SerializeField]
        private string m_Key;

        [SerializeField]
        private bool m_ShowCount = true;

        [SerializeField]
        private string m_CountTextFormat = "{0}";

        [SerializeField]
        private int m_CountCap = 99;

        [Tooltip("计数文本组件（可为空，为空则不显示数字）")]
        [SerializeField]
        private TMP_Text m_CountText;

        [Tooltip("数字上限样式文本，如 99+（超过上限时显示）")]
        [SerializeField]
        private string m_CapSuffix = "+";

        private Action m_Unsubscribe;

        /// <summary>
        /// 红点 key（运行时可在 Awake 前设置）。
        /// </summary>
        public string Key
        {
            get => m_Key;
            set => m_Key = value;
        }

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(m_Key))
            {
                SetVisible(false);
                return;
            }

            if (m_Unsubscribe == null)
            {
                var redDot = App.RedDot;
                redDot.Register(m_Key);
                m_Unsubscribe = redDot.Subscribe(m_Key, OnRedDotChanged);
            }

            Refresh();
        }

        private void OnDisable()
        {
            m_Unsubscribe?.Invoke();
            m_Unsubscribe = null;
        }

        private void OnDestroy()
        {
            m_Unsubscribe?.Invoke();
            m_Unsubscribe = null;
        }

        private void OnRedDotChanged(string key)
        {
            Refresh();
        }

        private void Refresh()
        {
            var active = App.RedDot.IsActive(m_Key);
            SetVisible(active);
            if (!active)
            {
                return;
            }

            if (m_CountText == null || !m_ShowCount)
            {
                return;
            }

            var count = App.RedDot.GetCount(m_Key);
            m_CountText.text = count > m_CountCap
                ? string.Format(m_CountTextFormat, m_CountCap) + m_CapSuffix
                : string.Format(m_CountTextFormat, count);
        }

        private void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }
    }
}
