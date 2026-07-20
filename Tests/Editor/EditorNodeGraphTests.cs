using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GameDeveloperKit.EditorNodeGraph;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Authoring;

namespace GameDeveloperKit.Tests
{
    public sealed class EditorNodeGraphTests
    {
        [Test]
        public void Canvas_WhenAdapterSet_RendersReusableGraphSurface()
        {
            var adapter = CreateAdapter();
            var canvas = new EditorNodeGraphCanvas();

            canvas.SetAdapter(adapter);

            var paletteItems = canvas.Query<VisualElement>(className: "editor-node-graph-palette__item").ToList();
            var nodeViews = canvas.Query<VisualElement>(className: "editor-node-graph-node").ToList();
            var portDots = canvas.Query<VisualElement>(className: "editor-node-graph-node__port-dot").ToList();
            var minimapMarkers = canvas.Query<VisualElement>(className: "editor-node-graph-minimap__node").ToList();
            var labels = FindVisualChildren<Label>(canvas).Select(x => x.text).ToList();
            var textFields = canvas.Query<TextField>().ToList();
            var floatFields = canvas.Query<FloatField>().ToList();
            var toggles = canvas.Query<Toggle>().ToList();
            var dropdowns = canvas.Query<DropdownField>().ToList();

            Assert.AreEqual(2, paletteItems.Count);
            Assert.IsTrue(paletteItems.Any(x => x.ClassListContains("editor-node-graph-palette__item--action")));
            Assert.IsFalse(canvas.Query<Button>(className: "editor-node-graph-palette__item").ToList().Any());
            Assert.AreEqual(2, nodeViews.Count);
            Assert.IsTrue(nodeViews.Any(x => x.ClassListContains("editor-node-graph-node--action")));
            Assert.IsTrue(portDots.Any(x => x.userData is EditorGraphPortRef port && port.NodeId == "video" && port.PortId == "completed"));
            Assert.AreEqual(2, minimapMarkers.Count);
            CollectionAssert.Contains(labels, "节点库");
            CollectionAssert.Contains(labels, "导航");
            CollectionAssert.Contains(textFields.Select(x => x.label).ToList(), "标题");
            CollectionAssert.Contains(textFields.Select(x => x.label).ToList(), "视频 *");
            CollectionAssert.Contains(floatFields.Select(x => x.label).ToList(), "时长");
            CollectionAssert.Contains(toggles.Select(x => x.label).ToList(), "等待完成");
            CollectionAssert.Contains(dropdowns.Select(x => x.label).ToList(), "运算符");
        }

        [Test]
        public void Canvas_WhenTemplateCreated_DelegatesToAdapterWithGraphPosition()
        {
            var adapter = CreateAdapter();
            var canvas = new EditorNodeGraphCanvas();
            var connectFrom = new EditorGraphPortRef("video", "completed");

            canvas.SetAdapter(adapter);
            InvokeNonPublic(
                canvas,
                "CreateTemplateAt",
                adapter.Templates[0],
                new Vector2(320f, 160f),
                connectFrom);

            Assert.AreEqual(1, adapter.CreatedNodes.Count);
            Assert.AreSame(adapter.Templates[0], adapter.CreatedNodes[0].Template);
            Assert.AreEqual(new Vector2(320f, 160f), adapter.CreatedNodes[0].Position);
            Assert.AreEqual(connectFrom, adapter.CreatedNodes[0].ConnectFrom);
            Assert.IsTrue(adapter.Nodes.Any(x => x.NodeId == "created_1"));
        }

        [Test]
        public void Canvas_WhenPaletteDropFallsOutsideGraphArea_DoesNotCreateNode()
        {
            var adapter = CreateAdapter();
            var canvas = new EditorNodeGraphCanvas();

            canvas.SetAdapter(adapter);
            var created = InvokeNonPublic<bool>(
                canvas,
                "TryCreateTemplateFromPaletteDrop",
                adapter.Templates[0],
                new Vector2(-1000f, -1000f));

            Assert.IsFalse(created);
            Assert.AreEqual(0, adapter.CreatedNodes.Count);
        }

