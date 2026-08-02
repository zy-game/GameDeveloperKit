using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.LubanConfigEditor.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.DesignImporter
{
    internal sealed class DesignImporterWindow : EditorWindow
    {
        private enum LayerDropPlacement
        {
            Before,
            Inside,
            After
        }

        private enum AnchorValue
        {
            AnchorMinX,
            AnchorMinY,
            AnchorMaxX,
            AnchorMaxY,
            PivotX,
            PivotY
        }

        private const string WindowTitle = "设计稿转 Prefab";
        private const string StylePath = "Editor/DesignImporter/UI/DesignImporterWindow.uss";
        private const string FigmaTokenPreference = "GameDeveloperKit.DesignImporter.FigmaToken";
        private const string OutputPreference = "GameDeveloperKit.DesignImporter.Output";
        private const string LanhuUrlPreference = "GameDeveloperKit.DesignImporter.LanhuUrl";

        private EditorGlobalConfig m_ProjectConfig;
        private EditorUserConfig m_UserConfig;
        private DesignDocument m_Document;
        private DesignPage m_SelectedPage;
        private DesignNode m_SelectedNode;
        private DesignSyncSnapshot m_Snapshot;
        private DesignVersionDiffResult m_Diff;
        private string m_ProjectCacheRoot = string.Empty;
        private readonly List<DesignPageListItem> m_PageItems = new List<DesignPageListItem>();
        private readonly List<DesignNodeRow> m_NodeRows = new List<DesignNodeRow>();
        private readonly List<DesignNodeRow> m_AllNodeRows = new List<DesignNodeRow>();
        private readonly HashSet<string> m_CollapsedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> m_PageThumbnails = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private Texture2D m_PreviewTexture;
        private CancellationTokenSource m_ActionCancellation;
        private CancellationTokenSource m_PreviewCancellation;
        private int m_PreviewVersion;
        private int m_DocumentVersion;
        private bool m_Refreshing;
        private bool m_ResettingPageSelection;
        private bool m_Busy;
        private DesignNode m_DraggedNode;
        private VisualElement m_DragSourceElement;
        private VisualElement m_DragTargetElement;
        private DesignNode m_DragTargetNode;
        private LayerDropPlacement m_DragPlacement;
        private Vector2 m_DragStart;
        private bool m_DragActive;
        private bool m_LayerBrowserActive;
        private LanhuSyncBridgeServer m_LanhuBridge;

        private Button m_LoadButton;
        private Button m_LoadCacheButton;
        private Button m_WelcomeImportButton;
        private Button m_WelcomeCacheButton;
        private Button m_CancelButton;
        private VisualElement m_Workspace;
        private VisualElement m_WelcomeView;
        private VisualElement m_PageBrowserView;
        private VisualElement m_LayerBrowserView;
        private Button m_BreadcrumbHome;
        private Label m_BreadcrumbSeparator;
        private Label m_BreadcrumbPage;
        private Label m_ConfigSummary;
        private Label m_DragHint;
        private VisualElement m_DragPreview;
        private Label m_DragPreviewNode;
        private Label m_DragPreviewAction;
        private ListView m_PageList;
        private DesignPreviewElement m_Preview;
        private Slider m_ZoomSlider;
        private Label m_ZoomLabel;
        private Label m_PreviewTitle;
        private ListView m_NodeList;
        private Label m_NodeSummary;
        private TextField m_NodeNameField;
        private FloatField m_NodeX;
        private FloatField m_NodeY;
        private FloatField m_NodeWidth;
        private FloatField m_NodeHeight;
        private AnchorPresetElement m_AnchorPresetEditor;
        private FloatField m_AnchorMinX;
        private FloatField m_AnchorMinY;
        private FloatField m_AnchorMaxX;
        private FloatField m_AnchorMaxY;
        private FloatField m_PivotX;
        private FloatField m_PivotY;
        private VisualElement m_ImageInspector;
        private VisualElement m_TextInspector;
        private TextField m_NodeText;
        private TextField m_FontName;
        private TextField m_FontStyleName;
        private FloatField m_FontSize;
        private Toggle m_FontBold;
        private Toggle m_FontItalic;
        private ColorField m_TextColor;
        private PopupField<string> m_TextAlignment;
        private FloatField m_Tracking;
        private FloatField m_LineHeight;
        private Toggle m_WordWrap;
        private PopupField<string> m_TextOverflow;
        private Toggle m_NodeVisibleToggle;
        private PopupField<string> m_NodeComponent;
        private TextField m_BindingName;
        private Toggle m_Interactable;
        private VisualElement m_ToggleOptions;
        private Toggle m_ToggleValue;
        private VisualElement m_SliderOptions;
        private FloatField m_SliderMinValue;
        private FloatField m_SliderMaxValue;
        private FloatField m_SliderValue;
        private Toggle m_SliderWholeNumbers;
        private VisualElement m_ScrollOptions;
        private Toggle m_ScrollHorizontal;
        private Toggle m_ScrollVertical;
        private Toggle m_NodeSharedToggle;
        private Toggle m_NineSliceToggle;
        private IntegerField m_BorderLeft;
        private IntegerField m_BorderBottom;
        private IntegerField m_BorderRight;
        private IntegerField m_BorderTop;
        private Label m_GenerationSummary;
        private Button m_GenerateButton;
        private ProgressBar m_ProgressBar;
        private Label m_StatusLabel;

        [MenuItem("GameDeveloperKit/" + WindowTitle)]
        public static void Open()
        {
            var window = GetWindow<DesignImporterWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(1160f, 700f);
            window.Show();
        }

        public void CreateGUI()
        {
            m_ProjectConfig = EditorGlobalConfig.LoadOrCreate();
            m_UserConfig = EditorUserConfig.LoadOrCreate();
            MigrateLegacyPreferences();
            BuildLayout();
            RefreshSourceControls();
            RefreshDocument();
            TryLoadConfiguredCache();
        }

        private void OnFocus()
        {
            if (m_ProjectConfig == null || rootVisualElement.Q("design-importer-root") == null)
            {
                return;
            }

            m_ProjectConfig = EditorGlobalConfig.LoadOrCreate();
            m_UserConfig = EditorUserConfig.LoadOrCreate();
            RefreshSourceControls();
            RefreshGenerationSummary();
        }

        private void OnDisable()
        {
            CancelCurrentAction();
            m_PreviewCancellation?.Cancel();
            m_PreviewCancellation?.Dispose();
            m_PreviewCancellation = null;
            m_LanhuBridge?.Dispose();
            m_LanhuBridge = null;
            ClearDragState();
            DestroyPageThumbnails();
            DestroyPreviewTexture();
        }

        private void BuildLayout()
        {
            rootVisualElement.Clear();
            var styleSheet = GameDeveloperKitEditorPaths.LoadPackageAsset<StyleSheet>(StylePath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            var root = new VisualElement { name = "design-importer-root" };
            root.AddToClassList("design-importer");
            root.EnableInClassList("theme-dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("theme-light", !EditorGUIUtility.isProSkin);
            rootVisualElement.Add(root);
            root.Add(CreateSourceToolbar());

            m_WelcomeView = CreateWelcomeView();
            root.Add(m_WelcomeView);

            m_Workspace = new VisualElement { name = "workspace" };
            m_Workspace.AddToClassList("workspace");
            m_Workspace.Add(CreatePagePane());
            m_Workspace.Add(CreatePreviewPane());
            m_Workspace.Add(CreateInspectorPane());
            root.Add(m_Workspace);
            root.Add(CreateStatusBar());

            m_DragPreview = new VisualElement { name = "drag-preview", pickingMode = PickingMode.Ignore };
            m_DragPreviewNode = new Label { name = "drag-preview-node", pickingMode = PickingMode.Ignore };
            m_DragPreviewAction = new Label { name = "drag-preview-action", pickingMode = PickingMode.Ignore };
            m_DragPreview.Add(m_DragPreviewNode);
            m_DragPreview.Add(m_DragPreviewAction);
            m_DragPreview.style.display = DisplayStyle.None;
            root.Add(m_DragPreview);
        }

        private VisualElement CreateSourceToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("source-toolbar");
            toolbar.Add(new Label("UI Prefab Studio") { name = "brand" });
            m_BreadcrumbHome = new ToolbarButton(ShowPageBrowser)
            {
                text = "设计稿",
                name = "breadcrumb-home"
            };
            toolbar.Add(m_BreadcrumbHome);
            m_BreadcrumbSeparator = new Label("/") { name = "breadcrumb-separator" };
            toolbar.Add(m_BreadcrumbSeparator);
            m_BreadcrumbPage = new Label { name = "breadcrumb-page" };
            toolbar.Add(m_BreadcrumbPage);

            var spacer = new VisualElement { name = "toolbar-spacer" };
            toolbar.Add(spacer);
            m_ConfigSummary = new Label { name = "configured-source" };
            toolbar.Add(m_ConfigSummary);

            m_LoadButton = CreateToolbarButton("同步", ImportConfiguredSource, "按全局设置读取设计稿");
            toolbar.Add(m_LoadButton);
            m_LoadCacheButton = CreateToolbarButton("加载缓存", LoadLanhuCache, "加载该蓝湖项目上次同步的本地版本");
            toolbar.Add(m_LoadCacheButton);
            toolbar.Add(CreateToolbarButton("全局设置", MainWindow.OpenGlobalConfiguration, "打开配置表编辑器的全局设置"));
            m_CancelButton = CreateToolbarButton("取消", CancelCurrentAction, "取消当前网络或生成任务");
            m_CancelButton.style.display = DisplayStyle.None;
            toolbar.Add(m_CancelButton);
            return toolbar;
        }

        private VisualElement CreateWelcomeView()
        {
            var welcome = new VisualElement { name = "welcome-view" };
            welcome.AddToClassList("welcome-view");
            var content = new VisualElement { name = "welcome-content" };
            content.Add(new Label("UI Prefab Studio") { name = "welcome-title" });
            content.Add(new Label("尚未载入设计稿") { name = "welcome-subtitle" });
            var actions = new VisualElement { name = "welcome-actions" };
            m_WelcomeImportButton = new Button(ImportConfiguredSource) { name = "welcome-import-button" };
            actions.Add(m_WelcomeImportButton);
            m_WelcomeCacheButton = new Button(LoadLanhuCache)
            {
                text = "加载本地缓存",
                name = "welcome-cache-button"
            };
            actions.Add(m_WelcomeCacheButton);
            actions.Add(new Button(MainWindow.OpenGlobalConfiguration)
            {
                text = "全局设置",
                name = "welcome-settings-button"
            });
            content.Add(actions);
            welcome.Add(content);
            return welcome;
        }

        private VisualElement CreatePagePane()
        {
            var pane = new VisualElement { name = "page-pane" };
            pane.AddToClassList("pane");
            pane.AddToClassList("page-pane");

            m_PageBrowserView = new VisualElement { name = "page-browser" };
            m_PageBrowserView.AddToClassList("browser-view");
            m_PageList = new ListView
            {
                name = "page-list",
                selectionType = SelectionType.Single,
                fixedItemHeight = 82f,
                makeItem = MakePageRow,
                bindItem = BindPageRow
            };
            m_PageList.selectionChanged += selection =>
            {
                if (m_ResettingPageSelection)
                {
                    return;
                }

                var page = selection.OfType<DesignPageListItem>().FirstOrDefault()?.Page;
                if (ReferenceEquals(page, m_SelectedPage))
                {
                    return;
                }

                SelectPage(page);
            };
            m_PageBrowserView.Add(m_PageList);
            pane.Add(m_PageBrowserView);

            m_LayerBrowserView = new VisualElement { name = "layer-browser" };
            m_LayerBrowserView.AddToClassList("browser-view");
            var layerHeader = new VisualElement { name = "layer-header" };
            layerHeader.AddToClassList("layer-header");
            var layerHeaderTop = new VisualElement();
            layerHeaderTop.AddToClassList("layer-header__top");
            layerHeaderTop.Add(new Label("图层") { name = "layer-title" });
            var restore = new Button(RestoreSelectedPageMapping)
            {
                text = "恢复",
                tooltip = "恢复设计稿原始层级和属性"
            };
            layerHeaderTop.Add(restore);
            layerHeader.Add(layerHeaderTop);
            m_DragHint = new Label { name = "drag-hint" };
            m_DragHint.style.display = DisplayStyle.None;
            layerHeader.Add(m_DragHint);
            m_LayerBrowserView.Add(layerHeader);

            m_NodeList = new ListView
            {
                name = "node-list",
                selectionType = SelectionType.Single,
                fixedItemHeight = 28f,
                makeItem = MakeNodeRow,
                bindItem = BindNodeRow
            };
            m_NodeList.selectionChanged += selection =>
            {
                var row = selection.OfType<DesignNodeRow>().FirstOrDefault();
                SelectNode(row?.Node, false);
            };
            m_LayerBrowserView.Add(m_NodeList);
            pane.Add(m_LayerBrowserView);
            return pane;
        }

        private VisualElement CreatePreviewPane()
        {
            var pane = new VisualElement { name = "preview-pane" };
            pane.AddToClassList("preview-pane");
            var header = new VisualElement();
            header.AddToClassList("preview-toolbar");
            m_PreviewTitle = new Label("预览") { name = "preview-title" };
            header.Add(m_PreviewTitle);
            header.Add(new Button(() =>
            {
                m_ZoomSlider.SetValueWithoutNotify(1f);
                m_ZoomLabel.text = "100%";
                m_Preview.ResetPan();
                RefreshPreview();
            })
            {
                text = "适应",
                name = "zoom-fit-button",
                tooltip = "恢复适应窗口并重置平移"
            });
            header.Add(new Label("缩放"));
            m_ZoomSlider = new Slider(0.25f, 2f) { value = 1f, name = "zoom-slider" };
            m_ZoomSlider.RegisterValueChangedCallback(evt =>
            {
                m_ZoomLabel.text = $"{Mathf.RoundToInt(evt.newValue * 100f)}%";
                RefreshPreview();
            });
            header.Add(m_ZoomSlider);
            m_ZoomLabel = new Label("100%") { name = "zoom-value" };
            header.Add(m_ZoomLabel);
            pane.Add(header);

            m_Preview = new DesignPreviewElement { name = "design-preview" };
            m_Preview.NodeSelected += node =>
            {
                if (m_LayerBrowserActive)
                {
                    SelectNode(node);
                }
            };
            m_Preview.ZoomRequested += value => m_ZoomSlider.value = value;
            pane.Add(m_Preview);
            return pane;
        }

        private VisualElement CreateInspectorPane()
        {
            var pane = new ScrollView(ScrollViewMode.Vertical) { name = "inspector-pane" };
            pane.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            pane.AddToClassList("pane");
            pane.AddToClassList("inspector-pane");
            pane.Add(CreateSectionTitle("图层属性"));

            m_NodeSummary = new Label("选择左侧图层后编辑") { name = "node-summary" };
            pane.Add(m_NodeSummary);

            m_NodeNameField = new TextField("名称");
            m_NodeNameField.RegisterValueChangedCallback(evt =>
                MutateSelectedNode(node => node.Name = string.IsNullOrWhiteSpace(evt.newValue) ? node.Name : evt.newValue));
            pane.Add(m_NodeNameField);

            m_NodeVisibleToggle = new Toggle("生成节点");
            m_NodeVisibleToggle.RegisterValueChangedCallback(evt =>
                MutateSelectedNode(node => node.Visible = evt.newValue));
            pane.Add(m_NodeVisibleToggle);

            var positionRow = new VisualElement();
            positionRow.AddToClassList("field-row");
            m_NodeX = CreateNodeFloatField("X", (node, value) => node.X = value);
            m_NodeY = CreateNodeFloatField("Y", (node, value) => node.Y = value);
            m_NodeX.AddToClassList("compact-field");
            m_NodeY.AddToClassList("compact-field");
            positionRow.Add(m_NodeX);
            positionRow.Add(m_NodeY);

            var sizeRow = new VisualElement();
            sizeRow.AddToClassList("field-row");
            m_NodeWidth = CreateNodeFloatField("宽", (node, value) => node.Width = Mathf.Max(1f, value));
            m_NodeHeight = CreateNodeFloatField("高", (node, value) => node.Height = Mathf.Max(1f, value));
            m_NodeWidth.AddToClassList("compact-field");
            m_NodeHeight.AddToClassList("compact-field");
            sizeRow.Add(m_NodeWidth);
            sizeRow.Add(m_NodeHeight);
            pane.Add(positionRow);
            pane.Add(sizeRow);

            var anchorSection = new VisualElement { name = "anchor-section" };
            anchorSection.Add(CreateSectionTitle("锚点"));
            var anchorEditor = new VisualElement { name = "anchor-editor" };
            m_AnchorPresetEditor = new AnchorPresetElement { name = "anchor-preset-editor" };
            m_AnchorPresetEditor.PresetSelected += ApplyAnchorPreset;
            anchorEditor.Add(m_AnchorPresetEditor);
            var anchorValues = new VisualElement { name = "anchor-values" };
            m_AnchorMinX = CreateAnchorFloatField(AnchorValue.AnchorMinX);
            m_AnchorMinY = CreateAnchorFloatField(AnchorValue.AnchorMinY);
            m_AnchorMaxX = CreateAnchorFloatField(AnchorValue.AnchorMaxX);
            m_AnchorMaxY = CreateAnchorFloatField(AnchorValue.AnchorMaxY);
            m_PivotX = CreateAnchorFloatField(AnchorValue.PivotX);
            m_PivotY = CreateAnchorFloatField(AnchorValue.PivotY);
            anchorValues.Add(CreateAnchorValueRow("Min", m_AnchorMinX, m_AnchorMinY));
            anchorValues.Add(CreateAnchorValueRow("Max", m_AnchorMaxX, m_AnchorMaxY));
            anchorValues.Add(CreateAnchorValueRow("Pivot", m_PivotX, m_PivotY));
            anchorEditor.Add(anchorValues);
            anchorSection.Add(anchorEditor);
            pane.Add(anchorSection);

            pane.Add(CreateSectionTitle("交互与绑定"));
            var componentChoices = new List<string>
            {
                "无",
                "Button",
                "Toggle",
                "Slider",
                "InputField",
                "ScrollRect"
            };
            m_NodeComponent = new PopupField<string>("组件", componentChoices, 0);
            m_NodeComponent.RegisterValueChangedCallback(evt =>
            {
                var component = ParseComponentKind(evt.newValue);
                MutateSelectedNode(node =>
                {
                    node.Component = component;
                    if (component != DesignComponentKind.None && string.IsNullOrWhiteSpace(node.BindingName))
                    {
                        node.BindingName = CreateDefaultBindingName(node, component);
                    }
                });
                RefreshNodeInspector();
            });
            pane.Add(m_NodeComponent);

            m_BindingName = new TextField("绑定 Key")
            {
                tooltip = "写入 UIDocument mapping；留空表示不生成绑定"
            };
            m_BindingName.RegisterValueChangedCallback(evt =>
                MutateSelectedNode(node => node.BindingName = evt.newValue?.Trim() ?? string.Empty));
            pane.Add(m_BindingName);

            m_Interactable = new Toggle("可交互");
            m_Interactable.RegisterValueChangedCallback(evt =>
                MutateSelectedNode(node => node.Interactable = evt.newValue));
            pane.Add(m_Interactable);

            m_ToggleOptions = new VisualElement { name = "toggle-options" };
            m_ToggleValue = new Toggle("默认选中");
            m_ToggleValue.RegisterValueChangedCallback(evt =>
                MutateSelectedNode(node => node.ToggleValue = evt.newValue));
            m_ToggleOptions.Add(m_ToggleValue);
            pane.Add(m_ToggleOptions);

            m_SliderOptions = new VisualElement { name = "slider-options" };
            m_SliderMinValue = new FloatField("最小值");
            m_SliderMaxValue = new FloatField("最大值");
            m_SliderValue = new FloatField("默认值");
            m_SliderWholeNumbers = new Toggle("整数");
            m_SliderMinValue.RegisterValueChangedCallback(evt => MutateSelectedNode(node =>
            {
                node.SliderMinValue = evt.newValue;
                node.SliderMaxValue = Mathf.Max(node.SliderMaxValue, node.SliderMinValue);
                node.SliderValue = Mathf.Clamp(node.SliderValue, node.SliderMinValue, node.SliderMaxValue);
            }));
            m_SliderMaxValue.RegisterValueChangedCallback(evt => MutateSelectedNode(node =>
            {
                node.SliderMaxValue = Mathf.Max(node.SliderMinValue, evt.newValue);
                node.SliderValue = Mathf.Clamp(node.SliderValue, node.SliderMinValue, node.SliderMaxValue);
            }));
            m_SliderValue.RegisterValueChangedCallback(evt => MutateSelectedNode(node =>
                node.SliderValue = Mathf.Clamp(evt.newValue, node.SliderMinValue, node.SliderMaxValue)));
            m_SliderWholeNumbers.RegisterValueChangedCallback(evt =>
                MutateSelectedNode(node => node.SliderWholeNumbers = evt.newValue));
            m_SliderOptions.Add(m_SliderMinValue);
            m_SliderOptions.Add(m_SliderMaxValue);
            m_SliderOptions.Add(m_SliderValue);
            m_SliderOptions.Add(m_SliderWholeNumbers);
            pane.Add(m_SliderOptions);

            m_ScrollOptions = new VisualElement { name = "scroll-options" };
            m_ScrollHorizontal = new Toggle("横向滚动");
            m_ScrollVertical = new Toggle("纵向滚动");
            m_ScrollHorizontal.RegisterValueChangedCallback(evt =>
                MutateSelectedNode(node => node.ScrollHorizontal = evt.newValue));
            m_ScrollVertical.RegisterValueChangedCallback(evt =>
                MutateSelectedNode(node => node.ScrollVertical = evt.newValue));
            m_ScrollOptions.Add(m_ScrollHorizontal);
            m_ScrollOptions.Add(m_ScrollVertical);
            pane.Add(m_ScrollOptions);

            m_ImageInspector = new VisualElement { name = "image-inspector" };
            m_ImageInspector.Add(CreateSectionTitle("图片"));
            m_NodeSharedToggle = new Toggle("公共资源");
            m_NineSliceToggle = new Toggle("九宫格");
            m_NodeSharedToggle.RegisterValueChangedCallback(evt =>
                MutateSelectedNode(node => node.Shared = evt.newValue));
            m_NineSliceToggle.RegisterValueChangedCallback(evt =>
            {
                MutateSelectedNode(node =>
                {
                    node.NineSlice = evt.newValue;
                    if (evt.newValue && !node.Border.HasValue)
                    {
                        node.Border.Left = node.Border.Bottom = node.Border.Right = node.Border.Top = 16f;
                    }
                });
                RefreshNodeInspector();
            });
            m_ImageInspector.Add(m_NodeSharedToggle);
            m_ImageInspector.Add(m_NineSliceToggle);
            var borderGrid = new VisualElement();
            borderGrid.AddToClassList("border-grid");
            m_BorderLeft = CreateBorderField("左");
            m_BorderBottom = CreateBorderField("下");
            m_BorderRight = CreateBorderField("右");
            m_BorderTop = CreateBorderField("上");
            borderGrid.Add(m_BorderLeft);
            borderGrid.Add(m_BorderBottom);
            borderGrid.Add(m_BorderRight);
            borderGrid.Add(m_BorderTop);
            m_ImageInspector.Add(borderGrid);
            pane.Add(m_ImageInspector);

            m_TextInspector = new VisualElement { name = "text-inspector" };
            m_TextInspector.Add(CreateSectionTitle("文本"));
            m_NodeText = new TextField("内容") { multiline = true };
            m_FontName = new TextField("字体");
            m_FontStyleName = new TextField("字形");
            m_FontSize = new FloatField("字号");
            m_FontBold = new Toggle("粗体");
            m_FontItalic = new Toggle("斜体");
            m_TextColor = new ColorField("颜色");
            m_TextAlignment = new PopupField<string>("对齐", new List<string> { "left", "center", "right", "justified" }, 0);
            m_Tracking = new FloatField("字距");
            m_LineHeight = new FloatField("行高");
            m_WordWrap = new Toggle("自动换行");
            m_TextOverflow = new PopupField<string>("溢出", new List<string> { "overflow", "ellipsis", "truncate", "masking" }, 0);
            m_NodeText.RegisterValueChangedCallback(evt => MutateSelectedNode(node => node.Text = evt.newValue ?? string.Empty));
            m_FontName.RegisterValueChangedCallback(evt => MutateSelectedNode(node => node.FontName = evt.newValue ?? string.Empty));
            m_FontStyleName.RegisterValueChangedCallback(evt => MutateSelectedNode(node => node.FontStyleName = evt.newValue ?? string.Empty));
            m_FontSize.RegisterValueChangedCallback(evt => MutateSelectedNode(node => node.FontSize = Mathf.Max(1f, evt.newValue)));
            m_FontBold.RegisterValueChangedCallback(evt => MutateSelectedNode(node => node.Bold = evt.newValue));
            m_FontItalic.RegisterValueChangedCallback(evt => MutateSelectedNode(node => node.Italic = evt.newValue));
            m_TextColor.RegisterValueChangedCallback(evt => MutateSelectedNode(node => node.Color = "#" + ColorUtility.ToHtmlStringRGBA(evt.newValue)));
            m_TextAlignment.RegisterValueChangedCallback(evt => MutateSelectedNode(node => node.TextAlignment = evt.newValue));
            m_Tracking.RegisterValueChangedCallback(evt => MutateSelectedNode(node => node.Tracking = evt.newValue));
            m_LineHeight.RegisterValueChangedCallback(evt => MutateSelectedNode(node => node.LineHeight = Mathf.Max(0f, evt.newValue)));
            m_WordWrap.RegisterValueChangedCallback(evt => MutateSelectedNode(node => node.WordWrap = evt.newValue));
            m_TextOverflow.RegisterValueChangedCallback(evt => MutateSelectedNode(node => node.Overflow = evt.newValue));
            m_TextInspector.Add(m_NodeText);
            m_TextInspector.Add(m_FontName);
            m_TextInspector.Add(m_FontStyleName);
            m_TextInspector.Add(m_FontSize);
            var fontFlags = new VisualElement();
            fontFlags.AddToClassList("toggle-row");
            fontFlags.Add(m_FontBold);
            fontFlags.Add(m_FontItalic);
            m_TextInspector.Add(fontFlags);
            m_TextInspector.Add(m_TextColor);
            m_TextInspector.Add(m_TextAlignment);
            m_TextInspector.Add(m_Tracking);
            m_TextInspector.Add(m_LineHeight);
            m_TextInspector.Add(m_WordWrap);
            m_TextInspector.Add(m_TextOverflow);
            pane.Add(m_TextInspector);

            pane.Add(CreateSectionTitle("生成"));
            m_GenerationSummary = new Label { name = "generation-summary" };
            pane.Add(m_GenerationSummary);

            m_GenerateButton = new Button(() => Run(GenerateAsync()))
            {
                text = "生成",
                name = "generate-button"
            };
            pane.Add(m_GenerateButton);
            return pane;
        }
        private VisualElement CreateStatusBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("status-bar");
            m_StatusLabel = new Label("就绪") { name = "status-label" };
            m_ProgressBar = new ProgressBar { value = 0f, name = "progress-bar" };
            bar.Add(m_StatusLabel);
            bar.Add(m_ProgressBar);
            return bar;
        }

        private static Button CreateToolbarButton(string text, Action action, string tooltip)
        {
            return new ToolbarButton(action) { text = text, tooltip = tooltip };
        }

        private static Label CreateSectionTitle(string text)
        {
            var label = new Label(text);
            label.AddToClassList("section-title");
            return label;
        }

        private IntegerField CreateBorderField(string label)
        {
            var field = new IntegerField(label);
            field.RegisterValueChangedCallback(evt =>
            {
                if (m_Refreshing || m_SelectedNode?.Border == null)
                {
                    return;
                }

                var value = Mathf.Max(0, evt.newValue);
                var border = m_SelectedNode.Border;
                if (ReferenceEquals(field, m_BorderLeft)) border.Left = value;
                else if (ReferenceEquals(field, m_BorderBottom)) border.Bottom = value;
                else if (ReferenceEquals(field, m_BorderRight)) border.Right = value;
                else if (ReferenceEquals(field, m_BorderTop)) border.Top = value;
                m_SelectedNode.NineSlice = border.HasValue;
                SaveSelectedPageMapping();
                RefreshNodeInspector();
            });
            return field;
        }

        private VisualElement MakePageRow()
        {
            var row = new VisualElement();
            row.AddToClassList("page-row");
            row.Add(new DesignPageThumbnailElement { name = "page-thumbnail" });
            var labels = new VisualElement();
            labels.AddToClassList("page-row__labels");
            labels.Add(new Label { name = "page-name" });
            labels.Add(new Label { name = "page-size" });
            row.Add(labels);
            row.Add(new Label { name = "page-status" });
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.clickCount == 2 && row.userData is DesignPageListItem item)
                {
                    EnterPageEditor(item.Page);
                    evt.StopPropagation();
                }
            });
            row.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                if (row.userData is not DesignPageListItem item)
                {
                    return;
                }

                evt.menu.AppendAction("编辑图层", _ => EnterPageEditor(item.Page));
                if (item.Kind != DesignChangeKind.Deleted)
                {
                    evt.menu.AppendAction("恢复设计稿原始层级", _ =>
                    {
                        SelectPage(item.Page);
                        RestoreSelectedPageMapping();
                    });
                }
            }));
            return row;
        }

        private void BindPageRow(VisualElement element, int index)
        {
            var item = m_PageItems[index];
            var page = item.Page;
            element.userData = item;
            element.Q<DesignPageThumbnailElement>("page-thumbnail")
                .SetPage(page, GetPageThumbnail(page));
            element.Q<Label>("page-name").text = page.Name;
            element.Q<Label>("page-size").text = string.IsNullOrWhiteSpace(page.RevisionTimestamp)
                ? $"{page.Width:0} x {page.Height:0}"
                : $"{page.Width:0} x {page.Height:0} · {page.RevisionTimestamp}";
            BindStatusBadge(element.Q<Label>("page-status"), item.Kind);
        }

        private VisualElement MakeNodeRow()
        {
            var element = new VisualElement();
            element.AddToClassList("node-row");
            var expandButton = new Button { name = "node-expand" };
            expandButton.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            expandButton.clicked += () =>
            {
                if (expandButton.userData is DesignNode node)
                {
                    ToggleNodeExpansion(node);
                }
            };
            element.Add(expandButton);
            element.Add(new Label { name = "node-row-label" });
            var dragLabel = new Label { name = "node-drag-label", pickingMode = PickingMode.Ignore };
            dragLabel.style.display = DisplayStyle.None;
            element.Add(dragLabel);
            var dropLabel = new Label { name = "node-drop-label", pickingMode = PickingMode.Ignore };
            dropLabel.style.display = DisplayStyle.None;
            element.Add(dropLabel);
            element.Add(new Label { name = "node-status" });
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || element.userData is not DesignNodeRow row)
                {
                    return;
                }

                ClearDragState();
                m_DraggedNode = row.Node;
                m_DragSourceElement = element;
                m_DragStart = new Vector2(evt.position.x, evt.position.y);
                element.CapturePointer(evt.pointerId);
            });
            element.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (m_DraggedNode == null || m_DragSourceElement != element ||
                    Vector2.Distance(m_DragStart, new Vector2(evt.position.x, evt.position.y)) < 5f)
                {
                    return;
                }

                if (!m_DragActive)
                {
                    m_DragActive = true;
                    element.AddToClassList("node-row--dragging");
                    var dragLabel = element.Q<Label>("node-drag-label");
                    if (dragLabel != null)
                    {
                        dragLabel.text = "拖动中";
                        dragLabel.style.display = DisplayStyle.Flex;
                    }

                    m_DragPreviewNode.text = $"正在移动  {m_DraggedNode.Name}";
                    m_DragPreviewAction.text = "拖到目标图层上";
                    m_DragPreview.style.display = DisplayStyle.Flex;
                }

                var panelPosition = new Vector2(evt.position.x, evt.position.y);
                UpdateDragPreview(panelPosition);
                UpdateDragTarget(panelPosition);
                evt.StopPropagation();
            });
            element.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (m_DragSourceElement != element)
                {
                    return;
                }

                var dragged = m_DraggedNode;
                var target = m_DragTargetNode;
                var placement = m_DragPlacement;
                var wasDragging = m_DragActive;
                if (element.HasPointerCapture(evt.pointerId)) element.ReleasePointer(evt.pointerId);
                ClearDragState();
                if (!wasDragging || dragged == null)
                {
                    return;
                }

                if (target != null)
                {
                    MoveLayer(dragged, target, placement);
                }
                evt.StopPropagation();
            });
            element.RegisterCallback<PointerCancelEvent>(evt =>
            {
                if (m_DragSourceElement == element)
                {
                    if (element.HasPointerCapture(evt.pointerId)) element.ReleasePointer(evt.pointerId);
                    ClearDragState();
                }
            });
            return element;
        }

        private void UpdateDragTarget(Vector2 panelPosition)
        {
            var picked = m_NodeList?.panel?.Pick(panelPosition);
            while (picked != null && picked.userData is not DesignNodeRow)
            {
                picked = picked.parent;
            }

            var targetRow = picked?.userData as DesignNodeRow;
            var target = targetRow?.Node;
            var placement = ResolveDropPlacement(picked, panelPosition);
            if (!IsValidDropTarget(m_DraggedNode, target, placement))
            {
                ClearDragTarget();
                return;
            }

            if (ReferenceEquals(m_DragTargetElement, picked) &&
                ReferenceEquals(m_DragTargetNode, target) &&
                m_DragPlacement == placement)
            {
                return;
            }

            ClearDragTarget();
            m_DragTargetElement = picked;
            m_DragTargetNode = target;
            m_DragPlacement = placement;
            picked.AddToClassList(placement switch
            {
                LayerDropPlacement.Before => "node-row--drop-before",
                LayerDropPlacement.After => "node-row--drop-after",
                _ => "node-row--drop-inside"
            });
            var actionText = placement switch
            {
                LayerDropPlacement.Before => $"插入到：{target.Name} 前",
                LayerDropPlacement.After => $"插入到：{target.Name} 后",
                _ => $"作为子层级：{target.Name}"
            };
            var dropLabel = picked.Q<Label>("node-drop-label");
            if (dropLabel != null)
            {
                dropLabel.text = placement switch
                {
                    LayerDropPlacement.Before => "插入到此行前",
                    LayerDropPlacement.After => "插入到此行后",
                    _ => "作为子层级"
                };
                dropLabel.style.display = DisplayStyle.Flex;
            }

            m_DragHint.text = actionText;
            m_DragPreviewAction.text = actionText;
            m_DragHint.style.display = DisplayStyle.Flex;
        }

        private void UpdateDragPreview(Vector2 panelPosition)
        {
            if (m_DragPreview == null)
            {
                return;
            }

            var local = m_DragPreview.parent.WorldToLocal(panelPosition);
            var parentRect = m_DragPreview.parent.contentRect;
            var left = Mathf.Clamp(local.x + 16f, 8f, Mathf.Max(8f, parentRect.width - 250f));
            var top = Mathf.Clamp(local.y + 18f, 8f, Mathf.Max(8f, parentRect.height - 54f));
            m_DragPreview.style.left = left;
            m_DragPreview.style.top = top;
        }

        private static LayerDropPlacement ResolveDropPlacement(VisualElement targetElement, Vector2 panelPosition)
        {
            if (targetElement == null)
            {
                return LayerDropPlacement.Inside;
            }

            var local = targetElement.WorldToLocal(panelPosition);
            var edge = Mathf.Max(6f, targetElement.worldBound.height * 0.25f);
            if (local.y <= edge)
            {
                return LayerDropPlacement.Before;
            }

            return local.y >= targetElement.worldBound.height - edge
                ? LayerDropPlacement.After
                : LayerDropPlacement.Inside;
        }

        private bool IsValidDropTarget(DesignNode dragged, DesignNode target, LayerDropPlacement placement)
        {
            if (m_SelectedPage?.Root == null || dragged == null || target == null ||
                ReferenceEquals(dragged, m_SelectedPage.Root) || ReferenceEquals(dragged, target))
            {
                return false;
            }

            var newParent = placement == LayerDropPlacement.Inside
                ? target
                : DesignMappingStore.ParentOf(m_SelectedPage, target);
            return newParent != null && !dragged.DescendantsAndSelf().Contains(newParent);
        }

        private void ClearDragTarget()
        {
            var dropLabel = m_DragTargetElement?.Q<Label>("node-drop-label");
            if (dropLabel != null)
            {
                dropLabel.text = string.Empty;
                dropLabel.style.display = DisplayStyle.None;
            }

            m_DragTargetElement?.RemoveFromClassList("node-row--drop-inside");
            m_DragTargetElement?.RemoveFromClassList("node-row--drop-before");
            m_DragTargetElement?.RemoveFromClassList("node-row--drop-after");
            m_DragTargetElement = null;
            m_DragTargetNode = null;
            m_DragPlacement = LayerDropPlacement.Inside;
            if (m_DragHint != null)
            {
                m_DragHint.text = string.Empty;
                m_DragHint.style.display = DisplayStyle.None;
            }

            if (m_DragActive && m_DragPreviewAction != null)
            {
                m_DragPreviewAction.text = "当前无有效落点";
            }
        }

        private void ClearDragState()
        {
            var dragLabel = m_DragSourceElement?.Q<Label>("node-drag-label");
            if (dragLabel != null)
            {
                dragLabel.text = string.Empty;
                dragLabel.style.display = DisplayStyle.None;
            }

            m_DragSourceElement?.RemoveFromClassList("node-row--dragging");
            ClearDragTarget();
            m_DraggedNode = null;
            m_DragSourceElement = null;
            m_DragActive = false;
            if (m_DragPreview != null)
            {
                m_DragPreviewNode.text = string.Empty;
                m_DragPreviewAction.text = string.Empty;
                m_DragPreview.style.display = DisplayStyle.None;
            }
        }

        private void BindNodeRow(VisualElement element, int index)
        {
            var row = m_NodeRows[index];
            element.userData = row;
            var expandButton = element.Q<Button>("node-expand");
            var hasChildren = row.Node.Children != null && row.Node.Children.Any(child => child != null);
            expandButton.userData = row.Node;
            expandButton.style.display = hasChildren ? DisplayStyle.Flex : DisplayStyle.None;
            expandButton.text = m_CollapsedNodeIds.Contains(row.Node.Id) ? ">" : "v";
            expandButton.tooltip = m_CollapsedNodeIds.Contains(row.Node.Id) ? "展开图层" : "收起图层";
            var label = element.Q<Label>("node-row-label");
            label.text = NodeIcon(row.Node.Kind) + "  " + row.Node.Name;
            expandButton.style.marginLeft = 5f + row.Depth * 14f;
            label.style.paddingLeft = hasChildren ? 0f : 20f;
            label.tooltip = row.Node.Id;
            BindStatusBadge(
                element.Q<Label>("node-status"),
                m_Diff?.NodeChange(m_SelectedPage?.Id, row.Node.Id) ?? DesignChangeKind.Unchanged);
        }

        private void MoveLayer(DesignNode dragged, DesignNode target, LayerDropPlacement placement)
        {
            if (m_SelectedPage?.Root == null || ReferenceEquals(dragged, target))
            {
                return;
            }

            var draggedRow = m_AllNodeRows.FirstOrDefault(row => ReferenceEquals(row.Node, dragged));
            var targetRow = m_AllNodeRows.FirstOrDefault(row => ReferenceEquals(row.Node, target));
            if (draggedRow == null || targetRow == null)
            {
                return;
            }

            DesignNode newParent;
            int siblingIndex;
            if (placement == LayerDropPlacement.Inside)
            {
                newParent = target;
                siblingIndex = target.Children.Count;
            }
            else
            {
                newParent = DesignMappingStore.ParentOf(m_SelectedPage, target);
                siblingIndex = (newParent?.Children.IndexOf(target) ?? -1) +
                               (placement == LayerDropPlacement.After ? 1 : 0);
            }

            if (newParent == null)
            {
                return;
            }

            var parentRow = m_AllNodeRows.FirstOrDefault(row => ReferenceEquals(row.Node, newParent));
            var parentX = parentRow?.AbsoluteX ?? 0f;
            var parentY = parentRow?.AbsoluteY ?? 0f;
            if (!DesignMappingStore.MoveNode(m_SelectedPage, dragged, newParent, siblingIndex))
            {
                SetStatus("不能把图层拖入自身或后代。", 0f);
                return;
            }

            dragged.X = draggedRow.AbsoluteX - parentX;
            dragged.Y = draggedRow.AbsoluteY - parentY;
            m_CollapsedNodeIds.Remove(newParent.Id);
            SaveSelectedPageMapping();
            RefreshNodeList();
            SelectNode(dragged);
            SetStatus($"已移动图层“{dragged.Name}”，mapping 已保存。", 0f);
        }

        private static void BindStatusBadge(Label badge, DesignChangeKind kind)
        {
            badge.RemoveFromClassList("status-new");
            badge.RemoveFromClassList("status-updated");
            badge.RemoveFromClassList("status-deleted");
            badge.text = kind switch
            {
                DesignChangeKind.New => "NEW",
                DesignChangeKind.Updated => "UPDATE",
                DesignChangeKind.Deleted => "DELETE",
                _ => string.Empty
            };
            badge.style.display = kind == DesignChangeKind.Unchanged ? DisplayStyle.None : DisplayStyle.Flex;
            if (kind == DesignChangeKind.New) badge.AddToClassList("status-new");
            else if (kind == DesignChangeKind.Updated) badge.AddToClassList("status-updated");
            else if (kind == DesignChangeKind.Deleted) badge.AddToClassList("status-deleted");
        }

        private void RebuildPageItems()
        {
            m_PageItems.Clear();
            if (m_Document == null)
            {
                return;
            }

            if (m_Diff != null && ReferenceEquals(m_Snapshot?.Document, m_Document))
            {
                m_PageItems.AddRange(m_Diff.Pages.Select(change =>
                    new DesignPageListItem(change.Page, change.Kind)));
            }
            else
            {
                m_PageItems.AddRange(m_Document.Pages
                    .Where(page => page != null)
                    .Select(page => new DesignPageListItem(page, DesignChangeKind.Unchanged)));
            }
        }

        private void EnterPageEditor(DesignPage page)
        {
            if (page == null)
            {
                return;
            }

            m_LayerBrowserActive = true;
            UpdateBrowserMode();
            var index = m_PageItems.FindIndex(item => ReferenceEquals(item.Page, page));
            if (index >= 0)
            {
                m_PageList.SetSelection(index);
                m_PageList.ScrollToItem(index);
            }

            SelectPage(page);
            m_NodeList?.Focus();
        }

        private void ShowPageBrowser()
        {
            m_LayerBrowserActive = false;
            ClearDragState();
            m_SelectedNode = null;
            UpdateBrowserMode();
            RefreshNodeInspector();
            RefreshPreview();
        }

        private void UpdateBrowserMode()
        {
            if (m_PageBrowserView == null || m_LayerBrowserView == null)
            {
                return;
            }

            m_PageBrowserView.style.display = m_LayerBrowserActive ? DisplayStyle.None : DisplayStyle.Flex;
            m_LayerBrowserView.style.display = m_LayerBrowserActive ? DisplayStyle.Flex : DisplayStyle.None;
            m_BreadcrumbHome?.SetEnabled(m_LayerBrowserActive);
            if (m_BreadcrumbSeparator != null)
            {
                m_BreadcrumbSeparator.style.display = m_LayerBrowserActive ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (m_BreadcrumbPage != null)
            {
                m_BreadcrumbPage.text = m_SelectedPage?.Name ?? string.Empty;
                m_BreadcrumbPage.style.display = m_LayerBrowserActive ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void RestoreSelectedPageMapping()
        {
            if (m_SelectedPage == null || string.IsNullOrWhiteSpace(m_ProjectCacheRoot))
            {
                SetStatus("当前设计稿没有可恢复的蓝湖 mapping。", 0f);
                return;
            }

            var pageId = m_SelectedPage.Id;
            DesignMappingStore.Reset(m_ProjectCacheRoot, pageId);
            var snapshot = new DesignCacheStore().LoadLatest(m_ProjectConfig.UiPrefabStudio.LanhuProjectUrl);
            SetSnapshot(snapshot);
            var page = m_PageItems.FirstOrDefault(item => item.Page.Id == pageId)?.Page;
            if (page != null) EnterPageEditor(page);
            SetStatus("已恢复设计稿原始层级和属性。", 0f);
        }

        private FloatField CreateNodeFloatField(string label, Action<DesignNode, float> setter)
        {
            var field = new FloatField(label);
            field.RegisterValueChangedCallback(evt => MutateSelectedNode(node => setter(node, evt.newValue)));
            return field;
        }

        private static VisualElement CreateAnchorValueRow(string label, FloatField xField, FloatField yField)
        {
            var row = new VisualElement();
            row.AddToClassList("anchor-value-row");
            row.Add(new Label(label) { name = "anchor-value-label" });
            var pair = new VisualElement();
            pair.AddToClassList("anchor-value-pair");
            pair.Add(new Label("X") { name = "anchor-axis-label" });
            xField.AddToClassList("anchor-coordinate-field");
            yField.AddToClassList("anchor-coordinate-field");
            pair.Add(xField);
            pair.Add(new Label("Y") { name = "anchor-axis-label" });
            pair.Add(yField);
            row.Add(pair);
            return row;
        }

        private FloatField CreateAnchorFloatField(AnchorValue value)
        {
            var field = new FloatField
            {
                name = AnchorValueFieldName(value),
                tooltip = "锚点数值"
            };
            field.RegisterValueChangedCallback(evt => UpdateAnchorValue(value, evt.newValue));
            return field;
        }

        private static string AnchorValueFieldName(AnchorValue value)
        {
            return value switch
            {
                AnchorValue.AnchorMinX => "anchor-min-x",
                AnchorValue.AnchorMinY => "anchor-min-y",
                AnchorValue.AnchorMaxX => "anchor-max-x",
                AnchorValue.AnchorMaxY => "anchor-max-y",
                AnchorValue.PivotX => "pivot-x",
                AnchorValue.PivotY => "pivot-y",
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
            };
        }

        private void UpdateAnchorValue(AnchorValue value, float newValue)
        {
            if (m_Refreshing || m_SelectedNode == null)
            {
                return;
            }

            MutateSelectedNode(node =>
            {
                var anchors = ResolveInspectorAnchors(node);
                var pivot = ResolveInspectorPivot(node);
                var clamped = Mathf.Clamp01(newValue);
                switch (value)
                {
                    case AnchorValue.AnchorMinX:
                        anchors.Min.x = Mathf.Min(clamped, anchors.Max.x);
                        break;
                    case AnchorValue.AnchorMinY:
                        anchors.Min.y = Mathf.Min(clamped, anchors.Max.y);
                        break;
                    case AnchorValue.AnchorMaxX:
                        anchors.Max.x = Mathf.Max(clamped, anchors.Min.x);
                        break;
                    case AnchorValue.AnchorMaxY:
                        anchors.Max.y = Mathf.Max(clamped, anchors.Min.y);
                        break;
                    case AnchorValue.PivotX:
                        pivot.x = clamped;
                        break;
                    case AnchorValue.PivotY:
                        pivot.y = clamped;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(value), value, null);
                }

                node.AnchorMin = ToDesignVector(anchors.Min);
                node.AnchorMax = ToDesignVector(anchors.Max);
                node.Pivot = ToDesignVector(pivot);
            });
            RefreshNodeInspector();
        }

        private void ApplyAnchorPreset(AnchorPreset preset)
        {
            if (m_SelectedNode == null)
            {
                return;
            }

            MutateSelectedNode(node =>
            {
                node.AnchorMin = ToDesignVector(preset.AnchorMin);
                node.AnchorMax = ToDesignVector(preset.AnchorMax);
                node.Pivot = ToDesignVector(preset.Pivot);
            });
            RefreshNodeInspector();
        }

        private static DesignVector2 ToDesignVector(Vector2 value)
        {
            return new DesignVector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
        }

        private static DesignComponentKind ParseComponentKind(string value)
        {
            return value switch
            {
                "Button" => DesignComponentKind.Button,
                "Toggle" => DesignComponentKind.Toggle,
                "Slider" => DesignComponentKind.Slider,
                "InputField" => DesignComponentKind.InputField,
                "ScrollRect" => DesignComponentKind.ScrollRect,
                _ => DesignComponentKind.None
            };
        }

        private static string ComponentChoice(DesignComponentKind component)
        {
            return component == DesignComponentKind.None ? "无" : component.ToString();
        }

        private static string CreateDefaultBindingName(DesignNode node, DesignComponentKind component)
        {
            var prefix = component switch
            {
                DesignComponentKind.Button => "btn",
                DesignComponentKind.Toggle => "toggle",
                DesignComponentKind.Slider => "slider",
                DesignComponentKind.InputField => "input",
                DesignComponentKind.ScrollRect => "scroll",
                _ => "node"
            };
            var name = node?.Name ?? string.Empty;
            var characters = new List<char>(name.Length);
            var separatorPending = false;
            foreach (var character in name)
            {
                var isAsciiLetterOrDigit =
                    (character >= 'a' && character <= 'z') ||
                    (character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9');
                if (isAsciiLetterOrDigit)
                {
                    if (separatorPending && characters.Count > 0)
                    {
                        characters.Add('_');
                    }

                    characters.Add(char.ToLowerInvariant(character));
                    separatorPending = false;
                }
                else
                {
                    separatorPending = true;
                }
            }

            var stem = characters.Count == 0 ? "layer" : new string(characters.ToArray()).Trim('_');
            return $"b_{prefix}_{stem}_{StableBindingSuffix(node?.Id)}";
        }

        private static string StableBindingSuffix(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (var character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619;
                }

                return hash.ToString("x8");
            }
        }

        private static string ShortRevision(string revision)
        {
            return string.IsNullOrWhiteSpace(revision)
                ? "无版本"
                : revision.Substring(0, Mathf.Min(8, revision.Length));
        }

        private void RefreshSourceControls()
        {
            if (m_ProjectConfig == null || m_LoadButton == null)
            {
                return;
            }

            var source = m_ProjectConfig.UiPrefabStudio.Source;
            var figma = source == UiPrefabStudioProjectConfig.FigmaSource;
            var lanhu = source == UiPrefabStudioProjectConfig.LanhuSource;
            var sourceLabel = figma ? "Figma" : lanhu ? "蓝湖" : "JSON 清单";
            m_LoadButton.text = figma ? "导入 Figma" : lanhu ? "同步蓝湖" : "导入清单";
            m_LoadButton.tooltip = figma
                ? "按全局设置从 Figma 官方 API 读取设计稿"
                : lanhu
                    ? "读取蓝湖图层、版本和切图并写入本地缓存"
                    : "导入设计清单 JSON";
            m_LoadCacheButton.style.display = lanhu ? DisplayStyle.Flex : DisplayStyle.None;
            m_ConfigSummary.text = sourceLabel;
            m_ConfigSummary.tooltip = "来源与默认生成参数在配置表编辑器 > 全局设置中维护";
            if (m_WelcomeImportButton != null)
            {
                m_WelcomeImportButton.text = m_LoadButton.text;
                m_WelcomeImportButton.tooltip = m_LoadButton.tooltip;
            }

            if (m_WelcomeCacheButton != null)
            {
                m_WelcomeCacheButton.style.display = lanhu ? DisplayStyle.Flex : DisplayStyle.None;
            }

            RefreshGenerationSummary();
        }

        private void ImportConfiguredSource()
        {
            var source = m_ProjectConfig?.UiPrefabStudio.Source;
            if (source == UiPrefabStudioProjectConfig.FigmaSource)
            {
                Run(LoadFigmaAsync());
            }
            else if (source == UiPrefabStudioProjectConfig.LanhuSource)
            {
                Run(SyncLanhuAsync());
            }
            else
            {
                ImportManifest();
            }
        }

        private void MigrateLegacyPreferences()
        {
            var projectChanged = false;
            var userChanged = false;
            var studio = m_ProjectConfig.UiPrefabStudio;
            if (string.IsNullOrWhiteSpace(studio.LanhuProjectUrl) && EditorPrefs.HasKey(LanhuUrlPreference))
            {
                studio.LanhuProjectUrl = EditorPrefs.GetString(LanhuUrlPreference, string.Empty);
                projectChanged = !string.IsNullOrWhiteSpace(studio.LanhuProjectUrl);
            }

            if (studio.OutputRoot == UiPrefabStudioProjectConfig.DefaultOutputRoot &&
                EditorPrefs.HasKey(OutputPreference))
            {
                studio.OutputRoot = EditorPrefs.GetString(OutputPreference, studio.OutputRoot);
                projectChanged = true;
            }

            if (string.IsNullOrWhiteSpace(m_UserConfig.FigmaToken) && EditorPrefs.HasKey(FigmaTokenPreference))
            {
                m_UserConfig.FigmaToken = EditorPrefs.GetString(FigmaTokenPreference, string.Empty);
                userChanged = !string.IsNullOrWhiteSpace(m_UserConfig.FigmaToken);
            }

            if (projectChanged)
            {
                m_ProjectConfig.Save();
            }

            if (userChanged)
            {
                m_UserConfig.Save();
            }

            EditorPrefs.DeleteKey(LanhuUrlPreference);
            EditorPrefs.DeleteKey(OutputPreference);
            EditorPrefs.DeleteKey(FigmaTokenPreference);
        }

        private void TryLoadConfiguredCache()
        {
            if (m_Document != null ||
                m_ProjectConfig.UiPrefabStudio.Source != UiPrefabStudioProjectConfig.LanhuSource ||
                string.IsNullOrWhiteSpace(m_ProjectConfig.UiPrefabStudio.LanhuProjectUrl))
            {
                return;
            }

            try
            {
                var snapshot = new DesignCacheStore().LoadLatest(m_ProjectConfig.UiPrefabStudio.LanhuProjectUrl);
                SetSnapshot(snapshot);
                SetStatus($"已自动载入本地缓存版本 {ShortRevision(snapshot.Revision)}。", 0f);
            }
            catch (FileNotFoundException)
            {
                SetStatus("该蓝湖项目还没有本地缓存。", 0f);
            }
            catch (Exception exception)
            {
                SetStatus("本地缓存不可用：" + exception.Message, 0f);
            }
        }

        private async UniTask LoadFigmaAsync()
        {
            if (m_Busy)
            {
                return;
            }

            BeginAction("正在连接 Figma...");
            try
            {
                using var client = new FigmaDesignClient();
                var document = await client.LoadAsync(
                    m_ProjectConfig.UiPrefabStudio.FigmaFile,
                    m_UserConfig.FigmaToken,
                    1f,
                    m_ActionCancellation.Token);
                ClearSnapshot();
                SetDocument(document);
                SetStatus($"已读取 {document.Pages.Count} 个页面。", 0f);
            }
            catch (OperationCanceledException)
            {
                SetStatus("已取消。", 0f);
            }
            catch (Exception exception)
            {
                SetStatus("Figma 读取失败。", 0f);
                EditorUtility.DisplayDialog("Figma 读取失败", exception.Message, "确定");
            }
            finally
            {
                EndAction();
            }
        }

        private async UniTask SyncLanhuAsync()
        {
            if (m_Busy)
            {
                return;
            }

            BeginAction("正在等待蓝湖浏览器同步桥...");
            try
            {
                var address = LanhuProjectAddress.Parse(m_ProjectConfig.UiPrefabStudio.LanhuProjectUrl);
                m_LanhuBridge ??= new LanhuSyncBridgeServer();
                Application.OpenURL(address.Url);
                var json = await m_LanhuBridge.RequestManifestAsync(address, m_ActionCancellation.Token);
                SetStatus("已读取蓝湖图层，正在建立版本缓存...", 0.02f);
                var document = DesignManifestCodec.Parse(json);
                var progress = new Progress<DesignImportProgress>(item =>
                    SetStatus(item.Message, item.Normalized));
                var snapshot = await new DesignCacheStore().SaveSyncAsync(
                    document,
                    address.Url,
                    progress,
                    m_ActionCancellation.Token);
                await UniTask.SwitchToMainThread();
                SetSnapshot(snapshot);
                var changed = snapshot.Diff.Pages.Count(item => item.Kind != DesignChangeKind.Unchanged);
                SetStatus(
                    $"同步完成：{document.Pages.Count} 个设计稿，{document.Assets.Count} 个切图，{changed} 项变化。",
                    1f);
            }
            catch (OperationCanceledException)
            {
                SetStatus("蓝湖同步已取消。", 0f);
            }
            catch (Exception exception)
            {
                SetStatus("蓝湖同步失败。", 0f);
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "蓝湖同步失败",
                    exception.Message + "\n\n仅首次使用时，点击“首次安装桥”并在 Chrome/Edge 加载扩展；之后只需点击“同步蓝湖”。",
                    "确定");
            }
            finally
            {
                EndAction();
            }
        }

        private void LoadLanhuCache()
        {
            try
            {
                var snapshot = new DesignCacheStore().LoadLatest(m_ProjectConfig.UiPrefabStudio.LanhuProjectUrl);
                SetSnapshot(snapshot);
                SetStatus($"已加载本地缓存版本 {ShortRevision(snapshot.Revision)}。", 0f);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("加载缓存失败", exception.Message, "确定");
            }
        }

        private void ImportManifest()
        {
            var path = EditorUtility.OpenFilePanel("导入设计清单", string.Empty, "json");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                var document = DesignManifestCodec.ReadFile(path);
                ClearSnapshot();
                SetDocument(document);
                SetStatus($"已导入 {document.Name}，共 {document.Pages.Count} 个页面。", 0f);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("清单导入失败", exception.Message, "确定");
            }
        }

        private void SetSnapshot(DesignSyncSnapshot snapshot)
        {
            m_Snapshot = snapshot;
            m_Diff = snapshot?.Diff;
            m_ProjectCacheRoot = snapshot?.ProjectCacheRoot ?? string.Empty;
            SetDocument(snapshot?.Document);
        }

        private void ClearSnapshot()
        {
            m_Snapshot = null;
            m_Diff = null;
            m_ProjectCacheRoot = string.Empty;
        }

        private void SetDocument(DesignDocument document)
        {
            var documentVersion = ++m_DocumentVersion;
            DestroyPageThumbnails();
            m_ResettingPageSelection = true;
            m_PageList?.SetSelectionWithoutNotify(Array.Empty<int>());
            m_Document = document;
            RebuildPageItems();
            m_SelectedPage = m_PageItems.FirstOrDefault(item => item.Kind != DesignChangeKind.Deleted)?.Page;
            m_SelectedNode = null;
            m_LayerBrowserActive = false;
            RefreshDocument();
            if (m_SelectedPage != null)
            {
                var firstIndex = Mathf.Max(0, m_PageItems.FindIndex(item => ReferenceEquals(item.Page, m_SelectedPage)));
                m_PageList.SetSelectionWithoutNotify(new[] { firstIndex });
                m_PageList.ScrollToItem(firstIndex);
                SelectPage(m_SelectedPage);
                var firstPage = m_SelectedPage;
                EditorApplication.delayCall += () => CompletePageSelectionReset(documentVersion, firstPage, firstIndex);
            }
            else
            {
                m_ResettingPageSelection = false;
            }
        }

        private void CompletePageSelectionReset(int documentVersion, DesignPage firstPage, int firstIndex)
        {
            if (this == null || documentVersion != m_DocumentVersion || m_PageList == null)
            {
                return;
            }

            m_PageList.SetSelectionWithoutNotify(new[] { firstIndex });
            m_PageList.ScrollToItem(firstIndex);
            if (!ReferenceEquals(m_SelectedPage, firstPage))
            {
                SelectPage(firstPage);
            }

            m_ResettingPageSelection = false;
        }

        private void RefreshDocument()
        {
            if (m_PageList == null)
            {
                return;
            }

            m_Refreshing = true;
            try
            {
                var hasDocument = m_Document?.Pages.Count > 0;
                m_WelcomeView.style.display = hasDocument ? DisplayStyle.None : DisplayStyle.Flex;
                m_Workspace.style.display = hasDocument ? DisplayStyle.Flex : DisplayStyle.None;
                m_PageList.itemsSource = m_PageItems;
                m_PageList.Rebuild();
                UpdateBrowserMode();
                RefreshNodeList();
                RefreshNodeInspector();
                RefreshGenerationSummary();
                RefreshPreview();
            }
            finally
            {
                m_Refreshing = false;
            }
        }

        private void SelectPage(DesignPage page)
        {
            m_SelectedPage = page;
            m_SelectedNode = null;
            RefreshNodeList();
            if (m_LayerBrowserActive && m_NodeRows.Count > 0)
            {
                SelectNode(m_NodeRows[0].Node);
            }
            else
            {
                RefreshNodeInspector();
                RefreshPreview();
            }

            UpdateBrowserMode();
            RefreshGenerationSummary();
            Run(LoadPreviewAsync(page));
        }

        private async UniTask LoadPreviewAsync(DesignPage page)
        {
            m_PreviewCancellation?.Cancel();
            m_PreviewCancellation?.Dispose();
            m_PreviewCancellation = new CancellationTokenSource();
            var token = m_PreviewCancellation.Token;
            var version = ++m_PreviewVersion;
            DestroyPreviewTexture();
            RefreshPreview();
            if (page == null || string.IsNullOrWhiteSpace(page.PreviewUrl))
            {
                return;
            }

            try
            {
                byte[] previewBytes;
                if (!string.IsNullOrWhiteSpace(page.CachedPreviewPath) && System.IO.File.Exists(page.CachedPreviewPath))
                {
                    previewBytes = System.IO.File.ReadAllBytes(page.CachedPreviewPath);
                }
                else
                {
                    using var client = new DesignAssetDownloadClient();
                    var asset = new DesignAsset
                    {
                        Id = page.Id,
                        Name = page.Name,
                        Url = page.PreviewUrl,
                        Format = "png"
                    };
                    var download = await client.DownloadAsync(asset, token);
                    previewBytes = download.Bytes;
                    if (!string.IsNullOrWhiteSpace(m_ProjectCacheRoot))
                    {
                        var previewFolder = Path.Combine(m_ProjectCacheRoot, "previews");
                        Directory.CreateDirectory(previewFolder);
                        page.CachedPreviewPath = Path.Combine(
                            previewFolder,
                            DesignPathUtility.SanitizeFileName(page.Id, "page") + "." + download.Extension);
                        System.IO.File.WriteAllBytes(page.CachedPreviewPath, previewBytes);
                    }
                }

                if (token.IsCancellationRequested || version != m_PreviewVersion || !ReferenceEquals(page, m_SelectedPage))
                {
                    return;
                }

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = page.Name + " Preview",
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (!texture.LoadImage(previewBytes, true))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    return;
                }

                m_PreviewTexture = texture;
                m_PageList?.RefreshItems();
                RefreshPreview();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                SetStatus("预览加载失败：" + exception.Message, 0f);
            }
        }

        private void RefreshNodeList()
        {
            m_NodeRows.Clear();
            m_AllNodeRows.Clear();
            if (m_SelectedPage?.Root != null)
            {
                AddAllNodeRows(m_SelectedPage.Root, 0, 0f, 0f);
                AddVisibleNodeRows(m_SelectedPage.Root, 0, 0f, 0f);
            }

            if (m_NodeList != null)
            {
                m_NodeList.itemsSource = m_NodeRows;
                m_NodeList.Rebuild();
                var selectedIndex = m_NodeRows.FindIndex(row => ReferenceEquals(row.Node, m_SelectedNode));
                m_NodeList.SetSelectionWithoutNotify(selectedIndex >= 0 ? new[] { selectedIndex } : Array.Empty<int>());
            }
        }

        private void AddAllNodeRows(DesignNode node, int depth, float parentX, float parentY)
        {
            var absoluteX = depth == 0 ? 0f : parentX + node.X;
            var absoluteY = depth == 0 ? 0f : parentY + node.Y;
            m_AllNodeRows.Add(new DesignNodeRow(node, depth, absoluteX, absoluteY));
            foreach (var child in node.Children)
            {
                if (child != null)
                {
                    AddAllNodeRows(child, depth + 1, absoluteX, absoluteY);
                }
            }
        }

        private void AddVisibleNodeRows(DesignNode node, int depth, float parentX, float parentY)
        {
            var absoluteX = depth == 0 ? 0f : parentX + node.X;
            var absoluteY = depth == 0 ? 0f : parentY + node.Y;
            m_NodeRows.Add(new DesignNodeRow(node, depth, absoluteX, absoluteY));
            if (m_CollapsedNodeIds.Contains(node.Id))
            {
                return;
            }

            foreach (var child in node.Children)
            {
                if (child != null)
                {
                    AddVisibleNodeRows(child, depth + 1, absoluteX, absoluteY);
                }
            }
        }

        private void ToggleNodeExpansion(DesignNode node)
        {
            if (node == null || node.Children == null || node.Children.All(child => child == null))
            {
                return;
            }

            if (!m_CollapsedNodeIds.Add(node.Id))
            {
                m_CollapsedNodeIds.Remove(node.Id);
            }

            RefreshNodeList();
            SelectNode(node);
        }

        private bool ExpandAncestors(DesignNode node)
        {
            var changed = false;
            var current = DesignMappingStore.ParentOf(m_SelectedPage, node);
            while (current != null)
            {
                changed |= m_CollapsedNodeIds.Remove(current.Id);
                current = DesignMappingStore.ParentOf(m_SelectedPage, current);
            }

            return changed;
        }

        private void SelectNode(DesignNode node, bool syncList = true)
        {
            if (syncList && node != null && ExpandAncestors(node))
            {
                RefreshNodeList();
            }

            m_SelectedNode = node;
            if (syncList && m_NodeList != null)
            {
                var index = m_NodeRows.FindIndex(x => ReferenceEquals(x.Node, node));
                m_NodeList.SetSelectionWithoutNotify(index >= 0 ? new[] { index } : Array.Empty<int>());
                if (index >= 0)
                {
                    m_NodeList.ScrollToItem(index);
                }
            }

            RefreshNodeInspector();
            RefreshPreview();
        }

        private void RefreshNodeInspector()
        {
            if (m_NodeSummary == null)
            {
                return;
            }

            m_Refreshing = true;
            try
            {
                var node = m_SelectedNode;
                m_NodeSummary.text = node == null
                    ? "未选择节点"
                    : $"{node.Kind} · {node.Width:0.#} x {node.Height:0.#} · ({node.X:0.#}, {node.Y:0.#})";
                var hasNode = node != null;
                var isImage = node?.Kind == DesignNodeKind.Image;
                var isText = node?.Kind == DesignNodeKind.Text;
                var component = node?.Component ?? DesignComponentKind.None;
                m_NodeNameField.SetEnabled(hasNode);
                m_NodeNameField.SetValueWithoutNotify(node?.Name ?? string.Empty);
                m_NodeVisibleToggle.SetEnabled(hasNode);
                m_NodeVisibleToggle.SetValueWithoutNotify(node?.Visible ?? false);
                SetFloatField(m_NodeX, node?.X ?? 0f, hasNode);
                SetFloatField(m_NodeY, node?.Y ?? 0f, hasNode);
                SetFloatField(m_NodeWidth, node?.Width ?? 0f, hasNode);
                SetFloatField(m_NodeHeight, node?.Height ?? 0f, hasNode);
                var anchors = ResolveInspectorAnchors(node);
                var pivot = ResolveInspectorPivot(node);
                m_AnchorPresetEditor.SetEnabled(hasNode);
                m_AnchorPresetEditor.SetValueWithoutNotify(anchors.Min, anchors.Max, pivot);
                SetFloatField(m_AnchorMinX, anchors.Min.x, hasNode);
                SetFloatField(m_AnchorMinY, anchors.Min.y, hasNode);
                SetFloatField(m_AnchorMaxX, anchors.Max.x, hasNode);
                SetFloatField(m_AnchorMaxY, anchors.Max.y, hasNode);
                SetFloatField(m_PivotX, pivot.x, hasNode);
                SetFloatField(m_PivotY, pivot.y, hasNode);
                m_NodeComponent.SetEnabled(hasNode);
                m_NodeComponent.SetValueWithoutNotify(ComponentChoice(component));
                m_BindingName.SetEnabled(hasNode);
                m_BindingName.SetValueWithoutNotify(node?.BindingName ?? string.Empty);
                m_Interactable.SetEnabled(hasNode && component != DesignComponentKind.None);
                m_Interactable.SetValueWithoutNotify(node?.Interactable ?? false);
                m_ToggleOptions.style.display = component == DesignComponentKind.Toggle ? DisplayStyle.Flex : DisplayStyle.None;
                m_ToggleValue.SetEnabled(component == DesignComponentKind.Toggle);
                m_ToggleValue.SetValueWithoutNotify(node?.ToggleValue ?? false);
                m_SliderOptions.style.display = component == DesignComponentKind.Slider ? DisplayStyle.Flex : DisplayStyle.None;
                SetFloatField(m_SliderMinValue, node?.SliderMinValue ?? 0f, component == DesignComponentKind.Slider);
                SetFloatField(m_SliderMaxValue, node?.SliderMaxValue ?? 1f, component == DesignComponentKind.Slider);
                SetFloatField(m_SliderValue, node?.SliderValue ?? 0f, component == DesignComponentKind.Slider);
                m_SliderWholeNumbers.SetEnabled(component == DesignComponentKind.Slider);
                m_SliderWholeNumbers.SetValueWithoutNotify(node?.SliderWholeNumbers ?? false);
                m_ScrollOptions.style.display = component == DesignComponentKind.ScrollRect ? DisplayStyle.Flex : DisplayStyle.None;
                m_ScrollHorizontal.SetEnabled(component == DesignComponentKind.ScrollRect);
                m_ScrollHorizontal.SetValueWithoutNotify(node?.ScrollHorizontal ?? false);
                m_ScrollVertical.SetEnabled(component == DesignComponentKind.ScrollRect);
                m_ScrollVertical.SetValueWithoutNotify(node?.ScrollVertical ?? false);
                m_ImageInspector.style.display = isImage ? DisplayStyle.Flex : DisplayStyle.None;
                m_TextInspector.style.display = isText ? DisplayStyle.Flex : DisplayStyle.None;
                m_NodeSharedToggle.SetEnabled(isImage);
                m_NodeSharedToggle.SetValueWithoutNotify(node?.Shared ?? false);
                m_NineSliceToggle.SetEnabled(isImage);
                m_NineSliceToggle.SetValueWithoutNotify(node?.NineSlice ?? false);
                SetBorderField(m_BorderLeft, node?.Border?.Left ?? 0f, isImage);
                SetBorderField(m_BorderBottom, node?.Border?.Bottom ?? 0f, isImage);
                SetBorderField(m_BorderRight, node?.Border?.Right ?? 0f, isImage);
                SetBorderField(m_BorderTop, node?.Border?.Top ?? 0f, isImage);
                m_NodeText.SetValueWithoutNotify(node?.Text ?? string.Empty);
                m_FontName.SetValueWithoutNotify(node?.FontName ?? string.Empty);
                m_FontStyleName.SetValueWithoutNotify(node?.FontStyleName ?? string.Empty);
                m_FontSize.SetValueWithoutNotify(node?.FontSize ?? 0f);
                m_FontBold.SetValueWithoutNotify(node?.Bold ?? false);
                m_FontItalic.SetValueWithoutNotify(node?.Italic ?? false);
                m_TextColor.SetValueWithoutNotify(ParseInspectorColor(node?.Color));
                m_TextAlignment.SetValueWithoutNotify(NormalizeChoice(
                    node?.TextAlignment,
                    new[] { "left", "center", "right", "justified" },
                    "left"));
                m_Tracking.SetValueWithoutNotify(node?.Tracking ?? 0f);
                m_LineHeight.SetValueWithoutNotify(node?.LineHeight ?? 0f);
                m_WordWrap.SetValueWithoutNotify(node?.WordWrap ?? true);
                m_TextOverflow.SetValueWithoutNotify(NormalizeChoice(
                    node?.Overflow,
                    new[] { "overflow", "ellipsis", "truncate", "masking" },
                    "overflow"));
            }
            finally
            {
                m_Refreshing = false;
            }
        }

        private static void SetBorderField(IntegerField field, float value, bool enabled)
        {
            field.SetEnabled(enabled);
            field.SetValueWithoutNotify(Mathf.RoundToInt(value));
        }

        private static void SetFloatField(FloatField field, float value, bool enabled)
        {
            field.SetEnabled(enabled);
            field.SetValueWithoutNotify(value);
        }

        private AnchorValues ResolveInspectorAnchors(DesignNode node)
        {
            if (node == null)
            {
                return new AnchorValues(Vector2.zero, Vector2.zero);
            }

            if (node.AnchorMin != null)
            {
                var min = node.AnchorMin.Value;
                return new AnchorValues(min, node.AnchorMax?.Value ?? min);
            }

            var parent = DesignMappingStore.ParentOf(m_SelectedPage, node);
            var parentWidth = Mathf.Max(1f, parent?.Width ?? m_SelectedPage?.Width ?? node.Width);
            var parentHeight = Mathf.Max(1f, parent?.Height ?? m_SelectedPage?.Height ?? node.Height);
            var pivot = ResolveInspectorPivot(node);
            var anchor = new Vector2(
                (node.X + node.Width * pivot.x) / parentWidth,
                1f - (node.Y + node.Height * (1f - pivot.y)) / parentHeight);
            anchor.x = Mathf.Clamp01(anchor.x);
            anchor.y = Mathf.Clamp01(anchor.y);
            return new AnchorValues(anchor, anchor);
        }

        private static Vector2 ResolveInspectorPivot(DesignNode node)
        {
            return node?.Pivot?.Value ?? new Vector2(0.5f, 0.5f);
        }

        private static Color ParseInspectorColor(string value)
        {
            return ColorUtility.TryParseHtmlString(value ?? string.Empty, out var color) ? color : Color.white;
        }

        private static string NormalizeChoice(string value, IEnumerable<string> choices, string fallback)
        {
            return choices.Contains(value ?? string.Empty) ? value : fallback;
        }

        private void MutateSelectedNode(Action<DesignNode> mutation)
        {
            if (m_Refreshing || m_SelectedNode == null)
            {
                return;
            }

            mutation(m_SelectedNode);
            SaveSelectedPageMapping();
            RefreshNodeList();
            RefreshPreview();
        }

        private void SaveSelectedPageMapping()
        {
            if (!string.IsNullOrWhiteSpace(m_ProjectCacheRoot) && m_SelectedPage != null)
            {
                DesignMappingStore.Save(m_ProjectCacheRoot, m_SelectedPage);
            }
        }

        private void RefreshPreview()
        {
            if (m_Preview == null)
            {
                return;
            }

            m_PreviewTitle.text = m_SelectedPage == null
                ? "预览"
                : $"{m_SelectedPage.Name} · {m_SelectedPage.Width:0} x {m_SelectedPage.Height:0}";
            m_Preview.SetPage(m_SelectedPage, m_PreviewTexture, m_AllNodeRows, m_SelectedNode, m_ZoomSlider?.value ?? 1f);
        }

        private async UniTask GenerateAsync()
        {
            if (m_Busy || m_Document == null || m_SelectedPage == null)
            {
                return;
            }

            if (!CanGenerateSelectedPage())
            {
                SetStatus("当前设计稿已删除，不能生成。", 0f);
                return;
            }

            DesignImportOptions options;
            try
            {
                options = BuildOptions();
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("生成设置无效", exception.Message, "确定");
                return;
            }

            BeginAction("准备生成...");
            try
            {
                var pipeline = new DesignImportPipeline();
                var progress = new Progress<DesignImportProgress>(value => SetStatus(value.Message, value.Normalized));
                var report = await pipeline.ImportAsync(
                    CreateSinglePageDocument(m_Document, m_SelectedPage),
                    options,
                    progress,
                    m_ActionCancellation.Token);
                SetStatus(
                    $"完成：{report.PrefabPaths.Count} Prefab，{report.SharedAssetCount} 公共资源，{report.Duration.TotalSeconds:0.0}s",
                    1f);
                var first = report.PrefabPaths.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(first))
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(first);
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus("生成已取消。", 0f);
            }
            catch (Exception exception)
            {
                SetStatus("生成失败。", 0f);
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Prefab 生成失败", exception.Message, "确定");
            }
            finally
            {
                EndAction();
            }
        }

        internal static DesignDocument CreateSinglePageDocument(DesignDocument source, DesignPage page)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            return new DesignDocument
            {
                SchemaVersion = source.SchemaVersion,
                ExporterVersion = source.ExporterVersion,
                Id = source.Id,
                Name = source.Name,
                Source = source.Source,
                TeamId = source.TeamId,
                SourceRevision = source.SourceRevision,
                SourceUpdatedAt = source.SourceUpdatedAt,
                SourceLocation = source.SourceLocation,
                Assets = source.Assets,
                Pages = new List<DesignPage>
                {
                    new DesignPage
                    {
                        Id = page.Id,
                        Name = page.Name,
                        Width = page.Width,
                        Height = page.Height,
                        PreviewUrl = page.PreviewUrl,
                        RevisionId = page.RevisionId,
                        RevisionTimestamp = page.RevisionTimestamp,
                        Root = page.Root,
                        Selected = true,
                        CachedPreviewPath = page.CachedPreviewPath
                    }
                }
            };
        }

        private DesignImportOptions BuildOptions()
        {
            var studio = m_ProjectConfig.UiPrefabStudio;
            var width = Mathf.Max(1, studio.TargetWidth);
            var height = Mathf.Max(1, studio.TargetHeight);
            if (width > 16384 || height > 16384)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "目标分辨率不能超过 16384。" );
            }

            return new DesignImportOptions
            {
                OutputRoot = DesignPathUtility.EnsureAssetsPath(studio.OutputRoot),
                TargetResolution = new Vector2Int(width, height),
                ScaleMode = studio.ScaleMode switch
                {
                    UiPrefabStudioProjectConfig.FillScaleMode => DesignScaleMode.Fill,
                    UiPrefabStudioProjectConfig.StretchScaleMode => DesignScaleMode.Stretch,
                    _ => DesignScaleMode.Fit
                },
                MaxTextureSize = studio.MaxTextureSize,
                IncludeCanvas = studio.IncludeCanvas,
                ExtractSharedAssets = studio.ExtractSharedAssets,
                GenerateWindowCode = studio.GenerateWindowCode,
                GeneratedCodeRoot = DesignPathUtility.EnsureAssetsPath(studio.GeneratedCodeRoot),
                CodeNamespace = studio.CodeNamespace,
                LayerOrder = studio.LayerOrder,
                CacheEnabled = studio.CacheEnabled
            };
        }

        private void RefreshGenerationSummary()
        {
            if (m_GenerationSummary == null || m_ProjectConfig == null)
            {
                return;
            }

            var studio = m_ProjectConfig.UiPrefabStudio;
            var currentPage = m_SelectedPage == null ? "未选择设计稿" : "当前：" + m_SelectedPage.Name;
            m_GenerationSummary.text =
                currentPage + "\n" +
                $"{studio.TargetWidth} x {studio.TargetHeight} · {ScaleModeLabel(studio.ScaleMode)} · " +
                (studio.GenerateWindowCode ? "UIDocument + UIWindow" : "仅 Prefab");
            m_GenerationSummary.tooltip = "在配置表编辑器 > 全局设置中修改生成参数";
            m_GenerateButton?.SetEnabled(CanGenerateSelectedPage());
        }

        private bool CanGenerateSelectedPage()
        {
            if (m_Busy || m_SelectedPage == null)
            {
                return false;
            }

            return !m_PageItems.Any(item =>
                ReferenceEquals(item.Page, m_SelectedPage) && item.Kind == DesignChangeKind.Deleted);
        }

        private static string ScaleModeLabel(string scaleMode)
        {
            return scaleMode switch
            {
                UiPrefabStudioProjectConfig.FillScaleMode => "填充裁切",
                UiPrefabStudioProjectConfig.StretchScaleMode => "拉伸",
                _ => "等比适配"
            };
        }

        private void BeginAction(string message)
        {
            CancelCurrentAction();
            m_ActionCancellation = new CancellationTokenSource();
            m_Busy = true;
            m_CancelButton.style.display = DisplayStyle.Flex;
            RefreshBusyState();
            SetStatus(message, 0f);
        }

        private void EndAction()
        {
            m_ActionCancellation?.Dispose();
            m_ActionCancellation = null;
            m_Busy = false;
            m_CancelButton.style.display = DisplayStyle.None;
            RefreshBusyState();
        }

        private void CancelCurrentAction()
        {
            m_ActionCancellation?.Cancel();
        }

        private void RefreshBusyState()
        {
            m_LoadButton?.SetEnabled(!m_Busy);
            m_LoadCacheButton?.SetEnabled(!m_Busy);
            m_WelcomeImportButton?.SetEnabled(!m_Busy);
            m_WelcomeCacheButton?.SetEnabled(!m_Busy);
            RefreshGenerationSummary();
        }

        private void SetStatus(string message, float progress)
        {
            if (m_StatusLabel == null)
            {
                return;
            }

            m_StatusLabel.text = message ?? string.Empty;
            m_ProgressBar.value = Mathf.Clamp01(progress) * 100f;
            m_ProgressBar.title = progress > 0f && progress < 1f ? $"{progress:P0}" : string.Empty;
        }

        private void DestroyPreviewTexture()
        {
            if (m_PreviewTexture == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(m_PreviewTexture);
            m_PreviewTexture = null;
        }

        private Texture2D GetPageThumbnail(DesignPage page)
        {
            var path = page?.CachedPreviewPath;
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                return null;
            }

            if (m_PageThumbnails.TryGetValue(path, out var cached))
            {
                return cached;
            }

            Texture2D source = null;
            RenderTexture target = null;
            var previous = RenderTexture.active;
            try
            {
                source = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (!source.LoadImage(System.IO.File.ReadAllBytes(path), false))
                {
                    return null;
                }

                var scale = Mathf.Min(128f / Mathf.Max(1, source.width), 72f / Mathf.Max(1, source.height));
                var width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
                var height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
                target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                var thumbnail = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = page.Name + " Thumbnail",
                    hideFlags = HideFlags.HideAndDontSave
                };
                thumbnail.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                thumbnail.Apply(false, true);
                m_PageThumbnails[path] = thumbnail;
                return thumbnail;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"加载设计稿缩略图失败：{path}。{exception.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (source != null) UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private void DestroyPageThumbnails()
        {
            foreach (var thumbnail in m_PageThumbnails.Values)
            {
                if (thumbnail != null)
                {
                    UnityEngine.Object.DestroyImmediate(thumbnail);
                }
            }

            m_PageThumbnails.Clear();
        }

        private static void Run(UniTask operation)
        {
            operation.Forget(exception => Debug.LogException(exception));
        }

        private static string NodeIcon(DesignNodeKind kind)
        {
            return kind switch
            {
                DesignNodeKind.Image => "▣",
                DesignNodeKind.Text => "T",
                _ => "□"
            };
        }

        private struct AnchorValues
        {
            public AnchorValues(Vector2 min, Vector2 max)
            {
                Min = min;
                Max = max;
            }

            public Vector2 Min;
            public Vector2 Max;
        }

        private sealed class DesignPageListItem
        {
            public DesignPageListItem(DesignPage page, DesignChangeKind kind)
            {
                Page = page;
                Kind = kind;
            }

            public DesignPage Page { get; }
            public DesignChangeKind Kind { get; }
        }
    }
}
