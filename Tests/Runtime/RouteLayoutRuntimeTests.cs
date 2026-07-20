using System;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Model;
using Newtonsoft.Json.Linq;
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
                new[] { new EpisodePlacement("episode", new Placement(0.33f, 0.33f)) },
                new[]
                {
                    new RouteEdgePlacement(
                        "edge_root",
                        new[] { new Placement(0.17f, 0.17f) },
                        "main")
                }));

            Assert.DoesNotThrow(() => new StoryModule().Register(program));
        }

        [Test]
        public void Register_WhenLayoutPositionIsNotNormalized_RejectsLayout()
        {
            var program = ProgramWith(Layout(
                new[] { new EpisodePlacement("episode", new Placement(1.1f, 0f)) },
                new[] { new RouteEdgePlacement("edge_root", Array.Empty<Placement>()) }));

            var exception = Assert.Throws<GameException>(() => new StoryModule().Register(program));

            StringAssert.Contains("normalized to [0,1]", exception.Message);
        }

        [Test]
        public void Register_WhenLayoutDoesNotCoverRoute_RejectsLayout()
        {
            var program = ProgramWith(Layout(
                Array.Empty<EpisodePlacement>(),
                Array.Empty<RouteEdgePlacement>()));

            var exception = Assert.Throws<GameException>(() => new StoryModule().Register(program));

            StringAssert.Contains("must place every episode", exception.Message);
        }

        [Test]
        public void Register_WhenControlPointIsOutsideCanvas_RejectsLayout()
        {
            var program = ProgramWith(Layout(
                new[] { new EpisodePlacement("episode", new Placement(0.33f, 0.33f)) },
                new[]
                {
                    new RouteEdgePlacement(
                        "edge_root",
                        new[] { new Placement(float.NaN, 0.17f) })
                }));

            var exception = Assert.Throws<GameException>(() => new StoryModule().Register(program));

            StringAssert.Contains("finite and normalized to [0,1]", exception.Message);
        }

        [Test]
        public void ProgramAsset_WhenLayoutsRoundTrip_PreservesAllRuntimeFields()
        {
            var source = ProgramWith(Layout(
                new[] { new EpisodePlacement("episode", new Placement(0.46f, 0.43f)) },
                new[]
                {
                    new RouteEdgePlacement(
                        "edge_root",
                        new[] { new Placement(0.22f, 0.21f), new Placement(0.33f, 0.32f) },
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
                Assert.AreEqual("Assets/Bundles/Story/route.png", restored.BackgroundImagePath);
                Assert.AreEqual(0.1f, restored.RootPlacement.X);
                Assert.AreEqual("episode", restored.Episodes[0].EpisodeId);
                Assert.AreEqual(2, restored.Edges[0].ControlPoints.Count);
                Assert.AreEqual("secret", restored.Edges[0].StyleKey);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ProgramAsset_WhenLegacyPixelLayoutIsLoaded_NormalizesCoordinates()
        {
            var source = ProgramWith(Layout(
                new[] { new EpisodePlacement("episode", new Placement(0.5f, 0.5f)) },
                new[]
                {
                    new RouteEdgePlacement(
                        "edge_root",
                        new[] { new Placement(0.2f, 0.2f), new Placement(0.8f, 0.8f) })
                }));
            var asset = ScriptableObject.CreateInstance<ProgramAsset>();
            try
            {
                asset.SetProgram(source);
                var json = JObject.Parse(JsonUtility.ToJson(asset));
                var layout = (JObject)json["m_Volumes"][0]["m_Layouts"][0];
                layout["m_ReferenceWidth"] = 1920;
                layout["m_ReferenceHeight"] = 1080;
                SetPosition(layout["m_RootPlacement"], 192f, 108f);
                SetPosition(layout["m_Episodes"][0]["m_Position"], 960f, 540f);
                SetPosition(layout["m_Edges"][0]["m_ControlPoints"][0], 384f, 216f);
                SetPosition(layout["m_Edges"][0]["m_ControlPoints"][1], 1536f, 864f);
                JsonUtility.FromJsonOverwrite(json.ToString(), asset);

                var restored = asset.ToProgram().Volumes[0].Layouts[0];

                Assert.AreEqual(0.1f, restored.RootPlacement.X, 0.0001f);
                Assert.AreEqual(0.1f, restored.RootPlacement.Y, 0.0001f);
                Assert.AreEqual(0.5f, restored.Episodes[0].Position.X, 0.0001f);
                Assert.AreEqual(0.5f, restored.Episodes[0].Position.Y, 0.0001f);
                Assert.AreEqual(0.2f, restored.Edges[0].ControlPoints[0].X, 0.0001f);
                Assert.AreEqual(0.8f, restored.Edges[0].ControlPoints[1].Y, 0.0001f);
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
            EpisodePlacement[] episodes,
            RouteEdgePlacement[] edges,
            LayoutOrientation orientation = LayoutOrientation.Landscape,
            string background = null)
        {
            return new RouteLayout(
                "layout",
                orientation,
                background,
                new Placement(0.1f, 0.1f),
                episodes,
                edges);
        }

        private static void SetPosition(JToken token, float x, float y)
        {
            token["m_X"] = x;
            token["m_Y"] = y;
        }
    }
}
