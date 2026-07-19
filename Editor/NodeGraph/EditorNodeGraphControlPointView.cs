using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.EditorNodeGraph
{
    internal sealed class EditorNodeGraphControlPointView : VisualElement
    {
        private const float Size = 14f;

        private readonly Func<float> m_Zoom;
        private readonly Action<EditorGraphControlPointRef, Vector2> m_Moved;
        private bool m_Dragging;
        private Vector2 m_StartMouse;
        private Vector2 m_StartPosition;

        public EditorNodeGraphControlPointView(
            EditorGraphControlPointRef pointRef,
            Vector2 position,
            Func<float> zoom,
            Action<EditorGraphControlPointRef, Vector2> moved)
        {
            PointRef = pointRef;
            Position = position;
            m_Zoom = zoom;
            m_Moved = moved;
            userData = pointRef;
            AddToClassList("editor-node-graph-control-point");
            style.position = UnityEngine.UIElements.Position.Absolute;
            style.width = Size;
            style.height = Size;
            ApplyPosition();
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        public EditorGraphControlPointRef PointRef { get; }

        public Vector2 Position { get; private set; }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            m_Dragging = true;
            m_StartMouse = evt.mousePosition;
            m_StartPosition = Position;
            this.CaptureMouse();
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!m_Dragging)
            {
                return;
            }

            Position = m_StartPosition + (evt.mousePosition - m_StartMouse) / Mathf.Max(0.0001f, m_Zoom());
            ApplyPosition();
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (!m_Dragging || evt.button != 0)
            {
                return;
            }

            m_Dragging = false;
            this.ReleaseMouse();
            m_Moved?.Invoke(PointRef, Position);
            evt.StopPropagation();
        }

        private void ApplyPosition()
        {
            style.left = Position.x - Size * 0.5f;
            style.top = Position.y - Size * 0.5f;
        }
    }
}
