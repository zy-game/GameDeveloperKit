using System;
using System.Linq;
using GameDeveloperKit.StoryEditor.Model;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Migration
{
    internal sealed class MigrationWindow : EditorWindow
    {
        private AuthoringAsset m_Asset;
        private MigrationPreview m_Preview;
        private Vector2 m_Scroll;

        [MenuItem("GameDeveloperKit/Story/Migrate Route Content")]
        private static void Open()
        {
            var window = GetWindow<MigrationWindow>();
            window.titleContent = new GUIContent("Story Migration");
            window.minSize = new Vector2(520f, 360f);
            window.Show();
        }

        private void OnDisable()
        {
            DisposePreview();
        }

        private void OnGUI()
        {
            var selected = (AuthoringAsset)EditorGUILayout.ObjectField("Authoring Asset", m_Asset, typeof(AuthoringAsset), false);
            if (selected != m_Asset)
            {
                m_Asset = selected;
                DisposePreview();
            }

            using (new EditorGUI.DisabledScope(m_Asset == null))
            {
                if (GUILayout.Button("Analyze"))
                {
                    DisposePreview();
                    m_Preview = MigrationService.Analyze(m_Asset);
                }
            }

            DrawReport();

            var canApply = m_Preview != null && !m_Preview.IsNoOp && m_Preview.Report.CanApply;
            using (new EditorGUI.DisabledScope(!canApply))
            {
                if (GUILayout.Button("Apply"))
                {
                    Apply();
                }
            }
        }

        private void DrawReport()
        {
            if (m_Preview == null)
            {
                return;
            }

            if (m_Preview.IsNoOp)
            {
                EditorGUILayout.HelpBox("The selected asset already uses the current Story route format.", MessageType.Info);
                return;
            }

            var conflicts = m_Preview.Report.Issues.Count(x => x.Severity == MigrationIssueSeverity.Conflict);
            var warnings = m_Preview.Report.Issues.Count(x => x.Severity == MigrationIssueSeverity.Warning);
            EditorGUILayout.LabelField($"Changes: {m_Preview.Report.Changes.Count}   Warnings: {warnings}   Conflicts: {conflicts}");
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            for (var i = 0; i < m_Preview.Report.Issues.Count; i++)
            {
                var issue = m_Preview.Report.Issues[i];
                EditorGUILayout.HelpBox(
                    $"[{issue.Code}] {issue.Location}\n{issue.Message}",
                    issue.Severity == MigrationIssueSeverity.Conflict ? MessageType.Error : MessageType.Warning);
            }

            for (var i = 0; i < m_Preview.Report.Changes.Count; i++)
            {
                var change = m_Preview.Report.Changes[i];
                EditorGUILayout.LabelField($"{change.Kind}  {change.Location}", change.Description);
            }

            EditorGUILayout.EndScrollView();
        }

        private void Apply()
        {
            var confirmWarnings = !m_Preview.Report.HasWarnings || EditorUtility.DisplayDialog(
                "Confirm Story Migration",
                "The migration report contains warnings. Apply the validated candidate?",
                "Apply",
                "Cancel");
            if (!confirmWarnings)
            {
                return;
            }

            var result = MigrationService.Apply(m_Asset, true);
            DisposePreview();
            m_Preview = MigrationService.Analyze(m_Asset);
            if (!result.Succeeded)
            {
                ShowNotification(new GUIContent($"Migration blocked: {result.Status}"));
                return;
            }

            ShowNotification(new GUIContent(result.Status == MigrationApplyStatus.NoOp ? "No migration needed" : "Migration applied"));
        }

        private void DisposePreview()
        {
            m_Preview?.Dispose();
            m_Preview = null;
        }
    }
}