        [Test]
        public void Canvas_WhenPortsConnected_UsesAdapterValidation()
        {
            var adapter = CreateAdapter();
            var canvas = new EditorNodeGraphCanvas();
            var output = new EditorGraphPortRef("video", "completed");
            var input = new EditorGraphPortRef("end", "in");

            canvas.SetAdapter(adapter);
            var success = InvokeNonPublic<EditorGraphConnectionResult>(canvas, "ConnectPorts", output, input);

            Assert.IsTrue(success.Allowed);
            Assert.AreEqual(1, adapter.ConnectionChecks.Count);
            Assert.AreEqual(1, adapter.Connections.Count);
            Assert.AreEqual(output, adapter.Connections[0].Output);
            Assert.AreEqual(input, adapter.Connections[0].Input);

            adapter.AllowConnection = false;
            adapter.FailureMessage = "不能连接到自己。";
            var failure = InvokeNonPublic<EditorGraphConnectionResult>(canvas, "ConnectPorts", output, output);
            var status = canvas.Q<Label>(className: "editor-node-graph__status");

            Assert.IsFalse(failure.Allowed);
            Assert.AreEqual(2, adapter.ConnectionChecks.Count);
            Assert.AreEqual(1, adapter.Connections.Count);
            Assert.AreEqual("不能连接到自己。", status.text);
        }

        [Test]
        public void Canvas_WhenZoomedAtMouse_KeepsGraphPointAnchored()
        {
            var canvas = new EditorNodeGraphCanvas();
            var mouse = new Vector2(220f, 180f);

            var before = canvas.CanvasToGraph(mouse);
            InvokeNonPublic(canvas, "ZoomAt", mouse, 1.8f);
            var after = canvas.CanvasToGraph(mouse);

            AssertVectorApproximately(before, after);
        }

        [Test]
        public void Canvas_WhenDeleteSelectionRequested_DelegatesToAdapter()
        {
            var adapter = CreateAdapter();
            var canvas = new EditorNodeGraphCanvas();

            canvas.SetAdapter(adapter);
            InvokeNonPublic(canvas, "DeleteSelection");

            Assert.AreEqual(1, adapter.DeleteSelectionCount);
        }

        [Test]
        public void NodeView_WhenDragged_PreviewsContinuouslyAndCommitsOnceAtEnd()
        {
            var commits = new List<Vector2>();
            var previews = new List<Vector2>();
            var node = new EditorGraphNodeModel(
                "episode",
                "Episode",
                "剧情段",
                "路线",
                new Vector2(100f, 100f),
                Array.Empty<EditorGraphPortModel>(),
                Array.Empty<EditorGraphPortModel>(),
                Array.Empty<EditorGraphFieldModel>());
            var view = new EditorNodeGraphNodeView(
                node,
                () => 1f,
                null,
                null,
                null,
                (_, position) => commits.Add(position),
                (_, delta) => previews.Add(delta),
                null,
                null,
                null);

            view.ApplyMoveDelta(new Vector2(20f, 5f));
            view.ApplyMoveDelta(new Vector2(30f, 10f));

            Assert.AreEqual(0, commits.Count);
            Assert.AreEqual(2, previews.Count);
            Assert.AreEqual(new Vector2(150f, 115f), view.Position);

            view.CommitMove();

            CollectionAssert.AreEqual(new[] { new Vector2(150f, 115f) }, commits);
        }

        [Test]
        public void Canvas_WhenNodeActivated_DelegatesWithoutChangingSelection()
        {
            var adapter = CreateAdapter();
            var canvas = new EditorNodeGraphCanvas();

            canvas.SetAdapter(adapter);
            InvokeNonPublic(canvas, "OnNodeActivated", "video");

            CollectionAssert.AreEqual(new[] { "video" }, adapter.ActivatedNodes);
            Assert.AreEqual(0, adapter.SelectedNodes.Count);
        }

