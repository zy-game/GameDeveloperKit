using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.EditorNodeGraph;
using GameDeveloperKit.Story;
using GameDeveloperKit.StoryEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.StoryEditor
{
    /// <summary>
    /// Story Editor shell.
    /// </summary>
    public sealed partial class StoryEditorWindow : EditorWindow
    {
        private const string WindowTitle = "剧情编辑器";
        private const string StylePath = "Assets/GameDeveloperKit/Editor/StoryEditor/UI/StoryEditorWindow.uss";
        private const string GraphStylePath = "Assets/GameDeveloperKit/Editor/NodeGraph/EditorNodeGraph.uss";

        private StoryAuthoringAsset m_Asset;
        private StoryAuthoringChapter m_SelectedChapter;
        private StoryAuthoringNode m_SelectedNode;
        private StoryAuthoringEdge m_SelectedEdge;
        private readonly HashSet<string> m_SelectedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        private SelectionKind m_SelectionKind = SelectionKind.Story;
        private StoryValidationReport m_Report = new StoryValidationReport();
        private StoryEditorDiagnosticSet m_LocalDiagnostics = StoryEditorDiagnosticSet.Empty;
        private StoryEditorDiagnosticSet m_CompilerDiagnostics = StoryEditorDiagnosticSet.Empty;
        private StoryEditorDiagnosticSet m_GraphDiagnostics = StoryEditorDiagnosticSet.Empty;
        private bool m_CompilerDiagnosticsStale;
        private StoryProgram m_LastCompiledProgram;
        private string m_PlayPreviewStatus;

        private VisualElement m_TreeContent;
        private VisualElement m_ReportContainer;
        private Label m_StatusLabel;
        private EditorNodeGraphCanvas m_Canvas;
        private StoryEditorGraphAdapter m_GraphAdapter;

        private enum SelectionKind
        {
            Story,
            Chapter,
            Node,
            Edge
        }

        internal StoryAuthoringChapter SelectedChapter => m_SelectedChapter;

        internal StoryAuthoringNode SelectedNode => m_SelectedNode;

        internal StoryAuthoringEdge SelectedEdge => m_SelectedEdge;

        internal StoryEditorDiagnosticSet GraphDiagnostics => m_GraphDiagnostics ?? StoryEditorDiagnosticSet.Empty;

        internal bool IsNodeSelected(StoryAuthoringNode node)
        {
            return node != null &&
                   (ReferenceEquals(m_SelectedNode, node) || m_SelectedNodeIds.Contains(node.NodeId));
        }

        private static string s_PendingAssetPath;

        public static void Open(string assetPath)
        {
            s_PendingAssetPath = assetPath;
            var window = GetWindow<StoryEditorWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(1180f, 720f);
            window.Show();
        }

        public static void Open()
        {
            var window = GetWindow<StoryEditorWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(1180f, 720f);
            window.Show();
        }

        public static void OpenSample()
        {
            var window = GetWindow<StoryEditorWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(1180f, 720f);
            window.Show();
            window.LoadSampleAsset();
        }

        public void CreateGUI()
        {
            if (string.IsNullOrWhiteSpace(s_PendingAssetPath))
            {
                m_Asset = StoryAuthoringAssetStore.LoadOrCreate();
            }
            else
            {
                var asset = AssetDatabase.LoadAssetAtPath<StoryAuthoringAsset>(s_PendingAssetPath);
                if (asset != null)
                {
                    m_Asset = asset;
                }
                else
                {
                    m_Asset = StoryAuthoringAssetStore.LoadOrCreate();
                }

                s_PendingAssetPath = null;
            }

            m_Asset.EnsureDefaults();
            SelectDefaults();
            BuildLayout();
            RefreshAll("就绪。");
        }

        private void BuildLayout()
        {
            rootVisualElement.Clear();
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            var graphStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(GraphStylePath);
            if (graphStyleSheet != null)
            {
                rootVisualElement.styleSheets.Add(graphStyleSheet);
            }

            var root = new VisualElement();
            root.AddToClassList("story-editor");
            root.EnableInClassList("story-editor--dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("story-editor--light", EditorGUIUtility.isProSkin is false);
            rootVisualElement.Add(root);

            root.Add(CreateToolbar());

            var body = new VisualElement();
            body.AddToClassList("story-editor__body");
            body.Add(CreateTreePane());
            body.Add(CreateWorkspacePane());
            root.Add(body);
        }

        private VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("story-editor__toolbar");
            toolbar.Add(new Label("剧情编辑") { name = "story-editor-title", tooltip = "按 StoryProgram 运行时契约组织的剧情编辑器。" });

            var actions = new VisualElement();
            actions.AddToClassList("story-editor__toolbar-actions");
            actions.Add(CreateButton("新建", "创建新的剧情编辑资源。", NewAsset));
            actions.Add(CreateButton("打开", "打开已有剧情编辑资源。", OpenAsset));
            actions.Add(CreateButton("保存", "保存当前剧情编辑资源。", SaveAsset));
            actions.Add(CreateButton("编译", "将当前剧情图编译为 StoryProgram，并同步写入运行时 StoryProgramAsset。", CompileProgram));
            toolbar.Add(actions);
            return toolbar;
        }

        private VisualElement CreateTreePane()
        {
            var pane = new VisualElement();
            pane.AddToClassList("story-editor__pane");
            pane.AddToClassList("story-editor__tree-pane");

            var header = new VisualElement();
            header.AddToClassList("story-editor__pane-header");
            header.Add(new Label("剧情树") { tooltip = "只显示剧情和章节；unit、payload、owner action 等旧结构不作为主入口显示。" });
            pane.Add(header);

            var scroll = new ScrollView();
            scroll.AddToClassList("story-editor__tree-scroll");
            m_TreeContent = new VisualElement();
            scroll.Add(m_TreeContent);
            pane.Add(scroll);

            m_StatusLabel = new Label();
            m_StatusLabel.AddToClassList("story-editor__status");
            m_ReportContainer = new VisualElement();
            m_ReportContainer.AddToClassList("story-editor__report");
            pane.Add(m_StatusLabel);
            pane.Add(m_ReportContainer);
            return pane;
        }

        private VisualElement CreateWorkspacePane()
        {
            var workspace = new VisualElement();
            workspace.AddToClassList("story-editor__workspace");

            var graphArea = new VisualElement();
            graphArea.AddToClassList("story-editor__graph-area");
            m_GraphAdapter = new StoryEditorGraphAdapter(this);
            m_Canvas = new EditorNodeGraphCanvas();
            m_Canvas.SetAdapter(m_GraphAdapter);
            graphArea.Add(m_Canvas);
            workspace.Add(graphArea);
            return workspace;
        }

        private void RefreshAll(string status = null)
        {
            if (m_Asset == null)
            {
                return;
            }

            m_Asset.EnsureDefaults();
            EnsureSelection();
            RefreshDiagnostics();
            RefreshTree();
            RefreshCanvas();
            RefreshReport(status);
        }

        private void RefreshTree()
        {
            if (m_TreeContent == null)
            {
                return;
            }

            m_TreeContent.Clear();

            var foldout = new Foldout
            {
                text = "章节",
                value = true,
                tooltip = "剧情由章节组成；章节相当于 Yarn node / Ink knot。"
            };
            foldout.AddToClassList("story-editor__chapter-foldout");
            foldout.AddManipulator(new ContextualMenuManipulator(BuildChapterGroupContextMenu));
            for (var i = 0; i < m_Asset.Chapters.Count; i++)
            {
                var chapter = m_Asset.Chapters[i];
                if (chapter == null)
                {
                    continue;
                }

                var chapterRow = CreateTreeRow(
                    FormatChapterLabel(chapter),
                    "选中章节并打开章节画布。",
                    m_SelectionKind == SelectionKind.Chapter && ReferenceEquals(m_SelectedChapter, chapter),
                    () => SelectChapter(chapter),
                    "story-editor__tree-row--chapter");
                chapterRow.AddManipulator(new ContextualMenuManipulator(evt => BuildChapterContextMenu(evt, chapter)));
                foldout.Add(chapterRow);
            }

            m_TreeContent.Add(foldout);
        }

        private void RefreshCanvas()
        {
            if (m_Canvas == null)
            {
                return;
            }

            m_Canvas.Rebuild();
        }

        internal VisualElement CreateGraphBlackboard()
        {
            var root = new VisualElement();

            var chapterTitle = m_SelectedChapter == null
                ? "未选择章节"
                : SafeText(m_SelectedChapter.Title, m_SelectedChapter.ChapterId);
            var status = new Label($"当前章节：{chapterTitle}")
            {
                tooltip = "播放按钮会打开独立播放窗口，并使用运行时 StoryModule 会话测试当前章节。"
            };
            status.AddToClassList("story-editor__blackboard-play-status");
            root.Add(status);

            var play = CreateButton("播放章节", "打开剧情播放窗口，按当前章节入口运行测试。", OpenPlaybackWindow);
            play.AddToClassList("story-editor__blackboard-play");
            root.Add(play);

            return root;
        }

        internal IReadOnlyList<EditorGraphFieldOption> GetJumpChapterFieldOptions(string currentValue)
        {
            if (m_Asset == null || m_Asset.Chapters == null || m_Asset.Chapters.Count == 0)
            {
                return Array.Empty<EditorGraphFieldOption>();
            }

            var options = new List<EditorGraphFieldOption>();
            for (var i = 0; i < m_Asset.Chapters.Count; i++)
            {
                var chapter = m_Asset.Chapters[i];
                if (chapter == null || ReferenceEquals(chapter, m_SelectedChapter))
                {
                    continue;
                }

                var chapterId = SafeText(chapter.ChapterId, "chapter");
                var title = SafeText(chapter.Title, chapterId);
                var label = string.Equals(title, chapterId, StringComparison.Ordinal)
                    ? chapterId
                    : $"{title} ({chapterId})";
                options.Add(new EditorGraphFieldOption(label, chapterId));
            }

            if (string.IsNullOrWhiteSpace(currentValue) is false &&
                options.Any(x => string.Equals(x.Value, currentValue, StringComparison.Ordinal)) is false)
            {
                options.Insert(0, new EditorGraphFieldOption(currentValue, currentValue));
            }

            return options;
        }

        internal string GetJumpChapterFieldDisplayValue(string currentValue)
        {
            var options = GetJumpChapterFieldOptions(currentValue);
            for (var i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].Value, currentValue, StringComparison.Ordinal))
                {
                    return options[i].Label;
                }
            }

            return currentValue ?? string.Empty;
        }

        private void RefreshReport(string status)
        {
            if (m_StatusLabel != null && status != null)
            {
                m_StatusLabel.text = status;
            }

            if (m_ReportContainer == null)
            {
                return;
            }

            m_ReportContainer.Clear();
            if (m_CompilerDiagnosticsStale && m_Report.Issues.Count > 0)
            {
                var stale = new Label("图已修改，请重新编译确认。") { tooltip = "下方过期问题来自上一次编译结果，可能已经不是最新状态。" };
                stale.AddToClassList("story-editor__issue--stale");
                m_ReportContainer.Add(stale);
            }

            var items = GraphDiagnostics.Items;
            if (items.Count == 0)
            {
                var empty = new Label("当前没有发现问题。") { tooltip = "本地浅层校验未发现错误或警告；正式导出前仍建议点击编译。" };
                empty.AddToClassList("story-editor__issue--empty");
                m_ReportContainer.Add(empty);
                return;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var button = new Button(() => FocusDiagnostic(item))
                {
                    text = item.SummaryText,
                    tooltip = item.Tooltip
                };
                button.AddToClassList("story-editor__issue");
                button.AddToClassList(ReportIssueClass(item.GraphDiagnostic.Severity));
                button.EnableInClassList("story-editor__issue--stale", item.GraphDiagnostic.Stale);
                m_ReportContainer.Add(button);
            }
        }

        private void RefreshDiagnostics()
        {
            m_LocalDiagnostics = StoryEditorDiagnostics.BuildLocal(m_Asset, m_SelectedChapter);
            if (m_Report.Issues.Count > 0)
            {
                m_CompilerDiagnostics = StoryEditorDiagnostics.FromReport(m_Report, m_Asset, m_SelectedChapter, m_CompilerDiagnosticsStale);
            }
            else
            {
                m_CompilerDiagnostics = StoryEditorDiagnosticSet.Empty;
            }

            var items = new List<StoryEditorDiagnosticItem>();
            items.AddRange(m_LocalDiagnostics.Items);
            items.AddRange(m_CompilerDiagnostics.Items);
            if (m_LastCompiledProgram != null && m_CompilerDiagnosticsStale is false)
            {
                items.AddRange(StoryEditorDiagnostics.FromCompiledProgram(m_LastCompiledProgram, m_Asset, m_SelectedChapter).Items);
            }

            m_GraphDiagnostics = new StoryEditorDiagnosticSet(items);
        }

        private void FocusDiagnostic(StoryEditorDiagnosticItem item)
        {
            if (item == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(item.Location.ChapterId) is false &&
                (m_SelectedChapter == null || string.Equals(m_SelectedChapter.ChapterId, item.Location.ChapterId, StringComparison.Ordinal) is false))
            {
                var chapter = m_Asset.FindChapter(item.Location.ChapterId);
                if (chapter != null)
                {
                    SelectChapter(chapter);
                }
            }

            if (string.IsNullOrWhiteSpace(item.GraphDiagnostic.WireId) is false)
            {
                SelectWireFromGraph(item.GraphDiagnostic.WireId);
                RefreshCanvas();
                return;
            }

            if (string.IsNullOrWhiteSpace(item.GraphDiagnostic.NodeId) is false)
            {
                SelectNodeFromGraph(item.GraphDiagnostic.NodeId);
                RefreshCanvas();
            }
        }

        private void BuildStoryContextMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("打开样例", _ => LoadSampleAsset());
            evt.menu.AppendAction("新增章节", _ => AddChapter());
        }

        private void BuildChapterGroupContextMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("新增章节", _ => AddChapter());
        }

        private void BuildChapterContextMenu(ContextualMenuPopulateEvent evt, StoryAuthoringChapter chapter)
        {
            evt.menu.AppendAction("快速检查", _ =>
            {
                SelectChapter(chapter);
                PlaySelectedChapter();
            });
            evt.menu.AppendAction("打开播放窗口", _ =>
            {
                SelectChapter(chapter);
                OpenPlaybackWindow();
            });
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("检查错误", _ =>
            {
                SelectChapter(chapter);
                CompileProgram();
            });

            var firstIssue = FirstChapterIssue(chapter);
            evt.menu.AppendAction(
                "定位第一个问题",
                _ =>
                {
                    SelectChapter(chapter);
                    FocusDiagnostic(firstIssue);
                },
                _ => firstIssue == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("新增章节", _ => AddChapter());
            evt.menu.AppendAction(
                "删除章节",
                _ => RemoveChapter(chapter),
                _ => m_Asset != null && m_Asset.Chapters.Count > 1 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        private StoryEditorDiagnosticItem FirstChapterIssue(StoryAuthoringChapter chapter)
        {
            if (chapter == null || GraphDiagnostics == null)
            {
                return null;
            }

            for (var i = 0; i < GraphDiagnostics.Items.Count; i++)
            {
                var item = GraphDiagnostics.Items[i];
                if (item == null ||
                    item.GraphDiagnostic.Severity != EditorGraphDiagnosticSeverity.Error &&
                    item.GraphDiagnostic.Severity != EditorGraphDiagnosticSeverity.Warning)
                {
                    continue;
                }

                if (string.Equals(item.Location.ChapterId, chapter.ChapterId, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private void MarkGraphChanged()
        {
            if (m_Report.Issues.Count > 0)
            {
                m_CompilerDiagnosticsStale = true;
            }

            m_LastCompiledProgram = null;
        }

        private void ResetCompilerDiagnostics()
        {
            m_Report = new StoryValidationReport();
            m_CompilerDiagnostics = StoryEditorDiagnosticSet.Empty;
            m_CompilerDiagnosticsStale = false;
            m_LastCompiledProgram = null;
        }

        private void NewAsset()
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

            m_Asset = asset;
            ResetCompilerDiagnostics();
            SelectDefaults();
            RefreshAll("已新建资源。");
        }

        private void OpenAsset()
        {
            var path = EditorUtility.OpenFilePanel("打开剧情资源", Application.dataPath, "asset");
            if (string.IsNullOrWhiteSpace(path) || path.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase) is false)
            {
                return;
            }

            var assetPath = "Assets" + path.Substring(Application.dataPath.Length).Replace('\\', '/');
            var asset = AssetDatabase.LoadAssetAtPath<StoryAuthoringAsset>(assetPath);
            if (asset == null)
            {
                RefreshReport("打开失败：请选择 StoryAuthoringAsset。");
                return;
            }

            m_Asset = asset;
            m_Asset.EnsureDefaults();
            ResetCompilerDiagnostics();
            SelectDefaults();
            RefreshAll("已打开资源。");
        }

        private void LoadSampleAsset()
        {
            m_Asset = StorySampleGraphFixture.LoadOrCreateAsset();
            Selection.activeObject = m_Asset;
            ResetCompilerDiagnostics();
            SelectDefaults();
            RefreshAll("已打开示例剧情图。");
        }

        private void SaveAsset()
        {
            StoryAuthoringAssetStore.Save(m_Asset);
            RefreshAll("已保存。");
        }

        private void CompileProgram()
        {
            m_LastCompiledProgram = StoryProgramCompiler.Compile(m_Asset, out m_Report);
            m_CompilerDiagnosticsStale = false;
            m_CompilerDiagnostics = StoryEditorDiagnostics.FromReport(m_Report, m_Asset, m_SelectedChapter, false);
            var message = "编译失败。";
            if (m_Report.HasErrors is false && m_LastCompiledProgram != null)
            {
                var export = StoryProgramAssetExporter.ExportCompiled(m_Asset, m_LastCompiledProgram);
                var summary = $"编译通过：{m_LastCompiledProgram.Chapters.Count} 章节，{m_LastCompiledProgram.CommandSchema.Definitions.Count} 命令。";
                if (export.Exported)
                {
                    message = $"{summary}已导出 {export.OutputPath}。";
                }
                else if (export.Canceled)
                {
                    message = $"{summary}已取消导出运行时资源。";
                }
                else
                {
                    message = $"{summary}当前资源未保存到项目内，跳过运行时资源导出。";
                }
            }

            RefreshAll(message);
        }

        private void PlaySelectedChapter()
        {
            if (m_SelectedChapter == null)
            {
                m_PlayPreviewStatus = "播放失败：请先选择章节。";
                RefreshAll(m_PlayPreviewStatus);
                return;
            }

            m_LastCompiledProgram = StoryProgramCompiler.Compile(m_Asset, out m_Report);
            m_CompilerDiagnosticsStale = false;
            m_CompilerDiagnostics = StoryEditorDiagnostics.FromReport(m_Report, m_Asset, m_SelectedChapter, false);
            if (m_Report.HasErrors || m_LastCompiledProgram == null)
            {
                m_PlayPreviewStatus = $"播放失败：编译存在 {CountCompilerErrors(m_Report)} 个错误。";
                RefreshAll(m_PlayPreviewStatus);
                return;
            }

            var result = StoryEditorPlaybackPreview.Play(m_LastCompiledProgram, m_SelectedChapter.ChapterId);
            m_PlayPreviewStatus = result.Message;
            RefreshAll(result.Message);
        }

        private void OpenPlaybackWindow()
        {
            if (m_Asset == null)
            {
                RefreshAll("打开播放窗口失败：没有剧情资源。");
                return;
            }

            var chapterId = m_SelectedChapter?.ChapterId;
            if (string.IsNullOrWhiteSpace(chapterId))
            {
                chapterId = m_Asset.EntryChapterId;
            }

            StoryEditorPlaybackWindow.Open(m_Asset, chapterId);
        }

        private void AddChapter()
        {
            var id = MakeUnique("chapter", m_Asset.Chapters.Select(x => x.ChapterId));
            var chapter = CreateChapter(id);
            m_Asset.Chapters.Add(chapter);

            m_SelectedChapter = chapter;
            m_SelectedNodeIds.Clear();
            m_SelectionKind = SelectionKind.Chapter;
            MarkDirty();
            RefreshAll("已添加章节。");
        }

        private void RemoveChapter(StoryAuthoringChapter chapter)
        {
            if (chapter == null)
            {
                return;
            }

            m_SelectedChapter = chapter;
            RemoveSelectedChapter();
        }

        private void RemoveSelectedChapter()
        {
            if (m_SelectedChapter == null || m_Asset.Chapters.Count <= 1)
            {
                return;
            }

            var chapter = m_SelectedChapter;
            m_Asset.Chapters.Remove(chapter);

            if (string.Equals(m_Asset.EntryChapterId, chapter.ChapterId, StringComparison.Ordinal))
            {
                m_Asset.EntryChapterId = m_Asset.Chapters[0].ChapterId;
            }

            m_SelectedChapter = m_Asset.Chapters[0];
            m_SelectedNodeIds.Clear();
            m_SelectionKind = SelectionKind.Chapter;
            MarkDirty();
            RefreshAll("已删除章节。");
        }

        private StoryAuthoringNode AddNodeAt(
            Vector2 position,
            NodeKind kind,
            StoryAuthoringNode fromNode,
            string fromPortId,
            string fromPortLabel)
        {
            if (m_SelectedChapter == null)
            {
                return null;
            }

            if (kind == NodeKind.Start || kind == NodeKind.End)
            {
                RefreshReport("开始和结束节点由章节自动维护。");
                return null;
            }

            if (NodeSchemaRegistry.IsDefaultAuthoringNode(kind) is false)
            {
                RefreshReport("该节点已退出默认作者节点库，请使用内容、媒体、音频、等待、选项或章节跳转节点表达剧情。");
                return null;
            }

            var schema = NodeSchemaRegistry.Get(kind);
            var node = new StoryAuthoringNode
            {
                NodeId = MakeUnique(ToIdBase(kind), m_SelectedChapter.Nodes.Select(x => x.NodeId)),
                Title = schema.DisplayName,
                NodeKind = kind
            };
            AddDefaultParameters(node, schema);
            if (kind == NodeKind.JumpChapter)
            {
                SetParameterValue(node, "chapterId", GetDefaultJumpChapterTargetId());
            }

            m_SelectedChapter.Nodes.Add(node);
            GetLayout(node).Position = position;
            if (fromNode != null)
            {
                if (StoryEditorPortPolicy.CanConnect(m_SelectedChapter, fromNode, fromPortId, node).Allowed)
                {
                    AddEdge(fromNode, fromPortId, fromPortLabel, node);
                }
            }

            m_SelectedNode = node;
            m_SelectedEdge = null;
            m_SelectedNodeIds.Clear();
            m_SelectedNodeIds.Add(node.NodeId);
            m_SelectionKind = SelectionKind.Node;
            MarkDirty();
            RefreshAll("已添加节点。");
            return node;
        }

        internal void AddNodeFromGraph(Vector2 position, NodeKind kind, EditorGraphPortRef connectFrom)
        {
            var fromNode = connectFrom.IsValid ? FindNode(connectFrom.NodeId) : null;
            var portLabel = fromNode == null ? null : ResolveOutputPortLabel(fromNode, connectFrom.PortId);
            AddNodeAt(position, kind, fromNode, connectFrom.PortId, portLabel);
        }

        internal void MoveNodeFromGraph(string nodeId, Vector2 position)
        {
            var node = FindNode(nodeId);
            if (node == null)
            {
                return;
            }

            GetLayout(node).Position = position;
            MarkDirty();
        }

        internal void SelectNodeFromGraph(string nodeId)
        {
            var node = FindNode(nodeId);
            if (node != null)
            {
                m_SelectedNode = node;
                m_SelectedEdge = null;
                m_SelectedNodeIds.Clear();
                m_SelectedNodeIds.Add(node.NodeId);
                m_SelectionKind = SelectionKind.Node;
                RefreshReport(null);
            }
        }

        internal void SelectNodesFromGraph(IReadOnlyList<string> nodeIds)
        {
            m_SelectedNodeIds.Clear();
            m_SelectedNode = null;
            m_SelectedEdge = null;

            for (var i = 0; i < (nodeIds?.Count ?? 0); i++)
            {
                var node = FindNode(nodeIds[i]);
                if (node != null)
                {
                    m_SelectedNodeIds.Add(node.NodeId);
                    m_SelectedNode = node;
                }
            }

            if (m_SelectedNodeIds.Count == 0)
            {
                m_SelectedNode = null;
                m_SelectionKind = SelectionKind.Chapter;
                RefreshReport(null);
                return;
            }

            m_SelectionKind = SelectionKind.Node;
            RefreshReport(m_SelectedNodeIds.Count == 1 ? null : $"已选中 {m_SelectedNodeIds.Count} 个节点。");
        }

        internal void SelectWireFromGraph(string wireId)
        {
            var edge = FindEdge(wireId);
            if (edge != null)
            {
                m_SelectedEdge = edge;
                m_SelectedNode = null;
                m_SelectedNodeIds.Clear();
                m_SelectionKind = SelectionKind.Edge;
                RefreshReport(null);
            }
        }

        internal void ConnectFromGraph(EditorGraphPortRef output, EditorGraphPortRef input)
        {
            var fromNode = FindNode(output.NodeId);
            var targetNode = FindNode(input.NodeId);
            if (fromNode == null || targetNode == null)
            {
                return;
            }

            if (StoryEditorPortPolicy.CanConnect(m_SelectedChapter, fromNode, output.PortId, targetNode).Allowed is false)
            {
                return;
            }

            AddEdge(fromNode, output.PortId, ResolveOutputPortLabel(fromNode, output.PortId), targetNode);
        }

        internal void DisconnectFromGraph(string wireId)
        {
            var edge = FindEdge(wireId);
            if (edge == null || m_SelectedChapter == null)
            {
                return;
            }

            m_SelectedChapter.Edges.Remove(edge);
            if (ReferenceEquals(m_SelectedEdge, edge))
            {
                m_SelectedEdge = null;
                m_SelectedNodeIds.Clear();
                m_SelectionKind = SelectionKind.Chapter;
            }

            MarkDirty();
            RefreshAll("已删除连线。");
        }

        internal void DeleteSelectionFromGraph()
        {
            RemoveSelection();
        }

        internal void SetNodeFieldFromGraph(string nodeId, string fieldId, string value)
        {
            var node = FindNode(nodeId);
            if (node == null)
            {
                return;
            }

            if (string.Equals(fieldId, "title", StringComparison.Ordinal))
            {
                node.Title = value;
            }
            else
            {
                SetParameterValue(node, fieldId, value);
            }

            MarkDirty();
            RefreshAll("已更新字段。");
        }

        private void AddEdge(StoryAuthoringNode fromNode, string portId, string portLabel, StoryAuthoringNode targetNode)
        {
            if (m_SelectedChapter == null || fromNode == null || targetNode == null)
            {
                return;
            }

            if (fromNode.NodeKind == NodeKind.Parallel && string.Equals(portId, "branch", StringComparison.Ordinal))
            {
                portId = NextParallelBranchPortId(fromNode);
                portLabel = $"轨道 {ParallelBranchIndex(portId)}";
            }

            var edge = CreateEdge(fromNode, portId, portLabel, TransitionTargetKind.Node, targetNode.NodeId, null);
            AddEdgeToChapter(fromNode, edge);
            m_SelectedEdge = edge;
            m_SelectedNode = null;
            m_SelectedNodeIds.Clear();
            m_SelectionKind = SelectionKind.Edge;
            MarkDirty();
            RefreshAll("已连接节点。");
        }

        private void AddStoryEndEdge(StoryAuthoringNode fromNode, string portId, string portLabel)
        {
            if (m_SelectedChapter == null || fromNode == null)
            {
                return;
            }

            var edge = CreateEdge(fromNode, portId, portLabel, TransitionTargetKind.StoryEnd, null, null);
            AddEdgeToChapter(fromNode, edge);
            m_SelectedEdge = edge;
            m_SelectedNode = null;
            m_SelectedNodeIds.Clear();
            m_SelectionKind = SelectionKind.Edge;
            MarkDirty();
            RefreshAll("已连接剧情结束。");
        }

        private StoryAuthoringEdge CreateEdge(
            StoryAuthoringNode fromNode,
            string portId,
            string portLabel,
            TransitionTargetKind targetKind,
            string targetNodeId,
            string targetChapterId)
        {
            portId = string.IsNullOrWhiteSpace(portId) ? FirstOutputPortId(fromNode) : portId;
            portLabel = string.IsNullOrWhiteSpace(portLabel) ? portId : portLabel;
            return new StoryAuthoringEdge
            {
                EdgeId = MakeUnique($"edge_{fromNode.NodeId}_{portId}", m_SelectedChapter.Edges.Select(x => x.EdgeId)),
                FromNodeId = fromNode.NodeId,
                FromPortId = portId,
                FromPortLabel = portLabel,
                TargetKind = targetKind,
                TargetNodeId = targetKind == TransitionTargetKind.Node ? targetNodeId : null,
                TargetChapterId = targetKind == TransitionTargetKind.Chapter ? targetChapterId : null
            };
        }

        private void AddEdgeToChapter(StoryAuthoringNode fromNode, StoryAuthoringEdge edge)
        {
            var targetNode = edge.TargetKind == TransitionTargetKind.Node ? FindNode(edge.TargetNodeId) : null;
            if (StoryEditorPortPolicy.IsLineChoicePort(fromNode, edge.FromPortId, targetNode))
            {
                m_SelectedChapter.Edges.RemoveAll(x =>
                    x != null &&
                    string.Equals(x.FromNodeId, edge.FromNodeId, StringComparison.Ordinal) &&
                    string.Equals(x.FromPortId, edge.FromPortId, StringComparison.Ordinal) &&
                    (x.TargetKind != TransitionTargetKind.Node ||
                     FindNode(x.TargetNodeId)?.NodeKind != NodeKind.Choice));
            }
            else if (IsMultipleOutputPort(fromNode, edge.FromPortId, targetNode) is false)
            {
                m_SelectedChapter.Edges.RemoveAll(x =>
                    x != null &&
                    string.Equals(x.FromNodeId, edge.FromNodeId, StringComparison.Ordinal) &&
                    string.Equals(x.FromPortId, edge.FromPortId, StringComparison.Ordinal));
            }

            m_SelectedChapter.Edges.Add(edge);
        }

        private void RemoveSelection()
        {
            if (m_SelectedNodeIds.Count > 1 && m_SelectedChapter != null)
            {
                var removableIds = new HashSet<string>(
                    m_SelectedChapter.Nodes
                        .Where(x => x != null &&
                                    m_SelectedNodeIds.Contains(x.NodeId) &&
                                    x.NodeKind != NodeKind.Start &&
                                    x.NodeKind != NodeKind.End)
                        .Select(x => x.NodeId),
                    StringComparer.Ordinal);
                if (removableIds.Count == 0)
                {
                    RefreshReport("开始和结束节点不能删除。");
                    return;
                }

                m_SelectedChapter.Nodes.RemoveAll(x => x != null && removableIds.Contains(x.NodeId));
                m_SelectedChapter.Edges.RemoveAll(x =>
                    x != null &&
                    (removableIds.Contains(x.FromNodeId) || removableIds.Contains(x.TargetNodeId)));
                m_SelectedNode = null;
                m_SelectedEdge = null;
                m_SelectedNodeIds.Clear();
                m_SelectionKind = SelectionKind.Chapter;
                MarkDirty();
                RefreshAll($"已删除 {removableIds.Count} 个节点。");
                return;
            }

            if (m_SelectionKind == SelectionKind.Edge && m_SelectedEdge != null && m_SelectedChapter != null)
            {
                m_SelectedChapter.Edges.Remove(m_SelectedEdge);
                m_SelectedEdge = null;
                m_SelectedNodeIds.Clear();
                m_SelectionKind = SelectionKind.Chapter;
                MarkDirty();
                RefreshAll("已删除连线。");
                return;
            }

            if (m_SelectionKind == SelectionKind.Node && m_SelectedNode != null && m_SelectedChapter != null)
            {
                if (m_SelectedNode.NodeKind == NodeKind.Start || m_SelectedNode.NodeKind == NodeKind.End)
                {
                    RefreshReport("开始和结束节点不能删除。");
                    return;
                }

                var nodeId = m_SelectedNode.NodeId;
                m_SelectedChapter.Nodes.Remove(m_SelectedNode);
                m_SelectedChapter.Edges.RemoveAll(x =>
                    x != null &&
                    (string.Equals(x.FromNodeId, nodeId, StringComparison.Ordinal) ||
                     string.Equals(x.TargetNodeId, nodeId, StringComparison.Ordinal)));
                m_SelectedNode = null;
                m_SelectedNodeIds.Clear();
                m_SelectionKind = SelectionKind.Chapter;
                MarkDirty();
                RefreshAll("已删除节点。");
            }
        }

        private void SelectStory()
        {
            m_SelectedNode = null;
            m_SelectedEdge = null;
            m_SelectedNodeIds.Clear();
            m_SelectionKind = SelectionKind.Story;
            RefreshAll();
        }

        private void SelectChapter(StoryAuthoringChapter chapter)
        {
            m_SelectedChapter = chapter;
            m_SelectedNode = null;
            m_SelectedEdge = null;
            m_SelectedNodeIds.Clear();
            m_SelectionKind = SelectionKind.Chapter;
            RefreshAll();
        }

        private void SelectNode(StoryAuthoringNode node)
        {
            m_SelectedNode = node;
            m_SelectedEdge = null;
            m_SelectedNodeIds.Clear();
            if (node != null)
            {
                m_SelectedNodeIds.Add(node.NodeId);
            }

            m_SelectionKind = SelectionKind.Node;
            RefreshAll();
        }

        private void SelectEdge(StoryAuthoringEdge edge)
        {
            m_SelectedEdge = edge;
            m_SelectedNode = null;
            m_SelectedNodeIds.Clear();
            m_SelectionKind = SelectionKind.Edge;
            RefreshAll();
        }

        private void ShowCreateNodeMenuAtCanvasCenter()
        {
            var position = GetCanvasCenterPosition();
            var menu = new GenericMenu();
            foreach (var schema in NodeSchemaRegistry.Schemas.OrderBy(x => x.Category).ThenBy(x => x.DisplayName))
            {
                if (schema.Kind == NodeKind.Start ||
                    schema.Kind == NodeKind.End ||
                    NodeSchemaRegistry.IsDefaultAuthoringNode(schema.Kind) is false)
                {
                    continue;
                }

                var kind = schema.Kind;
                menu.AddItem(new GUIContent($"创建/{CategoryLabel(schema.Category)}/{schema.DisplayName}"), false, () => AddNodeAt(position, kind, null, null, null));
            }

            menu.ShowAsContext();
        }

        private Vector2 GetCanvasCenterPosition()
        {
            return m_Canvas == null ? Vector2.zero : m_Canvas.GetGraphCenterPosition();
        }

        private string CurrentGraphId()
        {
            return m_SelectedChapter == null ? "none" : m_SelectedChapter.ChapterId;
        }

        private StoryNodeLayout GetLayout(object element)
        {
            var nodeId = ElementLayoutId(element);
            var graphId = CurrentGraphId();
            var layout = m_Asset.Layout.Nodes.FirstOrDefault(x =>
                x != null &&
                string.Equals(x.GraphId, graphId, StringComparison.Ordinal) &&
                string.Equals(x.NodeId, nodeId, StringComparison.Ordinal));
            if (layout != null)
            {
                return layout;
            }

            layout = new StoryNodeLayout
            {
                GraphId = graphId,
                NodeId = nodeId,
                Position = Vector2.zero
            };
            m_Asset.Layout.Nodes.Add(layout);
            return layout;
        }

        internal Vector2 GetNodeGraphPosition(StoryAuthoringNode node, int index)
        {
            var position = GetLayout(node).Position;
            if (position != Vector2.zero)
            {
                return position;
            }

            position = new Vector2(80f + index * 300f, 120f + (index % 3) * 160f);
            GetLayout(node).Position = position;
            return position;
        }

        internal StoryAuthoringNode FindNode(string nodeId)
        {
            if (m_SelectedChapter == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return null;
            }

            for (var i = 0; i < m_SelectedChapter.Nodes.Count; i++)
            {
                var node = m_SelectedChapter.Nodes[i];
                if (node != null && string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
                {
                    return node;
                }
            }

            return null;
        }

        internal StoryAuthoringEdge FindEdge(string edgeId)
        {
            if (m_SelectedChapter == null || string.IsNullOrWhiteSpace(edgeId))
            {
                return null;
            }

            for (var i = 0; i < m_SelectedChapter.Edges.Count; i++)
            {
                var edge = m_SelectedChapter.Edges[i];
                if (edge != null && string.Equals(edge.EdgeId, edgeId, StringComparison.Ordinal))
                {
                    return edge;
                }
            }

            return null;
        }

        internal string ResolveOutputPortLabel(StoryAuthoringNode node, string portId)
        {
            if (node == null)
            {
                return portId;
            }

            if (node.NodeKind == NodeKind.Parallel && StoryEditorPortPolicy.IsParallelBranchPort(portId))
            {
                return ResolveExistingParallelBranchLabel(node, portId) ?? $"轨道 {ParallelBranchIndex(portId)}";
            }

            var schema = NodeSchemaRegistry.Get(node.NodeKind);
            for (var i = 0; i < schema.Ports.Count; i++)
            {
                var port = schema.Ports[i];
                if (port.Direction == PortDirection.Output && string.Equals(port.PortId, portId, StringComparison.Ordinal))
                {
                    return port.Label;
                }
            }

            return string.IsNullOrWhiteSpace(portId) ? FirstOutputPortId(node) : portId;
        }

        private static string ElementLayoutId(object element)
        {
            if (element is StoryAuthoringNode node)
            {
                return node.NodeId;
            }

            return element == null ? "unknown" : element.ToString();
        }

        private void EnsureSelection()
        {
            if (m_Asset.Chapters.Count == 0)
            {
                m_Asset.Chapters.Add(CreateChapter("chapter_01"));
            }

            if (string.IsNullOrWhiteSpace(m_Asset.EntryChapterId) ||
                m_Asset.Chapters.Any(x => x != null && string.Equals(x.ChapterId, m_Asset.EntryChapterId, StringComparison.Ordinal)) is false)
            {
                m_Asset.EntryChapterId = m_Asset.Chapters[0].ChapterId;
            }

            if (m_SelectedChapter == null || m_Asset.Chapters.Contains(m_SelectedChapter) is false)
            {
                m_SelectedChapter = m_Asset.FindChapter(m_Asset.EntryChapterId) ?? m_Asset.Chapters.FirstOrDefault();
            }

            EnsureChapterBoundaryNodes(m_SelectedChapter);

            if (m_SelectedChapter == null)
            {
                m_SelectedNodeIds.Clear();
            }
            else
            {
                m_SelectedNodeIds.RemoveWhere(nodeId =>
                    m_SelectedChapter.Nodes.Any(x => x != null && string.Equals(x.NodeId, nodeId, StringComparison.Ordinal)) is false);
            }

            if (m_SelectedNode != null && (m_SelectedChapter == null || m_SelectedChapter.Nodes.Contains(m_SelectedNode) is false))
            {
                m_SelectedNode = null;
                m_SelectedNodeIds.Clear();
                m_SelectionKind = SelectionKind.Chapter;
            }

            if (m_SelectedEdge != null && (m_SelectedChapter == null || m_SelectedChapter.Edges.Contains(m_SelectedEdge) is false))
            {
                m_SelectedEdge = null;
                m_SelectedNodeIds.Clear();
                m_SelectionKind = SelectionKind.Chapter;
            }
        }

        private void SelectDefaults()
        {
            m_SelectedChapter = m_Asset.FindChapter(m_Asset.EntryChapterId) ?? m_Asset.Chapters.FirstOrDefault();
            m_SelectedNode = null;
            m_SelectedEdge = null;
            m_SelectedNodeIds.Clear();
            m_SelectionKind = SelectionKind.Story;
        }

        private static StoryAuthoringChapter CreateChapter(string id)
        {
            var chapter = new StoryAuthoringChapter
            {
                ChapterId = id,
                Title = id,
                EntryNodeId = $"{id}_entry"
            };
            chapter.Nodes.Add(new StoryAuthoringNode
            {
                NodeId = chapter.EntryNodeId,
                Title = "开始",
                NodeKind = NodeKind.Start
            });
            chapter.Nodes.Add(new StoryAuthoringNode
            {
                NodeId = $"{id}_end",
                Title = "结束",
                NodeKind = NodeKind.End
            });
            return chapter;
        }

        private static void EnsureChapterBoundaryNodes(StoryAuthoringChapter chapter)
        {
            if (chapter == null)
            {
                return;
            }

            var start = FindFirstNodeByKind(chapter, NodeKind.Start);
            if (start == null)
            {
                start = new StoryAuthoringNode
                {
                    NodeId = MakeUniqueNodeId(
                        chapter,
                        string.IsNullOrWhiteSpace(chapter.EntryNodeId) ? $"{chapter.ChapterId}_entry" : chapter.EntryNodeId),
                    Title = "开始",
                    NodeKind = NodeKind.Start
                };
                chapter.Nodes.Insert(0, start);
            }

            var end = FindFirstNodeByKind(chapter, NodeKind.End);
            if (end == null)
            {
                end = new StoryAuthoringNode
                {
                    NodeId = MakeUniqueNodeId(chapter, $"{chapter.ChapterId}_end"),
                    Title = "结束",
                    NodeKind = NodeKind.End
                };
                chapter.Nodes.Add(end);
            }

            chapter.EntryNodeId = start.NodeId;
            RemoveDuplicateBoundaryNodes(chapter, NodeKind.Start, start.NodeId);
            RemoveDuplicateBoundaryNodes(chapter, NodeKind.End, end.NodeId);
        }

        private static void RemoveDuplicateBoundaryNodes(StoryAuthoringChapter chapter, NodeKind kind, string keepNodeId)
        {
            if (chapter == null || string.IsNullOrWhiteSpace(keepNodeId))
            {
                return;
            }

            for (var i = chapter.Nodes.Count - 1; i >= 0; i--)
            {
                var node = chapter.Nodes[i];
                if (node == null ||
                    node.NodeKind != kind ||
                    string.Equals(node.NodeId, keepNodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                var nodeId = node.NodeId;
                chapter.Nodes.RemoveAt(i);
                chapter.Edges.RemoveAll(edge =>
                    edge != null &&
                    (string.Equals(edge.FromNodeId, nodeId, StringComparison.Ordinal) ||
                     string.Equals(edge.TargetNodeId, nodeId, StringComparison.Ordinal)));
            }
        }

        private static StoryAuthoringNode FindFirstNodeByKind(StoryAuthoringChapter chapter, NodeKind kind)
        {
            for (var i = 0; i < chapter.Nodes.Count; i++)
            {
                var node = chapter.Nodes[i];
                if (node != null && node.NodeKind == kind)
                {
                    return node;
                }
            }

            return null;
        }

        private static string MakeUniqueNodeId(StoryAuthoringChapter chapter, string preferredId)
        {
            var baseId = string.IsNullOrWhiteSpace(preferredId) ? "node" : preferredId;
            var candidate = baseId;
            var index = 2;
            while (chapter.Nodes.Any(node => node != null && string.Equals(node.NodeId, candidate, StringComparison.Ordinal)))
            {
                candidate = $"{baseId}_{index}";
                index++;
            }

            return candidate;
        }

        private void UpdateChapterReferences(string oldId, string newId)
        {
            if (string.IsNullOrWhiteSpace(oldId))
            {
                return;
            }

            if (string.Equals(m_Asset.EntryChapterId, oldId, StringComparison.Ordinal))
            {
                m_Asset.EntryChapterId = newId;
            }

            foreach (var edge in m_Asset.Chapters.Where(x => x != null).SelectMany(x => x.Edges))
            {
                if (edge != null && string.Equals(edge.TargetChapterId, oldId, StringComparison.Ordinal))
                {
                    edge.TargetChapterId = newId;
                }
            }
        }

        private void UpdateNodeReferences(string oldId, string newId)
        {
            if (string.IsNullOrWhiteSpace(oldId))
            {
                return;
            }

            if (m_SelectedChapter != null && string.Equals(m_SelectedChapter.EntryNodeId, oldId, StringComparison.Ordinal))
            {
                m_SelectedChapter.EntryNodeId = newId;
            }

            foreach (var edge in m_SelectedChapter?.Edges ?? new List<StoryAuthoringEdge>())
            {
                if (edge == null)
                {
                    continue;
                }

                if (string.Equals(edge.FromNodeId, oldId, StringComparison.Ordinal))
                {
                    edge.FromNodeId = newId;
                }

                if (string.Equals(edge.TargetNodeId, oldId, StringComparison.Ordinal))
                {
                    edge.TargetNodeId = newId;
                }
            }
        }

        private static void AddDefaultParameters(StoryAuthoringNode node, NodeParameterSchema schema)
        {
            for (var i = 0; i < schema.Parameters.Count; i++)
            {
                var parameter = schema.Parameters[i];
                if (parameter.Required)
                {
                    node.Parameters.Add(new StoryAuthoringParameter
                    {
                        Key = parameter.Key,
                        Value = DefaultParameterValue(node, parameter)
                    });
                }
            }
        }

        private static string DefaultParameterValue(StoryAuthoringNode node, NodeParameterDefinition parameter)
        {
            if (node != null &&
                node.NodeKind == NodeKind.JumpChapter &&
                string.Equals(parameter.Key, "chapterId", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            switch (parameter.ValueType)
            {
                case ParameterValueType.Number:
                    return "0";
                case ParameterValueType.Boolean:
                    return "false";
                case ParameterValueType.Option:
                    return "==";
                default:
                    return string.IsNullOrWhiteSpace(node.NodeId) ? parameter.Key : node.NodeId;
            }
        }

        private static string GetParameterValue(StoryAuthoringNode node, string key)
        {
            for (var i = 0; i < node.Parameters.Count; i++)
            {
                var parameter = node.Parameters[i];
                if (parameter != null && string.Equals(parameter.Key, key, StringComparison.Ordinal))
                {
                    return parameter.Value;
                }
            }

            return string.Empty;
        }

        private static void SetParameterValue(StoryAuthoringNode node, string key, string value)
        {
            for (var i = 0; i < node.Parameters.Count; i++)
            {
                var parameter = node.Parameters[i];
                if (parameter != null && string.Equals(parameter.Key, key, StringComparison.Ordinal))
                {
                    parameter.Value = value;
                    return;
                }
            }

            node.Parameters.Add(new StoryAuthoringParameter { Key = key, Value = value });
        }

        private string GetDefaultJumpChapterTargetId()
        {
            if (m_Asset?.Chapters == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < m_Asset.Chapters.Count; i++)
            {
                var chapter = m_Asset.Chapters[i];
                if (chapter != null &&
                    ReferenceEquals(chapter, m_SelectedChapter) is false &&
                    string.IsNullOrWhiteSpace(chapter.ChapterId) is false)
                {
                    return chapter.ChapterId;
                }
            }

            return string.Empty;
        }

        internal bool IsMultipleOutputPort(StoryAuthoringNode node, string portId)
        {
            return IsMultipleOutputPort(node, portId, null);
        }

        internal bool IsMultipleOutputPort(StoryAuthoringNode node, string portId, StoryAuthoringNode targetNode)
        {
            return StoryEditorPortPolicy.IsMultipleOutputPort(node, portId, targetNode);
        }

        private static string FirstOutputPortId(StoryAuthoringNode node)
        {
            var schema = NodeSchemaRegistry.Get(node.NodeKind);
            for (var i = 0; i < schema.Ports.Count; i++)
            {
                if (schema.Ports[i].Direction == PortDirection.Output)
                {
                    return schema.Ports[i].PortId;
                }
            }

            return "completed";
        }

        private string NextParallelBranchPortId(StoryAuthoringNode node)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            if (m_SelectedChapter != null && node != null)
            {
                for (var i = 0; i < m_SelectedChapter.Edges.Count; i++)
                {
                    var edge = m_SelectedChapter.Edges[i];
                    if (edge != null &&
                        string.Equals(edge.FromNodeId, node.NodeId, StringComparison.Ordinal) &&
                        StoryEditorPortPolicy.IsParallelBranchPort(edge.FromPortId))
                    {
                        used.Add(edge.FromPortId);
                    }
                }
            }

            var index = 1;
            var candidate = $"branch_{index}";
            while (used.Contains(candidate))
            {
                index++;
                candidate = $"branch_{index}";
            }

            return candidate;
        }

        private string ResolveExistingParallelBranchLabel(StoryAuthoringNode node, string portId)
        {
            if (m_SelectedChapter == null || node == null || string.IsNullOrWhiteSpace(portId))
            {
                return null;
            }

            for (var i = 0; i < m_SelectedChapter.Edges.Count; i++)
            {
                var edge = m_SelectedChapter.Edges[i];
                if (edge != null &&
                    string.Equals(edge.FromNodeId, node.NodeId, StringComparison.Ordinal) &&
                    string.Equals(edge.FromPortId, portId, StringComparison.Ordinal))
                {
                    return string.IsNullOrWhiteSpace(edge.FromPortLabel) ? null : edge.FromPortLabel;
                }
            }

            return null;
        }

        private static int ParallelBranchIndex(string portId)
        {
            if (string.IsNullOrWhiteSpace(portId) ||
                portId.StartsWith("branch_", StringComparison.Ordinal) is false ||
                int.TryParse(portId.Substring("branch_".Length), out var index) is false)
            {
                return 1;
            }

            return index;
        }

        private static VisualElement CreateTreeRow(string text, string tooltip, bool selected, Action click, string className)
        {
            var row = new Button(click) { text = text, tooltip = tooltip };
            row.AddToClassList("story-editor__tree-row");
            row.AddToClassList(className);
            row.EnableInClassList("story-editor__tree-row--selected", selected);
            return row;
        }

        private static Button CreateButton(string text, string tooltip, Action click)
        {
            return new Button(click) { text = text, tooltip = tooltip };
        }

        private void MarkDirty()
        {
            MarkGraphChanged();
            if (m_Asset != null)
            {
                EditorUtility.SetDirty(m_Asset);
            }
        }

        private static string CategoryLabel(NodeCategory category)
        {
            switch (category)
            {
                case NodeCategory.Flow:
                    return "流程";
                case NodeCategory.Action:
                    return "命令";
                case NodeCategory.Interaction:
                    return "交互";
                default:
                    return category.ToString();
            }
        }

        private static string FormatChapterLabel(StoryAuthoringChapter chapter)
        {
            return string.IsNullOrWhiteSpace(chapter.Title)
                ? $"章节  {SafeText(chapter.ChapterId, "chapter")}"
                : $"章节  {chapter.Title}";
        }

        private static string SafeText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string ShortText(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || maxLength <= 1 || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength - 1) + "...";
        }

        private static int CountCompilerErrors(StoryValidationReport report)
        {
            return report?.Issues.Count(x => x.Severity == StoryValidationSeverity.Error) ?? 0;
        }

        private static string ReportIssueClass(EditorGraphDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case EditorGraphDiagnosticSeverity.Error:
                    return "story-editor__issue--error";
                case EditorGraphDiagnosticSeverity.Warning:
                    return "story-editor__issue--warning";
                default:
                    return "story-editor__issue--info";
            }
        }

        private static string ToIdBase(NodeKind kind)
        {
            return kind.ToString().ToLowerInvariant();
        }

        private static string MakeUnique(string baseKey, IEnumerable<string> existing)
        {
            baseKey = string.IsNullOrWhiteSpace(baseKey) ? "item" : baseKey;
            var keys = new HashSet<string>((existing ?? Array.Empty<string>()).Where(x => string.IsNullOrWhiteSpace(x) is false), StringComparer.Ordinal);
            var key = baseKey;
            var index = 1;
            while (keys.Contains(key))
            {
                index++;
                key = $"{baseKey}_{index}";
            }

            return key;
        }
    }

}
