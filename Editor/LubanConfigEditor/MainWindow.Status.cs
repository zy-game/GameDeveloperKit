using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.LocalizationEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.LubanConfigEditor.UI
{
    public sealed partial class MainWindow
    {
        private VisualElement CreateStatusPanel()
        {
            var panel = CreatePanel();
            panel.style.flexGrow = 0;
            panel.style.minWidth = 0;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            panel.Add(header);

            header.Add(CreateSectionHeader("执行结果"));

            m_StatusLabel = new Label("Ready");
            m_StatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            m_StatusLabel.style.marginLeft = 12;
            header.Add(m_StatusLabel);

            m_VersionLabel = new Label();
            m_VersionLabel.style.marginLeft = 12;
            header.Add(m_VersionLabel);

            m_ErrorLabel = new Label();
            m_ErrorLabel.style.whiteSpace = WhiteSpace.Normal;
            m_ErrorLabel.style.marginBottom = 6;
            panel.Add(m_ErrorLabel);

            panel.Add(CreateFieldHeader("Command"));
            m_CommandField = CreateTextField(string.Empty);
            m_CommandField.isReadOnly = true;
            m_CommandField.multiline = true;
            m_CommandField.style.height = 48;
            m_CommandField.style.marginBottom = 6;
            panel.Add(m_CommandField);

            panel.Add(CreateFieldHeader("Log"));
            m_LogField = CreateTextField(string.Empty);
            m_LogField.isReadOnly = true;
            m_LogField.multiline = true;
            m_LogField.style.height = 86;
            m_LogField.style.marginBottom = 0;
            panel.Add(m_LogField);

            return panel;
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

            m_HeaderRefreshButton?.SetEnabled(isRunning is false);
            m_HeaderCheckButton?.SetEnabled(canCheck);
            m_HeaderGenerateButton?.SetEnabled(canGenerate);
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
                    var localizationPackExport = CreateLocalizationPackExport();
                    report = await transaction.RunAsync(
                        m_UserConfig.LubanDllPath,
                        workspace,
                        profile,
                        BeginRun(),
                        localizationPackExport);
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
