using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class RouteCompilerTests
    {
        [Test]
        public void Compile_WhenRouteExists_UsesDeclaredEdges()
        {
            var volume = CreateVolume("episode_a", "episode_b");
            volume.Route = new AuthoringRoute();
            volume.Route.Edges.Add(RootEdge("explicit_a", "episode_a"));
            volume.Route.Edges.Add(RootEdge("explicit_b", "episode_b"));
            var report = new ValidationReport();

            var route = RouteCompiler.Compile(
                "story",
                volume,
                new[]
                {
                    Episode("episode_a", "done_a"),
                    Episode("episode_b", "done_b")
                },
                new HashSet<string>(StringComparer.Ordinal),
                report);

            Assert.IsFalse(report.HasErrors, Format(report));
            CollectionAssert.AreEqual(
                new[] { "explicit_a", "explicit_b" },
                route.Edges.Select(x => x.EdgeId));
            Assert.IsFalse(route.Edges.Any(x => x.SourceKind == RouteEdgeSourceKind.EpisodeExit));
        }

        [Test]
        public void Compile_WhenRouteIsMissing_ReportsRequiredRoute()
        {
            var volume = CreateVolume("episode_a", "episode_b");
            var report = new ValidationReport();

            var route = RouteCompiler.Compile(
                "story",
                volume,
                new[]
                {
                    Episode("episode_a", "done_a"),
                    Episode("episode_b", "done_b")
                },
                new HashSet<string>(StringComparer.Ordinal),
                report);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, route.Edges.Count);
            StringAssert.Contains("Volume Route is required", Format(report));
        }

        [Test]
        public void Compile_WhenExplicitRouteHasMultipleIncomingAndCycle_ReportsCycleOnly()
        {
            var volume = CreateVolume("episode_a", "episode_b");
            volume.Route = new AuthoringRoute();
            volume.Route.Edges.Add(RootEdge("root_a", "episode_a"));
            volume.Route.Edges.Add(ExitEdge("edge_ab", "episode_a", "exit_a", "episode_b"));
            volume.Route.Edges.Add(ExitEdge("edge_ba", "episode_b", "exit_b", "episode_a"));
            var report = new ValidationReport();

            RouteCompiler.Compile(
                "story",
                volume,
                new[] { Episode("episode_a", "exit_a"), Episode("episode_b", "exit_b") },
                new HashSet<string>(StringComparer.Ordinal),
                report);

            var issues = Format(report);
            StringAssert.Contains("cycle", issues);
            StringAssert.DoesNotContain("multiple incoming", issues);
        }

        [Test]
        public void Compile_WhenEdgeIdAlreadyExistsInProgram_ReportsUniquenessError()
        {
            var volume = CreateVolume("episode_a");
            volume.Route = new AuthoringRoute();
            volume.Route.Edges.Add(RootEdge("duplicate_edge", "episode_a"));
            var report = new ValidationReport();

            RouteCompiler.Compile(
                "story",
                volume,
                new[] { Episode("episode_a", "done") },
                new HashSet<string>(StringComparer.Ordinal) { "duplicate_edge" },
                report);

            StringAssert.Contains("unique in the Program", Format(report));
        }

        private static AuthoringVolume CreateVolume(params string[] episodeIds)
        {
            var volume = new AuthoringVolume { VolumeId = "volume", Title = "Volume" };
            for (var i = 0; i < episodeIds.Length; i++)
            {
                volume.Episodes.Add(new AuthoringEpisode
                {
                    EpisodeId = episodeIds[i],
                    Title = episodeIds[i]
                });
            }

            return volume;
        }

        private static Episode Episode(string episodeId, string exitId)
        {
            return new Episode(
                episodeId,
                episodeId,
                "start",
                new[] { new EpisodeExit(exitId) },
                Array.Empty<Step>());
        }

        private static AuthoringRouteEdge RootEdge(string edgeId, string episodeId)
        {
            return new AuthoringRouteEdge
            {
                EdgeId = edgeId,
                SourceKind = RouteEdgeSourceKind.Root,
                ToEpisodeId = episodeId
            };
        }

        private static AuthoringRouteEdge ExitEdge(
            string edgeId,
            string fromEpisodeId,
            string exitId,
            string toEpisodeId)
        {
            return new AuthoringRouteEdge
            {
                EdgeId = edgeId,
                SourceKind = RouteEdgeSourceKind.EpisodeExit,
                FromEpisodeId = fromEpisodeId,
                FromExitId = exitId,
                ToEpisodeId = toEpisodeId
            };
        }

        private static string Format(ValidationReport report)
        {
            return string.Join(Environment.NewLine, report.Issues.Select(x => x.ToString()));
        }
    }
}
