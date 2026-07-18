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
            return GetOrCreateLayer(layer);
        }

        /// <summary>
        /// 执行 Push Back Stack。
        /// </summary>
        private void PushBackStack(WindowRecord record)
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
        private void DisableTopBeforePush(WindowRecord record)
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
        /// 创建 Layers。
        /// </summary>
        private void CreateLayers()
        {
            m_Layers.Clear();
            m_LayerStacks.Clear();
            foreach (var layer in LayerOrder)
            {
                CreateLayer(layer);
            }
        }

        private RectTransform GetOrCreateLayer(UILayer layer)
        {
            if (m_Layers.TryGetValue(layer, out var root))
            {
                return root;
            }

            root = CreateLayer(layer);
            ReorderLayerRoots();
            return root;
        }

        private RectTransform CreateLayer(UILayer layer)
        {
            var layerTransform = CreateStretchRect(layer.ToString(), m_SafeAreaRoot);
            m_Layers.Add(layer, layerTransform);
            m_LayerStacks.Add(layer, new WindowStack());
            return layerTransform;
        }

        private void ReorderLayerRoots()
        {
            var layers = new List<KeyValuePair<UILayer, RectTransform>>(m_Layers);
            layers.Sort((left, right) => left.Key.Order.CompareTo(right.Key.Order));
            for (var i = 0; i < layers.Count; i++)
            {
                layers[i].Value.SetSiblingIndex(i);
            }

            m_CacheRoot?.SetAsLastSibling();
        }

        private void ReorderLayerWindows(UILayer layer)
        {
            if (m_LayerStacks.TryGetValue(layer, out var stack) is false)
            {
                return;
            }

            var records = stack.Records;
            for (var i = 0; i < records.Count; i++)
            {
                var instance = records[i].Instance;
                if (instance != null)
                {
                    instance.transform.SetSiblingIndex(i);
                }
            }
        }
    }
}
