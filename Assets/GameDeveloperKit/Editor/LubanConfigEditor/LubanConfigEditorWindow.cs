using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.LubanConfigEditor
{
    /// <summary>
    /// 定义 Luban Config Editor Window 类型。
    /// </summary>
    public sealed partial class LubanConfigEditorWindow : EditorWindow
    {
        /// <summary>
        /// 定义 Window Title 常量。
        /// </summary>
        private const string WindowTitle = "配置表工具";

        private enum LubanEditorPage
        {
            Workspace,
            Generation,
            Tables,
            Run
        }

        private LubanEditorSettings m_Settings;

        private LubanRunReport m_ReleaseReport;

        private LubanConfModel m_ConfModel;

        private LubanTableIndex m_TableIndex;

        private LubanTableDefinition m_SelectedTable;

        private readonly Dictionary<LubanEditorPage, VisualElement> m_PageElements = new Dictionary<LubanEditorPage, VisualElement>();

        private readonly Dictionary<LubanEditorPage, Button> m_PageButtons = new Dictionary<LubanEditorPage, Button>();

        private readonly List<LubanTableDefinition> m_TableItems = new List<LubanTableDefinition>();

        private LubanEditorPage m_SelectedPage;

        private Button m_HeaderCheckButton;

        private Button m_HeaderGenerateButton;

        private TextField m_ReleasePathField;

        private DropdownField m_WorkspaceSelectorField;

        private TextField m_WorkspaceRootField;

        private Label m_WorkspaceStatusLabel;

        private TextField m_WorkspaceDetailField;

        private ListView m_TableListView;

        private DropdownField m_TableScopeField;

        private TextField m_TableDetailField;

        private TextField m_TableFieldsField;

        private Label m_TableDiagnosticsLabel;

        private Button m_OpenTableSourceButton;

        private Button m_OpenGeneratedCodeButton;

        private Button m_OpenGeneratedDataButton;

        private Button m_SaveTableDeclarationButton;

        private Button m_SelectSameSourceTablesButton;

        private Button m_ClearSameSourceTablesButton;

        private DropdownField m_ProfileSelectorField;

        private TextField m_ProfileNameField;

        private DropdownField m_TargetField;

        private DropdownField m_CodeTargetField;

        private DropdownField m_DataTargetField;

        private TextField m_IncludeTagField;

        private TextField m_ExcludeTagField;

        private TextField m_VariantField;

        private TextField m_PipelineField;

        private TextField m_XargsField;

        private TextField m_OutputCodeDirectoryField;

        private TextField m_OutputDataDirectoryField;

        private Toggle m_UseCustomTemplateDirToggle;

        private TextField m_CustomTemplateDirectoryField;

        private Button m_CustomTemplateDirectoryButton;

        private Toggle m_ValidationFailAsErrorToggle;

        private Label m_StatusLabel;

        private Label m_VersionLabel;

        private Label m_ErrorLabel;

        private TextField m_CommandField;

        private TextField m_LogField;

        private Button m_CheckButton;

        private Button m_GenerateButton;

        /// <summary>
        /// 执行 Open。
        /// </summary>
        [MenuItem("GameDeveloperKit/" + WindowTitle)]
        public static void Open()
        {
            var window = GetWindow<LubanConfigEditorWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(960, 560);
            window.Show();
        }

        /// <summary>
        /// 创建 GUI。
        /// </summary>
        public void CreateGUI()
        {
            m_Settings = LubanEditorSettings.LoadOrCreate();
            BuildLayout();
            DetectRelease();
        }

        /// <summary>
        /// 构建 Layout。
        /// </summary>
        private void BuildLayout()
        {
            rootVisualElement.Clear();
            m_PageElements.Clear();
            m_PageButtons.Clear();
            if (m_SelectedPage == default)
            {
                m_SelectedPage = LubanEditorPage.Workspace;
            }

            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.minWidth = 0;
            root.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.15f, 0.16f, 0.18f) : new Color(0.94f, 0.96f, 0.98f);
            rootVisualElement.Add(root);

            var titleBar = new VisualElement();
            titleBar.style.flexDirection = FlexDirection.Row;
            titleBar.style.alignItems = Align.Center;
            titleBar.style.minHeight = 52;
            titleBar.style.paddingLeft = 14;
            titleBar.style.paddingRight = 14;
            titleBar.style.borderBottomWidth = 1;
            titleBar.style.borderBottomColor = EditorGUIUtility.isProSkin ? new Color(0.28f, 0.3f, 0.33f) : new Color(0.82f, 0.86f, 0.9f);
            titleBar.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.18f, 0.19f, 0.21f) : Color.white;
            root.Add(titleBar);

            var title = new Label("Luban 配置编辑器");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 16;
            title.style.flexGrow = 1;
            titleBar.Add(title);

            m_HeaderCheckButton = new Button(RunCheck) { text = "检查" };
            m_HeaderCheckButton.style.marginRight = 8;
            titleBar.Add(m_HeaderCheckButton);

            m_HeaderGenerateButton = new Button(RunGenerate) { text = "生成" };
            titleBar.Add(m_HeaderGenerateButton);

            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.minWidth = 0;
            body.style.overflow = Overflow.Hidden;
            root.Add(body);

            body.Add(CreateNavigationMenu());

            var workspace = new VisualElement();
            workspace.style.flexGrow = 1;
            workspace.style.minWidth = 0;
            workspace.style.overflow = Overflow.Hidden;
            body.Add(workspace);

            AddPage(workspace, LubanEditorPage.Workspace, CreateWorkspacePage());
            AddPage(workspace, LubanEditorPage.Generation, CreateGenerationPage());
            AddPage(workspace, LubanEditorPage.Tables, CreateTablesPage());
            AddPage(workspace, LubanEditorPage.Run, CreateRunPage());

            SelectPage(m_SelectedPage);
            RefreshActionState();
        }

        /// <summary>
        /// 创建 Navigation Menu。
        /// </summary>
        /// <returns>执行结果。</returns>
        private VisualElement CreateNavigationMenu()
        {
            var menu = new VisualElement();
            menu.style.width = 190;
            menu.style.minWidth = 190;
            menu.style.flexShrink = 0;
            menu.style.paddingTop = 14;
            menu.style.paddingLeft = 12;
            menu.style.paddingRight = 12;
            menu.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.19f, 0.2f, 0.22f) : new Color(0.89f, 0.93f, 0.97f);
            menu.style.borderRightWidth = 1;
            menu.style.borderRightColor = EditorGUIUtility.isProSkin ? new Color(0.28f, 0.3f, 0.33f) : new Color(0.82f, 0.86f, 0.9f);

            var title = new Label("菜单");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 10;
            menu.Add(title);

            AddMenuButton(menu, LubanEditorPage.Workspace, "工作区");
            AddMenuButton(menu, LubanEditorPage.Generation, "生成配置");
            AddMenuButton(menu, LubanEditorPage.Tables, "配置表");
            AddMenuButton(menu, LubanEditorPage.Run, "运行状态");
            return menu;
        }

        /// <summary>
        /// 添加 Menu Button。
        /// </summary>
        /// <param name="menu">menu 参数。</param>
        /// <param name="page">page 参数。</param>
        /// <param name="text">text 参数。</param>
        private void AddMenuButton(VisualElement menu, LubanEditorPage page, string text)
        {
            var button = new Button(() => SelectPage(page))
            {
                text = text
            };
            button.style.height = 38;
            button.style.marginBottom = 6;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.paddingLeft = 12;
            button.style.borderTopLeftRadius = 6;
            button.style.borderTopRightRadius = 6;
            button.style.borderBottomLeftRadius = 6;
            button.style.borderBottomRightRadius = 6;
            menu.Add(button);
            m_PageButtons[page] = button;
        }

        /// <summary>
        /// 创建 Workspace Page。
        /// </summary>
        /// <returns>执行结果。</returns>
        private VisualElement CreateWorkspacePage()
        {
            var page = CreatePageScrollView();
            page.Add(CreateReleasePanel());
            page.Add(CreateWorkspacePanel());
            return page;
        }

        /// <summary>
        /// 创建 Generation Page。
        /// </summary>
        /// <returns>执行结果。</returns>
        private VisualElement CreateGenerationPage()
        {
            var page = CreatePageScrollView();
            page.Add(CreateProfilePanel());
            return page;
        }

        /// <summary>
        /// 创建 Tables Page。
        /// </summary>
        /// <returns>执行结果。</returns>
        private VisualElement CreateTablesPage()
        {
            var page = new VisualElement();
            page.style.flexGrow = 1;
            page.style.minWidth = 0;
            page.style.paddingLeft = 14;
            page.style.paddingRight = 14;
            page.style.paddingTop = 14;
            page.style.paddingBottom = 14;
            page.Add(CreateTablePanel());
            return page;
        }

        /// <summary>
        /// 创建 Run Page。
        /// </summary>
        /// <returns>执行结果。</returns>
        private VisualElement CreateRunPage()
        {
            var page = CreatePageScrollView();
            page.Add(CreateStatusPanel());
            return page;
        }

        /// <summary>
        /// 创建 Page Scroll View。
        /// </summary>
        /// <returns>执行结果。</returns>
        private static ScrollView CreatePageScrollView()
        {
            var page = new ScrollView(ScrollViewMode.Vertical);
            page.style.flexGrow = 1;
            page.style.minWidth = 0;
            page.style.paddingLeft = 14;
            page.style.paddingRight = 14;
            page.style.paddingTop = 14;
            page.style.paddingBottom = 14;
            page.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            page.verticalScrollerVisibility = ScrollerVisibility.Auto;
            return page;
        }

        /// <summary>
        /// 添加 Page。
        /// </summary>
        /// <param name="root">root 参数。</param>
        /// <param name="page">page 参数。</param>
        /// <param name="element">element 参数。</param>
        private void AddPage(VisualElement root, LubanEditorPage page, VisualElement element)
        {
            element.style.flexGrow = 1;
            root.Add(element);
            m_PageElements[page] = element;
        }

        /// <summary>
        /// 选择 Page。
        /// </summary>
        /// <param name="page">page 参数。</param>
        private void SelectPage(LubanEditorPage page)
        {
            m_SelectedPage = page;
            foreach (var pair in m_PageElements)
            {
                pair.Value.style.display = pair.Key == page ? DisplayStyle.Flex : DisplayStyle.None;
            }

            foreach (var pair in m_PageButtons)
            {
                var selected = pair.Key == page;
                pair.Value.style.backgroundColor = selected
                    ? (EditorGUIUtility.isProSkin ? new Color(0.16f, 0.36f, 0.34f) : new Color(0.82f, 0.95f, 0.92f))
                    : (EditorGUIUtility.isProSkin ? new Color(0.23f, 0.24f, 0.27f) : Color.white);
                pair.Value.style.color = selected
                    ? (EditorGUIUtility.isProSkin ? new Color(0.78f, 1f, 0.94f) : new Color(0.05f, 0.42f, 0.38f))
                    : (EditorGUIUtility.isProSkin ? new Color(0.86f, 0.88f, 0.9f) : new Color(0.1f, 0.13f, 0.18f));
            }

            if (page == LubanEditorPage.Tables)
            {
                RefreshTablePanel();
            }
            else if (page == LubanEditorPage.Run)
            {
                RefreshCommandPreview();
                RefreshActionState();
            }
        }
    }
}
