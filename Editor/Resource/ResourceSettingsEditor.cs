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
                DrawProperty(ref position, property, "DefaultPackages");
                DrawProperty(ref position, property, "ManifestName");
                DrawProperty(ref position, property, "CachePath");
                DrawProperty(ref position, property, "ChannelId");
                DrawProperty(ref position, property, "ChannelName");
                DrawProperty(ref position, property, "ServerUrl");
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
            height += GetPropertyHeight(property, "DefaultPackages");
            height += GetPropertyHeight(property, "ManifestName");
            height += GetPropertyHeight(property, "CachePath");
            height += GetPropertyHeight(property, "ChannelId");
            height += GetPropertyHeight(property, "ChannelName");
            height += GetPropertyHeight(property, "ServerUrl");
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
