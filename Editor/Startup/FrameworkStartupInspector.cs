using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Procedure;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.StartupEditor
{
    /// <summary>
    /// Inspector for FrameworkStartup.
    /// </summary>
    [CustomEditor(typeof(FrameworkStartup))]
    public sealed class FrameworkStartupInspector : UnityEditor.Editor
    {
        private SerializedProperty m_TargetProcedureTypeName;
        private SerializedProperty m_TargetUserData;
        private SerializedProperty m_Modules;
        private SerializedProperty m_ShutdownAppOnDestroy;

        private ProcedureTypeEntry[] m_ProcedureTypes = Array.Empty<ProcedureTypeEntry>();
        private string[] m_ProcedureLabels = { "None" };

        private void OnEnable()
        {
            m_TargetProcedureTypeName = serializedObject.FindProperty("m_TargetProcedureTypeName");
            m_TargetUserData = serializedObject.FindProperty("m_TargetUserData");
            m_Modules = serializedObject.FindProperty("m_Modules");
            m_ShutdownAppOnDestroy = serializedObject.FindProperty("m_ShutdownAppOnDestroy");
            RefreshProcedureTypes();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawTargetProcedurePopup();
            EditorGUILayout.PropertyField(m_TargetUserData);
            EditorGUILayout.PropertyField(m_Modules, true);
            EditorGUILayout.PropertyField(m_ShutdownAppOnDestroy);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTargetProcedurePopup()
        {
            EditorGUILayout.LabelField("Procedure", EditorStyles.boldLabel);
            if (m_ProcedureTypes.Length == 0)
            {
                EditorGUILayout.HelpBox("No concrete ProcedureBase type found.", MessageType.Warning);
                EditorGUILayout.PropertyField(m_TargetProcedureTypeName);
                return;
            }

            var selectedIndex = FindSelectedProcedureIndex();
            var displayIndex = selectedIndex < 0 ? 0 : selectedIndex;
            var nextIndex = EditorGUILayout.Popup("Target Procedure", displayIndex, m_ProcedureLabels);
            if (nextIndex != displayIndex)
            {
                m_TargetProcedureTypeName.stringValue = nextIndex <= 0
                    ? string.Empty
                    : m_ProcedureTypes[nextIndex - 1].AssemblyQualifiedName;
            }

            if (selectedIndex < 0 && string.IsNullOrWhiteSpace(m_TargetProcedureTypeName.stringValue) is false)
            {
                EditorGUILayout.HelpBox($"Saved procedure type is missing: {m_TargetProcedureTypeName.stringValue}", MessageType.Warning);
            }
        }

        private int FindSelectedProcedureIndex()
        {
            var current = m_TargetProcedureTypeName.stringValue;
            for (var i = 0; i < m_ProcedureTypes.Length; i++)
            {
                if (string.Equals(m_ProcedureTypes[i].AssemblyQualifiedName, current, StringComparison.Ordinal))
                {
                    return i + 1;
                }
            }

            if (string.IsNullOrWhiteSpace(current))
            {
                return 0;
            }

            return -1;
        }

        private void RefreshProcedureTypes()
        {
            var entries = new List<ProcedureTypeEntry>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<ProcedureBase>())
            {
                if (CanCreateProcedure(type) is false)
                {
                    continue;
                }

                entries.Add(new ProcedureTypeEntry(type));
            }

            m_ProcedureTypes = entries
                .OrderBy(x => x.Label, StringComparer.Ordinal)
                .ToArray();
            m_ProcedureLabels = new[] { "None" }
                .Concat(m_ProcedureTypes.Select(x => x.Label))
                .ToArray();
        }

        private static bool CanCreateProcedure(Type type)
        {
            return type != null &&
                   type.IsAbstract is false &&
                   type.ContainsGenericParameters is false &&
                   (type.IsPublic || type.IsNestedPublic) &&
                   type.GetConstructor(Type.EmptyTypes) != null &&
                   typeof(ProcedureBase).IsAssignableFrom(type);
        }

        private readonly struct ProcedureTypeEntry
        {
            public ProcedureTypeEntry(Type type)
            {
                AssemblyQualifiedName = type.AssemblyQualifiedName;
                Label = string.IsNullOrWhiteSpace(type.Namespace)
                    ? type.Name
                    : $"{type.Namespace}.{type.Name}";
            }

            public string AssemblyQualifiedName { get; }

            public string Label { get; }
        }
    }
}
