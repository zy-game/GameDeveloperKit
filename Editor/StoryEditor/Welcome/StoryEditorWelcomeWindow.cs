using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.StoryEditor
{
    public sealed class StoryEditorWelcomeWindow : EditorWindow
    {
        private const string WindowTitle = "剧情编辑器";
        private const string StylePath = "Editor/StoryEditor/Welcome/StoryEditorWelcomeWindow.uss";

        private VisualElement m_RecentList;
        private Label m_RecentEmpty;

        [MenuItem("GameDeveloperKit/剧情编辑/编辑器")]
        public static void Open()
        {
            var window = GetWindow<StoryEditorWelcomeWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(560f, 520f);
            window.Show();
        }

        [MenuItem("GameDeveloperKit/剧情编辑/打开示例剧情图")]
        public static void OpenSampleMenu()
        {
            Open();
        }

        public void CreateGUI()
        {
            BuildLayout();
        }

        private void BuildLayout()
        {
            rootVisualElement.Clear();
            var styleSheet = GameDeveloperKitEditorPaths.LoadPackageAsset<StyleSheet>(StylePath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            var root = new VisualElement();
            root.AddToClassList("story-editor-welcome");
            rootVisualElement.Add(root);

            var content = new VisualElement();
            content.AddToClassList("story-editor-welcome__content");
            root.Add(content);

            var title = new Label("欢迎使用剧情编辑器");
            title.AddToClassList("story-editor-welcome__title");
            content.Add(title);

            var subtitle = new Label("按 StoryProgram 运行时契约组织的剧情编辑工具。新建一个剧情资源或打开已有资源开始编辑。");
            subtitle.AddToClassList("story-editor-welcome__subtitle");
            content.Add(subtitle);

            var actions = new VisualElement();
            actions.AddToClassList("story-editor-welcome__actions");
            content.Add(actions);

            var newButton = new Button(HandleNew) { text = "新建", tooltip = "创建新的剧情编辑资源。" };
            newButton.AddToClassList("story-editor-welcome__action-new");
            actions.Add(newButton);

            var openButton = new Button(HandleOpen) { text = "打开", tooltip = "打开已有剧情编辑资源。" };
            openButton.AddToClassList("story-editor-welcome__action-open");
            actions.Add(openButton);

            var sampleButton = new Button(HandleOpenSample) { text = "打开示例剧情", tooltip = "打开包含四章中文样例的示例剧情图。" };
            sampleButton.AddToClassList("story-editor-welcome__action-sample");
            actions.Add(sampleButton);

            var importExcelButton = new Button(HandleImportExcel) { text = "从 Excel 导入", tooltip = "从 Excel 文件导入剧情数据，创建新的剧情编辑资源。" };
            importExcelButton.AddToClassList("story-editor-welcome__action-import-excel");
            actions.Add(importExcelButton);

            var recentHeader = new Label("最近");
            recentHeader.AddToClassList("story-editor-welcome__section-title");
            content.Add(recentHeader);

            var recentList = new VisualElement();
            recentList.AddToClassList("story-editor-welcome__recent-list");
            m_RecentList = recentList;
            content.Add(recentList);

            m_RecentEmpty = new Label("暂无最近资源");
            m_RecentEmpty.AddToClassList("story-editor-welcome__recent-empty");
            recentList.Add(m_RecentEmpty);

            var guide = BuildGuide();
            content.Add(guide);

            RefreshRecentList();
        }

        private void HandleNew()
        {
            var path = EditorUtility.SaveFilePanelInProject("新建剧情资源", "NewStoryAuthoring", "asset", "选择剧情资源保存位置。");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var asset = StoryAuthoringAssetStore.CreateAtPath(path);
            if (asset == null)
            {
                return;
            }

            StoryEditorRecentAssets.RecordOpen(path);
            StoryEditorWindow.Open(path);
            Close();
        }

        private void HandleOpen()
        {
            var path = EditorUtility.OpenFilePanel("打开剧情资源", "Assets", "asset");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var fullPath = path.Replace('\\', '/');
            if (fullPath.StartsWith(Application.dataPath, System.StringComparison.OrdinalIgnoreCase) is false)
            {
                return;
            }

            var assetPath = "Assets" + fullPath.Substring(Application.dataPath.Length);
            var asset = AssetDatabase.LoadAssetAtPath<StoryAuthoringAsset>(assetPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("打开失败", "请选择 StoryAuthoringAsset 资源文件。", "确定");
                return;
            }

            StoryEditorRecentAssets.RecordOpen(assetPath);
            StoryEditorWindow.Open(assetPath);
            Close();
        }

        private void HandleOpenSample()
        {
            StoryEditorWindow.OpenSample();
            Close();
        }

        private void HandleImportExcel()
        {
            var excelPath = EditorUtility.OpenFilePanel("选择 Excel 文件", "Assets", "xlsx");
            if (string.IsNullOrWhiteSpace(excelPath))
            {
                return;
            }

            var assetPath = EditorUtility.SaveFilePanelInProject("保存剧情资源", "ImportedStory", "asset", "选择导入后的剧情资源保存位置。");
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            var asset = StoryAuthoringAssetStore.CreateAtPath(assetPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("导入失败", "无法创建剧情资源。", "确定");
                return;
            }

            try
            {
                var report = StoryExcelImporter.Import(excelPath, asset);
                if (report.HasErrors)
                {
                    EditorUtility.DisplayDialog("导入失败", $"Excel 校验未通过，请检查文件格式。\n第一个错误：{report.Issues[0]}", "确定");
                    return;
                }

                StoryEditorRecentAssets.RecordOpen(assetPath);
                StoryEditorWindow.Open(assetPath);
                Close();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("导入失败", ex.Message, "确定");
                Debug.LogException(ex);
            }
        }

        private void RefreshRecentList()
        {
            if (m_RecentList == null)
            {
                return;
            }

            m_RecentList.Clear();
            m_RecentList.Add(m_RecentEmpty);

            var paths = StoryEditorRecentAssets.GetRecentPaths();
            if (paths.Count == 0)
            {
                m_RecentEmpty.style.display = DisplayStyle.Flex;
                return;
            }

            m_RecentEmpty.style.display = DisplayStyle.None;

            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                var isValid = StoryEditorRecentAssets.IsValidAsset(path);
                var displayPath = path;

                var item = new Button(() => HandleOpenRecent(path, isValid))
                {
                    text = displayPath,
                    tooltip = isValid ? displayPath : "资源不可用"
                };
                item.AddToClassList("story-editor-welcome__recent-item");
                if (isValid is false)
                {
                    item.AddToClassList("story-editor-welcome__recent-item--invalid");
                }

                m_RecentList.Add(item);
            }
        }

        private void HandleOpenRecent(string assetPath, bool isValid)
        {
            if (isValid is false)
            {
                EditorUtility.DisplayDialog("资源不可用", $"资源不存在或不是有效的剧情编辑资源：\n{assetPath}", "确定");
                return;
            }

            StoryEditorRecentAssets.RecordOpen(assetPath);
            StoryEditorWindow.Open(assetPath);
            Close();
        }

        private static VisualElement BuildGuide()
        {
            var guide = new VisualElement();
            guide.AddToClassList("story-editor-welcome__guide");

            var guideTitle = new Label("快速开始");
            guideTitle.AddToClassList("story-editor-welcome__section-title");
            guide.Add(guideTitle);

            guide.Add(BuildGuideStep("1", "新建或打开一个剧情编辑资源"));
            guide.Add(BuildGuideStep("2", "在左侧章节树中选择或新增章节"));
            guide.Add(BuildGuideStep("3", "在画布中右键或从节点库拖入来创建剧情节点"));
            guide.Add(BuildGuideStep("4", "连接节点构建剧情流程，点击编译生成运行时资源"));
            guide.Add(BuildGuideStep("5", "使用播放窗口测试剧情运行效果"));

            return guide;
        }

        private static VisualElement BuildGuideStep(string number, string text)
        {
            var row = new VisualElement();
            row.AddToClassList("story-editor-welcome__guide-step");

            var circle = new Label(number);
            circle.AddToClassList("story-editor-welcome__guide-number");
            row.Add(circle);

            var label = new Label(text);
            label.AddToClassList("story-editor-welcome__guide-text");
            row.Add(label);

            return row;
        }
    }
}