        [Test]
        public void Canvas_WhenGraphRectOverlapsNodes_FindsNodeIds()
        {
            var adapter = CreateAdapter();
            var canvas = new EditorNodeGraphCanvas();

            canvas.SetAdapter(adapter);
            var selected = InvokeNonPublic<IReadOnlyList<string>>(
                canvas,
                "FindNodesInGraphRect",
                Rect.MinMaxRect(80f, 80f, 760f, 260f));

            CollectionAssert.AreEquivalent(new[] { "video", "end" }, selected);
        }

        [Test]
        public void Canvas_WhenGraphRectSelected_DelegatesMultiSelection()
        {
            var adapter = CreateAdapter();
            var canvas = new EditorNodeGraphCanvas();

            canvas.SetAdapter(adapter);
            var selected = InvokeNonPublic<IReadOnlyList<string>>(
                canvas,
                "SelectNodesInGraphRect",
                Rect.MinMaxRect(80f, 80f, 760f, 260f));

            CollectionAssert.AreEquivalent(new[] { "video", "end" }, selected);
            CollectionAssert.AreEquivalent(new[] { "video", "end" }, adapter.SelectedNodes);
        }

        [Test]
        public void Canvas_WhenCreated_ContentTransformOriginIsTopLeft()
        {
            var canvas = new EditorNodeGraphCanvas();
            var content = canvas.Q(className: "editor-node-graph__content");

            Assert.IsNotNull(content);
            Assert.AreEqual(0f, content.style.transformOrigin.value.x.value, 0.0001f);
            Assert.AreEqual(0f, content.style.transformOrigin.value.y.value, 0.0001f);
        }

        [Test]
        public void Canvas_WhenNodeFieldCommitted_WritesThroughAdapter()
        {
            var adapter = CreateAdapter();
            var canvas = new EditorNodeGraphCanvas();

            canvas.SetAdapter(adapter);
            var title = canvas.Query<TextField>().ToList().First(x => x.label == "标题");
            Assert.IsTrue(title.isDelayed);
            InvokeNonPublic(canvas, "OnNodeFieldChanged", "video", "title", "新的标题");

            Assert.IsTrue(adapter.FieldChanges.Any(x =>
                x.NodeId == "video" &&
                x.FieldId == "title" &&
                x.Value == "新的标题"));
        }

        [Test]
        public void Canvas_WhenCustomFieldProvided_UsesAdapterRendererAndWritesChanges()
        {
            var adapter = CreateAdapter();
            adapter.NodeList.Clear();
            adapter.NodeList.Add(new EditorGraphNodeModel(
                "custom",
                "自定义",
                "自定义",
                "测试",
                Vector2.zero,
                Array.Empty<EditorGraphPortModel>(),
                Array.Empty<EditorGraphPortModel>(),
                new[]
                {
                    new EditorGraphFieldModel(
                        "value",
                        "自定义值",
                        "before",
                        EditorGraphFieldValueType.Custom,
                        customType: "test.custom")
                }));
            var canvas = new EditorNodeGraphCanvas();

            canvas.SetAdapter(adapter);
            Assert.IsNotNull(canvas.Q<Button>("test-custom-field"));
            adapter.CustomValueChanged("after");

            Assert.GreaterOrEqual(adapter.CustomFieldCreateCount, 1);
            Assert.IsTrue(adapter.FieldChanges.Any(x =>
                x.NodeId == "custom" &&
                x.FieldId == "value" &&
                x.Value == "after"));
        }

        [Test]
        public void NodeView_WhenTitleMatchesSubtitle_RendersTitleOnce()
        {
            var node = new EditorGraphNodeModel(
                "parallel",
                "并行",
                "并行",
                "流程",
                Vector2.zero,
                Array.Empty<EditorGraphPortModel>(),
                Array.Empty<EditorGraphPortModel>(),
                Array.Empty<EditorGraphFieldModel>());

            var view = new EditorNodeGraphNodeView(
                node,
                () => 1f,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);

            var header = view.Q(className: "editor-node-graph-node__header");
            var labels = FindVisualChildren<Label>(header).Select(x => x.text).ToList();

            Assert.AreEqual(1, labels.Count(x => string.Equals(x, "并行", StringComparison.Ordinal)));
            Assert.IsFalse(header.Query<Label>(className: "editor-node-graph-node__type").ToList().Any());
        }

