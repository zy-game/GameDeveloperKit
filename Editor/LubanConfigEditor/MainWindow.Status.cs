using System;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.LubanConfigEditor.UI
{
    public sealed partial class MainWindow
    {
        /// <summary>
        /// 创建 Status Panel。
        /// </summary>
        /// <returns>执行结果。</returns>
        private VisualElement CreateStatusPanel()
        {
            var panel = CreatePanel();
            panel.style.flexGrow = 1;
            panel.style.minWidth = 0;

            panel.Add(CreateSectionHeader("运行状态"));

            m_StatusLabel = new Label();
            m_StatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            m_StatusLabel.style.marginBottom = 6;
            panel.Add(m_StatusLabel);

            m_VersionLabel = new Label();
            m_VersionLabel.style.marginBottom = 6;
            panel.Add(m_VersionLabel);

            m_ErrorLabel = new Label();
            m_ErrorLabel.style.whiteSpace = WhiteSpace.Normal;
            m_ErrorLabel.style.marginBottom = 8;
            panel.Add(m_ErrorLabel);

            panel.Add(CreateFieldHeader("Command"));
            m_CommandField = CreateTextField(string.Empty);
            m_CommandField.isReadOnly = true;
            m_CommandField.multiline = true;
            m_CommandField.style.height = 54;
            m_CommandField.style.marginBottom = 8;
            panel.Add(m_CommandField);

            panel.Add(CreateFieldHeader("Log"));
            m_LogField = CreateTextField(string.Empty);
            m_LogField.isReadOnly = true;
            m_LogField.multiline = true;
            m_LogField.style.height = 150;
            m_LogField.style.marginBottom = 8;
            panel.Add(m_LogField);

            var actions = CreateButtonRow();
            panel.Add(actions);

            m_CheckButton = new Button(RunCheck) { text = "检查" };
            AddRowButton(actions, m_CheckButton);

            m_GenerateButton = new Button(RunGenerate) { text = "生成" };
            AddRowButton(actions, m_GenerateButton);

            m_CancelButton = new Button(CancelCurrentRun) { text = "取消" };
            AddRowButton(actions, m_CancelButton);

            return panel;
        }

        /// <summary>
        /// 刷新 Command Preview。
        /// </summary>
        private void RefreshCommandPreview()
        {
            if (m_CommandField == null)
            {
                return;
            }

            var workspace = GetSelectedWorkspace();
            var profile = GetSelectedGenerationProfile();
            if (workspace == null || profile == null)
            {
                m_CommandField.SetValueWithoutNotify(m_ReleaseReport?.Command ?? string.Empty);
                return;
            }

            try
            {
                if (TryGetGenerateTableSelectionReady(out var selectionMessage) is false)
                {
                    m_CommandField.SetValueWithoutNotify(selectionMessage);
                    return;
                }

                m_CommandField.SetValueWithoutNotify(
                    "Luban generation uses transaction-owned staging output directories.");
            }
            catch (Exception exception)
            {
                m_CommandField.SetValueWithoutNotify(exception.Message);
            }
        }

        /// <summary>
        /// 刷新 Action State。
        /// </summary>
        private void RefreshActionState()
        {
            var canUseCli = m_ReleaseReport != null
                && m_ReleaseReport.Success
                && m_ConfModel != null
                && GetSelectedWorkspace() != null
                && GetSelectedGenerationProfile() != null
                && LubanCommandRunner.IsRunning is false;
            var canGenerate = canUseCli && TryGetGenerateTableSelectionReady(out _);
            m_CheckButton?.SetEnabled(canUseCli);
            m_GenerateButton?.SetEnabled(canGenerate);
            m_HeaderCheckButton?.SetEnabled(canUseCli);
            m_HeaderGenerateButton?.SetEnabled(canGenerate);
            m_CancelButton?.SetEnabled(LubanCommandRunner.IsRunning);
        }

        /// <summary>
        /// 运行 Check。
        /// </summary>
        private void RunCheck()
        {
            var preview = LubanCommandPreview.CreateCheck(m_Settings.ReleasePath, GetSelectedWorkspace(), GetSelectedGenerationProfile());
            SelectPage(Page.Run);
            RunLubanAsync(preview).Forget(Debug.LogException);
        }

        /// <summary>
        /// 运行 Generate。
        /// </summary>
        private void RunGenerate()
        {
            if (TryGetGenerateTableSelectionReady(out var selectionMessage) is false)
            {
                SelectPage(Page.Run);
                RefreshRunReport(LubanRunReport.CreateFailure(string.Empty, LubanCommandRunner.GetProjectRoot(), selectionMessage));
                RefreshActionState();
                return;
            }

            SelectPage(Page.Run);
            RunGenerateAsync(
                GetSelectedWorkspace(),
                GetSelectedGenerationProfile()).Forget(Debug.LogException);
        }

        /// <summary>
        /// 运行 Luban。
        /// </summary>
        /// <param name="preview">preview 参数。</param>
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
                        m_Settings.ReleasePath,
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
            m_CancelButton?.SetEnabled(true);
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
            }

            RefreshRunReport(report);
            RefreshActionState();
        }

        /// <summary>
        /// 刷新 Run Report。
        /// </summary>
        /// <param name="report">report 参数。</param>
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
