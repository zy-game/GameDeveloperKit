using System;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Publishing;
using NUnit.Framework;
using StoryProgram = GameDeveloperKit.Story.Model.Program;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryPublishedIdentityTests
    {
        [Test]
        public void PublishedIdentity_WhenProgramManifestCreated_SortsAllIdentityCollections()
        {
            var episodeB = Episode("episode_b", "exit_b", "B");
            var episodeA = Episode("episode_a", "exit_a", "A");
            var program = new StoryProgram(
                "story",
                "1",
                new[]
                {
                    new Volume(
                        "volume",
                        "Volume",
                        new[] { episodeB, episodeA },
                        new Route(new[]
                        {
                            RouteEdge.FromRoot("edge_z", episodeB.EpisodeId),
                            RouteEdge.FromExit("edge_a", episodeB.EpisodeId, "exit_b", episodeA.EpisodeId)
                        }))
                });

            var manifest = IdentityManifest.Create(program);

            CollectionAssert.AreEqual(new[] { "episode_a", "episode_b" }, manifest.EpisodeIds);
            CollectionAssert.AreEqual(new[] { "edge_a", "edge_z" }, manifest.EdgeIds);
            Assert.AreEqual("episode_a", manifest.Exits[0].EpisodeId);
            Assert.AreEqual("exit_a", manifest.Exits[0].ExitId);
            Assert.AreEqual("episode_b", manifest.Exits[1].EpisodeId);
            Assert.AreEqual("exit_b", manifest.Exits[1].ExitId);
        }

        [Test]
        public void PublishedIdentity_WhenMetadataVolumeAndOrderChange_ReportsNoIdentityChanges()
        {
            var episodeA = Episode("episode_a", "exit_a", "First title");
            var episodeB = Episode("episode_b", "exit_b", "Second title");
            var first = new StoryProgram(
                "story",
                "1",
                new[]
                {
                    new Volume("volume_a", "A", new[] { episodeA }, new Route(new[] { RouteEdge.FromRoot("edge_a", "episode_a") })),
                    new Volume("volume_b", "B", new[] { episodeB }, new Route(new[] { RouteEdge.FromRoot("edge_b", "episode_b") }))
                });
            var second = new StoryProgram(
                "story",
                "2",
                new[]
                {
                    new Volume("volume_b", "Renamed B", new[] { Episode("episode_a", "exit_a", "Changed") }, new Route(new[] { RouteEdge.FromRoot("edge_a", "episode_a") })),
                    new Volume("volume_a", "Renamed A", new[] { Episode("episode_b", "exit_b", "Changed") }, new Route(new[] { RouteEdge.FromRoot("edge_b", "episode_b") }))
                });

            var report = IdentityChangeReport.Compare(
                IdentityManifest.Create(first),
                IdentityManifest.Create(second));

            Assert.AreEqual(0, report.AddedEpisodeIds.Count);
            Assert.AreEqual(0, report.RemovedEpisodeIds.Count);
            Assert.AreEqual(0, report.AddedEdgeIds.Count);
            Assert.AreEqual(0, report.RemovedEdgeIds.Count);
            Assert.AreEqual(0, report.RemovedExits.Count);
            Assert.IsFalse(report.HasBreakingChanges);
        }

        [Test]
        public void PublishedIdentity_WhenCompared_ReportsAddedAndRemovedIdentity()
        {
            var baseline = Manifest("story", new[] { "episode_a", "episode_b" }, new[] { "edge_a", "edge_b" },
                new[] { new ExitIdentity("episode_a", "exit_a"), new ExitIdentity("episode_b", "exit_b") });
            var current = Manifest("story", new[] { "episode_a", "episode_c" }, new[] { "edge_a", "edge_c" },
                new[] { new ExitIdentity("episode_a", "exit_a"), new ExitIdentity("episode_c", "exit_c") });

            var report = IdentityChangeReport.Compare(baseline, current);

            CollectionAssert.AreEqual(new[] { "episode_c" }, report.AddedEpisodeIds);
            CollectionAssert.AreEqual(new[] { "episode_b" }, report.RemovedEpisodeIds);
            CollectionAssert.AreEqual(new[] { "edge_c" }, report.AddedEdgeIds);
            CollectionAssert.AreEqual(new[] { "edge_b" }, report.RemovedEdgeIds);
            Assert.AreEqual(new ExitIdentity("episode_b", "exit_b"), report.RemovedExits[0]);
            Assert.IsTrue(report.HasBreakingChanges);
        }

        [Test]
        public void PublishedIdentity_WhenStoryIdChanges_TreatsAllBaselineIdentityAsRemoved()
        {
            var baseline = Manifest("story_old", new[] { "episode" }, new[] { "edge" },
                new[] { new ExitIdentity("episode", "exit") });
            var current = Manifest("story_new", new[] { "episode" }, new[] { "edge" },
                new[] { new ExitIdentity("episode", "exit") });

            var report = IdentityChangeReport.Compare(baseline, current);

            CollectionAssert.AreEqual(new[] { "episode" }, report.AddedEpisodeIds);
            CollectionAssert.AreEqual(new[] { "episode" }, report.RemovedEpisodeIds);
            CollectionAssert.AreEqual(new[] { "edge" }, report.AddedEdgeIds);
            CollectionAssert.AreEqual(new[] { "edge" }, report.RemovedEdgeIds);
            Assert.AreEqual(1, report.RemovedExits.Count);
        }

        [Test]
        public void PublishedIdentity_WhenManifestContainsDuplicateIdentity_RejectsManifest()
        {
            Assert.Throws<ArgumentException>(() => new IdentityManifest(
                "story",
                "1",
                new[] { "episode", "episode" },
                Array.Empty<string>(),
                Array.Empty<ExitIdentity>()));
        }

        [Test]
        public void PublishedIdentity_WhenIdsCreated_UsesUniqueOpaqueAndStableDerivedIds()
        {
            var first = IdentityId.New();
            var second = IdentityId.New();

            Assert.AreEqual(32, first.Length);
            Assert.AreNotEqual(first, second);
            Assert.AreEqual("root_7_episode", IdentityId.RootEdge("episode"));
            Assert.AreEqual("route_7_episode_4_exit", IdentityId.ExitEdge("episode", "exit"));
            Assert.AreNotEqual(
                IdentityId.ExitEdge("a_b", "c"),
                IdentityId.ExitEdge("a", "b_c"));
        }

        private static IdentityManifest Manifest(
            string storyId,
            string[] episodeIds,
            string[] edgeIds,
            ExitIdentity[] exits)
        {
            return new IdentityManifest(storyId, "1", episodeIds, edgeIds, exits);
        }

        private static Episode Episode(string episodeId, string exitId, string title)
        {
            return new Episode(
                episodeId,
                title,
                "start",
                new[] { new EpisodeExit(exitId) },
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("end"))),
                    new Step("end", StepKind.End, new StepData(exitId: exitId))
                });
        }
    }
}
