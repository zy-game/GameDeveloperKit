using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.EditorNodeGraph;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Authoring;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Graph;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryRouteConvergenceAuthoringTests
    {
        private readonly List<UnityEngine.Object> m_Objects = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            for (var i = m_Objects.Count - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(m_Objects[i]);
            }

            m_Objects.Clear();
        }

        [Test]
        public void Connect_WhenSecondExitConverges_SynchronizesEveryLayoutAndUndoRedo()
        {
            var asset = CreateAsset(false);
            var volume = asset.Volumes[0];
            var mutation = new RouteMutation(asset);

            var validation = mutation.ValidateConnection(
                volume.VolumeId,
                "episode_b",
                "transition_b",
                "episode_c");
            var connected = mutation.Connect(
                volume.VolumeId,
                "episode_b",
                "transition_b",
                "episode_c");

            Assert.IsTrue(validation.Succeeded, validation.Message);
            Assert.IsTrue(connected.Succeeded, connected.Message);
            Assert.AreEqual(4, volume.Route.Edges.Count);
            Assert.AreEqual(2, volume.Route.Edges.Count(x => x.ToEpisodeId == "episode_c"));
            Assert.IsTrue(volume.Layouts.All(x => x.Edges.Any(y => y.EdgeId == connected.EdgeId)));

            Undo.PerformUndo();
            Assert.AreEqual(3, asset.Volumes[0].Route.Edges.Count);
            Assert.IsTrue(asset.Volumes[0].Layouts.All(x => x.Edges.All(y => y.EdgeId != connected.EdgeId)));
            Undo.PerformRedo();
            Assert.AreEqual(4, asset.Volumes[0].Route.Edges.Count);
            Assert.IsTrue(asset.Volumes[0].Layouts.All(x => x.Edges.Any(y => y.EdgeId == connected.EdgeId)));

            var adapter = new RouteGraphAdapter(new RouteGraphActions
            {
                CanConnect = (_, _, _) => EditorGraphConnectionResult.Success
            });
            adapter.SetRoute(asset.Volumes[0], null, new ValidationReport(), "episode_c", asset.Volumes[0].Layouts[0]);
            var input = adapter.Nodes.Single(x => x.NodeId == "episode_c").InputPorts.Single();
            Assert.AreEqual(EditorGraphPortCapacity.Multiple, input.Capacity);
            CollectionAssert.Contains(
                adapter.Nodes.Single(x => x.NodeId == "episode_b").OutputPorts.Select(x => x.PortId).ToArray(),
                "transition_b");
        }

        [Test]
        public void RedirectAndDisconnect_KeepEdgeIdentityValidateDagAndSynchronizeLayouts()
        {
            var asset = CreateAsset(true);
            var volume = asset.Volumes[0];
            var edgeId = IdentityId.ExitEdge("episode_a", "transition_a");
            var mutation = new RouteMutation(asset);

            var redirected = mutation.Connect(
                volume.VolumeId,
                "episode_a",
                "transition_a",
                "episode_b");

            Assert.IsTrue(redirected.Succeeded, redirected.Message);
            Assert.AreEqual(edgeId, redirected.EdgeId);
            Assert.AreEqual("episode_b", volume.Route.Edges.Single(x => x.EdgeId == edgeId).ToEpisodeId);
            Assert.IsTrue(volume.Layouts.All(x => x.Edges.Count == 4));

            Undo.PerformUndo();
            volume = asset.Volumes[0];
            Assert.AreEqual("episode_c", volume.Route.Edges.Single(x => x.EdgeId == edgeId).ToEpisodeId);

            var cycle = new RouteMutation(asset).Connect(
                volume.VolumeId,
                "episode_c",
                "end_c",
                "episode_a");
            Assert.IsFalse(cycle.Succeeded);
            Assert.AreEqual(RouteMutation.RouteCycle, cycle.ErrorCode);
            Assert.AreEqual(4, volume.Route.Edges.Count);
            Assert.IsTrue(volume.Layouts.All(x => x.Edges.Count == 4));

            var disconnected = new RouteMutation(asset).Disconnect(volume.VolumeId, edgeId, true);
            Assert.IsTrue(disconnected.Succeeded, disconnected.Message);
            Assert.AreEqual(3, volume.Route.Edges.Count);
            Assert.IsTrue(volume.Layouts.All(x => x.Edges.All(y => y.EdgeId != edgeId)));
            Undo.PerformUndo();
            Assert.AreEqual(4, asset.Volumes[0].Route.Edges.Count);
            Assert.IsTrue(asset.Volumes[0].Layouts.All(x => x.Edges.Any(y => y.EdgeId == edgeId)));
        }

        [Test]
        public void Disconnect_WhenItWouldMakeEpisodeUnreachable_FailsWithoutWrites()
        {
            var asset = CreateAsset(false);
            var volume = asset.Volumes[0];
            var edgeId = IdentityId.ExitEdge("episode_a", "transition_a");
            var beforeRoute = volume.Route.Edges.Select(x => x.EdgeId).ToArray();
            var beforeLayouts = volume.Layouts.Select(x => x.Edges.Select(y => y.EdgeId).ToArray()).ToArray();

            var result = new RouteMutation(asset).Disconnect(volume.VolumeId, edgeId, true);

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.AreEqual(beforeRoute, volume.Route.Edges.Select(x => x.EdgeId));
            for (var i = 0; i < beforeLayouts.Length; i++)
            {
                CollectionAssert.AreEqual(beforeLayouts[i], volume.Layouts[i].Edges.Select(x => x.EdgeId));
            }
        }

        [Test]
        public void RemoveLeafEpisode_WhenItHasMultipleIncomingEdges_RemovesAllPlacementsInOneUndo()
        {
            var asset = CreateAsset(true);
            var volume = asset.Volumes[0];

            var result = new RouteMutation(asset).RemoveLeafEpisode(volume.VolumeId, "episode_c", true);

            Assert.IsTrue(result.Succeeded, result.Message);
            Assert.AreEqual(2, volume.Episodes.Count);
            Assert.AreEqual(2, volume.Route.Edges.Count);
            Assert.IsTrue(volume.Layouts.All(x => x.Episodes.Count == 2 && x.Edges.Count == 2));
            Undo.PerformUndo();
            Assert.AreEqual(3, asset.Volumes[0].Episodes.Count);
            Assert.AreEqual(4, asset.Volumes[0].Route.Edges.Count);
            Assert.IsTrue(asset.Volumes[0].Layouts.All(x => x.Episodes.Count == 3 && x.Edges.Count == 4));
        }

        [Test]
        public void Validate_WhenTransitionHasNoRouteEdge_ReportsBlockingError()
        {
            var asset = CreateAsset(false);

            var report = AuthoringValidator.Validate(asset);

            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains(
                "Transition Exit requires one RouteEdge. episode:episode_b exit:transition_b",
                Format(report));
        }

        [Test]
        public void Compile_WhenTransitionIsInsideParallel_ReportsExplicitError()
        {
            var asset = CreateParallelAsset(NodeKind.Transition, false);

            var program = ProgramCompiler.Compile(asset, out var report);

            Assert.IsNull(program);
            StringAssert.Contains("Transition cannot be used inside a Parallel branch.", Format(report));
        }

        [Test]
        public void Compile_WhenParallelEndHasSettlementId_ReportsExplicitError()
        {
            var asset = CreateParallelAsset(NodeKind.End, true);

            var program = ProgramCompiler.Compile(asset, out var report);

            Assert.IsNull(program);
            StringAssert.Contains("Parallel branch End cannot define a settlement id.", Format(report));
        }

        [Test]
        public void Compile_WhenNonEndHasSettlementId_ReportsStaleField()
        {
            var asset = CreateAsset(true);
            asset.FindEpisode("episode_a").Nodes[0].Parameters.Add(new AuthoringParameter
            {
                Key = "settlementId",
                Value = "stale.settlement"
            });

            var program = ProgramCompiler.Compile(asset, out var report);

            Assert.IsNull(program);
            StringAssert.Contains("Settlement id is only supported by End nodes.", Format(report));
        }

        [Test]
        public void FixedNodeSchemas_ExposeParameterlessTransitionAndEndSettlementInspectorField()
        {
            var transition = NodeSchemaRegistry.Get(NodeKind.Transition);
            var end = NodeSchemaRegistry.Get(NodeKind.End);

            Assert.AreEqual(0, transition.Parameters.Count);
            Assert.AreEqual(0, transition.Ports.Count);
            Assert.AreEqual(1, end.Parameters.Count);
            Assert.AreEqual("settlementId", end.Parameters[0].Key);
            Assert.AreEqual("结算 ID", end.Parameters[0].Label);
            Assert.AreEqual(ParameterValueType.String, end.Parameters[0].ValueType);
            Assert.IsFalse(end.Parameters[0].Required);
        }

        private AuthoringAsset CreateAsset(bool converged)
        {
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            m_Objects.Add(asset);
            asset.StoryId = "route_convergence";
            asset.Version = "1";
            asset.Volumes.Clear();
            var volume = new AuthoringVolume
            {
                VolumeId = "volume",
                Title = "Volume",
                Route = new AuthoringRoute()
            };
            volume.Episodes.Add(Episode("episode_a", "transition_a", NodeKind.Transition));
            volume.Episodes.Add(Episode("episode_b", "transition_b", NodeKind.Transition));
            volume.Episodes.Add(Episode("episode_c", "end_c", NodeKind.End));
            volume.Route.Edges.Add(Root("root_a", "episode_a"));
            volume.Route.Edges.Add(Root("root_b", "episode_b"));
            volume.Route.Edges.Add(Exit("episode_a", "transition_a", "episode_c"));
            if (converged)
            {
                volume.Route.Edges.Add(Exit("episode_b", "transition_b", "episode_c"));
            }

            volume.Layouts.Add(Layout("landscape", LayoutOrientation.Landscape, volume));
            volume.Layouts.Add(Layout("portrait", LayoutOrientation.Portrait, volume));
            asset.Volumes.Add(volume);
            return asset;
        }

        private AuthoringAsset CreateParallelAsset(NodeKind firstBranchKind, bool settlement)
        {
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            m_Objects.Add(asset);
            asset.StoryId = "parallel_constraints";
            asset.Version = "1";
            asset.Volumes.Clear();
            var volume = new AuthoringVolume
            {
                VolumeId = "volume",
                Title = "Volume",
                Route = new AuthoringRoute()
            };
            var episode = new AuthoringEpisode
            {
                EpisodeId = "episode",
                Title = "Episode",
                EntryNodeId = "parallel"
            };
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = "parallel",
                Title = "Parallel",
                NodeKind = NodeKind.Parallel
            });
            var firstBranch = new AuthoringNode
            {
                NodeId = "branch_a",
                Title = "Branch A",
                NodeKind = firstBranchKind
            };
            if (settlement)
            {
                firstBranch.Parameters.Add(new AuthoringParameter
                {
                    Key = "settlementId",
                    Value = "reward.parallel"
                });
            }

            episode.Nodes.Add(firstBranch);
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = "branch_b",
                Title = "Branch B",
                NodeKind = NodeKind.End
            });
            episode.Edges.Add(DetailEdge("parallel_a", "parallel", "branch_a", "branch_a"));
            episode.Edges.Add(DetailEdge("parallel_b", "parallel", "branch_b", "branch_b"));
            volume.Episodes.Add(episode);
            volume.Route.Edges.Add(Root("root", episode.EpisodeId));
            asset.Volumes.Add(volume);
            return asset;
        }

        private static AuthoringEpisode Episode(string episodeId, string exitId, NodeKind kind)
        {
            var episode = new AuthoringEpisode
            {
                EpisodeId = episodeId,
                Title = episodeId,
                EntryNodeId = exitId
            };
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = exitId,
                Title = exitId,
                NodeKind = kind
            });
            return episode;
        }

        private static AuthoringRouteEdge Root(string edgeId, string episodeId)
        {
            return new AuthoringRouteEdge
            {
                EdgeId = edgeId,
                SourceKind = RouteEdgeSourceKind.Root,
                ToEpisodeId = episodeId
            };
        }

        private static AuthoringRouteEdge Exit(string episodeId, string exitId, string targetEpisodeId)
        {
            return new AuthoringRouteEdge
            {
                EdgeId = IdentityId.ExitEdge(episodeId, exitId),
                SourceKind = RouteEdgeSourceKind.EpisodeExit,
                FromEpisodeId = episodeId,
                FromExitId = exitId,
                ToEpisodeId = targetEpisodeId
            };
        }

        private static AuthoringEdge DetailEdge(
            string edgeId,
            string fromNodeId,
            string fromPortId,
            string targetNodeId)
        {
            return new AuthoringEdge
            {
                EdgeId = edgeId,
                FromNodeId = fromNodeId,
                FromPortId = fromPortId,
                FromPortLabel = fromPortId,
                TargetKind = TransitionTargetKind.Node,
                TargetNodeId = targetNodeId
            };
        }

        private static AuthoringRouteLayout Layout(
            string layoutId,
            LayoutOrientation orientation,
            AuthoringVolume volume)
        {
            var layout = new AuthoringRouteLayout
            {
                LayoutId = layoutId,
                Orientation = orientation,
                UsesRelativeCoordinates = true,
                RootPlacement = new AuthoringPlacement { Position = new Vector2(0.1f, 0.5f) }
            };
            for (var i = 0; i < volume.Episodes.Count; i++)
            {
                layout.Episodes.Add(new AuthoringEpisodePlacement
                {
                    EpisodeId = volume.Episodes[i].EpisodeId,
                    Position = new AuthoringPlacement { Position = new Vector2(0.35f + i * 0.2f, 0.3f + i * 0.1f) }
                });
            }

            for (var i = 0; i < volume.Route.Edges.Count; i++)
            {
                layout.Edges.Add(new AuthoringRouteEdgePlacement { EdgeId = volume.Route.Edges[i].EdgeId });
            }

            return layout;
        }

        private static string Format(ValidationReport report)
        {
            return string.Join(Environment.NewLine, report.Issues.Select(x => x.ToString()));
        }
    }
}
