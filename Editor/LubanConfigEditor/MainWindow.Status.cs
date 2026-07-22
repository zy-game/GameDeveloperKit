using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.LubanConfigEditor.UI
{
    public sealed partial class MainWindow
    {
        private VisualElement CreateStatusPanel()
        {
            var panel = new VisualElement { name = "luban-status-panel" };
            panel.style.flexGrow = 0;
            panel.style.flexShrink = 0;
            panel.style.minWidth = 0;
            panel.style.borderTopWidth = 1;
            panel.style.borderTopColor = EditorGUIUtility.isProSkin
                ? new Color(0.28f, 0.29f, 0.31f)
                : new Color(0.72f, 0.74f, 0.77f);

            var header = new VisualElement { name = "luban-status-header" };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.minHeight = 26;
            header.style.maxHeight = 26;
            header.style.paddingLeft = 10;
            header.style.paddingRight = 10;
            panel.Add(header);

            m_StatusLabel = new Label("Ready");
            m_StatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(m_StatusLabel);

            m_VersionLabel = new Label();
            m_VersionLabel.style.marginLeft = 12;
            header.Add(m_VersionLabel);

            m_ErrorLabel = new Label();
            m_ErrorLabel.style.flexGrow = 1;
            m_ErrorLabel.style.minWidth = 0;
            m_ErrorLabel.style.marginLeft = 12;
            m_ErrorLabel.style.whiteSpace = WhiteSpace.NoWrap;
            m_ErrorLabel.style.overflow = Overflow.Hidden;
            header.Add(m_ErrorLabel);

            var detailButton = new Button(ToggleStatusDetails) { text = "日志" };
            detailButton.name = "luban-status-details-button";
            detailButton.style.width = 56;
            detailButton.style.height = 20;
            header.Add(detailButton);

            m_StatusDetails = new VisualElement { name = "luban-status-details" };
            m_StatusDetails.style.display = DisplayStyle.None;
            m_StatusDetails.style.paddingLeft = 10;
            m_StatusDetails.style.paddingRight = 10;
            m_StatusDetails.style.paddingBottom = 8;
            panel.Add(m_StatusDetails);

            m_StatusDetails.Add(CreateFieldHeader("Command"));
            m_CommandField = CreateTextField(string.Empty);
            m_CommandField.isReadOnly = true;
            m_CommandField.multiline = true;
            m_CommandField.style.height = 48;
            m_CommandField.style.marginBottom = 6;
            m_StatusDetails.Add(m_CommandField);

            m_StatusDetails.Add(CreateFieldHeader("Log"));
            m_LogField = CreateTextField(string.Empty);
            m_LogField.isReadOnly = true;
            m_LogField.multiline = true;
            m_LogField.style.height = 110;
            m_LogField.style.marginBottom = 0;
            m_StatusDetails.Add(m_LogField);

            return panel;
        }

        private VisualElement m_StatusDetails;
        private bool m_StatusDetailsExpanded;

        private void ToggleStatusDetails()
        {
            m_StatusDetailsExpanded = !m_StatusDetailsExpanded;
            if (m_StatusDetails != null)
            {
                m_StatusDetails.style.display = m_StatusDetailsExpanded
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }

            var button = rootVisualElement.Q<Button>("luban-status-details-button");
            if (button != null)
            {
                button.text = m_StatusDetailsExpanded ? "收起" : "日志";
            }
        }

        private void RefreshCommandPreview()
        {
            if (m_CommandField == null)
            {
                return;
            }

            try
            {
                var preview = LubanCommandPreview.CreateGenerate(
                    m_UserConfig?.LubanDllPath,
                    CreateFixedWorkspaceProfile(),
                    CreateFixedGenerationProfile(),
                    "{transaction-code-staging}",
                    "{transaction-data-staging}");
                m_CommandField.SetValueWithoutNotify(preview.Command);
            }
            catch (Exception exception)
            {
                m_CommandField.SetValueWithoutNotify(exception.Message);
            }
        }

        private void RefreshActionState()
        {
            var hasConfig = m_GlobalConfig != null && m_GlobalConfig.TryValidate(out _) && m_ConfModel != null;
            var cliReady = m_ReleaseReport != null && m_ReleaseReport.Success;
            var hasBlockingError = m_SourceSnapshot != null &&
                                   m_SourceSnapshot.Diagnostics.Any(diagnostic => diagnostic.Severity == LubanDiagnosticSeverity.Error);
            var isRunning = LubanCommandRunner.IsRunning;
            var canCheck = hasConfig && cliReady && isRunning is false;
            var canGenerate = canCheck && hasBlockingError is false;

            if (isRunning is false && cliReady && hasBlockingError && m_StatusLabel?.text != "Failed")
            {
                m_StatusLabel.text = "配置错误";
                m_StatusLabel.style.color = new Color(0.95f, 0.35f, 0.3f);
                if (m_ErrorLabel != null)
                {
                    m_ErrorLabel.text = m_SourceSnapshot.Diagnostics
                        .First(diagnostic => diagnostic.Severity == LubanDiagnosticSeverity.Error)
                        .Message;
                    m_ErrorLabel.style.color = new Color(0.95f, 0.35f, 0.3f);
                }
            }
            else if (hasBlockingError is false && m_StatusLabel?.text == "配置错误")
            {
                m_StatusLabel.text = "Ready";
                m_StatusLabel.style.color = new Color(0.35f, 0.8f, 0.45f);
                if (m_ErrorLabel != null)
                {
                    m_ErrorLabel.text = string.Empty;
                }
            }

            var sourcePage = m_Page == Page.SourceTables;
            m_HeaderRefreshButton?.SetEnabled(isRunning is false && m_Page != Page.GlobalSettings);
            m_HeaderCheckButton?.SetEnabled(canCheck && sourcePage);
            m_HeaderGenerateButton?.SetEnabled(canGenerate && sourcePage);
            m_HeaderCancelButton?.SetEnabled(isRunning);
        }

        private void RunCheck()
        {
            var preview = LubanCommandPreview.CreateCheck(
                m_UserConfig.LubanDllPath,
                CreateFixedWorkspaceProfile(),
                CreateFixedGenerationProfile());
            RunLubanAsync(preview).Forget(Debug.LogException);
        }

        private void RunGenerate()
        {
            RunGenerateAsync(
                CreateFixedWorkspaceProfile(),
                CreateFixedGenerationProfile()).Forget(Debug.LogException);
        }

        private async UniTask RunLubanAsync(LubanCommandPreview preview)
        {
            SetRunning(preview.Command);
            var report = await LubanCommandRunner.RunAsync(preview, BeginRun());
            CompleteRun(report, false);
        }

        private async UniTask RunGenerateAsync(
            LubanWorkspaceProfile workspace,
            LubanGenerationProfile profile)
        {
            SetRunning("Preparing transaction-owned Luban staging directories.");
            LubanRunReport report;
            try
            {
                using (var transaction = LubanGenerationTransaction.Create(profile))
                {
                    report = await transaction.RunAsync(
                        m_UserConfig.LubanDllPath,
                        workspace,
                        profile,
                        BeginRun());
                }
            }
            catch (Exception exception)
            {
                report = LubanRunReport.CreateFailure(
                    string.Empty,
                    LubanCommandRunner.GetProjectRoot(),
                    $"Luban staging 初始化失败：{exception.Message}");
            }

            CompleteRun(report, report.Success);
        }

        private void SetRunning(string command)
        {
            RefreshActionState();
            if (m_StatusLabel != null)
            {
                m_StatusLabel.text = "Running";
                m_StatusLabel.style.color = new Color(0.95f, 0.75f, 0.3f);
            }

            if (m_ErrorLabel != null)
            {
                m_ErrorLabel.text = string.Empty;
            }

            m_CommandField?.SetValueWithoutNotify(command);
            m_LogField?.SetValueWithoutNotify(string.Empty);
            m_HeaderCancelButton?.SetEnabled(true);
        }

        private void CompleteRun(LubanRunReport report, bool refreshAssets)
        {
            if (this == null)
            {
                return;
            }

            if (refreshAssets)
            {
                AssetDatabase.Refresh();
                RefreshSourceCatalog();
            }

            RefreshRunReport(report);
            RefreshActionState();
        }

        private void RefreshRunReport(LubanRunReport report)
        {
            if (m_StatusLabel != null)
            {
                m_StatusLabel.text = report.Success ? "Success" : "Failed";
                m_StatusLabel.style.color = report.Success ? new Color(0.35f, 0.8f, 0.45f) : new Color(0.95f, 0.35f, 0.3f);
            }

            if (m_VersionLabel != null)
            {
                m_VersionLabel.text = $"Exit {report.ExitCode} · {report.Elapsed.TotalSeconds:0.000}s";
            }

            if (m_ErrorLabel != null)
            {
                m_ErrorLabel.text = report.ErrorMessage ?? string.Empty;
            }

            m_CommandField?.SetValueWithoutNotify(report.Command ?? string.Empty);
            var logText = string.IsNullOrWhiteSpace(report.StandardError)
                ? report.StandardOutput
                : $"{report.StandardOutput}\n{report.StandardError}";
            m_LogField?.SetValueWithoutNotify(logText ?? string.Empty);
        }
    }
}
