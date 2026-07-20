using System;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.StoryEditor.Authoring;
using GameDeveloperKit.StoryEditor.Model;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class LayoutSynchronizationTests
    {
        [Test]
        public void TryAdd_WhenTwoLayoutsExist_AddsEpisodeAndEdgeToBoth()
        {
            var volume = VolumeWithLayouts(2);
            var episodes = new[] { Episode("episode_a"), Episode("episode_b") };
            var route = RouteWith(
                Root("root_a", "episode_a"),
                Exit("edge_ab", "episode_a", "done_a", "episode_b"));

            var succeeded = LayoutSynchronizer.TryAdd(
                volume,
                episodes,
                route,
                "episode_b",
                "edge_ab",
                "episode_a",
                out var layouts,
                out var error);

            Assert.IsTrue(succeeded, error);
            Assert.AreEqual(2, layouts.Count);
            for (var i = 0; i < layouts.Count; i++)
            {
                Assert.AreEqual(2, layouts[i].Episodes.Count);
                Assert.AreEqual(2, layouts[i].Edges.Count);
                Assert.AreEqual("episode_b", layouts[i].Episodes[1].EpisodeId);
                Assert.AreEqual("edge_ab", layouts[i].Edges[1].EdgeId);
            }
            Assert.AreEqual(1, volume.Layouts[0].Episodes.Count);
        }

        [Test]
        public void TryRemove_WhenTwoLayoutsExist_RemovesOnlyTargetPlacements()
        {
            var volume = VolumeWithLayouts(2);
            for (var i = 0; i < volume.Layouts.Count; i++)
            {
                volume.Layouts[i].Episodes.Add(EpisodePlacement("episode_b", 0.8f, 0.4f));
                volume.Layouts[i].Edges.Add(new AuthoringRouteEdgePlacement { EdgeId = "edge_ab" });
            }
            var episodes = new[] { Episode("episode_a") };
            var route = RouteWith(Root("root_a", "episode_a"));

            var succeeded = LayoutSynchronizer.TryRemove(
                volume,
                episodes,
                route,
                "episode_b",
                "edge_ab",
                out var layouts,
                out var error);

            Assert.IsTrue(succeeded, error);
            Assert.AreEqual(1, layouts[0].Episodes.Count);
            Assert.AreEqual(1, layouts[0].Edges.Count);
            Assert.AreEqual("episode_a", layouts[0].Episodes[0].EpisodeId);
        }

        [Test]
        public void TryAdd_WhenExistingLayoutIsIncomplete_ReturnsFailureWithoutChangingSource()
        {
            var volume = VolumeWithLayouts(1);
            volume.Layouts[0].RootPlacement = null;
            var episodes = new[] { Episode("episode_a"), Episode("episode_b") };
            var route = RouteWith(
                Root("root_a", "episode_a"),
                Exit("edge_ab", "episode_a", "done_a", "episode_b"));

            var succeeded = LayoutSynchronizer.TryAdd(
                volume,
                episodes,
                route,
                "episode_b",
                "edge_ab",
                "episode_a",
                out _,
                out var error);

            Assert.IsFalse(succeeded);
            StringAssert.Contains("缺少虚拟根", error);
            Assert.AreEqual(1, volume.Layouts[0].Episodes.Count);
            Assert.AreEqual(1, volume.Layouts[0].Edges.Count);
        }

        [Test]
        public void TryAdd_WhenLayoutIsPortrait_GrowsVerticallyInsideHorizontalAxis()
        {
            var volume = VolumeWithLayouts(1);
            volume.Layouts[0].Orientation = LayoutOrientation.Portrait;
            volume.Layouts[0].RootPlacement.Position = new Vector2(0.5f, 0.1f);
            volume.Layouts[0].Episodes[0].Position.Position = new Vector2(0.5f, 0.4f);
            var episodes = new[] { Episode("episode_a"), Episode("episode_b") };
            var route = RouteWith(
                Root("root_a", "episode_a"),
                Exit("edge_ab", "episode_a", "done_a", "episode_b"));

            var succeeded = LayoutSynchronizer.TryAdd(
                volume,
                episodes,
                route,
                "episode_b",
                "edge_ab",
                "episode_a",
                out var layouts,
                out var error);

            Assert.IsTrue(succeeded, error);
            var position = layouts[0].Episodes[1].Position.Position;
            Assert.Greater(position.y, 0.4f);
            Assert.That(position.x, Is.InRange(0f, 1f));
        }

        private static AuthoringVolume VolumeWithLayouts(int count)
        {
            var volume = new AuthoringVolume { VolumeId = "volume" };
            for (var i = 0; i < count; i++)
            {
                var layout = new AuthoringRouteLayout
                {
                    LayoutId = "layout_" + i,
                    Orientation = LayoutOrientation.Landscape,
                    UsesRelativeCoordinates = true,
                    RootPlacement = new AuthoringPlacement { Position = new Vector2(0.1f, 0.5f) }
                };
                layout.Episodes.Add(EpisodePlacement("episode_a", 0.4f, 0.5f));
                layout.Edges.Add(new AuthoringRouteEdgePlacement { EdgeId = "root_a" });
                volume.Layouts.Add(layout);
            }

            return volume;
        }

        private static AuthoringEpisodePlacement EpisodePlacement(string episodeId, float x, float y)
        {
            return new AuthoringEpisodePlacement
            {
                EpisodeId = episodeId,
                Position = new AuthoringPlacement { Position = new Vector2(x, y) }
            };
        }

        private static AuthoringEpisode Episode(string episodeId)
        {
            return new AuthoringEpisode { EpisodeId = episodeId, Title = episodeId };
        }

        private static AuthoringRoute RouteWith(params AuthoringRouteEdge[] edges)
        {
            var route = new AuthoringRoute();
            route.Edges.AddRange(edges);
            return route;
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
    }
}
