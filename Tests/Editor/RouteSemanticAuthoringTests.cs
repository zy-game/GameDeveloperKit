using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Authoring;
using GameDeveloperKit.StoryEditor.Graph;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.UI;
using GameDeveloperKit.StoryEditor.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Tests
{
    public sealed class RouteSemanticAuthoringTests
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
        public void AddRootEpisode_CreatesEpisodeAndRootEdgeInOneUndo()
        {
            var asset = CreateAssetWithRoute("episode_a");
            var volume = asset.Volumes[0];
            var mutation = new RouteMutation(asset);

            var result = mutation.AddRootEpisode(
                volume.VolumeId,
                new EpisodeMetadata("New Episode", "Description", null));

            Assert.IsTrue(result.Succeeded, result.Message);
            Assert.AreEqual(2, volume.Episodes.Count);
            Assert.AreEqual(2, volume.Route.Edges.Count);
            Assert.AreEqual(RouteEdgeSourceKind.Root, volume.Route.Edges[1].SourceKind);
            Assert.AreEqual(result.EpisodeId, volume.Route.Edges[1].ToEpisodeId);
            Assert.AreEqual(1, volume.Episodes[1].Edges.Count);

            Undo.PerformUndo();
            Assert.AreEqual(1, asset.Volumes[0].Episodes.Count);
            Assert.AreEqual(1, asset.Volumes[0].Route.Edges.Count);

            Undo.PerformRedo();
            Assert.AreEqual(2, asset.Volumes[0].Episodes.Count);
            Assert.AreEqual(2, asset.Volumes[0].Route.Edges.Count);
        }

        [Test]
        public void AddChildEpisode_BindsCompiledExitAndRejectsRebindingWithoutWrites()
        {
            var asset = CreateAssetWithRoute("episode_a");
            var volume = asset.Volumes[0];
            var source = volume.Episodes[0];
            var exitId = EndId(source);
            var mutation = new RouteMutation(asset);

            var first = mutation.AddChildEpisode(
                volume.VolumeId,
                source.EpisodeId,
                exitId,
                new EpisodeMetadata("Child", string.Empty, null));

            Assert.IsTrue(first.Succeeded, first.Message);
            Assert.AreEqual(2, volume.Episodes.Count);
            Assert.AreEqual(2, volume.Route.Edges.Count);
            Assert.AreEqual(source.EpisodeId, volume.Route.Edges[1].FromEpisodeId);
            Assert.AreEqual(exitId, volume.Route.Edges[1].FromExitId);
            var episodeIds = volume.Episodes.Select(x => x.EpisodeId).ToArray();
            var edgeIds = volume.Route.Edges.Select(x => x.EdgeId).ToArray();

            var second = mutation.AddChildEpisode(
                volume.VolumeId,
                source.EpisodeId,
                exitId,
                new EpisodeMetadata("Rejected", string.Empty, null));

            Assert.IsFalse(second.Succeeded);
            Assert.AreEqual(RouteMutation.ExitAlreadyBound, second.ErrorCode);
            CollectionAssert.AreEqual(episodeIds, volume.Episodes.Select(x => x.EpisodeId));
            CollectionAssert.AreEqual(edgeIds, volume.Route.Edges.Select(x => x.EdgeId));
        }

        [Test]
        public void RemoveLeafEpisode_RejectsParentThenLeavesSourceExitReusable()
        {
            var asset = CreateAssetWithRoute("episode_a");
            var volume = asset.Volumes[0];
            var source = volume.Episodes[0];
            var exitId = EndId(source);
            var mutation = new RouteMutation(asset);
            var added = mutation.AddChildEpisode(
                volume.VolumeId,
                source.EpisodeId,
                exitId,
                new EpisodeMetadata("Child", string.Empty, null));
            Assert.IsTrue(added.Succeeded, added.Message);

            var parentRemoval = mutation.RemoveLeafEpisode(volume.VolumeId, source.EpisodeId, false);
            Assert.IsFalse(parentRemoval.Succeeded);
            Assert.AreEqual(RouteMutation.EpisodeHasChildren, parentRemoval.ErrorCode);
            Assert.AreEqual(2, volume.Episodes.Count);

            var leafRemoval = mutation.RemoveLeafEpisode(volume.VolumeId, added.EpisodeId, false);
            Assert.IsTrue(leafRemoval.Succeeded, leafRemoval.Message);
            Assert.AreEqual(1, volume.Episodes.Count);
            Assert.AreEqual(1, volume.Route.Edges.Count);

            Undo.PerformUndo();
            Assert.AreEqual(2, asset.Volumes[0].Episodes.Count);
            Assert.AreEqual(2, asset.Volumes[0].Route.Edges.Count);
            Undo.PerformRedo();
            volume = asset.Volumes[0];
            source = volume.Episodes.Single(x => x.EpisodeId == "episode_a");
            mutation = new RouteMutation(asset);
            Assert.AreEqual(1, volume.Episodes.Count);
            Assert.AreEqual(1, volume.Route.Edges.Count);

            var replacement = mutation.AddChildEpisode(
                volume.VolumeId,
                source.EpisodeId,
                exitId,
                new EpisodeMetadata("Replacement", string.Empty, null));
            Assert.IsTrue(replacement.Succeeded, replacement.Message);
        }

        [Test]
        public void RemoveLeafEpisode_WhenIdentityWasPublished_RequiresExplicitConfirmation()
        {
            var asset = CreateAssetWithRoute("episode_a", "episode_b");
            var volume = asset.Volumes[0];
            Bind(volume, "edge_ab", "episode_a", EndId(volume.Episodes[0]), "episode_b");
            asset.CommitPublishedIdentity(new IdentityManifest(
                asset.StoryId,
                asset.Version,
                new[] { "episode_a", "episode_b" },
                volume.Route.Edges.Select(x => x.EdgeId).ToArray(),
                new[]
                {
                    new ExitIdentity("episode_a", EndId(volume.Episodes[0])),
                    new ExitIdentity("episode_b", EndId(volume.Episodes[1]))
                }));
            var mutation = new RouteMutation(asset);

            var denied = mutation.RemoveLeafEpisode(volume.VolumeId, "episode_b", false);

            Assert.IsFalse(denied.Succeeded);
            Assert.AreEqual(RouteMutation.PublishedIdentityRemoval, denied.ErrorCode);
            Assert.AreEqual(2, volume.Episodes.Count);

            var confirmed = mutation.RemoveLeafEpisode(volume.VolumeId, "episode_b", true);
            Assert.IsTrue(confirmed.Succeeded, confirmed.Message);
            Assert.AreEqual(1, volume.Episodes.Count);
        }

        [Test]
        public void MetadataUpdates_PreserveIdentityAndDoNotMaterializeLegacyRoute()
        {
            var asset = CreateLegacyAsset("episode_a");
            var volume = asset.Volumes[0];
            var episode = volume.Episodes[0];
            var episodeId = episode.EpisodeId;
            var entryId = asset.LegacyEntryEpisodeId;
            var preview = CreateTexture();
            var mutation = new RouteMutation(asset);

            var volumeResult = mutation.UpdateVolume(
                volume.VolumeId,
                new VolumeMetadata("Renamed Volume", "Volume Description", preview));
            var episodeResult = mutation.UpdateEpisode(
                volume.VolumeId,
                episodeId,
                new EpisodeMetadata("Renamed Episode", "Episode Description", preview));

            Assert.IsTrue(volumeResult.Succeeded, volumeResult.Message);
            Assert.IsTrue(episodeResult.Succeeded, episodeResult.Message);
            Assert.IsNull(volume.Route);
            Assert.AreEqual(episodeId, episode.EpisodeId);
            Assert.AreEqual(entryId, asset.LegacyEntryEpisodeId);
            Assert.AreSame(preview, volume.PreviewImage);
            Assert.AreSame(preview, episode.PreviewImage);

            Undo.PerformUndo();
            Assert.AreNotEqual("Renamed Episode", asset.Volumes[0].Episodes[0].Title);
            Undo.PerformUndo();
            Assert.AreNotEqual("Renamed Volume", asset.Volumes[0].Title);
            Undo.PerformRedo();
            Undo.PerformRedo();
            Assert.AreEqual("Renamed Episode", asset.Volumes[0].Episodes[0].Title);
            Assert.AreEqual("Renamed Volume", asset.Volumes[0].Title);
        }

        [Test]
        public void AddRootEpisode_OnLegacyVolume_MaterializesExistingRouteInSameUndo()
        {
            var asset = CreateLegacyAsset("episode_a");
            var volume = asset.Volumes[0];
            var mutation = new RouteMutation(asset);

            var result = mutation.AddRootEpisode(
                volume.VolumeId,
                new EpisodeMetadata("Second Root", string.Empty, null));

            Assert.IsTrue(result.Succeeded, result.Message);
            Assert.IsNotNull(volume.Route);
            Assert.AreEqual(2, volume.Route.Edges.Count);
            Assert.AreEqual("episode_a", volume.Route.Edges[0].ToEpisodeId);

            Undo.PerformUndo();
            Assert.IsNull(asset.Volumes[0].Route);
            Assert.AreEqual(1, asset.Volumes[0].Episodes.Count);

            Undo.PerformRedo();
            Assert.IsNotNull(asset.Volumes[0].Route);
            Assert.AreEqual(2, asset.Volumes[0].Episodes.Count);
        }

        [Test]
        public void Mutations_WhenReferencesAreUnknown_ReturnStableErrorsWithoutWrites()
        {
            var asset = CreateAssetWithRoute("episode_a");
            var volume = asset.Volumes[0];
            var mutation = new RouteMutation(asset);

            var unknownVolume = mutation.AddRootEpisode(
                "missing_volume",
                new EpisodeMetadata("Unused", string.Empty, null));
            var unknownEpisode = mutation.AddChildEpisode(
                volume.VolumeId,
                "missing_episode",
                "missing_exit",
                new EpisodeMetadata("Unused", string.Empty, null));
            var unknownExit = mutation.AddChildEpisode(
                volume.VolumeId,
                "episode_a",
                "missing_exit",
                new EpisodeMetadata("Unused", string.Empty, null));

            Assert.AreEqual(RouteMutation.UnknownVolume, unknownVolume.ErrorCode);
            Assert.AreEqual(RouteMutation.UnknownEpisode, unknownEpisode.ErrorCode);
            Assert.AreEqual(RouteMutation.UnknownExit, unknownExit.ErrorCode);
            Assert.AreEqual(1, volume.Episodes.Count);
            Assert.AreEqual(1, volume.Route.Edges.Count);
        }

        [Test]
        public void RemoveLeafEpisode_WhenItIsLastGlobalEpisode_ReturnsRootImmutableWithoutWrites()
        {
            var asset = CreateAssetWithRoute("episode_a");
            var volume = asset.Volumes[0];
            var entryId = asset.LegacyEntryEpisodeId;

            var result = new RouteMutation(asset).RemoveLeafEpisode(volume.VolumeId, "episode_a", true);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(RouteMutation.RootImmutable, result.ErrorCode);
            Assert.AreEqual(1, volume.Episodes.Count);
            Assert.AreEqual(entryId, asset.LegacyEntryEpisodeId);
        }

        [Test]
        public void RouteContextMenu_OnlyAppearsForRootUnboundExitAndLeafDelete()
        {
            var volume = new AuthoringVolume { VolumeId = "volume", Title = "Volume" };
            volume.Episodes.Add(EpisodeAuthoring("episode_a"));
            volume.Episodes.Add(EpisodeAuthoring("episode_b"));
            var compiled = new Volume(
                volume.VolumeId,
                volume.Title,
                new[]
                {
                    RuntimeEpisode("episode_a", "bound"),
                    RuntimeEpisode("episode_b")
                },
                new Route(new[]
                {
                    RouteEdge.FromRoot("root_a", "episode_a"),
                    RouteEdge.FromExit("edge_ab", "episode_a", "bound", "episode_b")
                }));
            var adapter = new RouteGraphAdapter(new RouteGraphActions());
            adapter.SetRoute(volume, compiled, new ValidationReport(), adapter.VirtualRootNodeId);

            var rootMenu = new GenericMenu();
            var parentMenu = new GenericMenu();
            var leafMenu = new GenericMenu();

            Assert.IsTrue(adapter.PopulateNodeContextMenu(adapter.VirtualRootNodeId, rootMenu));
            Assert.AreEqual(1, rootMenu.GetItemCount());
            Assert.IsFalse(adapter.PopulateNodeContextMenu("episode_a", parentMenu));
            Assert.AreEqual(0, parentMenu.GetItemCount());
            Assert.IsTrue(adapter.PopulateNodeContextMenu("episode_b", leafMenu));
            Assert.AreEqual(1, leafMenu.GetItemCount());
        }

        [Test]
        public void RouteContextMenu_WhenLeafHasUnboundExit_OffersAddAndDeleteOnly()
        {
            var volume = new AuthoringVolume { VolumeId = "volume", Title = "Volume" };
            volume.Episodes.Add(EpisodeAuthoring("episode_a"));
            var compiled = new Volume(
                volume.VolumeId,
                volume.Title,
                new[] { RuntimeEpisode("episode_a", "open") },
                new Route(new[] { RouteEdge.FromRoot("root_a", "episode_a") }));
            var adapter = new RouteGraphAdapter(new RouteGraphActions());
            adapter.SetRoute(volume, compiled, new ValidationReport(), "episode_a");
            var menu = new GenericMenu();

            Assert.IsTrue(adapter.PopulateNodeContextMenu("episode_a", menu));
            Assert.AreEqual(3, menu.GetItemCount());
        }

        [Test]
        public void RouteInspector_UsesDelayedMetadataFieldsAndPreservesRouteSelection()
        {
            var asset = CreateAssetWithRoute("episode_a");
            var volume = asset.Volumes[0];
            var edgeId = volume.Route.Edges[0].EdgeId;
            var window = CreateWindow(asset);
            var fields = window.rootVisualElement.Query<TextField>(className: "story-editor__route-inspector-field").ToList();
            var preview = window.rootVisualElement.Query<ObjectField>(className: "story-editor__route-inspector-field").First();

            Assert.AreEqual(2, fields.Count);
            Assert.IsTrue(fields.All(x => x.isDelayed));
            Assert.AreEqual(typeof(Texture2D), preview.objectType);

            var title = fields.First(x => x.label == "标题");
            title.value = "Renamed Volume";

            Assert.AreEqual("Renamed Volume", volume.Title);
            Assert.AreEqual(edgeId, volume.Route.Edges[0].EdgeId);
            Assert.AreEqual(RouteGraphAdapter.GetVirtualRootNodeId(volume.VolumeId),
                GetPrivateField<string>(window, "m_SelectedRouteNodeId"));
        }

        private AuthoringAsset CreateAssetWithRoute(params string[] episodeIds)
        {
            var asset = CreateLegacyAsset(episodeIds);
            var volume = asset.Volumes[0];
            volume.Route = new AuthoringRoute();
            for (var i = 0; i < episodeIds.Length; i++)
            {
                volume.Route.Edges.Add(new AuthoringRouteEdge
                {
                    EdgeId = "root_" + episodeIds[i],
                    SourceKind = RouteEdgeSourceKind.Root,
                    ToEpisodeId = episodeIds[i]
                });
            }

            return asset;
        }

        private AuthoringAsset CreateLegacyAsset(params string[] episodeIds)
        {
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            m_CreatedObjects.Add(asset);
            asset.StoryId = "story";
            asset.Version = "1";
            asset.Volumes.Clear();
            var volume = new AuthoringVolume { VolumeId = "volume", Title = "Volume" };
            for (var i = 0; i < episodeIds.Length; i++)
            {
                volume.Episodes.Add(EpisodeAuthoring(episodeIds[i]));
            }

            asset.Volumes.Add(volume);
            asset.LegacyEntryEpisodeId = episodeIds[0];
            return asset;
        }

        private static AuthoringEpisode EpisodeAuthoring(string episodeId)
        {
            var startId = episodeId + "_start";
            var endId = episodeId + "_end";
            var episode = new AuthoringEpisode
            {
                EpisodeId = episodeId,
                Title = episodeId,
                EntryNodeId = startId
            };
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = startId,
                Title = "Start",
                NodeKind = NodeKind.Start
            });
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = endId,
                Title = "End",
                NodeKind = NodeKind.End
            });
            episode.Edges.Add(new AuthoringEdge
            {
                EdgeId = episodeId + "_flow",
                FromNodeId = startId,
                FromPortId = "completed",
                FromPortLabel = "Completed",
                TargetKind = TransitionTargetKind.Node,
                TargetNodeId = endId
            });
            return episode;
        }

        private static Episode RuntimeEpisode(string episodeId, params string[] exitIds)
        {
            return new Episode(
                episodeId,
                episodeId,
                episodeId + "_start",
                exitIds.Select(x => new EpisodeExit(x)).ToArray(),
                Array.Empty<Step>());
        }

        private static string EndId(AuthoringEpisode episode)
        {
            return episode.Nodes.Single(x => x.NodeKind == NodeKind.End).NodeId;
        }

        private static void Bind(
            AuthoringVolume volume,
            string edgeId,
            string sourceEpisodeId,
            string exitId,
            string targetEpisodeId)
        {
            volume.Route.Edges.RemoveAll(x => x.ToEpisodeId == targetEpisodeId);
            volume.Route.Edges.Add(new AuthoringRouteEdge
            {
                EdgeId = edgeId,
                SourceKind = RouteEdgeSourceKind.EpisodeExit,
                FromEpisodeId = sourceEpisodeId,
                FromExitId = exitId,
                ToEpisodeId = targetEpisodeId
            });
        }

        private Texture2D CreateTexture()
        {
            var texture = new Texture2D(1, 1);
            m_CreatedObjects.Add(texture);
            return texture;
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