        [Test]
        public void NodeView_WhenDoubleClicked_ActivatesWithoutSelectingOrDragging()
        {
            var selected = new List<string>();
            var activated = new List<string>();
            var node = new EditorGraphNodeModel(
                "episode_a",
                "剧情段 A",
                "剧情段",
                "路线",
                Vector2.zero,
                Array.Empty<EditorGraphPortModel>(),
                Array.Empty<EditorGraphPortModel>(),
                Array.Empty<EditorGraphFieldModel>());
            var view = new EditorNodeGraphNodeView(
                node,
                () => 1f,
                selected.Add,
                activated.Add,
                null,
                null,
                null,
                null,
                null,
                null);
            var systemEvent = new UnityEngine.Event
            {
                type = EventType.MouseDown,
                button = 0,
                clickCount = 2
            };
            var mouseDown = MouseDownEvent.GetPooled(systemEvent);

            try
            {
                InvokeNonPublic(view, "OnMouseDown", mouseDown);
            }
            finally
            {
                mouseDown.Dispose();
            }

            CollectionAssert.AreEqual(new[] { "episode_a" }, activated);
            Assert.AreEqual(0, selected.Count);
            Assert.IsFalse(GetNonPublicField<bool>(view, "m_Dragging"));
        }

        [Test]
        public void Canvas_WhenDiagnosticsProvided_RendersElementClassesAndTooltips()
        {
            var adapter = CreateAdapter(true);
            var canvas = new EditorNodeGraphCanvas();

            canvas.SetAdapter(adapter);

            var videoNode = canvas.Query<VisualElement>(className: "editor-node-graph-node").ToList()
                .First(x => string.Equals(x.userData as string, "video", StringComparison.Ordinal));
            var clipField = videoNode.Query<TextField>().ToList().First(x => x.label == "视频 *");
            var completedPort = videoNode.Query<VisualElement>(className: "editor-node-graph-node__port-dot").ToList()
                .First(x => x.userData is EditorGraphPortRef port && port.PortId == "completed");
            var wireColor = InvokeStaticNonPublic<Color>(
                typeof(EditorNodeGraphWireLayer),
                "ResolveWireColor",
                adapter.Wires[0]);

            Assert.IsTrue(videoNode.ClassListContains("editor-node-graph-node--diagnostic-error"));
            Assert.IsTrue(clipField.ClassListContains("editor-node-graph-node__field--diagnostic-error"));
            Assert.IsTrue(completedPort.ClassListContains("editor-node-graph-node__port-dot--diagnostic-warning"));
            StringAssert.Contains("必填命令字段未填写", clipField.tooltip);
            Assert.AreEqual(1f, wireColor.r, 0.0001f);
            Assert.Less(wireColor.g, 0.5f);
        }

        [Test]
        public void Canvas_WhenReferenceCanvasAndSelectedPathProvided_RendersBoundedCanvasAndHandles()
        {
            var adapter = CreateAdapter();
            adapter.Canvas = new EditorGraphCanvasModel(new Vector2(1920f, 1080f), null, null);
            adapter.WireList.Clear();
            adapter.WireList.Add(new EditorGraphWireModel(
                "wire_path",
                new EditorGraphPortRef("video", "completed"),
                new EditorGraphPortRef("end", "in"),
                selected: true,
                controlPoints: new[] { new Vector2(260f, 180f), new Vector2(380f, 180f) },
                styleKey: "main",
                controlPointsEditable: true));
            var canvas = new EditorNodeGraphCanvas();

            canvas.SetAdapter(adapter);

            var reference = canvas.Q(className: "editor-node-graph-reference-canvas");
            Assert.IsNotNull(reference);
            Assert.AreEqual(DisplayStyle.Flex, reference.style.display.value);
            Assert.IsTrue(adapter.Canvas.IsBounded);
            Assert.AreEqual(2, canvas.Query<VisualElement>(className: "editor-node-graph-control-point").ToList().Count);
            Assert.AreEqual("main", adapter.Wires[0].StyleKey);
        }

