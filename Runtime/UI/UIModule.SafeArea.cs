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
        private void RegisterUpdate()
        {
            if (m_UpdateHandle != null &&
                m_UpdateHandle.IsCancelled is false &&
                m_UpdateHandle.IsCompleted is false)
            {
                return;
            }

            if (App.TryGetRegistered<TimerModule>(out var timer) is false)
            {
                return;
            }

            m_UpdateHandle = timer.OnUpdate(OnUpdate, this, "UIModule.Update");
        }

        /// <summary>
        /// 注销 Safe Area Update。
        /// </summary>
        private void UnregisterUpdate()
        {
            if (m_UpdateHandle == null)
            {
                return;
            }

            m_UpdateHandle.Cancel();
            m_UpdateHandle = null;
        }

        /// <summary>
        /// 处理 Safe Area Update。
        /// </summary>
        private void OnUpdate(TimerUpdateContext context)
        {
            RefreshSafeArea();
            m_UpdateRecords.Clear();
            foreach (var record in m_Records.Values)
            {
                if (record?.Status == WindowStatus.Opened && record.Window != null)
                {
                    m_UpdateRecords.Add(record);
                }
            }

            for (var i = 0; i < m_UpdateRecords.Count; i++)
            {
                try
                {
                    m_UpdateRecords[i].Window.OnUpdate(context.DeltaTime, context.UnscaledDeltaTime);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            m_UpdateRecords.Clear();
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
