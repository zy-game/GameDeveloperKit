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
        public void Excel_WhenCurrentAssetRoundTrips_PreservesRouteLayoutsAndIdentity()
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
            Assert.AreEqual(1, volume.Episodes.Count);
            Assert.AreEqual(1, volume.Route.Edges.Count);
            Assert.AreEqual("root_episode", volume.Route.Edges[0].EdgeId);
            Assert.AreEqual(1, volume.Layouts.Count);
            Assert.AreEqual(LayoutOrientation.Landscape, volume.Layouts[0].Orientation);
            Assert.AreEqual(2.47f, volume.Layouts[0].Episodes[0].Position.Position.x);
            Assert.AreEqual(-0.16f, volume.Layouts[0].Edges[0].ControlPoints[0].Position.x);
            Assert.AreEqual(1.36f, volume.Layouts[0].Edges[0].ControlPoints[1].Position.x);
            Assert.AreEqual(1, volume.Layouts[0].Edges.Count);
            Assert.AreEqual(2, volume.Layouts[0].Edges[0].ControlPoints.Count);
            Assert.AreEqual(2, volume.Episodes[0].DetailLayout.Nodes.Count);
            Assert.IsTrue(target.TryGetPublishedIdentity(out var identity, out _));
            CollectionAssert.AreEqual(new[] { "episode" }, identity.EpisodeIds);
            CollectionAssert.AreEqual(new[] { "root_episode" }, identity.EdgeIds);
            Assert.AreEqual("end", identity.Exits.Single().ExitId);
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
            var episode = new AuthoringEpisode
            {
                EpisodeId = "episode",
                Title = "Episode",
                EntryNodeId = "start"
            };
            episode.Nodes.Add(new AuthoringNode { NodeId = "start", Title = "Start", NodeKind = NodeKind.Start });
            episode.Nodes.Add(new AuthoringNode { NodeId = "end", Title = "End", NodeKind = NodeKind.End });
            episode.Edges.Add(new AuthoringEdge
            {
                EdgeId = "start_end",
                FromNodeId = "start",
                FromPortId = "completed",
                FromPortLabel = "Completed",
                TargetKind = TransitionTargetKind.Node,
                TargetNodeId = "end"
            });
            episode.DetailLayout.Nodes.Add(new EpisodeNodePlacement { NodeId = "start", Position = new Vector2(100f, 200f) });
            episode.DetailLayout.Nodes.Add(new EpisodeNodePlacement { NodeId = "end", Position = new Vector2(400f, 200f) });
            volume.Episodes.Add(episode);
            volume.Route.Edges.Add(new AuthoringRouteEdge
            {
                EdgeId = "root_episode",
                SourceKind = RouteEdgeSourceKind.Root,
                ToEpisodeId = "episode"
            });
            var layout = new AuthoringRouteLayout
            {
                LayoutId = "landscape",
                Orientation = LayoutOrientation.Landscape,
                UsesRelativeCoordinates = true,
                RootPlacement = new AuthoringPlacement { Position = new Vector2(0.05f, 0.5f) }
            };
            layout.Episodes.Add(new AuthoringEpisodePlacement
            {
                EpisodeId = "episode",
                Position = new AuthoringPlacement { Position = new Vector2(2.47f, 0.5f) }
            });
            var edge = new AuthoringRouteEdgePlacement { EdgeId = "root_episode", StyleKey = "main" };
            edge.ControlPoints.Add(new AuthoringPlacement { Position = new Vector2(-0.16f, 0.5f) });
            edge.ControlPoints.Add(new AuthoringPlacement { Position = new Vector2(1.36f, 0.5f) });
            layout.Edges.Add(edge);
            volume.Layouts.Add(layout);
            asset.Volumes.Add(volume);
            return asset;
        }

        private string TempFile()
        {
            var path = Path.Combine(Path.GetTempPath(), "story_excel_" + Guid.NewGuid().ToString("N") + ".xlsx");
            m_Files.Add(path);
            return path;
        }
    }
}
