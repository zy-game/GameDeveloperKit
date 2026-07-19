using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.EditorNodeGraph
{
    public interface IEditorNodeGraphAdapter
    {
        IReadOnlyList<EditorGraphNodeModel> Nodes { get; }

        IReadOnlyList<EditorGraphWireModel> Wires { get; }

        IReadOnlyList<EditorGraphNodeTemplate> Templates { get; }

        EditorGraphCanvasModel Canvas { get; }

        VisualElement CreateBlackboard();

        VisualElement CreateCustomField(string nodeId, EditorGraphFieldModel field, Action<string> valueChanged);

        EditorGraphConnectionResult CanConnect(EditorGraphPortRef output, EditorGraphPortRef input);

        void CreateNode(EditorGraphNodeTemplate template, Vector2 graphPosition, EditorGraphPortRef connectFrom);

        void MoveNode(string nodeId, Vector2 graphPosition);

        void MoveNodes(IReadOnlyList<EditorNodeGraphMove> moves);

        void SelectNode(string nodeId);

        void ActivateNode(string nodeId);

        bool PopulateNodeContextMenu(string nodeId, GenericMenu menu);

        void SelectNodes(IReadOnlyList<string> nodeIds);

        void SelectWire(string wireId);

        void MoveWireControlPoint(string wireId, int pointIndex, Vector2 graphPosition);

        void InsertWireControlPoint(string wireId, int segmentIndex, Vector2 graphPosition);

        void RemoveWireControlPoint(string wireId, int pointIndex);

        void Connect(EditorGraphPortRef output, EditorGraphPortRef input);

        void Disconnect(string wireId);

        void DeleteSelection();

        void SetNodeField(string nodeId, string fieldId, string value);
    }
}
