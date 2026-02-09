using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.SceneTools
{
    /// <summary>
    /// Scene工具设置，使用EditorPrefs持久化
    /// </summary>
    public static class SceneToolsSettings
    {
        private const string PREFIX = "GameFramework.SceneTools.";
        
        private const string KEY_ENABLED = PREFIX + "Enabled";
        private const string KEY_SNAP_ENABLED = PREFIX + "SnapEnabled";
        private const string KEY_GUIDELINES_VISIBLE = PREFIX + "GuidelinesVisible";
        private const string KEY_SNAP_SIZE = PREFIX + "SnapSize";
        private const string KEY_GUIDELINE_COLOR = PREFIX + "GuidelineColor";
        private const string KEY_GRID_SCALE = PREFIX + "GridScale";
        
        // 默认值
        private const bool DEFAULT_ENABLED = true;
        private const bool DEFAULT_SNAP_ENABLED = false;
        private const bool DEFAULT_GUIDELINES_VISIBLE = true;
        private const float DEFAULT_SNAP_SIZE = 1f;
        private const float DEFAULT_GRID_SCALE = 1f;
        
        /// <summary>
        /// Scene工具是否启用
        /// </summary>
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(KEY_ENABLED, DEFAULT_ENABLED);
            set => EditorPrefs.SetBool(KEY_ENABLED, value);
        }
        
        /// <summary>
        /// 吸附功能是否启用
        /// </summary>
        public static bool SnapEnabled
        {
            get => EditorPrefs.GetBool(KEY_SNAP_ENABLED, DEFAULT_SNAP_ENABLED);
            set => EditorPrefs.SetBool(KEY_SNAP_ENABLED, value);
        }
        
        /// <summary>
        /// 辅助线是否可见
        /// </summary>
        public static bool GuidelinesVisible
        {
            get => EditorPrefs.GetBool(KEY_GUIDELINES_VISIBLE, DEFAULT_GUIDELINES_VISIBLE);
            set => EditorPrefs.SetBool(KEY_GUIDELINES_VISIBLE, value);
        }
        
        /// <summary>
        /// 吸附网格大小
        /// </summary>
        public static float SnapSize
        {
            get => EditorPrefs.GetFloat(KEY_SNAP_SIZE, DEFAULT_SNAP_SIZE);
            set => EditorPrefs.SetFloat(KEY_SNAP_SIZE, Mathf.Max(0.1f, value));
        }
        
        /// <summary>
        /// 网格缩放倍数（手动调整）
        /// </summary>
        public static float GridScale
        {
            get => EditorPrefs.GetFloat(KEY_GRID_SCALE, DEFAULT_GRID_SCALE);
            set => EditorPrefs.SetFloat(KEY_GRID_SCALE, Mathf.Clamp(value, 0.001f, 100f));
        }
        
        /// <summary>
        /// 辅助线颜色
        /// </summary>
        public static Color GuidelineColor
        {
            get
            {
                var colorStr = EditorPrefs.GetString(KEY_GUIDELINE_COLOR, "");
                if (string.IsNullOrEmpty(colorStr))
                    return new Color(0.5f, 0.8f, 1f, 0.5f);
                
                if (ColorUtility.TryParseHtmlString(colorStr, out var color))
                    return color;
                
                return new Color(0.5f, 0.8f, 1f, 0.5f);
            }
            set => EditorPrefs.SetString(KEY_GUIDELINE_COLOR, "#" + ColorUtility.ToHtmlStringRGBA(value));
        }
        
        /// <summary>
        /// 重置为默认设置
        /// </summary>
        public static void ResetToDefaults()
        {
            Enabled = DEFAULT_ENABLED;
            SnapEnabled = DEFAULT_SNAP_ENABLED;
            GuidelinesVisible = DEFAULT_GUIDELINES_VISIBLE;
            SnapSize = DEFAULT_SNAP_SIZE;
            GridScale = DEFAULT_GRID_SCALE;
            GuidelineColor = new Color(0.5f, 0.8f, 1f, 0.5f);
        }
    }
}
