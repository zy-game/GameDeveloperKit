using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.ResourceEditor
{
    public sealed class ResourceBuildPublishResultWindow : EditorWindow
    {
        private readonly List<object> m_Items = new List<object>();
        private string m_Title;
        private string m_Summary;

        public static void OpenPlan(ResourceBuildPlan plan)
        {
            var window = GetWindow<ResourceBuildPublishResultWindow>(true, "资源构建计划");
            window.minSize = new Vector2(720, 420);
            window.SetPlan(plan);
            window.Show();
        }

        public static void OpenBuildResult(ResourceBuildResult result)
        {
            var window = GetWindow<ResourceBuildPublishResultWindow>(true, "资源构建结果");
            window.minSize = new Vector2(760, 460);
            window.SetBuildResult(result);
            window.Show();
        }

        public void CreateGUI()
        {
            Render();
        }

        private void SetPlan(ResourceBuildPlan plan)
        {
            m_Items.Clear();
            if (plan != null)
            {
                m_Items.AddRange(plan.Bundles);
            }

            m_Title = "资源构建计划";
            m_Summary = plan == null ? "没有构建计划" : $"{plan.Bundles.Count} Bundles";
            Render();
        }

        private void SetBuildResult(ResourceBuildResult result)
        {
            m_Items.Clear();
            if (result?.Artifacts != null)
            {
                m_Items.AddRange(result.Artifacts);
            }

            m_Title = "资源构建结果";
            m_Summary = result == null
                ? "没有构建结果"
                : $"{(result.Succeeded ? "成功" : "失败")} · {result.Artifacts.Count} Files · {result.ErrorMessage}";
            Render();
        }

        private void Render()
        {
            if (rootVisualElement == null)
            {
                return;
            }

            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("check-window");
            rootVisualElement.EnableInClassList("resource-editor--dark", EditorGUIUtility.isProSkin);
            rootVisualElement.EnableInClassList("resource-editor--light", EditorGUIUtility.isProSkin is false);

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/GameDeveloperKit/Editor/ResourceEditor/UI/ResourceEditorWindow.uss");
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            var header = new VisualElement();
            header.AddToClassList("check-header");
            var title = new Label(m_Title ?? "资源构建");
            title.AddToClassList("check-title");
            header.Add(title);
            var summary = new Label(m_Summary ?? string.Empty);
            summary.AddToClassList("check-summary");
            header.Add(summary);
            rootVisualElement.Add(header);

            var list = new ListView
            {
                itemsSource = m_Items,
                fixedItemHeight = 46,
                makeItem = MakeRow,
                bindItem = BindRow
            };
            list.AddToClassList("check-list");
            rootVisualElement.Add(list);
        }

        private static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("check-row");
            row.Add(new Label { name = "severity" });
            row.Add(new Label { name = "message" });
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            var badge = element.Q<Label>("severity");
            var message = element.Q<Label>("message");
            var item = m_Items[index];
            element.RemoveFromClassList("check-row--error");
            element.RemoveFromClassList("check-row--warning");

            switch (item)
            {
                case ResourceBuildPlanBundle bundle:
                    badge.text = "PLAN";
                    message.text = $"{bundle.BundleName} · {bundle.Resources.Count} assets";
                    break;
                case ResourceBuildArtifact artifact:
                    badge.text = artifact.Size > 0 ? "FILE" : "WARN";
                    if (artifact.Size <= 0)
                    {
                        element.AddToClassList("check-row--warning");
                    }

                    message.text = $"{artifact.RemoteKey} · {artifact.Size} bytes · crc {artifact.Crc}";
                    break;
                default:
                    badge.text = "INFO";
                    message.text = item?.ToString() ?? string.Empty;
                    break;
            }
        }
    }
}
