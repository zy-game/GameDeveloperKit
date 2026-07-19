using System;
using System.Linq;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class LayoutCompilerTests
    {
        [Test]
        public void Compile_WhenVolumeHasNoLayouts_ReturnsEmptyCollection()
        {
            var volume = new AuthoringVolume { VolumeId = "volume" };
            var report = new ValidationReport();

            var layouts = LayoutCompiler.Compile(
                "story",
                volume,
                new[] { Episode() },
                Route(),
                report);

            Assert.AreEqual(0, layouts.Count);
            Assert.IsFalse(report.HasErrors);
        }

        [Test]
        public void Compile_WhenLayoutIsComplete_PreservesRuntimeFields()
        {
            var volume = VolumeWithCompleteLayout();
            var report = new ValidationReport();

            var layouts = LayoutCompiler.Compile(
                "story",
                volume,
                new[] { Episode() },
                Route(),
                report);

            Assert.IsFalse(report.HasErrors, Format(report));
            Assert.AreEqual(1, layouts.Count);
            Assert.AreEqual("layout", layouts[0].LayoutId);
            Assert.AreEqual(1920, layouts[0].ReferenceWidth);
            Assert.AreEqual(120f, layouts[0].RootPlacement.X);
            Assert.AreEqual("episode", layouts[0].Episodes[0].EpisodeId);
            Assert.AreEqual("edge_root", layouts[0].Edges[0].EdgeId);
            Assert.AreEqual(2, layouts[0].Edges[0].ControlPoints.Count);
            Assert.AreEqual("main", layouts[0].Edges[0].StyleKey);
        }

        [Test]
        public void Compile_WhenEpisodeOrEdgePlacementIsMissing_ReportsCompleteCoverageErrors()
        {
            var volume = VolumeWithCompleteLayout();
            volume.Layouts[0].Episodes.Clear();
            volume.Layouts[0].Edges.Clear();
            var report = new ValidationReport();

            LayoutCompiler.Compile("story", volume, new[] { Episode() }, Route(), report);

            var issues = Format(report);
            StringAssert.Contains("place every Episode", issues);
            StringAssert.Contains("place every RouteEdge", issues);
        }

        [Test]
        public void Compile_WhenPlacementIsOutsideCanvas_ReportsLocatedError()
        {
            var volume = VolumeWithCompleteLayout();
            volume.Layouts[0].Episodes[0].Position.Position = new Vector2(float.PositiveInfinity, 40f);
            var report = new ValidationReport();

            LayoutCompiler.Compile("story", volume, new[] { Episode() }, Route(), report);

            StringAssert.Contains("layout:layout/episode:episode", Format(report));
            StringAssert.Contains("finite and inside", Format(report));
        }

        private static AuthoringVolume VolumeWithCompleteLayout()
        {
            var layout = new AuthoringRouteLayout
            {
                LayoutId = "layout",
                Orientation = LayoutOrientation.Landscape,
                ReferenceWidth = 1920,
                ReferenceHeight = 1080,
                RootPlacement = Point(120f, 200f)
            };
            layout.Episodes.Add(new AuthoringEpisodePlacement
            {
                EpisodeId = "episode",
                Position = Point(620f, 300f)
            });
            var edge = new AuthoringRouteEdgePlacement { EdgeId = "edge_root", StyleKey = "main" };
            edge.ControlPoints.Add(Point(280f, 220f));
            edge.ControlPoints.Add(Point(440f, 260f));
            layout.Edges.Add(edge);
            var volume = new AuthoringVolume { VolumeId = "volume" };
            volume.Layouts.Add(layout);
            return volume;
        }

        private static AuthoringPlacement Point(float x, float y)
        {
            return new AuthoringPlacement { Position = new Vector2(x, y) };
        }

        private static Episode Episode()
        {
            return new Episode(
                "episode",
                "Episode",
                "start",
                new[] { new EpisodeExit("done") },
                Array.Empty<Step>());
        }

        private static Route Route()
        {
            return new Route(new[] { RouteEdge.FromRoot("edge_root", "episode") });
        }

        private static string Format(ValidationReport report)
        {
            return string.Join(Environment.NewLine, report.Issues.Select(x => x.ToString()));
        }
    }
}
