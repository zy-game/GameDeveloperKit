using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.LubanConfigEditor.UI
{
    public sealed partial class MainWindow
    {
        private void DetectRelease()
        {
            DetectReleaseAsync().Forget(Debug.LogException);
        }

        private async UniTask DetectReleaseAsync()
        {
            m_UserConfig = GameDeveloperKit.EditorConfiguration.EditorUserConfig.LoadOrCreate();
            m_ReleaseReport = await LubanCommandRunner.DetectReleaseAsync(
                m_UserConfig.LubanDllPath,
                BeginRun());
            if (this != null)
            {
                RefreshReleaseStatus();
            }
        }

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

            if (m_CommandField != null && string.IsNullOrWhiteSpace(m_CommandField.value))
            {
                RefreshCommandPreview();
            }

            RefreshActionState();
        }
    }
}
