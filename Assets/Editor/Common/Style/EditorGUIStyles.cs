using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor
{
    /// <summary>
    /// 通用 IMGUI 样式 - 与 EditorCommonStyle.uss 保持一致的视觉风格
    /// </summary>
    public static class EditorGUIStyles
    {
        // 颜色定义 - 与 USS 保持一致
        public static readonly Color BackgroundDark = new Color(0.165f, 0.165f, 0.165f);      // rgb(42, 42, 42)
        public static readonly Color BackgroundMedium = new Color(0.2f, 0.2f, 0.2f);          // rgb(51, 51, 51)
        public static readonly Color BackgroundLight = new Color(0.145f, 0.145f, 0.149f);     // rgb(37, 37, 38)
        public static readonly Color BorderColor = new Color(0.102f, 0.102f, 0.102f);         // rgb(26, 26, 26)
        
        public static readonly Color TextPrimary = new Color(0.863f, 0.863f, 0.863f);         // rgb(220, 220, 220)
        public static readonly Color TextSecondary = new Color(0.706f, 0.706f, 0.706f);       // rgb(180, 180, 180)
        public static readonly Color TextMuted = new Color(0.5f, 0.5f, 0.5f);                 // rgb(128, 128, 128)
        
        public static readonly Color PrimaryColor = new Color(0.231f, 0.51f, 0.965f);         // rgb(59, 130, 246)
        public static readonly Color PrimaryLight = new Color(0.576f, 0.773f, 0.992f);        // rgb(147, 197, 253)
        public static readonly Color PrimaryBg = new Color(0.231f, 0.51f, 0.965f, 0.2f);      // rgba(59, 130, 246, 0.2)
        
        public static readonly Color SuccessColor = new Color(0.063f, 0.725f, 0.506f);        // rgb(16, 185, 129)
        public static readonly Color SuccessLight = new Color(0.655f, 0.953f, 0.816f);        // rgb(167, 243, 208)
        public static readonly Color SuccessBg = new Color(0.063f, 0.725f, 0.506f, 0.15f);
        
        public static readonly Color WarningColor = new Color(0.961f, 0.62f, 0.043f);         // rgb(245, 158, 11)
        public static readonly Color WarningLight = new Color(0.992f, 0.902f, 0.541f);        // rgb(253, 230, 138)
        public static readonly Color WarningBg = new Color(0.961f, 0.62f, 0.043f, 0.1f);
        
        public static readonly Color DangerColor = new Color(0.937f, 0.267f, 0.267f);         // rgb(239, 68, 68)
        public static readonly Color DangerLight = new Color(0.988f, 0.647f, 0.647f);         // rgb(252, 165, 165)
        public static readonly Color DangerBg = new Color(0.937f, 0.267f, 0.267f, 0.15f);

        // 缓存的样式
        private static GUIStyle _cardStyle;
        private static GUIStyle _cardTitleStyle;
        private static GUIStyle _toolbarStyle;
        private static GUIStyle _toolbarButtonStyle;
        private static GUIStyle _toolbarButtonSelectedStyle;
        private static GUIStyle _miniLabelStyle;
        private static GUIStyle _statusLabelStyle;
        private static GUIStyle _helpBoxInfoStyle;
        private static GUIStyle _helpBoxWarningStyle;
        private static GUIStyle _separatorStyle;

        /// <summary>
        /// 卡片容器样式
        /// </summary>
        public static GUIStyle CardStyle
        {
            get
            {
                if (_cardStyle == null)
                {
                    _cardStyle = new GUIStyle(EditorStyles.helpBox)
                    {
                        padding = new RectOffset(12, 12, 10, 10),
                        margin = new RectOffset(0, 0, 4, 8)
                    };
                }
                return _cardStyle;
            }
        }

        /// <summary>
        /// 卡片标题样式
        /// </summary>
        public static GUIStyle CardTitleStyle
        {
            get
            {
                if (_cardTitleStyle == null)
                {
                    _cardTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 12,
                        margin = new RectOffset(0, 0, 0, 8)
                    };
                    _cardTitleStyle.normal.textColor = TextPrimary;
                }
                return _cardTitleStyle;
            }
        }

        /// <summary>
        /// 工具栏样式
        /// </summary>
        public static GUIStyle ToolbarStyle
        {
            get
            {
                if (_toolbarStyle == null)
                {
                    _toolbarStyle = new GUIStyle(EditorStyles.toolbar)
                    {
                        padding = new RectOffset(8, 8, 2, 2),
                        fixedHeight = 0
                    };
                }
                return _toolbarStyle;
            }
        }

        /// <summary>
        /// 工具栏按钮样式
        /// </summary>
        public static GUIStyle ToolbarButtonStyle
        {
            get
            {
                if (_toolbarButtonStyle == null)
                {
                    _toolbarButtonStyle = new GUIStyle(EditorStyles.miniButton)
                    {
                        padding = new RectOffset(8, 8, 4, 4),
                        margin = new RectOffset(2, 2, 2, 2),
                        fontSize = 11
                    };
                }
                return _toolbarButtonStyle;
            }
        }

        /// <summary>
        /// 工具栏按钮选中样式
        /// </summary>
        public static GUIStyle ToolbarButtonSelectedStyle
        {
            get
            {
                if (_toolbarButtonSelectedStyle == null)
                {
                    _toolbarButtonSelectedStyle = new GUIStyle(ToolbarButtonStyle);
                    _toolbarButtonSelectedStyle.normal.textColor = PrimaryLight;
                }
                return _toolbarButtonSelectedStyle;
            }
        }

        /// <summary>
        /// 小标签样式
        /// </summary>
        public static GUIStyle MiniLabelStyle
        {
            get
            {
                if (_miniLabelStyle == null)
                {
                    _miniLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        fontSize = 10
                    };
                    _miniLabelStyle.normal.textColor = TextMuted;
                }
                return _miniLabelStyle;
            }
        }

        /// <summary>
        /// 状态标签样式（成功）
        /// </summary>
        public static GUIStyle StatusLabelStyle
        {
            get
            {
                if (_statusLabelStyle == null)
                {
                    _statusLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        fontSize = 10
                    };
                    _statusLabelStyle.normal.textColor = SuccessLight;
                }
                return _statusLabelStyle;
            }
        }

        /// <summary>
        /// 绘制分隔线
        /// </summary>
        public static void DrawSeparator(float alpha = 0.3f)
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, alpha));
        }

        /// <summary>
        /// 绘制带颜色的分隔线
        /// </summary>
        public static void DrawColoredSeparator(Color color)
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, color);
        }

        /// <summary>
        /// 绘制卡片背景
        /// </summary>
        public static Rect BeginCard()
        {
            return EditorGUILayout.BeginVertical(CardStyle);
        }

        /// <summary>
        /// 结束卡片
        /// </summary>
        public static void EndCard()
        {
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制卡片标题
        /// </summary>
        public static void DrawCardTitle(string title)
        {
            EditorGUILayout.LabelField(title, CardTitleStyle);
        }

        /// <summary>
        /// 绘制带图标的按钮
        /// </summary>
        public static bool DrawIconButton(string icon, string tooltip, float width = 22)
        {
            return GUILayout.Button(new GUIContent(icon, tooltip), EditorStyles.miniButton, GUILayout.Width(width));
        }

        /// <summary>
        /// 绘制主要按钮（蓝色高亮）
        /// </summary>
        public static bool DrawPrimaryButton(string text, params GUILayoutOption[] options)
        {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = PrimaryColor;
            var result = GUILayout.Button(text, options);
            GUI.backgroundColor = oldColor;
            return result;
        }

        /// <summary>
        /// 绘制危险按钮（红色）
        /// </summary>
        public static bool DrawDangerButton(string text, params GUILayoutOption[] options)
        {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = DangerColor;
            var result = GUILayout.Button(text, options);
            GUI.backgroundColor = oldColor;
            return result;
        }

        /// <summary>
        /// 绘制成功按钮（绿色）
        /// </summary>
        public static bool DrawSuccessButton(string text, params GUILayoutOption[] options)
        {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = SuccessColor;
            var result = GUILayout.Button(text, options);
            GUI.backgroundColor = oldColor;
            return result;
        }

        /// <summary>
        /// 绘制切换按钮组中的单个按钮
        /// </summary>
        public static bool DrawToggleButton(string text, bool isSelected, params GUILayoutOption[] options)
        {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = isSelected ? PrimaryColor : Color.white;
            var result = GUILayout.Button(text, EditorStyles.miniButton, options);
            GUI.backgroundColor = oldColor;
            return result;
        }

        /// <summary>
        /// 绘制信息提示框
        /// </summary>
        public static void DrawInfoBox(string message)
        {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(PrimaryColor.r, PrimaryColor.g, PrimaryColor.b, 0.3f);
            EditorGUILayout.HelpBox(message, MessageType.Info);
            GUI.backgroundColor = oldColor;
        }

        /// <summary>
        /// 绘制警告提示框
        /// </summary>
        public static void DrawWarningBox(string message)
        {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(WarningColor.r, WarningColor.g, WarningColor.b, 0.3f);
            EditorGUILayout.HelpBox(message, MessageType.Warning);
            GUI.backgroundColor = oldColor;
        }

        /// <summary>
        /// 绘制错误提示框
        /// </summary>
        public static void DrawErrorBox(string message)
        {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(DangerColor.r, DangerColor.g, DangerColor.b, 0.3f);
            EditorGUILayout.HelpBox(message, MessageType.Error);
            GUI.backgroundColor = oldColor;
        }

        /// <summary>
        /// 创建带颜色的标签样式
        /// </summary>
        public static GUIStyle CreateColoredLabelStyle(Color color, int fontSize = 11)
        {
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = fontSize
            };
            style.normal.textColor = color;
            return style;
        }
    }
}
