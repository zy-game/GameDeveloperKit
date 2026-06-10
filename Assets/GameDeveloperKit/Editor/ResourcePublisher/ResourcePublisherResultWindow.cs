using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.ResourcePublisher
{
    /// <summary>
    /// 定义 Resource Publisher Result Window 类型。
    /// </summary>
    public sealed class ResourcePublisherResultWindow : EditorWindow
    {
        /// <summary>         /// 存储 Items。         /// </summary>
        private readonly List<ResourcePublishOperationItem> m_Items = new List<ResourcePublishOperationItem>();
        /// <summary>
        /// 存储 Result。
        /// </summary>
        private ResourcePublishOperationResult m_Result;

        /// <summary>
        /// 执行 Open。
        /// </summary>
        /// <param name="result">result 参数。</param>
        public static void Open(ResourcePublishOperationResult result)
        {
            var window = GetWindow<ResourcePublisherResultWindow>(true, "资源发布结果");
            window.minSize = new Vector2(720, 520);
            window.SetResult(result);
            window.Show();
        }

        /// <summary>
        /// 设置 Result。
        /// </summary>
        /// <param name="result">result 参数。</param>
        private void SetResult(ResourcePublishOperationResult result)
        {
            m_Result = result;
            m_Items.Clear();
            if (result?.Items != null)
            {
                m_Items.AddRange(result.Items);
            }

            BuildLayout();
        }

        /// <summary>
        /// 构建 Layout。
        /// </summary>
        private void BuildLayout()
        {
            if (rootVisualElement == null)
            {
                return;
            }

            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("publisher-result-window");
            rootVisualElement.EnableInClassList("resource-publisher--dark", EditorGUIUtility.isProSkin);
            rootVisualElement.EnableInClassList("resource-publisher--light", EditorGUIUtility.isProSkin is false);

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/GameDeveloperKit/Editor/ResourcePublisher/UI/ResourcePublisherWindow.uss");
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            var title = new Label(m_Result?.Succeeded == true ? "发布成功" : "发布失败");
            title.AddToClassList("result-title");
            rootVisualElement.Add(title);

            var message = new Label(m_Result?.Message ?? string.Empty);
            message.AddToClassList("summary-text");
            rootVisualElement.Add(message);

            var list = new ListView
            {
                itemsSource = m_Items,
                fixedItemHeight = 56,
                makeItem = MakeRow,
                bindItem = BindRow
            };
            list.AddToClassList("publisher-list");
            list.AddToClassList("publisher-result-list");
            rootVisualElement.Add(list);
        }

        /// <summary>
        /// 执行 Make Row。
        /// </summary>
        /// <returns>执行结果。</returns>
        private static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("list-row");

            var top = new VisualElement();
            top.AddToClassList("list-row__top");

            var name = new Label { name = "name" };
            name.AddToClassList("list-row__name");
            var badge = new Label { name = "badge" };
            badge.AddToClassList("badge");
            var meta = new Label { name = "meta" };
            meta.AddToClassList("list-row__meta");

            top.Add(name);
            top.Add(badge);
            row.Add(top);
            row.Add(meta);
            return row;
        }

        /// <summary>
        /// 执行 Bind Row。
        /// </summary>
        /// <param name="element">element 参数。</param>
        /// <param name="index">index 参数。</param>
        private void BindRow(VisualElement element, int index)
        {
            var item = m_Items[index];
            element.Q<Label>("name").text = item.Key;
            element.Q<Label>("badge").text = item.Succeeded ? "OK" : "Failed";
            element.Q<Label>("meta").text = item.Message;
        }
    }
}
