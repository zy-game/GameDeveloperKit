using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.EditorCloud;
using GameDeveloperKit.LocalizationEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.LubanConfigEditor.UI
{
    public sealed partial class MainWindow
    {
        private enum Page
        {
            SourceTables,
            GlobalSettings,
            Cloud,
            Localization
        }

        private VisualElement CreateGlobalConfigurationView()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "global-settings-view" };
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            var panel = new EditorConfigurationPanel(() =>
                rootVisualElement.schedule.Execute(RefreshSourceCatalog));
            panel.name = "global-settings-content";
            scroll.Add(panel);
            return scroll;
        }

        private VisualElement CreateLocalizationView()
        {
            m_LocalizationWorkbench = new LocalizationAssetWorkbench(
                LocalizationAuthoringService.Shared,
                ShowLocalizationError);
            return m_LocalizationWorkbench;
        }

        private VisualElement CreateCloudConfigurationView()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "cloud-settings-view" };
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            scroll.Add(new CloudConfigurationPanel());
            return scroll;
        }

        private void ToggleGlobalSettingsMode()
        {
            SetPage(m_Page == Page.GlobalSettings ? Page.SourceTables : Page.GlobalSettings);
        }

        private void ToggleLocalizationMode()
        {
            SetPage(m_Page == Page.Localization ? Page.SourceTables : Page.Localization);
        }

        private void ToggleCloudSettingsMode()
        {
            SetPage(m_Page == Page.Cloud ? Page.SourceTables : Page.Cloud);
        }

        private void SetPage(Page page)
        {
            m_Page = page;
            m_SearchField?.SetValueWithoutNotify(string.Empty);
            RefreshContentMode();
            RefreshActionState();
        }

        private void RefreshContentMode()
        {
            if (m_ContentHost == null)
            {
                return;
            }

            m_ContentHost.Clear();
            m_LocalizationWorkbench = null;
            switch (m_Page)
            {
                case Page.GlobalSettings:
                    m_SourceTableBody = null;
                    m_ContentHost.Add(CreateGlobalConfigurationView());
                    SetHeaderTitle("全局设置");
                    SetHeaderSummary("项目级与本机工具配置");
                    break;
                case Page.Localization:
                    m_SourceTableBody = null;
                    m_ContentHost.Add(CreateLocalizationView());
                    SetHeaderTitle("本地化");
                    SetHeaderSummary("本地化 Key 与语言资产");
                    break;
                case Page.Cloud:
                    m_SourceTableBody = null;
                    m_ContentHost.Add(CreateCloudConfigurationView());
                    SetHeaderTitle("云配置");
                    SetHeaderSummary("对象存储与本机凭证");
                    break;
                default:
                    m_ContentHost.Add(CreateSourceTable());
                    SetHeaderTitle("配置表");
                    RebuildSourceTable();
                    RefreshSourceSummary();
                    break;
            }

            var searchVisible = m_Page == Page.SourceTables || m_Page == Page.Localization;
            if (m_SearchField != null)
            {
                m_SearchField.style.display = searchVisible ? DisplayStyle.Flex : DisplayStyle.None;
                m_SearchField.SetEnabled(searchVisible);
            }

            if (m_GenerateSelectedTableToggle != null)
            {
                m_GenerateSelectedTableToggle.style.display = m_Page == Page.SourceTables
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                m_GenerateSelectedTableToggle.SetEnabled(m_Page == Page.SourceTables);
            }

            RefreshPageToggleStyles();
        }

        private void RefreshPageToggleStyles()
        {
            ApplyPageToggleStyle(m_SourceTablesToggle, m_Page == Page.SourceTables);
            ApplyPageToggleStyle(m_GlobalSettingsToggle, m_Page == Page.GlobalSettings);
            ApplyPageToggleStyle(m_CloudSettingsToggle, m_Page == Page.Cloud);
            ApplyPageToggleStyle(m_LocalizationToggle, m_Page == Page.Localization);
        }

        private static void ApplyPageToggleStyle(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            button.EnableInClassList("luban-config-editor__page-button--selected", selected);
        }

        private void RefreshCurrentPage()
        {
            if (m_Page == Page.Localization)
            {
                m_LocalizationWorkbench?.Rebuild();
                return;
            }

            if (m_Page == Page.SourceTables)
            {
                RefreshSourceCatalog();
            }
        }

        private void ShowLocalizationError(string message)
        {
            if (m_ErrorLabel == null)
            {
                return;
            }

            m_ErrorLabel.text = message ?? string.Empty;
            m_ErrorLabel.style.color = new Color(0.95f, 0.35f, 0.3f);
        }
    }
}
