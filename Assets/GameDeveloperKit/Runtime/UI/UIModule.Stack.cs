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
        /// 获取指定 UI 层级的根节点。
        /// </summary>
        public RectTransform GetLayerRoot(UILayer layer)
        {
            EnsureStarted();
            if (IsValidLayer(layer) is false)
            {
                throw new ArgumentException("UILayer must be a valid layer.", nameof(layer));
            }

            if (m_Layers.TryGetValue(layer, out var root))
            {
                return root;
            }

            throw new GameException($"UI layer '{layer}' is not created.");
        }

        /// <summary>
        /// 执行 Push Back Stack。
        /// </summary>
        private void PushBackStack(UIWindowRecord record)
        {
            if (record == null || IsNavigable(record.Layer) is false)
            {
                return;
            }

            m_BackStack.Remove(record);
            m_BackStack.Add(record);
        }

        /// <summary>
        /// 执行 Disable Top Before Push。
        /// </summary>
        private void DisableTopBeforePush(UIWindowRecord record)
        {
            if (record == null || IsNavigable(record.Layer) is false)
            {
                return;
            }

            var top = m_LayerStacks[record.Layer].Top;
            if (top == null || top == record)
            {
                return;
            }

            top.Window?.OnDisable();
        }

        /// <summary>
        /// 执行 Is Navigable。
        /// </summary>
        private static bool IsNavigable(UILayer layer)
        {
            return layer == UILayer.Main || layer == UILayer.Window;
        }

        /// <summary>
        /// 执行 Is Valid Layer。
        /// </summary>
        private static bool IsValidLayer(UILayer layer)
        {
            return layer == UILayer.Background || layer == UILayer.Main || layer == UILayer.Window
                || layer == UILayer.Loading || layer == UILayer.Message || layer == UILayer.StoryPlayback;
        }

        /// <summary>
        /// 创建 Layers。
        /// </summary>
        private void CreateLayers()
        {
            m_Layers.Clear();
            m_LayerStacks.Clear();
            foreach (var layer in LayerOrder)
            {
                var layerTransform = CreateStretchRect(layer.ToString(), m_SafeAreaRoot);
                m_Layers.Add(layer, layerTransform);
                m_LayerStacks.Add(layer, new UIWindowStack());
            }
        }
    }
}
