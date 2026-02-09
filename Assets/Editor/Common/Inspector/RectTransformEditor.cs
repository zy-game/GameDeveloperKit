using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor
{
    [CustomEditor(typeof(RectTransform))]
    [CanEditMultipleObjects]
    public class RectTransformEditor : UnityEditor.Editor
    {
        private static Vector2? _copiedAnchoredPosition;
        private static Vector2? _copiedSizeDelta;
        private static Vector2? _copiedAnchorMin;
        private static Vector2? _copiedAnchorMax;
        private static Vector2? _copiedPivot;
        private static Vector3? _copiedRotation;
        private static Vector3? _copiedScale;

        private RectTransform _rectTransform;
        
        // Unity内置锚点弹窗
        private static Type _layoutDropdownType;
        private static ConstructorInfo _layoutDropdownCtor;

        private void OnEnable()
        {
            _rectTransform = target as RectTransform;
            
            if (_layoutDropdownType == null)
            {
                var unityEditorAssembly = typeof(UnityEditor.Editor).Assembly;
                _layoutDropdownType = unityEditorAssembly.GetType("UnityEditor.LayoutDropdownWindow");
                
                if (_layoutDropdownType != null)
                {
                    // 获取构造函数
                    _layoutDropdownCtor = _layoutDropdownType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)[0];
                }
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            float labelWidth = EditorGUIUtility.labelWidth;
            float buttonWidth = 20f;
            float buttonCount = 3;
            float spacing = 2f;
            float totalButtonWidth = buttonCount * buttonWidth + (buttonCount - 1) * spacing;

            // 第一行：锚点预设按钮 + Position/Size
            EditorGUILayout.BeginHorizontal();
            
            // 锚点预设按钮
            Rect anchorButtonRect = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64));
            if (GUI.Button(anchorButtonRect, GUIContent.none, "box"))
            {
                ShowLayoutDropdown(anchorButtonRect);
            }
            DrawAnchorPreview(anchorButtonRect);
            
            // Position 和 Size
            EditorGUILayout.BeginVertical();
            DrawVector2Field("Pos", serializedObject.FindProperty("m_AnchoredPosition"), 
                () => _copiedAnchoredPosition = _rectTransform.anchoredPosition,
                () => { if (_copiedAnchoredPosition.HasValue) _rectTransform.anchoredPosition = _copiedAnchoredPosition.Value; },
                () => _rectTransform.anchoredPosition = Vector2.zero,
                _copiedAnchoredPosition.HasValue, totalButtonWidth);
            
            DrawVector2Field("Size", serializedObject.FindProperty("m_SizeDelta"),
                () => _copiedSizeDelta = _rectTransform.sizeDelta,
                () => { if (_copiedSizeDelta.HasValue) _rectTransform.sizeDelta = _copiedSizeDelta.Value; },
                () => _rectTransform.sizeDelta = Vector2.zero,
                _copiedSizeDelta.HasValue, totalButtonWidth);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            // Anchors Min
            DrawVector2Field("Anchor Min", serializedObject.FindProperty("m_AnchorMin"),
                () => _copiedAnchorMin = _rectTransform.anchorMin,
                () => { if (_copiedAnchorMin.HasValue) _rectTransform.anchorMin = _copiedAnchorMin.Value; },
                () => _rectTransform.anchorMin = Vector2.zero,
                _copiedAnchorMin.HasValue, totalButtonWidth);

            // Anchors Max
            DrawVector2Field("Anchor Max", serializedObject.FindProperty("m_AnchorMax"),
                () => _copiedAnchorMax = _rectTransform.anchorMax,
                () => { if (_copiedAnchorMax.HasValue) _rectTransform.anchorMax = _copiedAnchorMax.Value; },
                () => _rectTransform.anchorMax = Vector2.one,
                _copiedAnchorMax.HasValue, totalButtonWidth);

            EditorGUILayout.Space(2);

            // Pivot
            DrawVector2Field("Pivot", serializedObject.FindProperty("m_Pivot"),
                () => _copiedPivot = _rectTransform.pivot,
                () => { if (_copiedPivot.HasValue) _rectTransform.pivot = _copiedPivot.Value; },
                () => _rectTransform.pivot = new Vector2(0.5f, 0.5f),
                _copiedPivot.HasValue, totalButtonWidth);

            EditorGUILayout.Space(2);

            // Rotation
            DrawVector3Field("Rotation", _rectTransform.localEulerAngles,
                v => _rectTransform.localEulerAngles = v,
                () => _copiedRotation = _rectTransform.localEulerAngles,
                () => { if (_copiedRotation.HasValue) _rectTransform.localEulerAngles = _copiedRotation.Value; },
                () => _rectTransform.localRotation = Quaternion.identity,
                _copiedRotation.HasValue, totalButtonWidth);

            // Scale
            DrawVector3Field("Scale", _rectTransform.localScale,
                v => _rectTransform.localScale = v,
                () => _copiedScale = _rectTransform.localScale,
                () => { if (_copiedScale.HasValue) _rectTransform.localScale = _copiedScale.Value; },
                () => _rectTransform.localScale = Vector3.one,
                _copiedScale.HasValue, totalButtonWidth);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawVector2Field(string label, SerializedProperty prop, Action onCopy, Action onPaste, Action onReset, bool hasCopied, float totalButtonWidth)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
            DrawButtons(onCopy, onPaste, onReset, hasCopied, 20f);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawVector3Field(string label, Vector3 value, Action<Vector3> onChange, Action onCopy, Action onPaste, Action onReset, bool hasCopied, float totalButtonWidth)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var newValue = EditorGUILayout.Vector3Field(label, value);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_rectTransform, $"Change {label}");
                onChange(newValue);
            }
            DrawButtons(onCopy, onPaste, onReset, hasCopied, 20f);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawButtons(Action onCopy, Action onPaste, Action onReset, bool hasCopied, float buttonWidth)
        {
            if (GUILayout.Button("C", GUILayout.Width(buttonWidth)))
            {
                onCopy();
            }
            EditorGUI.BeginDisabledGroup(!hasCopied);
            if (GUILayout.Button("P", GUILayout.Width(buttonWidth)))
            {
                Undo.RecordObject(_rectTransform, "Paste");
                onPaste();
            }
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("R", GUILayout.Width(buttonWidth)))
            {
                Undo.RecordObject(_rectTransform, "Reset");
                onReset();
            }
        }

        private void ShowLayoutDropdown(Rect buttonRect)
        {
            // 使用Unity内置的LayoutDropdownWindow
            if (_layoutDropdownType != null && _layoutDropdownCtor != null)
            {
                try
                {
                    var instance = _layoutDropdownCtor.Invoke(new object[] { serializedObject });
                    if (instance != null)
                    {
                        PopupWindow.Show(buttonRect, (PopupWindowContent)instance);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to show LayoutDropdownWindow: {e.Message}");
                }
            }
        }
        
        private class AnchorPresetPopup : PopupWindowContent
        {
            private RectTransform _rectTransform;
            private SerializedObject _serializedObject;
            private const float ButtonSize = 32f;
            private const float Padding = 8f;
            
            public AnchorPresetPopup(RectTransform rectTransform, SerializedObject serializedObject)
            {
                _rectTransform = rectTransform;
                _serializedObject = serializedObject;
            }
            
            public override Vector2 GetWindowSize()
            {
                return new Vector2(ButtonSize * 4 + Padding * 2, ButtonSize * 4 + Padding * 2 + 20);
            }
            
            public override void OnGUI(Rect rect)
            {
                GUILayout.Label("Anchor Presets", EditorStyles.boldLabel);
                
                // 4x4 网格布局
                float x = Padding;
                float y = 22;
                
                // 第一行: 左上, 中上, 右上, 横向拉伸上
                DrawAnchorButton(new Rect(x, y, ButtonSize, ButtonSize), 0, 1, 0, 1, "◤");
                DrawAnchorButton(new Rect(x + ButtonSize, y, ButtonSize, ButtonSize), 0.5f, 1, 0.5f, 1, "▲");
                DrawAnchorButton(new Rect(x + ButtonSize * 2, y, ButtonSize, ButtonSize), 1, 1, 1, 1, "◥");
                DrawAnchorButton(new Rect(x + ButtonSize * 3, y, ButtonSize, ButtonSize), 0, 1, 1, 1, "⬌");
                
                y += ButtonSize;
                // 第二行: 左中, 中心, 右中, 横向拉伸中
                DrawAnchorButton(new Rect(x, y, ButtonSize, ButtonSize), 0, 0.5f, 0, 0.5f, "◀");
                DrawAnchorButton(new Rect(x + ButtonSize, y, ButtonSize, ButtonSize), 0.5f, 0.5f, 0.5f, 0.5f, "●");
                DrawAnchorButton(new Rect(x + ButtonSize * 2, y, ButtonSize, ButtonSize), 1, 0.5f, 1, 0.5f, "▶");
                DrawAnchorButton(new Rect(x + ButtonSize * 3, y, ButtonSize, ButtonSize), 0, 0.5f, 1, 0.5f, "━");
                
                y += ButtonSize;
                // 第三行: 左下, 中下, 右下, 横向拉伸下
                DrawAnchorButton(new Rect(x, y, ButtonSize, ButtonSize), 0, 0, 0, 0, "◣");
                DrawAnchorButton(new Rect(x + ButtonSize, y, ButtonSize, ButtonSize), 0.5f, 0, 0.5f, 0, "▼");
                DrawAnchorButton(new Rect(x + ButtonSize * 2, y, ButtonSize, ButtonSize), 1, 0, 1, 0, "◢");
                DrawAnchorButton(new Rect(x + ButtonSize * 3, y, ButtonSize, ButtonSize), 0, 0, 1, 0, "⬌");
                
                y += ButtonSize;
                // 第四行: 纵向拉伸左, 纵向拉伸中, 纵向拉伸右, 全拉伸
                DrawAnchorButton(new Rect(x, y, ButtonSize, ButtonSize), 0, 0, 0, 1, "┃");
                DrawAnchorButton(new Rect(x + ButtonSize, y, ButtonSize, ButtonSize), 0.5f, 0, 0.5f, 1, "│");
                DrawAnchorButton(new Rect(x + ButtonSize * 2, y, ButtonSize, ButtonSize), 1, 0, 1, 1, "┃");
                DrawAnchorButton(new Rect(x + ButtonSize * 3, y, ButtonSize, ButtonSize), 0, 0, 1, 1, "▣");
            }
            
            private void DrawAnchorButton(Rect rect, float minX, float minY, float maxX, float maxY, string icon)
            {
                if (GUI.Button(rect, icon))
                {
                    Undo.RecordObject(_rectTransform, "Set Anchor");
                    _rectTransform.anchorMin = new Vector2(minX, minY);
                    _rectTransform.anchorMax = new Vector2(maxX, maxY);
                    _serializedObject.Update();
                    editorWindow.Close();
                }
            }
        }

        private void DrawAnchorPreview(Rect rect)
        {
            var padding = 12f;
            var innerRect = new Rect(rect.x + padding, rect.y + padding, rect.width - padding * 2, rect.height - padding * 2);
            
            // 绘制父矩形框
            Handles.color = new Color(0.6f, 0.6f, 0.6f);
            Handles.DrawSolidRectangleWithOutline(innerRect, new Color(0.2f, 0.2f, 0.2f, 0.3f), new Color(0.6f, 0.6f, 0.6f));
            
            var anchorMin = _rectTransform.anchorMin;
            var anchorMax = _rectTransform.anchorMax;
            
            float minX = innerRect.x + anchorMin.x * innerRect.width;
            float maxX = innerRect.x + anchorMax.x * innerRect.width;
            float minY = innerRect.yMax - anchorMax.y * innerRect.height;
            float maxY = innerRect.yMax - anchorMin.y * innerRect.height;
            
            // 绘制锚点
            Handles.color = new Color(0.2f, 0.7f, 1f);
            float anchorSize = 4f;
            
            // 四个锚点
            DrawAnchorTriangle(new Vector2(minX, minY), anchorSize, 0); // 左上
            DrawAnchorTriangle(new Vector2(maxX, minY), anchorSize, 1); // 右上
            DrawAnchorTriangle(new Vector2(minX, maxY), anchorSize, 2); // 左下
            DrawAnchorTriangle(new Vector2(maxX, maxY), anchorSize, 3); // 右下
        }

        private void DrawAnchorTriangle(Vector2 pos, float size, int corner)
        {
            Vector3[] points = new Vector3[3];
            switch (corner)
            {
                case 0: // 左上
                    points[0] = new Vector3(pos.x, pos.y);
                    points[1] = new Vector3(pos.x + size, pos.y);
                    points[2] = new Vector3(pos.x, pos.y + size);
                    break;
                case 1: // 右上
                    points[0] = new Vector3(pos.x, pos.y);
                    points[1] = new Vector3(pos.x - size, pos.y);
                    points[2] = new Vector3(pos.x, pos.y + size);
                    break;
                case 2: // 左下
                    points[0] = new Vector3(pos.x, pos.y);
                    points[1] = new Vector3(pos.x + size, pos.y);
                    points[2] = new Vector3(pos.x, pos.y - size);
                    break;
                case 3: // 右下
                    points[0] = new Vector3(pos.x, pos.y);
                    points[1] = new Vector3(pos.x - size, pos.y);
                    points[2] = new Vector3(pos.x, pos.y - size);
                    break;
            }
            Handles.DrawAAConvexPolygon(points);
        }
    }
}
