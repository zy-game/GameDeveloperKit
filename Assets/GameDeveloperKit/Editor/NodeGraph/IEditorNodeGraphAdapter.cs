using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.EditorNodeGraph
{
    public interface IEditorNodeGraphAdapter
    {
        IReadOnlyList<EditorGraphNodeModel> Nodes { get; }

        IReadOnlyList<EditorGraphWireModel> Wires { get; }

        IReadOnlyList<EditorGraphNodeTemplate> Templates { get; }

        VisualElement CreateBlackboard();

        EditorGraphConnectionResult CanConnect(EditorGraphPortRef output, EditorGraphPortRef input);

        void CreateNode(EditorGraphNodeTemplate template, Vector2 graphPosition, EditorGraphPortRef connectFrom);

        void MoveNode(string nodeId, Vector2 graphPosition);

        void SelectNode(string nodeId);

        void SelectNodes(IReadOnlyList<string> nodeIds);

        void SelectWire(string wireId);

        void Connect(EditorGraphPortRef output, EditorGraphPortRef input);

        void Disconnect(string wireId);

        void DeleteSelection();

        void SetNodeField(string nodeId, string fieldId, string value);
    }
}
