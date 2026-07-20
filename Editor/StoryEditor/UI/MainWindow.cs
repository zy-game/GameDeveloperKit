using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameDeveloperKit.EditorNodeGraph;
using GameDeveloperKit.Story;
using GameDeveloperKit.StoryEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Authoring;
using GameDeveloperKit.StoryEditor.Excel;
using GameDeveloperKit.StoryEditor.Graph;
using GameDeveloperKit.StoryEditor.Validation;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.Story.Logic;
using GameDeveloperKit.StoryEditor.Logic;

namespace GameDeveloperKit.StoryEditor.UI
{
    /// <summary>
    /// Story Editor shell.
    /// </summary>
    public sealed partial class MainWindow : EditorWindow
    {
        private const string WindowTitle = "剧情编辑器";
        private const string StylePath = "Editor/StoryEditor/UI/MainWindow.uss";
        private const string GraphStylePath = "Editor/NodeGraph/EditorNodeGraph.uss";

        private AuthoringAsset m_Asset;
        private AuthoringEpisode m_SelectedEpisode;
        private AuthoringNode m_SelectedNode;
        private AuthoringEdge m_SelectedEdge;
        private readonly HashSet<string> m_SelectedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        private SelectionKind m_SelectionKind = SelectionKind.Story;
        private ValidationReport m_Report = new ValidationReport();
        private DiagnosticSet m_LocalDiagnostics = DiagnosticSet.Empty;
        private DiagnosticSet m_CompilerDiagnostics = DiagnosticSet.Empty;
        private DiagnosticSet m_GraphDiagnostics = DiagnosticSet.Empty;
        private bool m_CompilerDiagnosticsStale;
        private Program m_LastCompiledProgram;

        private VisualElement m_TreeContent;
        private VisualElement m_StatusBar;
        private VisualElement m_StatusBarHeader;
        private Label m_StatusLabel;
        private VisualElement m_StatusBarBody;
        private bool m_StatusBarExpanded;
        private EditorNodeGraphCanvas m_Canvas;
        private GraphAdapter m_GraphAdapter;

        private enum SelectionKind
        {
            Story,
            Episode,
            Node,
            Edge
        }

        internal AuthoringEpisode SelectedEpisode => m_SelectedEpisode;

        internal AuthoringNode SelectedNode => m_SelectedNode;

        internal AuthoringEdge SelectedEdge => m_SelectedEdge;

        internal DiagnosticSet GraphDiagnostics => m_GraphDiagnostics ?? DiagnosticSet.Empty;

        internal bool IsNodeSelected(AuthoringNode node)
        {
            return node != null &&
                   (ReferenceEquals(m_SelectedNode, node) || m_SelectedNodeIds.Contains(node.NodeId));
        }

        private static string s_PendingAssetPath;

        public static void Open(string assetPath)
        {
            s_PendingAssetPath = assetPath;
            var window = GetWindow<MainWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(1180f, 720f);
            window.Show();
        }

        public static void Open()
        {
            var window = GetWindow<MainWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(1180f, 720f);
            window.Show();
        }

        public static void OpenSample()
        {
            var window = GetWindow<MainWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(1180f, 720f);
            window.Show();
            window.LoadSampleAsset();
        }

        public void CreateGUI()
        {
            if (string.IsNullOrWhiteSpace(s_PendingAssetPath))
            {
                m_Asset = AuthoringAssetStore.LoadOrCreate();
            }
            else
            {
                var asset = AssetDatabase.LoadAssetAtPath<AuthoringAsset>(s_PendingAssetPath);
                if (asset != null)
                {
                    m_Asset = asset;
                }
                else
                {
                    m_Asset = AuthoringAssetStore.LoadOrCreate();
                }

                s_PendingAssetPath = null;
            }

            m_Asset.EnsureDefaults();
            SelectDefaults();
            BuildLayout();
            RefreshAll("就绪。");
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            if (m_Asset == null)
            {
                return;
            }

            RefreshAll("已应用 Undo/Redo。再保存可持久化当前状态。");
        }

