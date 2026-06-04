using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.ResourceEditor
{
    public sealed class ResourceEditorCheckResultWindow : EditorWindow
    {
        private readonly List<ResourceValidationIssue> m_Issues = new List<ResourceValidationIssue>();
        private Action<ResourceValidationIssue> m_OnSelect;

        public static void Open(IReadOnlyList<ResourceValidationIssue> issues, Action<ResourceValidationIssue> onSelect)
        {
            var window = GetWindow<ResourceEditorCheckResultWindow>(true, "资源检查结果");
            window.minSize = new Vector2(640, 420);
            window.SetIssues(issues, onSelect);
            window.Show();
        }

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

        public void CreateGUI()
        {
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

        private string BuildSummary()
        {
            var errors = m_Issues.Count(x => x.Severity == ResourceValidationSeverity.Error);
            var warnings = m_Issues.Count(x => x.Severity == ResourceValidationSeverity.Warning);
            var infos = m_Issues.Count(x => x.Severity == ResourceValidationSeverity.Info);
            return $"{errors} Errors · {warnings} Warnings · {infos} Info";
        }

        private static VisualElement MakeIssueRow()
        {
            var row = new VisualElement();
            row.AddToClassList("check-row");
            row.Add(new Label { name = "severity" });
            row.Add(new Label { name = "message" });
            return row;
        }

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
