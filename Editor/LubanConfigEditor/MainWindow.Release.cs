using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using IODirectory = System.IO.Directory;
using IOPath = System.IO.Path;

namespace GameDeveloperKit.LubanConfigEditor.UI
{
    public sealed partial class MainWindow
    {
        /// <summary>
        /// 创建 Release Panel。
        /// </summary>
        /// <returns>执行结果。</returns>
        private VisualElement CreateReleasePanel()
        {
            var panel = CreatePanel();

            panel.Add(CreateSectionHeader("Luban Release"));

            m_ReleasePathField = CreateTextField("Luban.dll");
            m_ReleasePathField.isDelayed = true;
            m_ReleasePathField.isReadOnly = true;
            m_ReleasePathField.value = m_Settings.ReleasePath;
            panel.Add(m_ReleasePathField);

            var actions = CreateButtonRow();
            panel.Add(actions);

            AddRowButton(actions, new Button(BrowseRelease) { text = "选择" });
            AddRowButton(actions, new Button(ResetReleasePath) { text = "默认" });
            AddRowButton(actions, new Button(DetectRelease) { text = "检测" });

            return panel;
        }

        /// <summary>
        /// 浏览 Release。
        /// </summary>
        private void BrowseRelease()
        {
            var currentPath = LubanCommandRunner.GetAbsoluteProjectPath(m_Settings.ReleasePath);
            var startDirectory = IODirectory.Exists(IOPath.GetDirectoryName(currentPath))
                ? IOPath.GetDirectoryName(currentPath)
                : LubanCommandRunner.GetProjectRoot();
            var selectedPath = EditorUtility.OpenFilePanel("Select Luban.dll", startDirectory, "dll");
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            m_Settings.ReleasePath = LubanCommandRunner.ToProjectRelativePath(selectedPath);
            m_Settings.SaveSettings();
            m_ReleasePathField.SetValueWithoutNotify(m_Settings.ReleasePath);
            DetectRelease();
        }

        /// <summary>
        /// 重置 Release Path。
        /// </summary>
        private void ResetReleasePath()
        {
            m_Settings.ReleasePath = LubanEditorSettings.DefaultReleasePath;
            m_Settings.SaveSettings();
            m_ReleasePathField.SetValueWithoutNotify(m_Settings.ReleasePath);
            DetectRelease();
        }

        /// <summary>
        /// 检测 Release。
        /// </summary>
        private void DetectRelease()
        {
            DetectReleaseAsync().Forget(Debug.LogException);
        }

        private async UniTask DetectReleaseAsync()
        {
            m_ReleaseReport = await LubanCommandRunner.DetectReleaseAsync(
                m_Settings.ReleasePath,
                BeginRun());
            if (this != null)
            {
                RefreshReleaseStatus();
            }
        }

        /// <summary>
        /// 刷新 Release Status。
        /// </summary>
        private void RefreshReleaseStatus()
        {
            var success = m_ReleaseReport != null && m_ReleaseReport.Success;
            if (m_StatusLabel != null)
            {
                m_StatusLabel.text = success ? "Ready" : "Error";
                m_StatusLabel.style.color = success ? new Color(0.35f, 0.8f, 0.45f) : new Color(0.95f, 0.35f, 0.3f);
            }

            if (m_VersionLabel != null)
            {
                m_VersionLabel.text = success ? m_ReleaseReport.VersionLine : string.Empty;
            }

            if (m_ErrorLabel != null)
            {
                m_ErrorLabel.text = success ? string.Empty : m_ReleaseReport?.ErrorMessage ?? "Luban release 未检测。";
                m_ErrorLabel.style.color = new Color(0.95f, 0.35f, 0.3f);
            }

            RefreshCommandPreview();
            RefreshActionState();
        }
    }
}
