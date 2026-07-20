using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.StoryEditor.Authoring;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Graph;
using GameDeveloperKit.StoryEditor.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Tests
{
    public sealed class RouteLayoutAuthoringTests
    {
        private readonly List<UnityEngine.Object> m_CreatedObjects = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            for (var i = m_CreatedObjects.Count - 1; i >= 0; i--)
            {
                if (m_CreatedObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_CreatedObjects[i]);
                }
            }

            m_CreatedObjects.Clear();
        }

        [Test]
        public void LayoutMutation_AddMovePathUpdateAndRemove_RoundTripsUndo()
        {
            var asset = Asset();
            var volume = asset.Volumes[0];
            var mutation = new LayoutMutation(asset);
            var added = mutation.AddLayout(volume.VolumeId, LayoutOrientation.Landscape);
            Assert.IsTrue(added.Succeeded, added.Message);
            var layout = volume.Layouts[0];
            Assert.AreEqual(1920, layout.ReferenceWidth);
            Assert.AreEqual(1, layout.Episodes.Count);
            Assert.AreEqual(1, layout.Edges.Count);

            var moved = mutation.MoveNodes(
                volume.VolumeId,
                layout.LayoutId,
                new Placement(200f, 300f),
                new[] { new EpisodePlacement("episode_a", new Placement(800f, 420f)) });
            Assert.IsTrue(moved.Succeeded, moved.Message);
            Assert.AreEqual(new Vector2(800f, 420f), volume.Layouts[0].Episodes[0].Position.Position);

            var path = mutation.UpdateEdgePath(
                volume.VolumeId,
                layout.LayoutId,
                "root_episode_a",
                new[] { new Placement(400f, 340f), new Placement(600f, 380f) },
                "main");
            Assert.IsTrue(path.Succeeded, path.Message);
            Assert.AreEqual(2, volume.Layouts[0].Edges[0].ControlPoints.Count);
            Assert.AreEqual("main", volume.Layouts[0].Edges[0].StyleKey);

            Undo.PerformUndo();
            Assert.AreEqual(0, asset.Volumes[0].Layouts[0].Edges[0].ControlPoints.Count);
            Undo.PerformRedo();
            Assert.AreEqual(2, asset.Volumes[0].Layouts[0].Edges[0].ControlPoints.Count);

            var removed = new LayoutMutation(asset).RemoveLayout(volume.VolumeId, layout.LayoutId);
            Assert.IsTrue(removed.Succeeded, removed.Message);
            Assert.AreEqual(0, asset.Volumes[0].Layouts.Count);
        }

        [Test]
        public void RouteMutation_WhenLayoutsExist_SynchronizesAddAndRemoveInTopologyUndo()
        {
            var asset = Asset();
            var volume = asset.Volumes[0];
            var layoutResult = new LayoutMutation(asset).AddLayout(volume.VolumeId, LayoutOrientation.Landscape);
            Assert.IsTrue(layoutResult.Succeeded, layoutResult.Message);
            var topology = new RouteMutation(asset);
            var added = topology.AddChildEpisode(
                volume.VolumeId,
                "episode_a",
                "episode_a_end",
                new EpisodeMetadata("Child", string.Empty, null));

            Assert.IsTrue(added.Succeeded, added.Message);
            Assert.AreEqual(2, volume.Episodes.Count);
            Assert.AreEqual(2, volume.Layouts[0].Episodes.Count);
            Assert.AreEqual(2, volume.Layouts[0].Edges.Count);

            Undo.PerformUndo();
            Assert.AreEqual(1, asset.Volumes[0].Episodes.Count);
            Assert.AreEqual(1, asset.Volumes[0].Layouts[0].Episodes.Count);
            Assert.AreEqual(1, asset.Volumes[0].Layouts[0].Edges.Count);
            Undo.PerformRedo();

            volume = asset.Volumes[0];
            var removed = new RouteMutation(asset).RemoveLeafEpisode(volume.VolumeId, added.EpisodeId, false);
            Assert.IsTrue(removed.Succeeded, removed.Message);
            Assert.AreEqual(1, volume.Episodes.Count);
            Assert.AreEqual(1, volume.Layouts[0].Episodes.Count);
            Assert.AreEqual(1, volume.Layouts[0].Edges.Count);
        }

        [Test]
        public void RouteMutation_WhenExistingLayoutIsInvalid_ReturnsInvalidLayoutWithoutTopologyWrites()
        {
            var asset = Asset();
            var volume = asset.Volumes[0];
            var result = new LayoutMutation(asset).AddLayout(volume.VolumeId, LayoutOrientation.Landscape);
            Assert.IsTrue(result.Succeeded, result.Message);
            volume.Layouts[0].RootPlacement = null;
            var episodeCount = volume.Episodes.Count;
            var edgeCount = volume.Route.Edges.Count;

            var add = new RouteMutation(asset).AddRootEpisode(
                volume.VolumeId,
                new EpisodeMetadata("Rejected", string.Empty, null));

            Assert.IsFalse(add.Succeeded);
            Assert.AreEqual(RouteMutation.InvalidLayout, add.ErrorCode);
            Assert.AreEqual(episodeCount, volume.Episodes.Count);
            Assert.AreEqual(edgeCount, volume.Route.Edges.Count);
        }

        [Test]
        public void ProgramCompiler_WhenGuideImageExists_DoesNotEmitEditorImage()
        {
            var asset = Asset();
            var volume = asset.Volumes[0];
            var result = new LayoutMutation(asset).AddLayout(volume.VolumeId, LayoutOrientation.Portrait);
            Assert.IsTrue(result.Succeeded, result.Message);
            var guide = new Texture2D(1, 1);
            m_CreatedObjects.Add(guide);
            volume.Layouts[0].EditorGuideImage = guide;

            var program = ProgramCompiler.Compile(asset, out var report);

            Assert.IsFalse(report.HasErrors, string.Join("|", report.Issues.Select(x => x.ToString())));
            Assert.AreEqual(1, program.Volumes[0].Layouts.Count);
            Assert.IsNull(program.Volumes[0].Layouts[0].BackgroundImagePath);
            Assert.IsNotNull(volume.Layouts[0].EditorGuideImage);
            Assert.IsFalse(typeof(RouteLayout).GetProperties().Any(x => x.Name.Contains("Guide")));
        }

        [Test]
        public void MainWindow_WhenLayoutSelected_ShowsLayoutControlsCanvasAndEdgeInspector()
        {
            var asset = Asset();
            var volume = asset.Volumes[0];
            var layout = CompleteLayout();
            volume.Layouts.Add(layout);
            var window = CreateWindow(asset);

            Assert.IsNotNull(window.rootVisualElement.Q<DropdownField>(className: "story-editor__route-layout-selector"));
            Assert.AreEqual(2, window.rootVisualElement.Query<IntegerField>(className: "story-editor__route-inspector-field").ToList().Count);
            var objectFields = window.rootVisualElement
                .Query<UnityEditor.UIElements.ObjectField>(className: "story-editor__route-inspector-field")
                .ToList();
            Assert.IsTrue(objectFields.Any(x => x.label == "运行时背景"));
            Assert.IsTrue(objectFields.Any(x => x.label == "参考图"));
            Assert.AreEqual(
                DisplayStyle.Flex,
                window.rootVisualElement.Q(className: "editor-node-graph-reference-canvas").style.display.value);

            var adapter = GetPrivateField<RouteGraphAdapter>(window, "m_RouteGraphAdapter");
            adapter.SelectWire("root_episode_a");

            Assert.IsNotNull(window.rootVisualElement.Query<TextField>().ToList().FirstOrDefault(x => x.label == "样式 Key"));
        }

        [Test]
        public void MainWindow_WhenLayoutAdded_SelectsItAndUsesIconCommands()
        {
            var asset = Asset();
            var window = CreateWindow(asset);
            var add = window.rootVisualElement.Q<Button>("story-route-layout-add");
            var remove = window.rootVisualElement.Q<Button>("story-route-layout-remove");

            Assert.IsNotNull(add);
            Assert.IsNotNull(remove);
            Assert.IsTrue(string.IsNullOrEmpty(add.text));
            Assert.IsNotNull(add.Q<Image>()?.image);
            Assert.IsNotNull(remove.Q<Image>()?.image);

            InvokePrivate(window, "AddLayout", LayoutOrientation.Landscape);

            Assert.AreEqual(1, asset.Volumes[0].Layouts.Count);
            Assert.AreEqual(
                asset.Volumes[0].Layouts[0].LayoutId,
                GetPrivateField<string>(window, "m_SelectedRouteLayoutId"));
            Assert.AreEqual(
                DisplayStyle.Flex,
                window.rootVisualElement.Q(className: "editor-node-graph-reference-canvas").style.display.value);
        }

        private AuthoringAsset Asset()
        {
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            m_CreatedObjects.Add(asset);
            asset.StoryId = "story";
            asset.Version = "1";
            asset.Volumes.Clear();
            var volume = new AuthoringVolume
            {
                VolumeId = "volume",
                Title = "Volume",
                Route = new AuthoringRoute()
            };
            var episode = Episode();
            volume.Episodes.Add(episode);
            volume.Route.Edges.Add(new AuthoringRouteEdge
            {
                EdgeId = "root_episode_a",
                SourceKind = RouteEdgeSourceKind.Root,
                ToEpisodeId = episode.EpisodeId
            });
            asset.Volumes.Add(volume);
            asset.LegacyEntryEpisodeId = episode.EpisodeId;
            return asset;
        }

        private static AuthoringEpisode Episode()
        {
            var episode = new AuthoringEpisode
            {
                EpisodeId = "episode_a",
                Title = "Episode A",
                EntryNodeId = "episode_a_start"
            };
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = "episode_a_start",
                Title = "Start",
                NodeKind = NodeKind.Start
            });
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = "episode_a_end",
                Title = "End",
                NodeKind = NodeKind.End
            });
            episode.Edges.Add(new AuthoringEdge
            {
                EdgeId = "episode_a_flow",
                FromNodeId = "episode_a_start",
                FromPortId = "completed",
                FromPortLabel = "Completed",
                TargetKind = TransitionTargetKind.Node,
                TargetNodeId = "episode_a_end"
            });
            return episode;
        }

        private static AuthoringRouteLayout CompleteLayout()
        {
            var layout = new AuthoringRouteLayout
            {
                LayoutId = "layout",
                Orientation = LayoutOrientation.Landscape,
                ReferenceWidth = 1920,
                ReferenceHeight = 1080,
                RootPlacement = new AuthoringPlacement { Position = new Vector2(120f, 540f) }
            };
            layout.Episodes.Add(new AuthoringEpisodePlacement
            {
                EpisodeId = "episode_a",
                Position = new AuthoringPlacement { Position = new Vector2(720f, 540f) }
            });
            layout.Edges.Add(new AuthoringRouteEdgePlacement { EdgeId = "root_episode_a", StyleKey = "main" });
            return layout;
        }

        private MainWindow CreateWindow(AuthoringAsset asset)
        {
            asset.EnsureDefaults();
            var window = ScriptableObject.CreateInstance<MainWindow>();
            m_CreatedObjects.Add(window);
            SetPrivateField(window, "m_Asset", asset);
            InvokePrivate(window, "SelectDefaults");
            InvokePrivate(window, "BuildLayout");
            InvokePrivate(window, "RefreshAll", "Ready.");
            return window;
        }

        private static void SetPrivateField(object instance, string name, object value)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(instance, value);
        }

        private static T GetPrivateField<T>(object instance, string name)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            return (T)field.GetValue(instance);
        }

        private static void InvokePrivate(object instance, string name, params object[] args)
        {
            var method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, name);
            method.Invoke(instance, args);
        }
    }
}
