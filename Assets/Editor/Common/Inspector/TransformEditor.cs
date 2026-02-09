using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor
{
    /// <summary>
    /// 自定义 Transform Inspector - 添加重置、复制、粘贴按钮
    /// </summary>
    [CustomEditor(typeof(Transform))]
    [CanEditMultipleObjects]
    public class TransformEditor : UnityEditor.Editor
    {
        private static Vector3? _copiedPosition;
        private static Vector3? _copiedRotation;
        private static Vector3? _copiedScale;
        private static CopyType _copyType = CopyType.None;

        private enum CopyType { None, Position, Rotation, Scale }

        private Transform _transform;
        private SerializedProperty _positionProperty;
        private SerializedProperty _rotationProperty;
        private SerializedProperty _scaleProperty;

        private void OnEnable()
        {
            _transform = target as Transform;
            _positionProperty = serializedObject.FindProperty("m_LocalPosition");
            _rotationProperty = serializedObject.FindProperty("m_LocalRotation");
            _scaleProperty = serializedObject.FindProperty("m_LocalScale");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 监听 ESC 键取消复制
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                CancelCopy();
                Event.current.Use();
                Repaint();
            }

            // Position
            DrawPropertyWithButtons("Position", _positionProperty, CopyType.Position,
                () => _transform.localPosition = Vector3.zero,
                () => { _copiedPosition = _transform.localPosition; _copyType = CopyType.Position; },
                () => { if (_copiedPosition.HasValue) _transform.localPosition = _copiedPosition.Value; CancelCopy(); });

            // Rotation
            DrawRotationWithButtons();

            // Scale
            DrawPropertyWithButtons("Scale", _scaleProperty, CopyType.Scale,
                () => _transform.localScale = Vector3.one,
                () => { _copiedScale = _transform.localScale; _copyType = CopyType.Scale; },
                () => { if (_copiedScale.HasValue) _transform.localScale = _copiedScale.Value; CancelCopy(); });

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPropertyWithButtons(string label, SerializedProperty property, CopyType type,
            System.Action onReset, System.Action onCopy, System.Action onPaste)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PropertyField(property, new GUIContent(label));

            // Reset 按钮
            if (GUILayout.Button("R", GUILayout.Width(20)))
            {
                Undo.RecordObject(_transform, $"Reset {label}");
                onReset();
            }

            // Copy/Paste 按钮
            if (_copyType == type)
            {
                // 显示 Paste 按钮
                if (GUILayout.Button("P", GUILayout.Width(20)))
                {
                    Undo.RecordObject(_transform, $"Paste {label}");
                    onPaste();
                }
            }
            else
            {
                // 显示 Copy 按钮
                if (GUILayout.Button("C", GUILayout.Width(20)))
                {
                    onCopy();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawRotationWithButtons()
        {
            EditorGUILayout.BeginHorizontal();

            // 使用欧拉角显示旋转
            Vector3 eulerAngles = _transform.localEulerAngles;
            EditorGUI.BeginChangeCheck();
            eulerAngles = EditorGUILayout.Vector3Field("Rotation", eulerAngles);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_transform, "Change Rotation");
                _transform.localEulerAngles = eulerAngles;
            }

            // Reset 按钮
            if (GUILayout.Button("R", GUILayout.Width(20)))
            {
                Undo.RecordObject(_transform, "Reset Rotation");
                _transform.localRotation = Quaternion.identity;
            }

            // Copy/Paste 按钮
            if (_copyType == CopyType.Rotation)
            {
                if (GUILayout.Button("P", GUILayout.Width(20)))
                {
                    if (_copiedRotation.HasValue)
                    {
                        Undo.RecordObject(_transform, "Paste Rotation");
                        _transform.localEulerAngles = _copiedRotation.Value;
                    }
                    CancelCopy();
                }
            }
            else
            {
                if (GUILayout.Button("C", GUILayout.Width(20)))
                {
                    _copiedRotation = _transform.localEulerAngles;
                    _copyType = CopyType.Rotation;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void CancelCopy()
        {
            _copyType = CopyType.None;
        }
    }
}
