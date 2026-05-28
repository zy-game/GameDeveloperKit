using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.UI;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.UIEditor
{
    [CustomEditor(typeof(UIDocument))]
    public sealed class UIDocumentInspector : Editor
    {
        private SerializedProperty m_FullScreenRoot;
        private SerializedProperty m_SafeAreaRoot;
        private SerializedProperty m_Mappings;

        private string m_ClassName = "Example";
        private string m_OutputFolder = "Assets/Scripts/UI";
        private string m_UIPath;
        private UILayer m_Layer = UILayer.Window;

        private void OnEnable()
        {
            m_FullScreenRoot = serializedObject.FindProperty("fullScreenRoot");
            m_SafeAreaRoot = serializedObject.FindProperty("safeAreaRoot");
            m_Mappings = serializedObject.FindProperty("mappings");
            var assetPath = AssetDatabase.GetAssetPath(((UIDocument)target).gameObject);
            m_UIPath = string.IsNullOrWhiteSpace(assetPath) ? string.Empty : assetPath;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(m_FullScreenRoot);
            EditorGUILayout.PropertyField(m_SafeAreaRoot);
            DrawMappings();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawGenerator();
        }

        private void DrawMappings()
        {
            EditorGUILayout.LabelField("Bindings", EditorStyles.boldLabel);
            for (var i = 0; i < m_Mappings.arraySize; i++)
            {
                var mapping = m_Mappings.GetArrayElementAtIndex(i);
                var name = mapping.FindPropertyRelative("Name");
                var targetObject = mapping.FindPropertyRelative("Target");
                var components = mapping.FindPropertyRelative("Components");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(name);
                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    m_Mappings.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(targetObject);
                DrawComponentSelection(targetObject.objectReferenceValue as GameObject, components);
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add Binding"))
            {
                m_Mappings.InsertArrayElementAtIndex(m_Mappings.arraySize);
                var mapping = m_Mappings.GetArrayElementAtIndex(m_Mappings.arraySize - 1);
                mapping.FindPropertyRelative("Name").stringValue = string.Empty;
                mapping.FindPropertyRelative("Target").objectReferenceValue = null;
                mapping.FindPropertyRelative("Components").arraySize = 0;
            }
        }

        private static void DrawComponentSelection(GameObject targetObject, SerializedProperty components)
        {
            if (targetObject == null)
            {
                EditorGUILayout.HelpBox("Select a target GameObject to bind components.", MessageType.Info);
                return;
            }

            var selectedTypes = new HashSet<string>();
            for (var i = 0; i < components.arraySize; i++)
            {
                selectedTypes.Add(components.GetArrayElementAtIndex(i).FindPropertyRelative("TypeName").stringValue);
            }

            foreach (var component in targetObject.GetComponents<Component>().Where(component => component != null))
            {
                var type = component.GetType();
                var typeName = type.FullName;
                var selected = selectedTypes.Contains(typeName);
                var nextSelected = EditorGUILayout.ToggleLeft(type.Name, selected);
                if (nextSelected == selected)
                {
                    continue;
                }

                if (nextSelected)
                {
                    AddComponentBinding(components, type);
                }
                else
                {
                    RemoveComponentBinding(components, typeName);
                }
            }

            for (var i = 0; i < components.arraySize; i++)
            {
                var component = components.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(component.FindPropertyRelative("Name"));
                EditorGUILayout.LabelField(TypeLabel(component.FindPropertyRelative("TypeName").stringValue), GUILayout.Width(160));
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void AddComponentBinding(SerializedProperty components, Type type)
        {
            components.InsertArrayElementAtIndex(components.arraySize);
            var binding = components.GetArrayElementAtIndex(components.arraySize - 1);
            binding.FindPropertyRelative("Name").stringValue = string.Empty;
            binding.FindPropertyRelative("TypeName").stringValue = type.FullName;
        }

        private static void RemoveComponentBinding(SerializedProperty components, string typeName)
        {
            for (var i = components.arraySize - 1; i >= 0; i--)
            {
                if (components.GetArrayElementAtIndex(i).FindPropertyRelative("TypeName").stringValue == typeName)
                {
                    components.DeleteArrayElementAtIndex(i);
                }
            }
        }

        private void DrawGenerator()
        {
            EditorGUILayout.LabelField("Code Generation", EditorStyles.boldLabel);
            m_ClassName = EditorGUILayout.TextField("Class Name", m_ClassName);
            m_OutputFolder = EditorGUILayout.TextField("Output Folder", m_OutputFolder);
            m_UIPath = EditorGUILayout.TextField("UI Path", m_UIPath);
            m_Layer = (UILayer)EditorGUILayout.EnumPopup("Layer", m_Layer);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Folder"))
            {
                var selected = EditorUtility.OpenFolderPanel("Select output folder", Application.dataPath, string.Empty);
                if (string.IsNullOrWhiteSpace(selected) is false && selected.StartsWith(Application.dataPath, StringComparison.Ordinal))
                {
                    m_OutputFolder = "Assets" + selected.Substring(Application.dataPath.Length);
                }
            }

            if (GUILayout.Button("Generate Code"))
            {
                Generate();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void Generate()
        {
            try
            {
                UIDocumentGenerator.Generate((UIDocument)target, m_ClassName, m_OutputFolder, m_UIPath, m_Layer);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("UIDocument Generate Failed", exception.Message, "OK");
            }
        }

        private static string TypeLabel(string typeName)
        {
            return string.IsNullOrWhiteSpace(typeName) ? string.Empty : Path.GetFileName(typeName);
        }
    }
}
