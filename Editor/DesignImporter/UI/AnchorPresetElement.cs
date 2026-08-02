using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.DesignImporter
{
    internal readonly struct AnchorPreset
    {
        public AnchorPreset(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            AnchorMin = anchorMin;
            AnchorMax = anchorMax;
            Pivot = pivot;
        }

        public Vector2 AnchorMin { get; }
        public Vector2 AnchorMax { get; }
        public Vector2 Pivot { get; }
    }

    internal sealed class AnchorPresetElement : IMGUIContainer
    {
        private static readonly string[] s_HorizontalNames = { "左", "居中", "右", "水平拉伸" };
        private static readonly string[] s_VerticalNames = { "上", "居中", "下", "垂直拉伸" };

        private Vector2 m_AnchorMin = new Vector2(0.5f, 0.5f);
        private Vector2 m_AnchorMax = new Vector2(0.5f, 0.5f);
        private Vector2 m_Pivot = new Vector2(0.5f, 0.5f);
        private bool m_PopupOpen;

        public AnchorPresetElement()
        {
            tooltip = "打开锚点预设";
            onGUIHandler = Draw;
        }

        public event Action<AnchorPreset> PresetSelected;

        public void SetValueWithoutNotify(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            m_AnchorMin = Clamp(anchorMin);
            m_AnchorMax = new Vector2(
                Mathf.Max(m_AnchorMin.x, Mathf.Clamp01(anchorMax.x)),
                Mathf.Max(m_AnchorMin.y, Mathf.Clamp01(anchorMax.y)));
            m_Pivot = Clamp(pivot);
            MarkDirtyRepaint();
        }

        internal static AnchorPreset GetPresetForCell(int column, int row)
        {
            if (column < 0 || column > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(column));
            }

            if (row < 0 || row > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            var stretchX = column == 3;
            var stretchY = row == 3;
            var x = stretchX ? 0.5f : column * 0.5f;
            var y = stretchY ? 0.5f : 1f - row * 0.5f;
            return new AnchorPreset(
                new Vector2(stretchX ? 0f : x, stretchY ? 0f : y),
                new Vector2(stretchX ? 1f : x, stretchY ? 1f : y),
                new Vector2(x, y));
        }

        private void Draw()
        {
            var bounds = contentRect;
            if (bounds.width < 32f || bounds.height < 32f)
            {
                return;
            }

            var side = Mathf.Min(bounds.width - 6f, bounds.height - 6f);
            var button = new Rect(
                bounds.x + (bounds.width - side) * 0.5f,
                bounds.y + (bounds.height - side) * 0.5f,
                side,
                side);
            var stateRect = Inset(button, 1f);
            var hovered = enabledInHierarchy && button.Contains(UnityEngine.Event.current.mousePosition);
            if (m_PopupOpen)
            {
                EditorGUI.DrawRect(stateRect, SelectionBackground());
            }
            else if (hovered)
            {
                EditorGUI.DrawRect(stateRect, HoverBackground());
            }

            var previousEnabled = GUI.enabled;
            GUI.enabled = enabledInHierarchy;
            if (GUI.Button(button, new GUIContent(string.Empty, "打开锚点预设"), GUIStyle.none))
            {
                ShowPopup(button);
            }

            GUI.enabled = previousEnabled;
            DrawAnchorGlyph(Inset(button, 9f), m_AnchorMin, m_AnchorMax, m_Pivot, false);
            if (m_PopupOpen)
            {
                DrawOutline(stateRect, SelectionColor(), 2f);
            }
            else if (hovered)
            {
                DrawOutline(stateRect, BorderColor(), 1f);
            }
        }

        private void ShowPopup(Rect button)
        {
            if (m_PopupOpen)
            {
                return;
            }

            m_PopupOpen = true;
            MarkDirtyRepaint();
            UnityEditor.PopupWindow.Show(button, new AnchorPresetPopup(
                m_AnchorMin,
                m_AnchorMax,
                Select,
                () =>
                {
                    m_PopupOpen = false;
                    MarkDirtyRepaint();
                }));
        }

        private void Select(AnchorPreset preset)
        {
            SetValueWithoutNotify(preset.AnchorMin, preset.AnchorMax, preset.Pivot);
            PresetSelected?.Invoke(preset);
        }

        private sealed class AnchorPresetPopup : PopupWindowContent
        {
            private const float WindowWidth = 286f;
            private const float WindowHeight = 296f;
            private const float GridX = 64f;
            private const float GridY = 58f;
            private const float CellSize = 48f;
            private const float CellGap = 7f;

            private readonly Vector2 m_CurrentMin;
            private readonly Vector2 m_CurrentMax;
            private readonly Action<AnchorPreset> m_OnSelected;
            private readonly Action m_OnClosed;

            public AnchorPresetPopup(
                Vector2 currentMin,
                Vector2 currentMax,
                Action<AnchorPreset> onSelected,
                Action onClosed)
            {
                m_CurrentMin = currentMin;
                m_CurrentMax = currentMax;
                m_OnSelected = onSelected;
                m_OnClosed = onClosed;
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(WindowWidth, WindowHeight);
            }

            public override void OnGUI(Rect rect)
            {
                DrawHeader(rect);
                DrawLabels();
                DrawCells();
            }

            public override void OnClose()
            {
                m_OnClosed?.Invoke();
            }

            private static void DrawHeader(Rect rect)
            {
                GUI.Label(new Rect(12f, 8f, rect.width - 24f, 20f), "锚点预设", EditorStyles.boldLabel);
                var hintStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight
                };
                GUI.Label(new Rect(92f, 8f, rect.width - 104f, 20f), "4 × 4", hintStyle);
                EditorGUI.DrawRect(new Rect(10f, 34f, rect.width - 20f, 1f), BorderColor());
            }

            private static void DrawLabels()
            {
                var columnStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter
                };
                var rowStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight
                };

                for (var column = 0; column < 4; column++)
                {
                    var x = GridX + column * (CellSize + CellGap);
                    GUI.Label(new Rect(x - 2f, 37f, CellSize + 4f, 18f), s_HorizontalNames[column], columnStyle);
                }

                for (var row = 0; row < 4; row++)
                {
                    var y = GridY + row * (CellSize + CellGap);
                    GUI.Label(new Rect(8f, y, GridX - 14f, CellSize), s_VerticalNames[row], rowStyle);
                }
            }

            private void DrawCells()
            {
                for (var row = 0; row < 4; row++)
                {
                    for (var column = 0; column < 4; column++)
                    {
                        var rect = new Rect(
                            GridX + column * (CellSize + CellGap),
                            GridY + row * (CellSize + CellGap),
                            CellSize,
                            CellSize);
                        var preset = GetPresetForCell(column, row);
                        var selected = Matches(preset, m_CurrentMin, m_CurrentMax);
                        var hovered = rect.Contains(UnityEngine.Event.current.mousePosition);
                        if (selected)
                        {
                            EditorGUI.DrawRect(Expand(rect, 2f), SelectionBackground());
                        }
                        else if (hovered)
                        {
                            EditorGUI.DrawRect(Expand(rect, 2f), HoverBackground());
                        }

                        var tooltip = $"{s_VerticalNames[row]} · {s_HorizontalNames[column]}";
                        if (GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none))
                        {
                            m_OnSelected?.Invoke(preset);
                            editorWindow.Close();
                            GUIUtility.ExitGUI();
                        }

                        DrawAnchorGlyph(Inset(rect, 8f), preset.AnchorMin, preset.AnchorMax, preset.Pivot, true);
                        if (selected)
                        {
                            DrawOutline(Expand(rect, 1f), SelectionColor(), 2f);
                        }
                        else if (hovered)
                        {
                            DrawOutline(Expand(rect, 1f), BorderColor(), 1f);
                        }
                    }
                }
            }
        }

        private static void DrawAnchorGlyph(
            Rect bounds,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            bool showPivot)
        {
            var frame = Inset(bounds, 1f);
            DrawOutline(frame, BorderColor(), 1f);

            var xMin = Mathf.Lerp(frame.xMin, frame.xMax, anchorMin.x);
            var xMax = Mathf.Lerp(frame.xMin, frame.xMax, anchorMax.x);
            var yMin = Mathf.Lerp(frame.yMax, frame.yMin, anchorMin.y);
            var yMax = Mathf.Lerp(frame.yMax, frame.yMin, anchorMax.y);
            var fixedX = Mathf.Approximately(anchorMin.x, anchorMax.x);
            var fixedY = Mathf.Approximately(anchorMin.y, anchorMax.y);
            DrawFixedAxisGuides(frame, xMin, yMin, fixedX, fixedY);

            var childSize = Mathf.Max(7f, Mathf.Min(frame.width, frame.height) * 0.28f);
            var child = new Rect(
                frame.center.x - childSize * 0.5f,
                frame.center.y - childSize * 0.5f,
                childSize,
                childSize);
            DrawOutline(child, ChildColor(), 1f);

            var anchorColor = AnchorColor();

            if (fixedX && fixedY)
            {
                DrawAnchorPoint(new Vector2(xMin, yMin), anchorColor);
            }
            else if (fixedX)
            {
                DrawVerticalAnchor(xMin, yMin, yMax, anchorColor);
            }
            else if (fixedY)
            {
                DrawHorizontalAnchor(xMin, xMax, yMin, anchorColor);
            }
            else
            {
                DrawOutline(Rect.MinMaxRect(xMin, yMax, xMax, yMin), anchorColor, 1f);
                DrawAnchorPoint(new Vector2(xMin, yMin), anchorColor);
                DrawAnchorPoint(new Vector2(xMax, yMax), anchorColor);
            }

            if (showPivot)
            {
                var pivotPoint = new Vector2(
                    Mathf.Lerp(frame.xMin, frame.xMax, pivot.x),
                    Mathf.Lerp(frame.yMax, frame.yMin, pivot.y));
                EditorGUI.DrawRect(new Rect(pivotPoint.x - 1f, pivotPoint.y - 1f, 3f, 3f), PivotColor());
            }
        }

        private static void DrawFixedAxisGuides(
            Rect frame,
            float x,
            float y,
            bool fixedX,
            bool fixedY)
        {
            var color = GuideColor();
            if (fixedX)
            {
                EditorGUI.DrawRect(new Rect(x - 0.5f, frame.yMin, 1f, frame.height), color);
            }

            if (fixedY)
            {
                EditorGUI.DrawRect(new Rect(frame.xMin, y - 0.5f, frame.width, 1f), color);
            }
        }

        private static void DrawHorizontalAnchor(float xMin, float xMax, float y, Color color)
        {
            EditorGUI.DrawRect(new Rect(xMin, y - 0.5f, Mathf.Max(1f, xMax - xMin), 1f), color);
            DrawAnchorPoint(new Vector2(xMin, y), color);
            DrawAnchorPoint(new Vector2(xMax, y), color);
        }

        private static void DrawVerticalAnchor(float x, float yMin, float yMax, Color color)
        {
            var top = Mathf.Min(yMin, yMax);
            var bottom = Mathf.Max(yMin, yMax);
            EditorGUI.DrawRect(new Rect(x - 0.5f, top, 1f, Mathf.Max(1f, bottom - top)), color);
            DrawAnchorPoint(new Vector2(x, yMin), color);
            DrawAnchorPoint(new Vector2(x, yMax), color);
        }

        private static void DrawAnchorPoint(Vector2 point, Color color)
        {
            EditorGUI.DrawRect(new Rect(point.x - 1.5f, point.y - 1.5f, 3f, 3f), color);
        }

        private static bool Matches(AnchorPreset preset, Vector2 currentMin, Vector2 currentMax)
        {
            return Approximately(preset.AnchorMin, currentMin) && Approximately(preset.AnchorMax, currentMax);
        }

        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return Mathf.Approximately(left.x, right.x) && Mathf.Approximately(left.y, right.y);
        }

        private static Vector2 Clamp(Vector2 value)
        {
            return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
        }

        private static Rect Inset(Rect rect, float amount)
        {
            return new Rect(
                rect.x + amount,
                rect.y + amount,
                Mathf.Max(1f, rect.width - amount * 2f),
                Mathf.Max(1f, rect.height - amount * 2f));
        }

        private static Rect Expand(Rect rect, float amount)
        {
            return new Rect(
                rect.x - amount,
                rect.y - amount,
                rect.width + amount * 2f,
                rect.height + amount * 2f);
        }

        private static void DrawOutline(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
        }

        private static Color BorderColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.44f, 0.48f, 0.53f, 1f)
                : new Color(0.42f, 0.46f, 0.51f, 1f);
        }

        private static Color ChildColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.76f, 0.79f, 0.82f, 1f)
                : new Color(0.24f, 0.28f, 0.32f, 1f);
        }

        private static Color AnchorColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.20f, 0.76f, 0.86f, 1f)
                : new Color(0f, 0.48f, 0.64f, 1f);
        }

        private static Color GuideColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.65f, 0.28f, 0.30f, 1f)
                : new Color(0.58f, 0.16f, 0.20f, 1f);
        }

        private static Color PivotColor()
        {
            return new Color(1f, 0.65f, 0.05f, 1f);
        }

        private static Color SelectionColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.22f, 0.72f, 0.68f, 1f)
                : new Color(0.02f, 0.45f, 0.42f, 1f);
        }

        private static Color SelectionBackground()
        {
            var color = SelectionColor();
            color.a = 0.22f;
            return color;
        }

        private static Color HoverBackground()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.07f)
                : new Color(0f, 0f, 0f, 0.06f);
        }
    }
}
