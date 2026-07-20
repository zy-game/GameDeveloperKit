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
            Assert.AreEqual(0.1f, layouts[0].RootPlacement.X);
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
        public void Compile_WhenPlacementIsNotFinite_ReportsLocatedError()
        {
            var volume = VolumeWithCompleteLayout();
            volume.Layouts[0].Episodes[0].Position.Position = new Vector2(float.PositiveInfinity, 0.4f);
            var report = new ValidationReport();

            LayoutCompiler.Compile("story", volume, new[] { Episode() }, Route(), report);

            StringAssert.Contains("layout:layout/episode:episode", Format(report));
            StringAssert.Contains("finite coordinates", Format(report));
        }

        [Test]
        public void Compile_WhenPlacementSpansMultipleViewports_AcceptsLayout()
        {
            var volume = VolumeWithCompleteLayout();
            volume.Layouts[0].RootPlacement.Position = new Vector2(-0.25f, 0.5f);
            volume.Layouts[0].Episodes[0].Position.Position = new Vector2(3.4f, 0.8f);
            volume.Layouts[0].Edges[0].ControlPoints[0].Position = new Vector2(1.5f, 0.2f);
            var report = new ValidationReport();

            var layouts = LayoutCompiler.Compile("story", volume, new[] { Episode() }, Route(), report);

            Assert.IsFalse(report.HasErrors, Format(report));
            Assert.AreEqual(-0.25f, layouts[0].RootPlacement.X);
            Assert.AreEqual(3.4f, layouts[0].Episodes[0].Position.X);
            Assert.AreEqual(0.2f, layouts[0].Edges[0].ControlPoints[0].Y);
        }

        [Test]
        public void Compile_WhenLandscapePlacementLeavesVerticalStrip_ReportsError()
        {
            var volume = VolumeWithCompleteLayout();
            volume.Layouts[0].Episodes[0].Position.Position = new Vector2(2.4f, 1.2f);
            var report = new ValidationReport();

            LayoutCompiler.Compile("story", volume, new[] { Episode() }, Route(), report);

            StringAssert.Contains("vertical [0,1]", Format(report));
        }

        [Test]
        public void Compile_WhenPortraitPlacementGrowsHorizontally_AcceptsLayout()
        {
            var volume = VolumeWithCompleteLayout();
            volume.Layouts[0].Orientation = LayoutOrientation.Portrait;
            volume.Layouts[0].RootPlacement.Position = new Vector2(-0.25f, 0.5f);
            volume.Layouts[0].Episodes[0].Position.Position = new Vector2(3.4f, 0.8f);
            volume.Layouts[0].Edges[0].ControlPoints[0].Position = new Vector2(1.5f, 0.2f);
            var report = new ValidationReport();

            var layouts = LayoutCompiler.Compile("story", volume, new[] { Episode() }, Route(), report);

            Assert.IsFalse(report.HasErrors, Format(report));
            Assert.AreEqual(-0.25f, layouts[0].RootPlacement.X);
            Assert.AreEqual(3.4f, layouts[0].Episodes[0].Position.X);
        }

        private static AuthoringVolume VolumeWithCompleteLayout()
        {
            var layout = new AuthoringRouteLayout
            {
                LayoutId = "layout",
                Orientation = LayoutOrientation.Landscape,
                UsesRelativeCoordinates = true,
                RootPlacement = Point(0.1f, 0.2f)
            };
            layout.Episodes.Add(new AuthoringEpisodePlacement
            {
                EpisodeId = "episode",
                Position = Point(0.6f, 0.3f)
            });
            var edge = new AuthoringRouteEdgePlacement { EdgeId = "edge_root", StyleKey = "main" };
            edge.ControlPoints.Add(Point(0.28f, 0.22f));
            edge.ControlPoints.Add(Point(0.44f, 0.26f));
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
