using System;
using System.Collections.Generic;
using GameDeveloperKit.EditorNodeGraph;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Graph
{
    internal sealed class RouteGraphActions
    {
        public Action<string> SelectedNode { get; set; }

        public Action<string> ActivatedNode { get; set; }

        public Action AddRootEpisode { get; set; }

        public Action<string, string> AddChildEpisode { get; set; }

        public Action<string> RemoveEpisode { get; set; }

        public Action<string> SelectedWire { get; set; }

        public Func<string, string, string, EditorGraphConnectionResult> CanConnect { get; set; }

        public Action<string, string, string> Connect { get; set; }

        public Action<string> Disconnect { get; set; }

        public Action<IReadOnlyList<EditorNodeGraphMove>> MoveNodes { get; set; }

        public Action<string, IReadOnlyList<Vector2>, string> UpdateEdgePath { get; set; }
    }
}
