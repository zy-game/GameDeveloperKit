using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Excel;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryExcelCurrentSchemaTests
    {
        private readonly List<UnityEngine.Object> m_Objects = new List<UnityEngine.Object>();
        private readonly List<string> m_Files = new List<string>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < m_Objects.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(m_Objects[i]);
            }

            for (var i = 0; i < m_Files.Count; i++)
            {
                if (System.IO.File.Exists(m_Files[i]))
                {
                    System.IO.File.Delete(m_Files[i]);
                }
            }

            m_Objects.Clear();
            m_Files.Clear();
        }

        [Test]
        public void Excel_WhenCurrentAssetRoundTrips_PreservesTransitionSettlementDagLayoutsAndIdentity()
        {
            var source = CreateCurrentAsset();
            var target = ScriptableObject.CreateInstance<AuthoringAsset>();
            m_Objects.Add(target);
            var path = TempFile();

            Exporter.Export(source, path);

            var report = Importer.Import(path, target);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Issues.Select(x => x.ToString())));
            Assert.AreEqual("excel_story", target.StoryId);
            Assert.AreEqual(1, target.Volumes.Count);
            var volume = target.Volumes[0];
            Assert.AreEqual(3, volume.Episodes.Count);
            Assert.AreEqual(4, volume.Route.Edges.Count);
            Assert.AreEqual(2, volume.Route.Edges.Count(x => x.ToEpisodeId == "episode_c"));
            Assert.AreEqual(NodeKind.Transition, volume.Episodes[0].Nodes.Single(x => x.NodeId == "transition_a").NodeKind);
            Assert.AreEqual(NodeKind.Transition, volume.Episodes[1].Nodes.Single(x => x.NodeId == "transition_b").NodeKind);
            var ending = volume.Episodes[2].Nodes.Single(x => x.NodeId == "end_c");
            Assert.AreEqual(
                "reward.final",
                ending.Parameters.Single(x => x.Key == "settlementId").Value);
            Assert.AreEqual(1, volume.Layouts.Count);
            Assert.AreEqual(LayoutOrientation.Landscape, volume.Layouts[0].Orientation);
            Assert.AreEqual(2.47f, volume.Layouts[0].Episodes[0].Position.Position.x);
            Assert.AreEqual(-0.16f, volume.Layouts[0].Edges[0].ControlPoints[0].Position.x);
            Assert.AreEqual(1.36f, volume.Layouts[0].Edges[0].ControlPoints[1].Position.x);
            Assert.AreEqual(4, volume.Layouts[0].Edges.Count);
            Assert.AreEqual(2, volume.Layouts[0].Edges[0].ControlPoints.Count);
            Assert.IsTrue(volume.Layouts[0].Edges.All(x => volume.Route.Edges.Any(y => y.EdgeId == x.EdgeId)));
            Assert.IsTrue(volume.Episodes.All(x => x.DetailLayout.Nodes.Count == 2));
            Assert.IsTrue(target.TryGetPublishedIdentity(out var identity, out _));
            CollectionAssert.AreEqual(new[] { "episode_a", "episode_b", "episode_c" }, identity.EpisodeIds);
            CollectionAssert.AreEqual(
                new[] { "root_a", "root_b", "route_a_c", "route_b_c" },
                identity.EdgeIds);
            CollectionAssert.AreEquivalent(
                new[] { "transition_a", "transition_b", "end_c" },
                identity.Exits.Select(x => x.ExitId));
        }

        [Test]
        public void Import_WhenLegacyChapterSheetsAreUsed_RejectsAndPointsToMigration()
        {
            var report = new ValidationReport();
            var accepted = Importer.ValidateSheetProtocol(new[] { "ChapterDefine", "ChapterData" }, report);

            Assert.IsFalse(accepted);
            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains("Migrate Legacy Story Excel", string.Join("\n", report.Issues.Select(x => x.ToString())));
        }

        private AuthoringAsset CreateCurrentAsset()
        {
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            m_Objects.Add(asset);
            asset.StoryId = "excel_story";
            asset.Version = "2.0";
            asset.Volumes.Clear();
            var volume = new AuthoringVolume
            {
                VolumeId = "volume",
                Title = "Volume",
                Route = new AuthoringRoute()
            };
            var first = Episode("episode_a", "start_a", "transition_a", NodeKind.Transition);
            var second = Episode("episode_b", "start_b", "transition_b", NodeKind.Transition);
            var final = Episode("episode_c", "start_c", "end_c", NodeKind.End);
            final.Nodes[1].Parameters.Add(new AuthoringParameter
            {
                Key = "settlementId",
                Value = "reward.final"
            });
            volume.Episodes.Add(first);
            volume.Episodes.Add(second);
            volume.Episodes.Add(final);
            volume.Route.Edges.Add(Root("root_a", first.EpisodeId));
            volume.Route.Edges.Add(Root("root_b", second.EpisodeId));
            volume.Route.Edges.Add(Exit("route_a_c", first.EpisodeId, "transition_a", final.EpisodeId));
            volume.Route.Edges.Add(Exit("route_b_c", second.EpisodeId, "transition_b", final.EpisodeId));
            var layout = new AuthoringRouteLayout
            {
                LayoutId = "landscape",
                Orientation = LayoutOrientation.Landscape,
                UsesRelativeCoordinates = true,
                RootPlacement = new AuthoringPlacement { Position = new Vector2(0.05f, 0.5f) }
            };
            layout.Episodes.Add(EpisodePlacement(first.EpisodeId, new Vector2(2.47f, 0.3f)));
            layout.Episodes.Add(EpisodePlacement(second.EpisodeId, new Vector2(2.47f, 0.7f)));
            layout.Episodes.Add(EpisodePlacement(final.EpisodeId, new Vector2(4.5f, 0.5f)));
            var edge = new AuthoringRouteEdgePlacement { EdgeId = "root_a", StyleKey = "main" };
            edge.ControlPoints.Add(new AuthoringPlacement { Position = new Vector2(-0.16f, 0.5f) });
            edge.ControlPoints.Add(new AuthoringPlacement { Position = new Vector2(1.36f, 0.5f) });
            layout.Edges.Add(edge);
            layout.Edges.Add(new AuthoringRouteEdgePlacement { EdgeId = "root_b" });
            layout.Edges.Add(new AuthoringRouteEdgePlacement { EdgeId = "route_a_c" });
            layout.Edges.Add(new AuthoringRouteEdgePlacement { EdgeId = "route_b_c" });
            volume.Layouts.Add(layout);
            asset.Volumes.Add(volume);
            return asset;
        }

        private static AuthoringEpisode Episode(
            string episodeId,
            string startNodeId,
            string terminalNodeId,
            NodeKind terminalKind)
        {
            var episode = new AuthoringEpisode
            {
                EpisodeId = episodeId,
                Title = episodeId,
                EntryNodeId = startNodeId
            };
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = startNodeId,
                Title = "Start",
                NodeKind = NodeKind.Start
            });
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = terminalNodeId,
                Title = terminalKind.ToString(),
                NodeKind = terminalKind
            });
            episode.Edges.Add(new AuthoringEdge
            {
                EdgeId = startNodeId + "_terminal",
                FromNodeId = startNodeId,
                FromPortId = "completed",
                FromPortLabel = "Completed",
                TargetKind = TransitionTargetKind.Node,
                TargetNodeId = terminalNodeId
            });
            episode.DetailLayout.Nodes.Add(new EpisodeNodePlacement
            {
                NodeId = startNodeId,
                Position = new Vector2(100f, 200f)
            });
            episode.DetailLayout.Nodes.Add(new EpisodeNodePlacement
            {
                NodeId = terminalNodeId,
                Position = new Vector2(400f, 200f)
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

        private static AuthoringRouteEdge Exit(
            string edgeId,
            string episodeId,
            string exitId,
            string targetEpisodeId)
        {
            return new AuthoringRouteEdge
            {
                EdgeId = edgeId,
                SourceKind = RouteEdgeSourceKind.EpisodeExit,
                FromEpisodeId = episodeId,
                FromExitId = exitId,
                ToEpisodeId = targetEpisodeId
            };
        }

        private static AuthoringEpisodePlacement EpisodePlacement(string episodeId, Vector2 position)
        {
            return new AuthoringEpisodePlacement
            {
                EpisodeId = episodeId,
                Position = new AuthoringPlacement { Position = position }
            };
        }

        private string TempFile()
        {
            var path = Path.Combine(Path.GetTempPath(), "story_excel_" + Guid.NewGuid().ToString("N") + ".xlsx");
            m_Files.Add(path);
            return path;
        }
    }
}