        [Test]
        public void Canvas_WhenReferenceCanvasIsUnbounded_ShowsGuideWithoutClampingCoordinates()
        {
            var adapter = CreateAdapter();
            adapter.Canvas = new EditorGraphCanvasModel(
                new Vector2(1600f, 900f),
                null,
                null,
                false);
            var canvas = new EditorNodeGraphCanvas();

            canvas.SetAdapter(adapter);
            var position = InvokeNonPublic<Vector2>(
                canvas,
                "ClampToReferenceCanvas",
                new Vector2(2400f, -450f));

            Assert.AreEqual(DisplayStyle.Flex, canvas.Q(className: "editor-node-graph-reference-canvas").style.display.value);
            Assert.IsFalse(adapter.Canvas.IsBounded);
            Assert.AreEqual(new Vector2(2400f, -450f), position);
        }

        [Test]
        public void Canvas_WhenReferenceCanvasIsHorizontalStrip_ConstrainsOnlyYAxis()
        {
            var adapter = CreateAdapter();
            adapter.Canvas = new EditorGraphCanvasModel(
                new Vector2(1600f, 900f),
                null,
                null,
                EditorGraphCanvasConstraints.YAxis);
            var canvas = new EditorNodeGraphCanvas();

            canvas.SetAdapter(adapter);
            var position = InvokeNonPublic<Vector2>(
                canvas,
                "ClampToReferenceCanvas",
                new Vector2(2400f, -450f));

            Assert.IsFalse(adapter.Canvas.ConstrainsXAxis);
            Assert.IsTrue(adapter.Canvas.ConstrainsYAxis);
            Assert.AreEqual(new Vector2(2400f, 0f), position);
            Assert.AreEqual(
                DisplayStyle.Flex,
                canvas.Q(className: "editor-node-graph-reference-strip").style.display.value);
        }

        [Test]
        public void NodeGraphKit_WhenScanned_DoesNotReferenceStoryOrGraphView()
        {
            var files = Directory.GetFiles(FrameworkFilePath("Editor/NodeGraph"), "*.cs", SearchOption.AllDirectories);
            var source = string.Join(Environment.NewLine, files.Select(System.IO.File.ReadAllText));

            Assert.IsFalse(source.Contains("GameDeveloperKit.Story"), "NodeGraph kit must stay business-agnostic.");
            Assert.IsFalse(source.Contains("StoryEditor"), "NodeGraph kit must not depend on Story editor.");
            Assert.IsFalse(source.Contains("NodeKind"), "NodeGraph kit must not know Story node kinds.");
            Assert.IsFalse(source.Contains("GameDeveloperKit.Story.Model.Command"), "NodeGraph kit must not know Story command schema.");
            Assert.IsFalse(source.Contains("PlayVideo"), "NodeGraph kit must not know Story command names.");
            Assert.IsFalse(source.Contains("play_video"), "NodeGraph kit must not know Story command ids.");
            Assert.IsFalse(source.Contains("GraphView"), "NodeGraph kit must not use legacy GraphView.");
            Assert.IsFalse(source.Contains("\"流程\""), "NodeGraph kit must not hardcode Story category labels.");
            Assert.IsFalse(source.Contains("\"命令\""), "NodeGraph kit must not hardcode Story category labels.");
            Assert.IsFalse(source.Contains("\"交互\""), "NodeGraph kit must not hardcode Story category labels.");
            Assert.IsFalse(source.Contains("\"条件\""), "NodeGraph kit must not hardcode Story category labels.");
            Assert.IsFalse(source.Contains("\"辅助\""), "NodeGraph kit must not hardcode Story category labels.");
            Assert.IsFalse(source.Contains("\"flow\""), "NodeGraph kit must not hardcode Story style keys.");
            Assert.IsFalse(source.Contains("\"action\""), "NodeGraph kit must not hardcode Story style keys.");
            Assert.IsFalse(source.Contains("\"interaction\""), "NodeGraph kit must not hardcode Story style keys.");
            Assert.IsFalse(source.Contains("\"condition\""), "NodeGraph kit must not hardcode Story style keys.");
            Assert.IsFalse(source.Contains("\"auxiliary\""), "NodeGraph kit must not hardcode Story style keys.");
        }

