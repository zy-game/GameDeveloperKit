using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.EditorNodeGraph;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.StoryEditor.Graph;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class RouteLayoutAdapterTests
    {
        [Test]
        public void SetRoute_WhenLayoutSelected_ProjectsCanvasNodesAndEdgePath()
        {
            var volume = Volume();
            var layout = Layout();
            volume.Layouts.Add(layout);
            var adapter = new RouteGraphAdapter(new RouteGraphActions());

            adapter.SetRoute(volume, CompiledVolume(), new ValidationReport(), "episode", layout, "edge_root");

            Assert.AreEqual(new Vector2(1920f, 1080f), adapter.Canvas.ReferenceSize);
            Assert.AreEqual(new Vector2(120f, 540f), adapter.Nodes.Single(x => x.NodeId == adapter.VirtualRootNodeId).Position);
            Assert.AreEqual(new Vector2(720f, 540f), adapter.Nodes.Single(x => x.NodeId == "episode").Position);
            Assert.AreEqual(1, adapter.Wires.Count);
            Assert.IsTrue(adapter.Wires[0].Selected);
            Assert.IsTrue(adapter.Wires[0].ControlPointsEditable);
            CollectionAssert.AreEqual(
                new[] { new Vector2(320f, 540f), new Vector2(520f, 540f) },
                adapter.Wires[0].ControlPoints);
            Assert.AreEqual("main", adapter.Wires[0].StyleKey);
        }

        [Test]
        public void LayoutCallbacks_WhenInvoked_ForwardPathOnlyWithoutChangingTopology()
        {
            var moved = new List<EditorNodeGraphMove>();
            var selectedWires = new List<string>();
            var pathUpdates = new List<(string EdgeId, IReadOnlyList<Vector2> Points, string Style)>();
            var actions = new RouteGraphActions
            {
                MoveNodes = values => moved.AddRange(values),
                SelectedWire = selectedWires.Add,
                UpdateEdgePath = (edgeId, points, style) => pathUpdates.Add((edgeId, points, style))
            };
            var volume = Volume();
            var layout = Layout();
            volume.Layouts.Add(layout);
            var adapter = new RouteGraphAdapter(actions);
            adapter.SetRoute(volume, CompiledVolume(), new ValidationReport(), "episode", layout);

            adapter.MoveNode("episode", new Vector2(800f, 600f));
            adapter.SelectWire("edge_root");
            adapter.MoveWireControlPoint("edge_root", 0, new Vector2(360f, 560f));
            adapter.InsertWireControlPoint("edge_root", 1, new Vector2(440f, 560f));
            adapter.RemoveWireControlPoint("edge_root", 1);

            Assert.AreEqual(1, moved.Count);
            Assert.AreEqual("episode", moved[0].NodeId);
            CollectionAssert.AreEqual(new[] { "edge_root" }, selectedWires);
            Assert.AreEqual(3, pathUpdates.Count);
            Assert.AreEqual(new Vector2(360f, 560f), pathUpdates[0].Points[0]);
            Assert.AreEqual(3, pathUpdates[1].Points.Count);
            Assert.AreEqual(1, pathUpdates[2].Points.Count);
            Assert.AreEqual(1, CompiledVolume().Route.Edges.Count);
        }

        [Test]
        public void SetRoute_WhenNoLayoutSelected_UsesSessionPositionsAndNoCanvasContract()
        {
            var volume = Volume();
            var adapter = new RouteGraphAdapter(new RouteGraphActions());

            adapter.SetRoute(volume, CompiledVolume(), new ValidationReport(), "episode");
            var initial = adapter.Nodes.Single(x => x.NodeId == "episode").Position;
            adapter.MoveNode("episode", new Vector2(910f, 420f));

            Assert.IsNull(adapter.Canvas);
            Assert.AreNotEqual(Vector2.zero, initial);
            Assert.AreEqual(new Vector2(910f, 420f), adapter.Nodes.Single(x => x.NodeId == "episode").Position);
        }

        private static AuthoringVolume Volume()
        {
            var volume = new AuthoringVolume { VolumeId = "volume", Title = "Volume" };
            volume.Episodes.Add(new AuthoringEpisode { EpisodeId = "episode", Title = "Episode" });
            return volume;
        }

        private static AuthoringRouteLayout Layout()
        {
            var layout = new AuthoringRouteLayout
            {
                LayoutId = "layout",
                Orientation = LayoutOrientation.Landscape,
                ReferenceWidth = 1920,
                ReferenceHeight = 1080,
                RootPlacement = new AuthoringPlacement { Position = new Vector2(120f, 540f) }
            };
            layout.Episodes.Add(new AuthoringEpisodePlacement
            {
                EpisodeId = "episode",
                Position = new AuthoringPlacement { Position = new Vector2(720f, 540f) }
            });
            var edge = new AuthoringRouteEdgePlacement { EdgeId = "edge_root", StyleKey = "main" };
            edge.ControlPoints.Add(new AuthoringPlacement { Position = new Vector2(320f, 540f) });
            edge.ControlPoints.Add(new AuthoringPlacement { Position = new Vector2(520f, 540f) });
            layout.Edges.Add(edge);
            return layout;
        }

        private static Volume CompiledVolume()
        {
            return new Volume(
                "volume",
                "Volume",
                new[]
                {
                    new Episode(
                        "episode",
                        "Episode",
                        "start",
                        new[] { new EpisodeExit("done") },
                        Array.Empty<Step>())
                },
                new Route(new[] { RouteEdge.FromRoot("edge_root", "episode") }));
        }
    }
}
