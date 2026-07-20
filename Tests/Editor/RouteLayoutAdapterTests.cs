using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.EditorNodeGraph;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Text;
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

            Assert.AreEqual(new Vector2(1600f, 900f), adapter.Canvas.ReferenceSize);
            Assert.IsFalse(adapter.Canvas.IsBounded);
            Assert.IsFalse(adapter.Canvas.ConstrainsXAxis);
            Assert.IsTrue(adapter.Canvas.ConstrainsYAxis);
            Assert.AreEqual(new Vector2(120f, 450f), adapter.Nodes.Single(x => x.NodeId == adapter.VirtualRootNodeId).Position);
            Assert.AreEqual(new Vector2(2320f, 450f), adapter.Nodes.Single(x => x.NodeId == "episode").Position);
            Assert.AreEqual(1, adapter.Wires.Count);
            Assert.IsTrue(adapter.Wires[0].Selected);
            Assert.IsTrue(adapter.Wires[0].ControlPointsEditable);
            CollectionAssert.AreEqual(
                new[] { new Vector2(320f, 450f), new Vector2(2120f, 450f) },
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

            adapter.MoveNode("episode", new Vector2(2400f, -450f));
            adapter.SelectWire("edge_root");
            adapter.MoveWireControlPoint("edge_root", 0, new Vector2(1960f, 1120f));
            adapter.InsertWireControlPoint("edge_root", 1, new Vector2(440f, 560f));
            adapter.RemoveWireControlPoint("edge_root", 1);

            Assert.AreEqual(1, moved.Count);
            Assert.AreEqual("episode", moved[0].NodeId);
            Assert.AreEqual(new Vector2(1.5f, 0f), moved[0].Position);
            CollectionAssert.AreEqual(new[] { "edge_root" }, selectedWires);
            Assert.AreEqual(3, pathUpdates.Count);
            Assert.AreEqual(new Vector2(1.225f, 1f), pathUpdates[0].Points[0]);
            Assert.AreEqual(3, pathUpdates[1].Points.Count);
            Assert.AreEqual(1, pathUpdates[2].Points.Count);
            Assert.AreEqual(1, CompiledVolume().Route.Edges.Count);
        }

        [Test]
        public void SetRoute_WhenPortraitLayoutSelected_ProjectsVerticalStrip()
        {
            var volume = Volume();
            var layout = Layout();
            layout.Orientation = LayoutOrientation.Portrait;
            layout.RootPlacement.Position = new Vector2(0.5f, 0.075f);
            layout.Episodes[0].Position.Position = new Vector2(0.5f, 1.45f);
            layout.Edges[0].ControlPoints[0].Position = new Vector2(0.5f, 0.2f);
            layout.Edges[0].ControlPoints[1].Position = new Vector2(0.5f, 1.325f);
            volume.Layouts.Add(layout);
            var adapter = new RouteGraphAdapter(new RouteGraphActions());

            adapter.SetRoute(volume, CompiledVolume(), new ValidationReport(), "episode", layout, "edge_root");

            Assert.AreEqual(new Vector2(900f, 1600f), adapter.Canvas.ReferenceSize);
            Assert.IsTrue(adapter.Canvas.ConstrainsXAxis);
            Assert.IsFalse(adapter.Canvas.ConstrainsYAxis);
            Assert.AreEqual(new Vector2(450f, 2320f), adapter.Nodes.Single(x => x.NodeId == "episode").Position);
            CollectionAssert.AreEqual(
                new[] { new Vector2(450f, 320f), new Vector2(450f, 2120f) },
                adapter.Wires[0].ControlPoints);
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

        [Test]
        public void SetRoute_WhenChoiceExitHasText_ProjectsTextAsPortLabel()
        {
            var volume = Volume();
            var choice = new AuthoringNode
            {
                NodeId = "choice_laugh",
                Title = "选项",
                NodeKind = NodeKind.Choice
            };
            choice.Parameters.Add(new AuthoringParameter
            {
                Key = "textKey",
                Value = TextReferenceCodec.Serialize(new TextReference(TextMode.Literal, "哈哈"))
            });
            volume.Episodes[0].Nodes.Add(choice);
            volume.Episodes[0].Nodes.Add(new AuthoringNode
            {
                NodeId = "end",
                Title = "结束",
                NodeKind = NodeKind.End
            });
            var adapter = new RouteGraphAdapter(new RouteGraphActions());

            adapter.SetRoute(volume, CompiledVolume(), new ValidationReport(), "episode");

            var ports = adapter.Nodes.Single(x => x.NodeId == "episode").OutputPorts;
            Assert.AreEqual("哈哈", ports.Single(x => x.PortId == "choice_laugh").Label);
            Assert.AreEqual("结束", ports.Single(x => x.PortId == "end").Label);
            Assert.AreEqual("剧情段出口：哈哈", ports.Single(x => x.PortId == "choice_laugh").Tooltip);
        }

        private static AuthoringVolume Volume()
        {
            var volume = new AuthoringVolume
            {
                VolumeId = "volume",
                Title = "Volume",
                Route = new AuthoringRoute()
            };
            volume.Episodes.Add(new AuthoringEpisode { EpisodeId = "episode", Title = "Episode" });
            volume.Route.Edges.Add(new AuthoringRouteEdge
            {
                EdgeId = "edge_root",
                SourceKind = RouteEdgeSourceKind.Root,
                ToEpisodeId = "episode"
            });
            return volume;
        }

        private static AuthoringRouteLayout Layout()
        {
            var layout = new AuthoringRouteLayout
            {
                LayoutId = "layout",
                Orientation = LayoutOrientation.Landscape,
                UsesRelativeCoordinates = true,
                RootPlacement = new AuthoringPlacement { Position = new Vector2(0.075f, 0.5f) }
            };
            layout.Episodes.Add(new AuthoringEpisodePlacement
            {
                EpisodeId = "episode",
                Position = new AuthoringPlacement { Position = new Vector2(1.45f, 0.5f) }
            });
            var edge = new AuthoringRouteEdgePlacement { EdgeId = "edge_root", StyleKey = "main" };
            edge.ControlPoints.Add(new AuthoringPlacement { Position = new Vector2(0.2f, 0.5f) });
            edge.ControlPoints.Add(new AuthoringPlacement { Position = new Vector2(1.325f, 0.5f) });
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