        [Test]
        public void Runtime_WhenScanned_DoesNotReferenceEditorNodeGraphKit()
        {
            var files = Directory.GetFiles(FrameworkFilePath("Runtime"), "*.cs", SearchOption.AllDirectories);
            var source = string.Join(Environment.NewLine, files.Select(System.IO.File.ReadAllText));

            Assert.IsFalse(source.Contains("EditorNodeGraph"), "Runtime must not reference editor graph kit.");
            Assert.IsFalse(source.Contains("UIElements"), "Runtime must not reference UI Toolkit editor graph.");
            Assert.IsFalse(source.Contains("UnityEditor.Experimental.GraphView"), "Runtime must not reference GraphView.");
            Assert.IsFalse(source.Contains("ObjectField"), "Runtime must not reference UI Toolkit ObjectField.");
            Assert.IsFalse(source.Contains("UnityEngine.Video.VideoClip "), "Runtime must not reference the VideoClip type directly.");
        }

        private static FakeAdapter CreateAdapter(bool diagnostics = false)
        {
            var nodeDiagnostics = diagnostics
                ? new[]
                {
                    new EditorGraphDiagnostic(
                        "node-video",
                        EditorGraphDiagnosticSeverity.Error,
                        EditorGraphDiagnosticTargetKind.Node,
                        "节点存在错误。",
                        "节点存在错误。",
                        "video")
                }
                : null;
            var outputDiagnostics = diagnostics
                ? new[]
                {
                    new EditorGraphDiagnostic(
                        "port-completed",
                        EditorGraphDiagnosticSeverity.Warning,
                        EditorGraphDiagnosticTargetKind.Port,
                        "端口存在警告。",
                        "端口存在警告。",
                        "video",
                        portId: "completed")
                }
                : null;
            var clipDiagnostics = diagnostics
                ? new[]
                {
                    new EditorGraphDiagnostic(
                        "field-clip",
                        EditorGraphDiagnosticSeverity.Error,
                        EditorGraphDiagnosticTargetKind.Field,
                        "必填命令字段未填写。",
                        "必填命令字段未填写。",
                        "video",
                        "clip")
                }
                : null;
            var wireDiagnostics = diagnostics
                ? new[]
                {
                    new EditorGraphDiagnostic(
                        "wire-video-end",
                        EditorGraphDiagnosticSeverity.Error,
                        EditorGraphDiagnosticTargetKind.Wire,
                        "连线存在错误。",
                        "连线存在错误。",
                        wireId: "wire_video_end")
                }
                : null;
            var input = new EditorGraphPortModel("in", "进入", EditorGraphPortDirection.Input, EditorGraphPortCapacity.Multiple, Color.white, "输入端口。");
            var output = new EditorGraphPortModel("completed", "完成", EditorGraphPortDirection.Output, EditorGraphPortCapacity.Single, Color.cyan, "输出端口。", outputDiagnostics);
            var fields = new[]
            {
                new EditorGraphFieldModel("title", "标题", "播放视频", EditorGraphFieldValueType.Text, tooltip: "节点标题。"),
                new EditorGraphFieldModel("clip", "视频 *", "intro.mp4", EditorGraphFieldValueType.Text, tooltip: "必填。参数键：clip", diagnostics: clipDiagnostics),
                new EditorGraphFieldModel("duration", "时长", "1.5", EditorGraphFieldValueType.Number, tooltip: "可选。参数键：duration"),
                new EditorGraphFieldModel("wait", "等待完成", "true", EditorGraphFieldValueType.Boolean, tooltip: "可选。参数键：wait"),
                new EditorGraphFieldModel("operator", "运算符", "==", EditorGraphFieldValueType.Option, new[] { "==", "!=", ">" }, "必填。参数键：operator")
            };

            var adapter = new FakeAdapter();
            adapter.TemplateList.Add(new EditorGraphNodeTemplate(
                "dialogue",
                "对白",
                "命令",
                "对白",
                new[] { input, output },
                fields,
                "拖到画布中创建节点。",
                "action"));
            adapter.TemplateList.Add(new EditorGraphNodeTemplate(
                "comment",
                "注释",
                "辅助",
                "注释",
                Array.Empty<EditorGraphPortModel>(),
                Array.Empty<EditorGraphFieldModel>(),
                styleKey: "auxiliary"));
            adapter.NodeList.Add(new EditorGraphNodeModel(
                "video",
                "播放视频",
                "播放视频",
                "命令",
                new Vector2(100f, 120f),
                new[] { input },
                new[] { output },
                fields,
                true,
                true,
                nodeDiagnostics,
                "action"));
            adapter.NodeList.Add(new EditorGraphNodeModel(
                "end",
                "结束",
                "结束",
                "流程",
                new Vector2(460f, 120f),
                new[] { input },
                Array.Empty<EditorGraphPortModel>(),
                Array.Empty<EditorGraphFieldModel>(),
                styleKey: "flow"));
            adapter.WireList.Add(new EditorGraphWireModel(
                "wire_video_end",
                new EditorGraphPortRef("video", "completed"),
                new EditorGraphPortRef("end", "in"),
                "完成",
                diagnostics: wireDiagnostics));
            return adapter;
        }

