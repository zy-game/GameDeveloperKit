using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameDeveloperKit.EditorNodeGraph;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.StoryEditor.Graph;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.UI;
using GameDeveloperKit.StoryEditor.Validation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Tests
{
    public sealed class RouteEditorNavigationTests
    {
        private readonly List<UnityEngine.Object> m_CreatedObjects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
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
        public void RouteAdapter_WhenCompilationSucceeds_UsesCompiledTreeAndSessionPositions()
        {
            var volume = CreateVolume("volume_a", "第一卷", "episode_a", "episode_b");
            var compiledVolume = CreateCompiledVolume();
            var selected = new List<string>();
            var activated = new List<string>();
            var adapter = new RouteGraphAdapter(selected.Add, activated.Add);
            var rootNodeId = RouteGraphAdapter.GetVirtualRootNodeId(volume.VolumeId);

            adapter.SetRoute(volume, compiledVolume, new ValidationReport(), rootNodeId);

            Assert.AreEqual(3, adapter.Nodes.Count);
            CollectionAssert.AreEquivalent(
                new[] { rootNodeId, "episode_a", "episode_b" },
                adapter.Nodes.Select(x => x.NodeId));
            CollectionAssert.AreEqual(new[] { "edge_root", "edge_branch" }, adapter.Wires.Select(x => x.WireId));
            Assert.AreEqual(0, adapter.Templates.Count);
            Assert.IsFalse(adapter.Nodes.First(x => x.NodeId == rootNodeId).Entry);

            var movedPosition = new Vector2(740f, 310f);
            adapter.MoveNode("episode_b", movedPosition);
            adapter.SetRoute(volume, compiledVolume, new ValidationReport(), "episode_b");
            Assert.AreEqual(movedPosition, adapter.Nodes.First(x => x.NodeId == "episode_b").Position);

            adapter.SelectNode("episode_b");
            adapter.ActivateNode(rootNodeId);
            adapter.ActivateNode("episode_b");
            CollectionAssert.AreEqual(new[] { "episode_b" }, selected);
            CollectionAssert.AreEqual(new[] { "episode_b" }, activated);

            var chapterCount = volume.Chapters.Count;
            adapter.CreateNode(null, Vector2.zero, default);
            adapter.Connect(default, default);
            adapter.Disconnect("edge_root");
            adapter.DeleteSelection();
            adapter.SetNodeField("episode_a", "title", "changed");
            Assert.AreEqual(chapterCount, volume.Chapters.Count);
            Assert.AreEqual("episode_a", volume.Chapters[0].Title);
        }

        [Test]
        public void RouteAdapter_WhenCompilationFails_ShowsEpisodesWithoutInventingWires()
        {
            var volume = CreateVolume("volume_a", "第一卷", "episode_a", "episode_b");
            var report = new ValidationReport();
            report.AddError("story:test", "invalid route");
            var adapter = new RouteGraphAdapter(null, null);

            adapter.SetRoute(
                volume,
                null,
                report,
                RouteGraphAdapter.GetVirtualRootNodeId(volume.VolumeId));

            Assert.AreEqual(3, adapter.Nodes.Count);
            Assert.AreEqual(0, adapter.Wires.Count);
        }

        [Test]
        public void RouteAdapter_WhenCompilationFails_BlackboardShowsUnavailableState()
        {
            var volume = CreateVolume("volume_a", "第一卷", "episode_a", "episode_b");
            var report = new ValidationReport();
            report.AddError("story:test", "invalid route");
            var adapter = new RouteGraphAdapter(null, null);
            adapter.SetRoute(
                volume,
                null,
                report,
                RouteGraphAdapter.GetVirtualRootNodeId(volume.VolumeId));

            StringAssert.Contains("路线不可用", GetVisualText(adapter.CreateBlackboard()));
        }

        [Test]
        public void MainWindow_WhenOpened_DefaultsToEntryVolumeAndSupportsRouteRoundTrip()
        {
            var asset = CreateAsset();
            var volumeA = CreateVolume("volume_a", "第一卷", "episode_a");
            var volumeB = CreateVolume("volume_b", "第二卷", "episode_entry", "episode_branch");
            volumeB.Chapters[1].Description = "分支剧情介绍";
            asset.Volumes.Add(volumeA);
            asset.Volumes.Add(volumeB);
            asset.EntryChapterId = "episode_entry";
            var window = CreateWindow(asset);

            Assert.AreEqual("Route", GetPrivateField<object>(window, "m_EditorMode").ToString());
            Assert.AreSame(volumeB, GetPrivateField<AuthoringVolume>(window, "m_SelectedVolume"));
            Assert.AreEqual(2, window.rootVisualElement.Query<VisualElement>(className: "story-editor__tree-row--root").ToList().Count);
            Assert.AreEqual(0, window.rootVisualElement.Query<VisualElement>(className: "story-editor__tree-row--chapter").ToList().Count);
            Assert.AreEqual(3, RouteNodeViews(window).Count);

            var adapter = GetPrivateField<RouteGraphAdapter>(window, "m_RouteGraphAdapter");
            adapter.SelectNode("episode_branch");
            Assert.AreEqual("Route", GetPrivateField<object>(window, "m_EditorMode").ToString());
            StringAssert.Contains("分支剧情介绍", GetInspectorText(window));

            adapter.ActivateNode("episode_branch");
            Assert.AreEqual("EpisodeDetail", GetPrivateField<object>(window, "m_EditorMode").ToString());
            StringAssert.Contains("第二卷", GetBreadcrumbText(window));
            StringAssert.Contains("episode_branch", GetPrivateField<AuthoringChapter>(window, "m_SelectedChapter").ChapterId);

            InvokePrivate(window, "ReturnToRouteMode");
            Assert.AreEqual("Route", GetPrivateField<object>(window, "m_EditorMode").ToString());
            Assert.AreEqual("episode_branch", GetPrivateField<string>(window, "m_SelectedRouteNodeId"));
            Assert.AreEqual(3, RouteNodeViews(window).Count);
        }

        [Test]
        public void MainWindow_WhenSelectedEpisodeDisappears_FallsBackToVirtualRootWithoutPersistingPosition()
        {
            var asset = CreateAsset();
            var volume = CreateVolume("volume_a", "第一卷", "episode_a", "episode_b");
            asset.Volumes.Add(volume);
            asset.EntryChapterId = "episode_a";
            var window = CreateWindow(asset);
            var adapter = GetPrivateField<RouteGraphAdapter>(window, "m_RouteGraphAdapter");
            var layoutCount = asset.Layout.Nodes.Count;

            adapter.MoveNode("episode_b", new Vector2(910f, 420f));
            adapter.SelectNode("episode_b");
            volume.Chapters.RemoveAt(1);
            InvokePrivate(window, "RefreshAll", "external change");

            var rootNodeId = RouteGraphAdapter.GetVirtualRootNodeId(volume.VolumeId);
            Assert.AreEqual(rootNodeId, GetPrivateField<string>(window, "m_SelectedRouteNodeId"));
            Assert.AreEqual(layoutCount, asset.Layout.Nodes.Count);
            StringAssert.Contains(volume.VolumeId, GetInspectorText(window));
        }

        private AuthoringAsset CreateAsset()
        {
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            m_CreatedObjects.Add(asset);
            asset.Volumes.Clear();
            return asset;
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

        private static AuthoringVolume CreateVolume(string volumeId, string title, params string[] episodeIds)
        {
            var volume = new AuthoringVolume { VolumeId = volumeId, Title = title };
            for (var i = 0; i < episodeIds.Length; i++)
            {
                volume.Chapters.Add(CreateEpisode(episodeIds[i]));
            }

            return volume;
        }

        private static AuthoringChapter CreateEpisode(string episodeId)
        {
            var startId = episodeId + "_start";
            var episode = new AuthoringChapter
            {
                ChapterId = episodeId,
                Title = episodeId,
                EntryNodeId = startId
            };
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = startId,
                Title = "开始",
                NodeKind = NodeKind.Start
            });
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = episodeId + "_end",
                Title = "结束",
                NodeKind = NodeKind.End
            });
            return episode;
        }

        private static Volume CreateCompiledVolume()
        {
            var episodeA = new Episode(
                "episode_a",
                "episode_a",
                "episode_a_start",
                new[] { new EpisodeExit("exit_a", "继续") },
                Array.Empty<Step>());
            var episodeB = new Episode(
                "episode_b",
                "episode_b",
                "episode_b_start",
                Array.Empty<EpisodeExit>(),
                Array.Empty<Step>());
            return new Volume(
                "volume_a",
                "第一卷",
                new[] { episodeA, episodeB },
                new Route(new[]
                {
                    RouteEdge.FromRoot("edge_root", "episode_a"),
                    RouteEdge.FromExit("edge_branch", "episode_a", "exit_a", "episode_b")
                }));
        }

        private static IReadOnlyList<VisualElement> RouteNodeViews(MainWindow window)
        {
            return window.rootVisualElement.Query<VisualElement>(className: "editor-node-graph-node").ToList();
        }

        private static string GetBreadcrumbText(MainWindow window)
        {
            return GetVisualText(window.rootVisualElement.Q(className: "story-editor__breadcrumb"));
        }

        private static string GetInspectorText(MainWindow window)
        {
            return GetVisualText(window.rootVisualElement.Q(className: "story-editor__route-inspector"));
        }

        private static string GetVisualText(VisualElement element)
        {
            if (element == null)
            {
                return string.Empty;
            }

            var text = element is TextElement textElement ? textElement.text : string.Empty;
            foreach (var child in element.Children())
            {
                text += "|" + GetVisualText(child);
            }

            return text;
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
