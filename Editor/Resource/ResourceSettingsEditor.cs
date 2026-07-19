using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.ResourceEditor
{
    /// <summary>
    /// 定义 Resource Settings Editor 类型。
    /// </summary>
    [CustomPropertyDrawer(typeof(ResourceSettings))]
    public sealed class ResourceSettingsEditor : PropertyDrawer
    {
        private const float Spacing = 2f;

        /// <summary>
        /// Unity OnGUI 回调。
        /// </summary>
        /// <param name="position">position 参数。</param>
        /// <param name="property">property 参数。</param>
        /// <param name="label">label 参数。</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            position.height = EditorGUIUtility.singleLineHeight;
            property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, label, true);
            if (property.isExpanded is false)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                DrawProperty(ref position, property, "Mode");
                DrawDefaultPackages(ref position, property);
                DrawProperty(ref position, property, "MaxConcurrentBatchLoads");
                DrawChannel(ref position, property);
                DrawProperty(ref position, property, "ServerUrl");
                DrawProperty(ref position, property, "ClientBuild");
                DrawProperty(ref position, property, "TrustedKeys");
                DrawProperty(ref position, property, "ManifestName");
            }
        }

        /// <summary>
        /// 获取 Property Height。
        /// </summary>
        /// <param name="property">property 参数。</param>
        /// <param name="label">label 参数。</param>
        /// <returns>执行结果。</returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = EditorGUIUtility.singleLineHeight;
            if (property.isExpanded is false)
            {
                return height;
            }

            height += GetPropertyHeight(property, "Mode");
            height += GetDefaultPackagesHeight(property);
            height += GetPropertyHeight(property, "MaxConcurrentBatchLoads");
            height += GetChannelHeight(property);
            height += GetPropertyHeight(property, "ServerUrl");
            height += GetPropertyHeight(property, "ClientBuild");
            height += GetPropertyHeight(property, "TrustedKeys");
            height += GetPropertyHeight(property, "ManifestName");
            return height;
        }

        /// <summary>
        /// 绘制 Property。
        /// </summary>
        /// <param name="position">position 参数。</param>
        /// <param name="parent">parent 参数。</param>
        /// <param name="propertyName">property Name 参数。</param>
        private static void DrawProperty(ref Rect position, SerializedProperty parent, string propertyName)
        {
            var property = parent.FindPropertyRelative(propertyName);
            if (property == null)
            {
                return;
            }

            position.y += position.height + Spacing;
            position.height = EditorGUI.GetPropertyHeight(property, true);
            EditorGUI.PropertyField(position, property, true);
        }

        private static void DrawDefaultPackages(ref Rect position, SerializedProperty parent)
        {
            var property = parent.FindPropertyRelative("DefaultPackages");
            if (property == null)
            {
                return;
            }

            position.y += position.height + Spacing;
            position.height = EditorGUIUtility.singleLineHeight;

            using (new EditorGUI.PropertyScope(position, GUIContent.none, property))
            {
                var controlId = GUIUtility.GetControlID(FocusType.Keyboard, position);
                var buttonRect = EditorGUI.PrefixLabel(position, controlId, new GUIContent(property.displayName));
                var values = ReadStringArray(property);
                var label = FormatDefaultPackages(values);

                using (new EditorGUI.DisabledScope(property.serializedObject.isEditingMultipleObjects))
                {
                    if (EditorGUI.DropdownButton(buttonRect, new GUIContent(label), FocusType.Keyboard))
                    {
                        ShowDefaultPackagesMenu(buttonRect, property, values);
                    }
                }
            }
        }

        private static void ShowDefaultPackagesMenu(Rect buttonRect, SerializedProperty property, IReadOnlyList<string> selectedPackages)
        {
            var menu = new GenericMenu();
            var serializedObject = property.serializedObject;
            var propertyPath = property.propertyPath;
            var selectedSet = new HashSet<string>(selectedPackages, StringComparer.Ordinal);
            var packages = GetPackageOptions().ToArray();

            if (packages.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("No packages configured"));
            }
            else
            {
                foreach (var package in packages)
                {
                    var packageName = package;
                    menu.AddItem(
                        new GUIContent(packageName),
                        selectedSet.Contains(packageName),
                        () => ToggleDefaultPackage(serializedObject, propertyPath, packageName));
                }
            }

            var optionSet = new HashSet<string>(packages, StringComparer.Ordinal);
            var missingPackages = selectedPackages
                .Where(package => optionSet.Contains(package) is false)
                .ToArray();
            if (missingPackages.Length > 0)
            {
                menu.AddSeparator(string.Empty);
                foreach (var package in missingPackages)
                {
                    var packageName = package;
                    menu.AddItem(
                        new GUIContent($"Missing/{packageName}"),
                        true,
                        () => ToggleDefaultPackage(serializedObject, propertyPath, packageName));
                }
            }

            menu.AddSeparator(string.Empty);
            var hasEverything = packages.Length > 0 && packages.All(package => selectedSet.Contains(package));
            menu.AddItem(
                new GUIContent("Everything"),
                hasEverything,
                () => SetDefaultPackages(serializedObject, propertyPath, packages));

            menu.AddItem(
                new GUIContent("Nothing"),
                selectedPackages.Count == 0,
                () => SetDefaultPackages(serializedObject, propertyPath, Array.Empty<string>()));

            menu.DropDown(buttonRect);
        }

        private static IEnumerable<string> GetPackageOptions()
        {
            var settings = GameDeveloperKit.ResourceEditor.Authoring.Settings.LoadOrCreate();
            return settings.Packages
                .Where(package => package != null)
                .Where(package => GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsBuiltinPackage(package) is false)
                .Select(package => package.Name)
                .Where(package => string.IsNullOrWhiteSpace(package) is false)
                .Select(package => package.Trim())
                .Distinct(StringComparer.Ordinal);
        }

        private static void DrawChannel(ref Rect position, SerializedProperty parent)
        {
            var channelNameProperty = parent.FindPropertyRelative("ChannelName");
            if (channelNameProperty == null)
            {
                return;
            }

            position.y += position.height + Spacing;
            position.height = EditorGUIUtility.singleLineHeight;

            EditorGUI.PropertyField(position, channelNameProperty, new GUIContent("Channel"));
        }

        private static void ToggleDefaultPackage(SerializedObject serializedObject, string propertyPath, string packageName)
        {
            serializedObject.Update();
            var property = serializedObject.FindProperty(propertyPath);
            if (property == null)
            {
                return;
            }

            var values = ReadStringArray(property).ToList();
            var index = values.FindIndex(value => string.Equals(value, packageName, StringComparison.Ordinal));
            if (index >= 0)
            {
                values.RemoveAt(index);
            }
            else
            {
                values.Add(packageName);
            }

            SetDefaultPackages(serializedObject, propertyPath, values);
        }

        private static void SetDefaultPackages(SerializedObject serializedObject, string propertyPath, IEnumerable<string> packages)
        {
            Undo.RecordObjects(serializedObject.targetObjects, "Set Default Packages");
            serializedObject.Update();
            var property = serializedObject.FindProperty(propertyPath);
            if (property == null)
            {
                return;
            }

            WriteStringArray(property, packages);
            serializedObject.ApplyModifiedProperties();
            foreach (var target in serializedObject.targetObjects)
            {
                EditorUtility.SetDirty(target);
            }
        }

        private static List<string> ReadStringArray(SerializedProperty property)
        {
            var values = new List<string>();
            if (property == null || property.isArray is false)
            {
                return values;
            }

            for (var i = 0; i < property.arraySize; i++)
            {
                var value = property.GetArrayElementAtIndex(i).stringValue;
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                values.Add(value.Trim());
            }

            return values
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static void WriteStringArray(SerializedProperty property, IEnumerable<string> values)
        {
            var normalizedValues = values?
                .Where(value => string.IsNullOrWhiteSpace(value) is false)
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();

            property.arraySize = normalizedValues.Length;
            for (var i = 0; i < normalizedValues.Length; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = normalizedValues[i];
            }
        }

        private static string FormatDefaultPackages(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return "Nothing";
            }

            var packages = GetPackageOptions().ToArray();
            if (packages.Length > 0 && packages.All(package => values.Contains(package)))
            {
                return "Everything";
            }

            if (values.Count <= 3)
            {
                return string.Join(", ", values);
            }

            return $"{values.Count} packages selected";
        }

        private static float GetDefaultPackagesHeight(SerializedProperty parent)
        {
            var property = parent.FindPropertyRelative("DefaultPackages");
            return property == null ? 0f : EditorGUIUtility.singleLineHeight + Spacing;
        }

        private static float GetChannelHeight(SerializedProperty parent)
        {
            var channelNameProperty = parent.FindPropertyRelative("ChannelName");
            return channelNameProperty == null
                ? 0f
                : EditorGUIUtility.singleLineHeight + Spacing;
        }

        /// <summary>
        /// 获取 Property Height。
        /// </summary>
        /// <param name="parent">parent 参数。</param>
        /// <param name="propertyName">property Name 参数。</param>
        /// <returns>执行结果。</returns>
        private static float GetPropertyHeight(SerializedProperty parent, string propertyName)
        {
            var property = parent.FindPropertyRelative(propertyName);
            return property == null ? 0f : EditorGUI.GetPropertyHeight(property, true) + Spacing;
        }
    }
}
