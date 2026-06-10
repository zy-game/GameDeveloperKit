using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.ResourceEditor
{
    /// <summary>
    /// 定义 Resource Editor Check Result Window 类型。
    /// </summary>
    public sealed class ResourceEditorCheckResultWindow : EditorWindow
    {
        /// <summary>
        /// 存储 Issues。
        /// </summary>
        private readonly List<ResourceValidationIssue> m_Issues = new List<ResourceValidationIssue>();
        /// <summary>
        /// 存储 On Select。
        /// </summary>
        private Action<ResourceValidationIssue> m_OnSelect;

        /// <summary>
        /// 执行 Open。
        /// </summary>
        /// <param name="issues">issues 参数。</param>
        /// <param name="onSelect">on Select 参数。</param>
        public static void Open(IReadOnlyList<ResourceValidationIssue> issues, Action<ResourceValidationIssue> onSelect)
        {
            var window = GetWindow<ResourceEditorCheckResultWindow>(true, "资源检查结果");
            window.minSize = new Vector2(640, 420);
            window.SetIssues(issues, onSelect);
            window.Show();
        }

        /// <summary>
        /// 设置 Issues。
        /// </summary>
        /// <param name="issues">issues 参数。</param>
        /// <param name="onSelect">on Select 参数。</param>
        private void SetIssues(IReadOnlyList<ResourceValidationIssue> issues, Action<ResourceValidationIssue> onSelect)
        {
            m_Issues.Clear();
            if (issues != null)
            {
                m_Issues.AddRange(issues);
            }

            m_OnSelect = onSelect;
            Render();
        }

        /// <summary>
        /// 创建 GUI。
        /// </summary>
        public void CreateGUI()
        {
            Render();
        }

        /// <summary>
        /// 渲染 member。
        /// </summary>
        private void Render()
        {
            if (rootVisualElement == null)
            {
                return;
            }

            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("check-window");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/GameDeveloperKit/Editor/ResourceEditor/UI/ResourceEditorWindow.uss");
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            var header = new VisualElement();
            header.AddToClassList("check-header");
            var title = new Label("资源检查结果");
            title.AddToClassList("check-title");
            header.Add(title);

            var summary = new Label(BuildSummary());
            summary.AddToClassList("check-summary");
            header.Add(summary);
            rootVisualElement.Add(header);

            var list = new ListView
            {
                itemsSource = m_Issues,
                fixedItemHeight = 42,
                selectionType = SelectionType.Single,
                makeItem = MakeIssueRow,
                bindItem = BindIssueRow
            };
            list.AddToClassList("check-list");
            list.selectionChanged += selection =>
            {
                var issue = selection.OfType<ResourceValidationIssue>().FirstOrDefault();
                if (issue != null)
                {
                    m_OnSelect?.Invoke(issue);
                }
            };
            rootVisualElement.Add(list);
        }

        /// <summary>
        /// 构建 Summary。
        /// </summary>
        /// <returns>执行结果。</returns>
        private string BuildSummary()
        {
            var errors = m_Issues.Count(x => x.Severity == ResourceValidationSeverity.Error);
            var warnings = m_Issues.Count(x => x.Severity == ResourceValidationSeverity.Warning);
            var infos = m_Issues.Count(x => x.Severity == ResourceValidationSeverity.Info);
            return $"{errors} Errors · {warnings} Warnings · {infos} Info";
        }

        /// <summary>
        /// 执行 Make Issue Row。
        /// </summary>
        /// <returns>执行结果。</returns>
        private static VisualElement MakeIssueRow()
        {
            var row = new VisualElement();
            row.AddToClassList("check-row");
            row.Add(new Label { name = "severity" });
            row.Add(new Label { name = "message" });
            return row;
        }

        /// <summary>
        /// 执行 Bind Issue Row。
        /// </summary>
        /// <param name="element">element 参数。</param>
        /// <param name="index">index 参数。</param>
        private void BindIssueRow(VisualElement element, int index)
        {
            var issue = m_Issues[index];
            var severity = element.Q<Label>("severity");
            var message = element.Q<Label>("message");

            severity.text = issue.Severity.ToString();
            message.text = $"{issue.Source}: {issue.Message}{ResourceEditorWindow.IssueTarget(issue)}";

            element.RemoveFromClassList("check-row--error");
            element.RemoveFromClassList("check-row--warning");
            if (issue.Severity == ResourceValidationSeverity.Error)
            {
                element.AddToClassList("check-row--error");
            }
            else if (issue.Severity == ResourceValidationSeverity.Warning)
            {
                element.AddToClassList("check-row--warning");
            }
        }
    }
}
