using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Debugger;
using GameDeveloperKit.Resource;
using GameDeveloperKit.Timer;
using GameDeveloperKit.UI.Internal;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.UI
{
    public sealed partial class UIModule : GameModuleBase
    {
        /// <summary>
        /// 刷新 Safe Area。
        /// </summary>
        public void RefreshSafeArea()
        {
            m_SafeAreaDriver.RefreshIfChanged();
        }

        /// <summary>
        /// 注册 Document。
        /// </summary>
        internal void RegisterDocument(UIDocument document)
        {
            m_SafeAreaDriver.Add(document);
        }

        /// <summary>
        /// 注销 Document。
        /// </summary>
        internal void UnregisterDocument(UIDocument document)
        {
            m_SafeAreaDriver.Remove(document);
        }

        /// <summary>
        /// 注册 Safe Area Update。
        /// </summary>
        private void RegisterSafeAreaUpdate()
        {
            if (m_SafeAreaUpdateHandle != null &&
                m_SafeAreaUpdateHandle.IsCancelled is false &&
                m_SafeAreaUpdateHandle.IsCompleted is false)
            {
                return;
            }

            if (App.TryGetRegistered<TimerModule>(out var timer) is false)
            {
                return;
            }

            m_SafeAreaUpdateHandle = timer.OnUpdate(OnSafeAreaUpdate, this, "UIModule.SafeArea");
        }

        /// <summary>
        /// 注销 Safe Area Update。
        /// </summary>
        private void UnregisterSafeAreaUpdate()
        {
            if (m_SafeAreaUpdateHandle == null)
            {
                return;
            }

            m_SafeAreaUpdateHandle.Cancel();
            m_SafeAreaUpdateHandle = null;
        }

        /// <summary>
        /// 处理 Safe Area Update。
        /// </summary>
        private void OnSafeAreaUpdate(TimerUpdateContext context)
        {
            RefreshSafeArea();
        }

        /// <summary>
        /// 创建 Stretch Rect。
        /// </summary>
        private static RectTransform CreateStretchRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rectTransform = (RectTransform)gameObject.transform;
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            return rectTransform;
        }
    }
}