        private void BuildLayout()
        {
            rootVisualElement.Clear();
            var styleSheet = GameDeveloperKitEditorPaths.LoadPackageAsset<StyleSheet>(StylePath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            var graphStyleSheet = GameDeveloperKitEditorPaths.LoadPackageAsset<StyleSheet>(GraphStylePath);
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
            toolbar.Add(new Label("剧情编辑") { name = "story-editor-title", tooltip = "按 Program 运行时契约组织的剧情编辑器。" });

            var actions = new VisualElement();
            actions.AddToClassList("story-editor__toolbar-actions");
            actions.Add(CreateButton("新建", "创建新的剧情编辑资源。", NewAsset));
            actions.Add(CreateButton("打开", "打开已有剧情编辑资源。", OpenAsset));
            actions.Add(CreateButton("保存", "保存当前剧情编辑资源。", SaveAsset));
            actions.Add(CreateButton("编译", "将当前剧情图编译为 Program，并同步写入运行时 ProgramAsset。", CompileProgram));
            actions.Add(CreateButton("导出 Excel", "将当前剧情图导出为 Excel 文件。", ExportExcel));
            actions.Add(CreateButton("导入 Excel", "从 Excel 文件导入覆盖当前剧情图。", ImportExcel));
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
            header.Add(new Label("卷") { tooltip = "选择卷以查看卷内剧情路线。" });
            pane.Add(header);

            var scroll = new ScrollView();
            scroll.AddToClassList("story-editor__tree-scroll");
            m_TreeContent = new VisualElement();
            scroll.Add(m_TreeContent);
            pane.Add(scroll);

            return pane;
        }

        private VisualElement CreateWorkspacePane()
        {
            var workspace = new VisualElement();
            workspace.AddToClassList("story-editor__workspace");
            workspace.Add(CreateNavigationHeader());

            var workspaceBody = new VisualElement();
            workspaceBody.AddToClassList("story-editor__workspace-body");

            var graphArea = new VisualElement();
            graphArea.AddToClassList("story-editor__graph-area");
            m_GraphAdapter = new GraphAdapter(this);
            InitializeRouteNavigation();
            m_Canvas = new EditorNodeGraphCanvas();
            m_Canvas.SetAdapter(m_RouteGraphAdapter);
            graphArea.Add(m_Canvas);

            m_StatusBar = new VisualElement();
            m_StatusBar.AddToClassList("story-editor__status-bar");

            m_StatusBarHeader = new VisualElement();
            m_StatusBarHeader.AddToClassList("story-editor__status-bar-header");
            m_StatusLabel = new Label();
            m_StatusLabel.AddToClassList("story-editor__status-bar-text");
            m_StatusBarHeader.Add(m_StatusLabel);
            m_StatusBarHeader.RegisterCallback<MouseDownEvent>(_ => ToggleStatusBar());

            m_StatusBarBody = new VisualElement();
            m_StatusBarBody.AddToClassList("story-editor__status-bar-body");
            m_StatusBarBody.style.display = DisplayStyle.None;

            m_StatusBar.Add(m_StatusBarHeader);
            m_StatusBar.Add(m_StatusBarBody);
            graphArea.Add(m_StatusBar);
            workspaceBody.Add(graphArea);
            workspaceBody.Add(CreateRouteInspectorPane());
            workspace.Add(workspaceBody);
            return workspace;
        }

        private void RefreshAll(string status = null)
        {
            if (m_Asset == null)
            {
                return;
            }

            EnsureSelection();
            RefreshDiagnostics();
            RefreshTree();
            RefreshCanvas();
            RefreshNavigationChrome();
            RefreshReport(status);
        }

        private void RefreshTree()
        {
            if (m_TreeContent == null)
            {
                return;
            }

            m_TreeContent.Clear();

            for (var v = 0; v < m_Asset.Volumes.Count; v++)
            {
                var volume = m_Asset.Volumes[v];
                if (volume == null)
                {
                    continue;
                }

                var volumeText = string.IsNullOrWhiteSpace(volume.Title)
                    ? $"第{v + 1}卷"
                    : volume.Title;
                var volumeRow = new VisualElement();
                volumeRow.AddToClassList("story-editor__tree-row");
                volumeRow.AddToClassList("story-editor__tree-row--root");
                volumeRow.EnableInClassList(
                    "story-editor__tree-row--selected",
                    ReferenceEquals(m_SelectedVolume, volume));
                volumeRow.tooltip = "查看此卷的剧情路线。";
                volumeRow.Add(new Label(volumeText));
                volumeRow.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button == 0 && evt.clickCount == 1)
                    {
                        SelectVolume(volume);
                        evt.StopPropagation();
                    }
                });
                m_TreeContent.Add(volumeRow);
            }
        }

        private void RefreshCanvas()
        {
            if (m_Canvas == null)
            {
                return;
            }

            RefreshNavigationCanvas();
        }

        internal VisualElement CreateGraphBlackboard()
        {
            var root = new VisualElement();

            var episodeTitle = m_SelectedEpisode == null
                ? "未选择章节"
                : SafeText(m_SelectedEpisode.Title, m_SelectedEpisode.EpisodeId);
            var status = new Label($"当前章节：{episodeTitle}")
            {
                tooltip = "当前正在编辑的剧情段。"
            };
            status.AddToClassList("story-editor__blackboard-status");
            root.Add(status);

            return root;
        }

        private void RefreshReport(string status)
        {
            if (m_StatusLabel != null && status != null)
            {
                m_StatusLabel.text = status;
            }

            if (m_StatusBarBody == null)
            {
                return;
            }

            m_StatusBarBody.Clear();
            if (m_CompilerDiagnosticsStale && m_Report.Issues.Count > 0)
            {
                var stale = new Label("图已修改，请重新编译确认。") { tooltip = "下方过期问题来自上一次编译结果，可能已经不是最新状态。" };
                stale.AddToClassList("story-editor__issue--stale");
                m_StatusBarBody.Add(stale);
            }

            var items = GraphDiagnostics.Items;
            if (items.Count == 0)
            {
                if (m_StatusBarExpanded)
                {
                    var empty = new Label("当前没有发现问题。") { tooltip = "本地浅层校验未发现错误或警告；正式导出前仍建议点击编译。" };
                    empty.AddToClassList("story-editor__issue--empty");
                    m_StatusBarBody.Add(empty);
                }
            }
            else
            {
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var row = new VisualElement();
                    row.AddToClassList("story-editor__issue");
                    row.AddToClassList(ReportIssueClass(item.GraphDiagnostic.Severity));
                    row.EnableInClassList("story-editor__issue--stale", item.GraphDiagnostic.Stale);
                    row.tooltip = item.Tooltip;

                    var label = new Label(item.SummaryText);
                    row.Add(label);
                    row.RegisterCallback<MouseDownEvent>(_ => FocusDiagnostic(item));
                    m_StatusBarBody.Add(row);
                }
            }
        }

        private void ToggleStatusBar()
        {
            m_StatusBarExpanded = !m_StatusBarExpanded;
            m_StatusBarBody.style.display = m_StatusBarExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            m_StatusBarHeader.EnableInClassList("story-editor__status-bar-header--expanded", m_StatusBarExpanded);

            if (m_StatusBarExpanded)
            {
                RefreshAll(null);
            }
        }

        private void RefreshDiagnostics()
        {
            m_LocalDiagnostics = Diagnostics.BuildLocal(m_Asset, m_SelectedEpisode);
            if (m_Report.Issues.Count > 0)
            {
                m_CompilerDiagnostics = Diagnostics.FromReport(m_Report, m_Asset, m_SelectedEpisode, m_CompilerDiagnosticsStale);
            }
            else
            {
                m_CompilerDiagnostics = DiagnosticSet.Empty;
            }

            var items = new List<DiagnosticItem>();
            items.AddRange(m_LocalDiagnostics.Items);
            items.AddRange(m_CompilerDiagnostics.Items);
            if (m_LastCompiledProgram != null && m_CompilerDiagnosticsStale is false)
            {
                items.AddRange(Diagnostics.FromCompiledProgram(m_LastCompiledProgram, m_Asset, m_SelectedEpisode).Items);
            }

            m_GraphDiagnostics = new DiagnosticSet(items);
        }

        private void FocusDiagnostic(DiagnosticItem item)
        {
            if (item == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(item.Location.EpisodeId) is false &&
                (m_SelectedEpisode == null || string.Equals(m_SelectedEpisode.EpisodeId, item.Location.EpisodeId, StringComparison.Ordinal) is false))
            {
                var episode = m_Asset.FindEpisode(item.Location.EpisodeId);
                if (episode != null)
                {
                    SelectEpisode(episode);
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
            evt.menu.AppendAction("新增卷", _ => AddVolume());
        }

        private void BuildEpisodeGroupContextMenu(ContextualMenuPopulateEvent evt, int volumeIndex)
        {
            evt.menu.AppendAction("新增章节", _ => AddEpisode(volumeIndex));
        }

        private void BuildVolumeGroupContextMenu(ContextualMenuPopulateEvent evt, AuthoringVolume volume, int volumeIndex)
        {
            evt.menu.AppendAction("新增章节", _ => AddEpisode(volumeIndex));
            evt.menu.AppendAction("新增卷", _ => AddVolume());
            evt.menu.AppendAction(
                "删除卷",
                _ => RemoveVolume(volume),
                _ => m_Asset != null && m_Asset.Volumes.Count > 1 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        private void BuildEpisodeContextMenu(ContextualMenuPopulateEvent evt, AuthoringEpisode episode)
        {
            evt.menu.AppendAction("检查错误", _ =>
            {
                SelectEpisode(episode);
                CompileProgram();
            });

            var firstIssue = FirstEpisodeIssue(episode);
            evt.menu.AppendAction(
                "定位第一个问题",
                _ =>
                {
                    SelectEpisode(episode);
                    FocusDiagnostic(firstIssue);
                },
                _ => firstIssue == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("新增章节", _ => AddEpisode(FindVolumeIndexOfEpisode(episode)));
            evt.menu.AppendAction(
                "删除章节",
                _ => RemoveEpisode(episode),
                _ => m_Asset != null && GetEpisodeCount() > 1 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        private DiagnosticItem FirstEpisodeIssue(AuthoringEpisode episode)
        {
            if (episode == null || GraphDiagnostics == null)
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

                if (string.Equals(item.Location.EpisodeId, episode.EpisodeId, StringComparison.Ordinal))
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
            m_Report = new ValidationReport();
            m_CompilerDiagnostics = DiagnosticSet.Empty;
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

            var asset = AuthoringAssetStore.CreateAtPath(path);
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
            var asset = AssetDatabase.LoadAssetAtPath<AuthoringAsset>(assetPath);
            if (asset == null)
            {
                RefreshReport("打开失败：请选择 AuthoringAsset。");
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
            m_Asset = SampleGraphFixture.LoadOrCreateAsset();
            Selection.activeObject = m_Asset;
            ResetCompilerDiagnostics();
            SelectDefaults();
            RefreshAll("已打开示例剧情图。");
        }

        private void SaveAsset()
        {
            AuthoringAssetStore.Save(m_Asset);
            RefreshAll("已保存。");
        }

        private void CompileProgram()
        {
            m_LastCompiledProgram = ProgramCompiler.Compile(m_Asset, out m_Report);
            m_CompilerDiagnosticsStale = false;
            m_CompilerDiagnostics = Diagnostics.FromReport(m_Report, m_Asset, m_SelectedEpisode, false);
            var message = "编译失败。";
            if (m_Report.HasErrors is false && m_LastCompiledProgram != null)
            {
                var export = ProgramAssetExporter.ExportCompiled(m_Asset, m_LastCompiledProgram);
                var episodeCount = 0;
                for (var i = 0; i < m_LastCompiledProgram.Volumes.Count; i++)
                {
                    episodeCount += m_LastCompiledProgram.Volumes[i].Episodes.Count;
                }

                var summary = $"编译通过：{m_LastCompiledProgram.Volumes.Count} 卷，{episodeCount} 剧情段，{m_LastCompiledProgram.CommandSchema.Definitions.Count} 命令。";
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

        private void ExportExcel()
        {
            if (m_Asset == null)
            {
                EditorUtility.DisplayDialog("导出 Excel", "请先打开一个剧情编辑资源。", "确定");
                return;
            }

            var sourcePath = AssetDatabase.GetAssetPath(m_Asset);
            var directory = System.IO.Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            var fileName = string.IsNullOrWhiteSpace(m_Asset.StoryId) ? "story_export" : m_Asset.StoryId;

            var outputPath = EditorUtility.SaveFilePanel("导出 Excel", directory, fileName, "xlsx");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return;
            }

            try
            {
                Exporter.Export(m_Asset, outputPath);
                EditorUtility.DisplayDialog("导出 Excel", $"成功导出到:\n{outputPath}", "确定");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("导出失败", ex.Message, "确定");
                Debug.LogException(ex);
            }
        }

        private void ImportExcel()
        {
            if (m_Asset == null)
            {
                EditorUtility.DisplayDialog("导入 Excel", "请先打开一个剧情编辑资源。", "确定");
                return;
            }

            var sourcePath = AssetDatabase.GetAssetPath(m_Asset);
            var directory = System.IO.Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');

            var inputPath = EditorUtility.OpenFilePanel("导入 Excel", directory, "xlsx");
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                return;
            }

            try
            {
                var report = Importer.Import(inputPath, m_Asset);
                if (report.HasErrors)
                {
                    var builder = new StringBuilder();
                    builder.AppendLine("导入失败，以下校验未通过：");
                    builder.AppendLine();
                    for (var i = 0; i < report.Issues.Count; i++)
                    {
                        builder.AppendLine($"  {report.Issues[i]}");
                    }

                    EditorUtility.DisplayDialog("导入失败", builder.ToString(), "确定");
                }
                else
                {
                    AssetDatabase.Refresh();
                    RefreshAll("Excel 导入成功。");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("导入失败", ex.Message, "确定");
                Debug.LogException(ex);
            }
        }

        private void AddEpisode(int volumeIndex)
        {
            if (m_Asset == null || volumeIndex < 0 || volumeIndex >= m_Asset.Volumes.Count)
            {
                return;
            }

            var volume = m_Asset.Volumes[volumeIndex];
            var result = new RouteMutation(m_Asset).AddRootEpisode(
                volume.VolumeId,
                new EpisodeMetadata($"第{GetEpisodeCount() + 1}章", string.Empty, null));
            if (result.Succeeded is false)
            {
                RefreshReport(result.Message);
                return;
            }

            m_SelectedVolume = volume;
            m_SelectedEpisode = m_Asset.FindEpisode(result.EpisodeId);
            m_SelectedRouteNodeId = result.EpisodeId;
            m_SelectedNodeIds.Clear();
            m_SelectionKind = SelectionKind.Episode;
            MarkDirty();
            RefreshAll(result.Message);
        }

        private void AddEpisode()
        {
            AddEpisode(0);
        }

        private void AddVolume()
        {
            if (m_Asset == null)
            {
                return;
            }

            RecordStoryUndo("Add Story Volume");
            var id = MakeUnique("volume", m_Asset.Volumes.Select(x => x.VolumeId));
            var volume = new AuthoringVolume
            {
                VolumeId = id,
                Title = $"第{m_Asset.Volumes.Count + 1}卷",
                Route = new AuthoringRoute()
            };
            var episode = CreateEpisode(IdentityId.New());
            episode.Title = $"第{GetEpisodeCount() + 1}章";
            volume.Episodes.Add(episode);
            volume.Route.Edges.Add(new AuthoringRouteEdge
            {
                EdgeId = IdentityId.RootEdge(episode.EpisodeId),
                SourceKind = RouteEdgeSourceKind.Root,
                ToEpisodeId = episode.EpisodeId
            });
            m_Asset.Volumes.Add(volume);
            m_SelectedVolume = volume;
            m_SelectedEpisode = episode;
            m_SelectedRouteNodeId = episode.EpisodeId;
            MarkDirty();
            RefreshAll("已添加卷。");
        }

        private void RemoveVolume(AuthoringVolume volume)
        {
            if (m_Asset == null || volume == null || m_Asset.Volumes.Count <= 1)
            {
                return;
            }

            RecordStoryUndo("Remove Story Volume");
            if (volume.Episodes.Count > 0)
            {
                var targetVolume = m_Asset.Volumes.FirstOrDefault(x => x != null && !ReferenceEquals(x, volume));
                if (targetVolume != null)
                {
                    targetVolume.Episodes.AddRange(volume.Episodes);
                }
            }

            m_Asset.Volumes.Remove(volume);
            var remainingEpisodes = GetAllEpisodes();
            if (remainingEpisodes.Count > 0)
            {
                m_SelectedEpisode = remainingEpisodes[0];
            }

            m_SelectedNodeIds.Clear();
            m_SelectionKind = SelectionKind.Episode;
            MarkDirty();
            RefreshAll("已删除卷，章节已迁移至相邻卷。");
        }

        private void RemoveEpisode(AuthoringEpisode episode)
        {
            if (episode == null)
            {
                return;
            }

            m_SelectedEpisode = episode;
            RemoveSelectedEpisode();
        }

        private void RemoveSelectedEpisode()
        {
            if (m_SelectedEpisode == null)
            {
                return;
            }

            var episodeCount = GetEpisodeCount();
            if (episodeCount <= 1)
            {
                return;
            }

            RecordStoryUndo("Remove Story Episode");

            var episode = m_SelectedEpisode;
            for (var v = 0; v < m_Asset.Volumes.Count; v++)
            {
                var vol = m_Asset.Volumes[v];
                if (vol?.Episodes == null)
                {
                    continue;
                }

                if (vol.Episodes.Remove(episode))
                {
                    break;
                }
            }

            var allEpisodes = GetAllEpisodes();
            m_SelectedEpisode = allEpisodes.Count > 0 ? allEpisodes[0] : null;
            m_SelectedNodeIds.Clear();
            m_SelectionKind = SelectionKind.Episode;
            MarkDirty();
            RefreshAll("已删除章节。");
        }

        private int GetEpisodeCount()
        {
            var count = 0;
            for (var v = 0; v < m_Asset.Volumes.Count; v++)
            {
                var vol = m_Asset.Volumes[v];
                if (vol?.Episodes != null)
                {
                    count += vol.Episodes.Count;
                }
            }

            return count;
        }

        private List<AuthoringEpisode> GetAllEpisodes()
        {
            var result = new List<AuthoringEpisode>();
            for (var v = 0; v < m_Asset.Volumes.Count; v++)
            {
                var vol = m_Asset.Volumes[v];
                if (vol?.Episodes != null)
                {
                    for (var i = 0; i < vol.Episodes.Count; i++)
                    {
                        if (vol.Episodes[i] != null)
                        {
                            result.Add(vol.Episodes[i]);
                        }
                    }
                }
            }

            return result;
        }

        private int FindVolumeIndexOfEpisode(AuthoringEpisode episode)
        {
            for (var v = 0; v < m_Asset.Volumes.Count; v++)
            {
                var vol = m_Asset.Volumes[v];
                if (vol?.Episodes != null && vol.Episodes.Contains(episode))
                {
                    return v;
                }
            }

            return 0;
        }

        private AuthoringNode AddNodeAt(
            Vector2 position,
            NodeKind kind,
            AuthoringNode fromNode,
            string fromPortId,
            string fromPortLabel,
            string logicId = null)
        {
            if (m_SelectedEpisode == null)
            {
                return null;
            }

            if (kind == NodeKind.Start)
            {
                RefreshReport("开始节点由剧情段自动维护。");
                return null;
            }

            if (NodeSchemaRegistry.IsDefaultAuthoringNode(kind) is false)
            {
                RefreshReport("该节点已退出默认作者节点库，请使用内容、媒体、音频、等待、选项或事件节点表达剧情。");
                return null;
            }

            RecordStoryUndo("Add Story Node");
            var schema = kind == NodeKind.Logic
                ? LogicNodeSchemaResolver.Resolve(logicId)
                : NodeSchemaRegistry.Get(kind);
            var node = new AuthoringNode
            {
                NodeId = UsesPublishedExitIdentity(kind)
                    ? IdentityId.New()
                    : MakeUnique(ToIdBase(kind), m_SelectedEpisode.Nodes.Select(x => x.NodeId)),
                Title = schema.DisplayName,
                NodeKind = kind
            };
            AddDefaultParameters(node, schema);
            if (kind == NodeKind.Logic)
            {
                SetParameterValue(node, LogicCommandCodec.LogicIdParameter, logicId ?? string.Empty);
            }

            m_SelectedEpisode.Nodes.Add(node);
            GetLayout(node).Position = position;
            if (fromNode != null)
            {
                if (PortPolicy.CanConnect(m_SelectedEpisode, fromNode, fromPortId, node).Allowed)
                {
                    var edge = CreateEdge(fromNode, fromPortId, fromPortLabel, TransitionTargetKind.Node, node.NodeId);
                    AddEdgeToEpisode(fromNode, edge);
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

        internal void AddLogicNodeFromGraph(
            Vector2 position,
            string logicId,
            EditorGraphPortRef connectFrom)
        {
            var fromNode = connectFrom.IsValid ? FindNode(connectFrom.NodeId) : null;
            var portLabel = fromNode == null ? null : ResolveOutputPortLabel(fromNode, connectFrom.PortId);
            AddNodeAt(
                position,
                NodeKind.Logic,
                fromNode,
                connectFrom.PortId,
                portLabel,
                logicId);
        }

        internal void MoveNodeFromGraph(string nodeId, Vector2 position)
        {
            var node = FindNode(nodeId);
            if (node == null)
            {
                return;
            }

            RecordStoryUndo("Move Story Node");
            GetLayout(node).Position = position;
            MarkDirty();
        }

        internal void MoveNodesFromGraph(IReadOnlyList<EditorNodeGraphMove> moves)
        {
            if (moves == null || moves.Count == 0)
            {
                return;
            }

            RecordStoryUndo("Move Story Nodes");
            for (var i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                var node = FindNode(move.NodeId);
                if (node != null)
                {
                    GetLayout(node).Position = move.Position;
                }
            }

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
                m_SelectionKind = SelectionKind.Episode;
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

            if (PortPolicy.CanConnect(m_SelectedEpisode, fromNode, output.PortId, targetNode).Allowed is false)
            {
                return;
            }

            AddEdge(fromNode, output.PortId, ResolveOutputPortLabel(fromNode, output.PortId), targetNode);
        }

        internal void DisconnectFromGraph(string wireId)
        {
            var edge = FindEdge(wireId);
            if (edge == null || m_SelectedEpisode == null)
            {
                return;
            }

            RecordStoryUndo("Disconnect Story Nodes");
            m_SelectedEpisode.Edges.Remove(edge);
            if (ReferenceEquals(m_SelectedEdge, edge))
            {
                m_SelectedEdge = null;
                m_SelectedNodeIds.Clear();
                m_SelectionKind = SelectionKind.Episode;
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

            RecordStoryUndo("Edit Story Node");
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

        private void AddEdge(AuthoringNode fromNode, string portId, string portLabel, AuthoringNode targetNode)
        {
            if (m_SelectedEpisode == null || fromNode == null || targetNode == null)
            {
                return;
            }

            RecordStoryUndo("Connect Story Nodes");
            if (fromNode.NodeKind == NodeKind.Parallel && string.Equals(portId, "branch", StringComparison.Ordinal))
            {
                portId = NextParallelBranchPortId(fromNode);
                portLabel = $"轨道 {ParallelBranchIndex(portId)}";
            }

            var edge = CreateEdge(fromNode, portId, portLabel, TransitionTargetKind.Node, targetNode.NodeId);
            AddEdgeToEpisode(fromNode, edge);
            m_SelectedEdge = edge;
            m_SelectedNode = null;
            m_SelectedNodeIds.Clear();
            m_SelectionKind = SelectionKind.Edge;
            MarkDirty();
            RefreshAll("已连接节点。");
        }

        private void AddStoryEndEdge(AuthoringNode fromNode, string portId, string portLabel)
        {
            if (m_SelectedEpisode == null || fromNode == null)
            {
                return;
            }

            RecordStoryUndo("Connect Story End");
            var edge = CreateEdge(fromNode, portId, portLabel, TransitionTargetKind.StoryEnd, null);
            AddEdgeToEpisode(fromNode, edge);
            m_SelectedEdge = edge;
            m_SelectedNode = null;
            m_SelectedNodeIds.Clear();
            m_SelectionKind = SelectionKind.Edge;
            MarkDirty();
            RefreshAll("已连接剧情结束。");
        }

        private AuthoringEdge CreateEdge(
            AuthoringNode fromNode,
            string portId,
            string portLabel,
            TransitionTargetKind targetKind,
            string targetNodeId)
        {
            portId = string.IsNullOrWhiteSpace(portId) ? FirstOutputPortId(fromNode) : portId;
            portLabel = string.IsNullOrWhiteSpace(portLabel) ? portId : portLabel;
            return new AuthoringEdge
            {
                EdgeId = MakeUnique($"edge_{fromNode.NodeId}_{portId}", m_SelectedEpisode.Edges.Select(x => x.EdgeId)),
                FromNodeId = fromNode.NodeId,
                FromPortId = portId,
                FromPortLabel = portLabel,
                TargetKind = targetKind,
                TargetNodeId = targetKind == TransitionTargetKind.Node ? targetNodeId : null
            };
        }

        private void AddEdgeToEpisode(AuthoringNode fromNode, AuthoringEdge edge)
        {
            var targetNode = edge.TargetKind == TransitionTargetKind.Node ? FindNode(edge.TargetNodeId) : null;
            if (PortPolicy.IsLineChoicePort(fromNode, edge.FromPortId, targetNode))
            {
                m_SelectedEpisode.Edges.RemoveAll(x =>
                    x != null &&
                    string.Equals(x.FromNodeId, edge.FromNodeId, StringComparison.Ordinal) &&
                    string.Equals(x.FromPortId, edge.FromPortId, StringComparison.Ordinal) &&
                    (x.TargetKind != TransitionTargetKind.Node ||
                     FindNode(x.TargetNodeId)?.NodeKind != NodeKind.Choice));
            }
            else if (IsMultipleOutputPort(fromNode, edge.FromPortId, targetNode) is false)
            {
                m_SelectedEpisode.Edges.RemoveAll(x =>
                    x != null &&
                    string.Equals(x.FromNodeId, edge.FromNodeId, StringComparison.Ordinal) &&
                    string.Equals(x.FromPortId, edge.FromPortId, StringComparison.Ordinal));
            }

            m_SelectedEpisode.Edges.Add(edge);
        }

        private void RemoveSelection()
        {
            if (m_SelectedNodeIds.Count > 1 && m_SelectedEpisode != null)
            {
                var removableIds = new HashSet<string>(
                    m_SelectedEpisode.Nodes
                        .Where(x => x != null &&
                                    m_SelectedNodeIds.Contains(x.NodeId) &&
                                    x.NodeKind != NodeKind.Start &&
                                    IsBoundRouteExit(x.NodeId) is false)
                        .Select(x => x.NodeId),
                    StringComparer.Ordinal);
                if (removableIds.Count == 0)
                {
                    RefreshReport("开始节点和已绑定分支的出口不能删除。");
                    return;
                }

                RecordStoryUndo("Remove Story Nodes");

                m_SelectedEpisode.Nodes.RemoveAll(x => x != null && removableIds.Contains(x.NodeId));
                m_SelectedEpisode.Edges.RemoveAll(x =>
                    x != null &&
                    (removableIds.Contains(x.FromNodeId) || removableIds.Contains(x.TargetNodeId)));
                m_SelectedNode = null;
                m_SelectedEdge = null;
                m_SelectedNodeIds.Clear();
                m_SelectionKind = SelectionKind.Episode;
                MarkDirty();
                RefreshAll($"已删除 {removableIds.Count} 个节点。");
                return;
            }

            if (m_SelectionKind == SelectionKind.Edge && m_SelectedEdge != null && m_SelectedEpisode != null)
            {
                RecordStoryUndo("Remove Story Edge");
                m_SelectedEpisode.Edges.Remove(m_SelectedEdge);
                m_SelectedEdge = null;
                m_SelectedNodeIds.Clear();
                m_SelectionKind = SelectionKind.Episode;
                MarkDirty();
                RefreshAll("已删除连线。");
                return;
            }

            if (m_SelectionKind == SelectionKind.Node && m_SelectedNode != null && m_SelectedEpisode != null)
            {
                if (m_SelectedNode.NodeKind == NodeKind.Start || IsBoundRouteExit(m_SelectedNode.NodeId))
                {
                    RefreshReport("开始节点和已绑定分支的出口不能删除。");
                    return;
                }

                RecordStoryUndo("Remove Story Node");

                var nodeId = m_SelectedNode.NodeId;
                m_SelectedEpisode.Nodes.Remove(m_SelectedNode);
                m_SelectedEpisode.Edges.RemoveAll(x =>
                    x != null &&
                    (string.Equals(x.FromNodeId, nodeId, StringComparison.Ordinal) ||
                     string.Equals(x.TargetNodeId, nodeId, StringComparison.Ordinal)));
                m_SelectedNode = null;
                m_SelectedNodeIds.Clear();
                m_SelectionKind = SelectionKind.Episode;
                MarkDirty();
                RefreshAll("已删除节点。");
            }
        }

        private bool IsBoundRouteExit(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || m_SelectedEpisode == null)
            {
                return false;
            }

            var volume = FindVolume(m_SelectedEpisode);
            for (var i = 0; i < (volume?.Route?.Edges.Count ?? 0); i++)
            {
                var edge = volume.Route.Edges[i];
                if (edge != null &&
                    edge.SourceKind == RouteEdgeSourceKind.EpisodeExit &&
                    string.Equals(edge.FromEpisodeId, m_SelectedEpisode.EpisodeId, StringComparison.Ordinal) &&
                    string.Equals(edge.FromExitId, nodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void SelectStory()
        {
            m_SelectedNode = null;
            m_SelectedEdge = null;
            m_SelectedNodeIds.Clear();
            m_SelectionKind = SelectionKind.Story;
            RefreshAll();
        }

        private void SelectEpisode(AuthoringEpisode episode)
        {
            EnterEpisodeDetail(episode);
        }

        private void SelectNode(AuthoringNode node)
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

        private void SelectEdge(AuthoringEdge edge)
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
                    schema.Kind == NodeKind.Logic ||
                    NodeSchemaRegistry.IsDefaultAuthoringNode(schema.Kind) is false)
                {
                    continue;
                }

                var kind = schema.Kind;
                menu.AddItem(new GUIContent($"创建/{CategoryLabel(schema.Category)}/{schema.DisplayName}"), false, () => AddNodeAt(position, kind, null, null, null));
            }

            foreach (var definition in LogicDefinitionCatalog.Shared.Definitions)
            {
                var logicId = definition.LogicId;
                menu.AddItem(
                    new GUIContent($"创建/代码节点/{definition.Category}/{definition.DisplayName}"),
                    false,
                    () => AddNodeAt(position, NodeKind.Logic, null, null, null, logicId));
            }

            menu.ShowAsContext();
        }

        private Vector2 GetCanvasCenterPosition()
        {
            return m_Canvas == null ? Vector2.zero : m_Canvas.GetGraphCenterPosition();
        }

        private EpisodeNodePlacement GetLayout(object element)
        {
            var nodeId = ElementLayoutId(element);
            var placements = m_SelectedEpisode.DetailLayout.Nodes;
            var layout = placements.FirstOrDefault(x =>
                x != null && string.Equals(x.NodeId, nodeId, StringComparison.Ordinal));
            if (layout != null)
            {
                return layout;
            }

            layout = new EpisodeNodePlacement
            {
                NodeId = nodeId,
                Position = Vector2.zero
            };
            placements.Add(layout);
            return layout;
        }

        internal Vector2 GetNodeGraphPosition(AuthoringNode node, int index)
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

        internal AuthoringNode FindNode(string nodeId)
        {
            if (m_SelectedEpisode == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return null;
            }

            for (var i = 0; i < m_SelectedEpisode.Nodes.Count; i++)
            {
                var node = m_SelectedEpisode.Nodes[i];
                if (node != null && string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
                {
                    return node;
                }
            }

            return null;
        }

        internal AuthoringEdge FindEdge(string edgeId)
        {
            if (m_SelectedEpisode == null || string.IsNullOrWhiteSpace(edgeId))
            {
                return null;
            }

            for (var i = 0; i < m_SelectedEpisode.Edges.Count; i++)
            {
                var edge = m_SelectedEpisode.Edges[i];
                if (edge != null && string.Equals(edge.EdgeId, edgeId, StringComparison.Ordinal))
                {
                    return edge;
                }
            }

            return null;
        }

        internal string ResolveOutputPortLabel(AuthoringNode node, string portId)
        {
            if (node == null)
            {
                return portId;
            }

            if (node.NodeKind == NodeKind.Parallel && PortPolicy.IsParallelBranchPort(portId))
            {
                return ResolveExistingParallelBranchLabel(node, portId) ?? $"轨道 {ParallelBranchIndex(portId)}";
            }

            var schema = NodeSchemaResolver.Resolve(node);
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
            if (element is AuthoringNode node)
            {
                return node.NodeId;
            }

            return element == null ? "unknown" : element.ToString();
        }

        private void EnsureSelection()
        {
            var episodes = GetAllEpisodes();
            if (m_SelectedEpisode == null || episodes.Contains(m_SelectedEpisode) is false)
            {
                m_SelectedEpisode = m_Asset.FindDefaultEpisode() ?? episodes.FirstOrDefault();
            }

            if (m_SelectedEpisode == null)
            {
                m_SelectedNodeIds.Clear();
            }
            else
            {
                m_SelectedNodeIds.RemoveWhere(nodeId =>
                    m_SelectedEpisode.Nodes.Any(x => x != null && string.Equals(x.NodeId, nodeId, StringComparison.Ordinal)) is false);
            }

            if (m_SelectedNode != null && (m_SelectedEpisode == null || m_SelectedEpisode.Nodes.Contains(m_SelectedNode) is false))
            {
                m_SelectedNode = null;
                m_SelectedNodeIds.Clear();
                m_SelectionKind = SelectionKind.Episode;
            }

            if (m_SelectedEdge != null && (m_SelectedEpisode == null || m_SelectedEpisode.Edges.Contains(m_SelectedEdge) is false))
            {
                m_SelectedEdge = null;
                m_SelectedNodeIds.Clear();
                m_SelectionKind = SelectionKind.Episode;
            }

            EnsureRouteSelection();
        }

        private void SelectDefaults()
        {
            var allEpisodes = GetAllEpisodes();
            m_SelectedEpisode = m_Asset.FindDefaultEpisode() ?? allEpisodes.FirstOrDefault();
            m_SelectedNode = null;
            m_SelectedEdge = null;
            m_SelectedNodeIds.Clear();
            m_SelectionKind = SelectionKind.Story;
            SelectDefaultRoute();
        }

        private static AuthoringEpisode CreateEpisode(string id)
        {
            var entryId = IdentityId.New();
            var episode = new AuthoringEpisode
            {
                EpisodeId = id,
                Title = id,
                EntryNodeId = entryId
            };
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = episode.EntryNodeId,
                Title = "开始",
                NodeKind = NodeKind.Start
            });
            return episode;
        }

        private void UpdateEpisodeReferences(string oldId, string newId)
        {
            if (string.IsNullOrWhiteSpace(oldId))
            {
                return;
            }

        }

        private void UpdateNodeReferences(string oldId, string newId)
        {
            if (string.IsNullOrWhiteSpace(oldId))
            {
                return;
            }

            if (m_SelectedEpisode != null && string.Equals(m_SelectedEpisode.EntryNodeId, oldId, StringComparison.Ordinal))
            {
                m_SelectedEpisode.EntryNodeId = newId;
            }

            foreach (var edge in m_SelectedEpisode?.Edges ?? new List<AuthoringEdge>())
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

        private static void AddDefaultParameters(AuthoringNode node, NodeSchema schema)
        {
            for (var i = 0; i < schema.Parameters.Count; i++)
            {
                var parameter = schema.Parameters[i];
                if (parameter.Required)
                {
                    node.Parameters.Add(new AuthoringParameter
                    {
                        Key = parameter.Key,
                        Value = DefaultParameterValue(node, parameter)
                    });
                }
            }
        }

        private static string DefaultParameterValue(AuthoringNode node, NodeParameterDefinition parameter)
        {
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

        private static string GetParameterValue(AuthoringNode node, string key)
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

        private static void SetParameterValue(AuthoringNode node, string key, string value)
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

            node.Parameters.Add(new AuthoringParameter { Key = key, Value = value });
        }

        internal bool IsMultipleOutputPort(AuthoringNode node, string portId)
        {
            return IsMultipleOutputPort(node, portId, null);
        }

        internal bool IsMultipleOutputPort(AuthoringNode node, string portId, AuthoringNode targetNode)
        {
            return PortPolicy.IsMultipleOutputPort(node, portId, targetNode);
        }

        private static string FirstOutputPortId(AuthoringNode node)
        {
            var schema = NodeSchemaResolver.Resolve(node);
            for (var i = 0; i < schema.Ports.Count; i++)
            {
                if (schema.Ports[i].Direction == PortDirection.Output)
                {
                    return schema.Ports[i].PortId;
                }
            }

            return "completed";
        }

        private string NextParallelBranchPortId(AuthoringNode node)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            if (m_SelectedEpisode != null && node != null)
            {
                for (var i = 0; i < m_SelectedEpisode.Edges.Count; i++)
                {
                    var edge = m_SelectedEpisode.Edges[i];
                    if (edge != null &&
                        string.Equals(edge.FromNodeId, node.NodeId, StringComparison.Ordinal) &&
                        PortPolicy.IsParallelBranchPort(edge.FromPortId))
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

        private string ResolveExistingParallelBranchLabel(AuthoringNode node, string portId)
        {
            if (m_SelectedEpisode == null || node == null || string.IsNullOrWhiteSpace(portId))
            {
                return null;
            }

            for (var i = 0; i < m_SelectedEpisode.Edges.Count; i++)
            {
                var edge = m_SelectedEpisode.Edges[i];
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

        private static Label FindFoldoutHeaderLabel(Foldout foldout)
        {
            var toggle = foldout?.Q<Toggle>();
            return toggle?.Q<Label>();
        }

        private static bool IsInsideFoldoutHeader(VisualElement target, Foldout foldout)
        {
            if (target == null || foldout == null)
            {
                return false;
            }

            var toggle = foldout.Q<Toggle>();
            if (toggle == null)
            {
                return false;
            }

            while (target != null)
            {
                if (target == toggle)
                {
                    return true;
                }

                if (target.ClassListContains("story-editor__tree-row"))
                {
                    return false;
                }

                if (target == foldout)
                {
                    return false;
                }

                target = target.parent;
            }

            return false;
        }

        private static void BeginInlineRename(VisualElement target, VisualElement container, string currentText, Action<string> onRename)
        {
            if (target == null)
            {
                return;
            }

            var parent = container ?? target.parent;
            if (parent == null)
            {
                return;
            }

            var index = parent.IndexOf(target);
            target.RemoveFromHierarchy();

            var textField = new TextField { value = currentText ?? string.Empty, isDelayed = false };
            textField.AddToClassList("story-editor__inline-rename");
            textField.RegisterCallback<FocusOutEvent>(_ =>
            {
                CommitRename(textField, target, parent, index, onRename);
            });
            textField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    CommitRename(textField, target, parent, index, onRename);
                    evt.StopPropagation();
                }

                if (evt.keyCode == KeyCode.Escape)
                {
                    CancelRename(textField, target, parent, index);
                    evt.StopPropagation();
                }
            });

            if (index < 0 || index >= parent.childCount)
            {
                parent.Add(textField);
            }
            else
            {
                parent.Insert(index, textField);
            }

            textField.Focus();
            textField.SelectAll();
        }

        private static void CommitRename(TextField textField, VisualElement original, VisualElement parent, int index, Action<string> onRename)
        {
            var newText = textField?.value?.Trim() ?? string.Empty;
            textField?.RemoveFromHierarchy();
            if (index < 0 || index > parent.childCount)
            {
                parent.Add(original);
            }
            else
            {
                parent.Insert(index, original);
            }

            if (!string.IsNullOrWhiteSpace(newText))
            {
                onRename?.Invoke(newText);
            }
        }

        private static void CancelRename(TextField textField, VisualElement original, VisualElement parent, int index)
        {
            textField?.RemoveFromHierarchy();
            if (index < 0 || index > parent.childCount)
            {
                parent.Add(original);
            }
            else
            {
                parent.Insert(index, original);
            }
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

        private void RecordStoryUndo(string name)
        {
            if (m_Asset != null)
            {
                AuthoringUndo.Record(m_Asset, name);
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

        private static string FormatEpisodeLabel(AuthoringEpisode episode)
        {
            return string.IsNullOrWhiteSpace(episode.Title)
                ? $"章节  {SafeText(episode.EpisodeId, "episode")}"
                : $"章节  {episode.Title}";
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

        private static int CountCompilerErrors(ValidationReport report)
        {
            return report?.Issues.Count(x => x.Severity == ValidationSeverity.Error) ?? 0;
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

        private static bool UsesPublishedExitIdentity(NodeKind kind)
        {
            return kind == NodeKind.Choice || kind == NodeKind.End;
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
