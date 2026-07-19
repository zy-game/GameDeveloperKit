using System;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Model;
using NUnit.Framework;
using UnityEngine;
using StoryProgram = GameDeveloperKit.Story.Model.Program;

namespace GameDeveloperKit.Tests
{
    public sealed class RouteLayoutRuntimeTests
    {
        [Test]
        public void Register_WhenRouteLayoutIsComplete_AcceptsLayout()
        {
            var program = ProgramWith(Layout(
                1920,
                1080,
                new[] { new EpisodePlacement("episode", new Placement(640f, 360f)) },
                new[]
                {
                    new RouteEdgePlacement(
                        "edge_root",
                        new[] { new Placement(320f, 180f) },
                        "main")
                }));

            Assert.DoesNotThrow(() => new StoryModule().Register(program));
        }

        [Test]
        public void Register_WhenLayoutReferenceSizeIsInvalid_RejectsLayout()
        {
            var program = ProgramWith(Layout(
                0,
                1080,
                new[] { new EpisodePlacement("episode", new Placement(0f, 0f)) },
                new[] { new RouteEdgePlacement("edge_root", Array.Empty<Placement>()) }));

            var exception = Assert.Throws<GameException>(() => new StoryModule().Register(program));

            StringAssert.Contains("reference size must be positive", exception.Message);
        }

        [Test]
        public void Register_WhenLayoutDoesNotCoverRoute_RejectsLayout()
        {
            var program = ProgramWith(Layout(
                1920,
                1080,
                Array.Empty<EpisodePlacement>(),
                Array.Empty<RouteEdgePlacement>()));

            var exception = Assert.Throws<GameException>(() => new StoryModule().Register(program));

            StringAssert.Contains("must place every episode", exception.Message);
        }

        [Test]
        public void Register_WhenControlPointIsOutsideCanvas_RejectsLayout()
        {
            var program = ProgramWith(Layout(
                1920,
                1080,
                new[] { new EpisodePlacement("episode", new Placement(640f, 360f)) },
                new[]
                {
                    new RouteEdgePlacement(
                        "edge_root",
                        new[] { new Placement(float.NaN, 180f) })
                }));

            var exception = Assert.Throws<GameException>(() => new StoryModule().Register(program));

            StringAssert.Contains("finite and inside", exception.Message);
        }

        [Test]
        public void ProgramAsset_WhenLayoutsRoundTrip_PreservesAllRuntimeFields()
        {
            var source = ProgramWith(Layout(
                1080,
                1920,
                new[] { new EpisodePlacement("episode", new Placement(500f, 820f)) },
                new[]
                {
                    new RouteEdgePlacement(
                        "edge_root",
                        new[] { new Placement(240f, 410f), new Placement(360f, 620f) },
                        "secret")
                },
                LayoutOrientation.Portrait,
                "Assets/Bundles/Story/route.png"));
            var asset = ScriptableObject.CreateInstance<ProgramAsset>();
            try
            {
                asset.SetProgram(source);

                var restored = asset.ToProgram().Volumes[0].Layouts[0];

                Assert.AreEqual("layout", restored.LayoutId);
                Assert.AreEqual(LayoutOrientation.Portrait, restored.Orientation);
                Assert.AreEqual(1080, restored.ReferenceWidth);
                Assert.AreEqual(1920, restored.ReferenceHeight);
                Assert.AreEqual("Assets/Bundles/Story/route.png", restored.BackgroundImagePath);
                Assert.AreEqual(100f, restored.RootPlacement.X);
                Assert.AreEqual("episode", restored.Episodes[0].EpisodeId);
                Assert.AreEqual(2, restored.Edges[0].ControlPoints.Count);
                Assert.AreEqual("secret", restored.Edges[0].StyleKey);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private static StoryProgram ProgramWith(RouteLayout layout)
        {
            var episode = new Episode(
                "episode",
                "Episode",
                "start",
                new[] { new EpisodeExit("done") },
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("end"))),
                    new Step("end", StepKind.End, new StepData(exitId: "done"))
                });
            return new StoryProgram(
                "story",
                "1",
                new[]
                {
                    new Volume(
                        "volume",
                        "Volume",
                        new[] { episode },
                        new Route(new[] { RouteEdge.FromRoot("edge_root", "episode") }),
                        layouts: new[] { layout })
                });
        }

        private static RouteLayout Layout(
            int width,
            int height,
            EpisodePlacement[] episodes,
            RouteEdgePlacement[] edges,
            LayoutOrientation orientation = LayoutOrientation.Landscape,
            string background = null)
        {
            return new RouteLayout(
                "layout",
                orientation,
                width,
                height,
                background,
                new Placement(100f, 100f),
                episodes,
                edges);
        }
    }
}