        private static void InvokeNonPublic(object instance, string name, params object[] args)
        {
            InvokeNonPublic<object>(instance, name, args);
        }

        private static T InvokeNonPublic<T>(object instance, string name, params object[] args)
        {
            var method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, name);
            return (T)method.Invoke(instance, args);
        }

        private static T InvokeStaticNonPublic<T>(Type type, string name, params object[] args)
        {
            var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, name);
            return (T)method.Invoke(null, args);
        }

        private static T GetNonPublicField<T>(object instance, string name)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            return (T)field.GetValue(instance);
        }

        private static IEnumerable<T> FindVisualChildren<T>(VisualElement root) where T : VisualElement
        {
            if (root == null)
            {
                yield break;
            }

            if (root is T typed)
            {
                yield return typed;
            }

            foreach (var child in root.Children())
            {
                foreach (var nested in FindVisualChildren<T>(child))
                {
                    yield return nested;
                }
            }
        }

        private static void AssertVectorApproximately(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.0001f);
            Assert.AreEqual(expected.y, actual.y, 0.0001f);
        }

        private static string FrameworkFilePath(string relativePath)
        {
            var normalizedRelativePath = NormalizePath(relativePath).Trim('/');
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(EditorNodeGraphCanvas).Assembly);
            if (string.IsNullOrWhiteSpace(packageInfo?.resolvedPath) is false)
            {
                var packageFilePath = Path.Combine(packageInfo.resolvedPath, normalizedRelativePath);
                if (System.IO.File.Exists(packageFilePath) || Directory.Exists(packageFilePath))
                {
                    return NormalizePath(packageFilePath);
                }
            }

            var assetsFilePath = Path.Combine("Assets/GameDeveloperKit", normalizedRelativePath);
            if (System.IO.File.Exists(assetsFilePath) || Directory.Exists(assetsFilePath))
            {
                return NormalizePath(assetsFilePath);
            }

            return NormalizePath(Path.Combine("Packages/com.gamedeveloperkit.framework", normalizedRelativePath));
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private sealed class FakeAdapter : IEditorNodeGraphAdapter
        {
            public readonly List<EditorGraphNodeModel> NodeList = new List<EditorGraphNodeModel>();
            public readonly List<EditorGraphWireModel> WireList = new List<EditorGraphWireModel>();
            public readonly List<EditorGraphNodeTemplate> TemplateList = new List<EditorGraphNodeTemplate>();
            public readonly List<(EditorGraphNodeTemplate Template, Vector2 Position, EditorGraphPortRef ConnectFrom)> CreatedNodes =
                new List<(EditorGraphNodeTemplate Template, Vector2 Position, EditorGraphPortRef ConnectFrom)>();
            public readonly List<(EditorGraphPortRef Output, EditorGraphPortRef Input)> ConnectionChecks =
                new List<(EditorGraphPortRef Output, EditorGraphPortRef Input)>();
            public readonly List<(EditorGraphPortRef Output, EditorGraphPortRef Input)> Connections =
                new List<(EditorGraphPortRef Output, EditorGraphPortRef Input)>();
            public readonly List<(string NodeId, string FieldId, string Value)> FieldChanges =
                new List<(string NodeId, string FieldId, string Value)>();
            public readonly List<string> SelectedNodes = new List<string>();
            public readonly List<string> ActivatedNodes = new List<string>();

            public bool AllowConnection = true;
            public string FailureMessage = "连接失败。";
            public int DeleteSelectionCount;
            public int CustomFieldCreateCount;
            public Action<string> CustomValueChanged;

            public IReadOnlyList<EditorGraphNodeModel> Nodes => NodeList;

            public IReadOnlyList<EditorGraphWireModel> Wires => WireList;

            public IReadOnlyList<EditorGraphNodeTemplate> Templates => TemplateList;

            public EditorGraphCanvasModel Canvas { get; set; }

            public VisualElement CreateBlackboard()
            {
                return new Label("测试黑板");
            }

            public VisualElement CreateCustomField(string nodeId, EditorGraphFieldModel field, Action<string> valueChanged)
            {
                CustomFieldCreateCount++;
                if (string.Equals(field?.CustomType, "test.custom", StringComparison.Ordinal) is false)
                {
                    return null;
                }

                CustomValueChanged = valueChanged;
                var button = new Button { name = "test-custom-field", text = field.Value };
                return button;
            }

            public EditorGraphConnectionResult CanConnect(EditorGraphPortRef output, EditorGraphPortRef input)
            {
                ConnectionChecks.Add((output, input));
                return AllowConnection ? EditorGraphConnectionResult.Success : EditorGraphConnectionResult.Fail(FailureMessage);
            }

            public void CreateNode(EditorGraphNodeTemplate template, Vector2 graphPosition, EditorGraphPortRef connectFrom)
            {
                CreatedNodes.Add((template, graphPosition, connectFrom));
                NodeList.Add(new EditorGraphNodeModel(
                    "created_" + CreatedNodes.Count,
                    template.DefaultTitle,
                    template.DisplayName,
                    template.Category,
                    graphPosition,
                    template.Ports.Where(x => x.Direction == EditorGraphPortDirection.Input).ToList(),
                    template.Ports.Where(x => x.Direction == EditorGraphPortDirection.Output).ToList(),
                    template.Fields,
                    styleKey: template.StyleKey));
            }

            public void MoveNode(string nodeId, Vector2 graphPosition)
            {
            }

            public void MoveNodes(IReadOnlyList<EditorNodeGraphMove> moves)
            {
            }

            public void SelectNode(string nodeId)
            {
                SelectedNodes.Clear();
                if (string.IsNullOrWhiteSpace(nodeId) is false)
                {
                    SelectedNodes.Add(nodeId);
                }
            }

            public void ActivateNode(string nodeId)
            {
                ActivatedNodes.Add(nodeId);
            }

            public bool PopulateNodeContextMenu(string nodeId, GenericMenu menu)
            {
                return false;
            }

            public void SelectNodes(IReadOnlyList<string> nodeIds)
            {
                SelectedNodes.Clear();
                SelectedNodes.AddRange(nodeIds ?? Array.Empty<string>());
            }

            public void SelectWire(string wireId)
            {
            }

            public void MoveWireControlPoint(string wireId, int pointIndex, Vector2 graphPosition)
            {
            }

            public void InsertWireControlPoint(string wireId, int segmentIndex, Vector2 graphPosition)
            {
            }

            public void RemoveWireControlPoint(string wireId, int pointIndex)
            {
            }

            public void Connect(EditorGraphPortRef output, EditorGraphPortRef input)
            {
                Connections.Add((output, input));
            }

            public void Disconnect(string wireId)
            {
            }

            public void DeleteSelection()
            {
                DeleteSelectionCount++;
            }

            public void SetNodeField(string nodeId, string fieldId, string value)
            {
                FieldChanges.Add((nodeId, fieldId, value));
            }
        }
    }
}
