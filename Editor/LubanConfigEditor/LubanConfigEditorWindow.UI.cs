using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.LubanConfigEditor
{
    public sealed partial class LubanConfigEditorWindow
    {
        private const float FieldLabelWidth = 116f;

        /// <summary>
        /// 创建 Panel。
        /// </summary>
        /// <returns>执行结果。</returns>
        private static VisualElement CreatePanel()
        {
            var panel = new VisualElement();
            panel.style.flexShrink = 0;
            panel.style.flexGrow = 0;
            panel.style.minWidth = 0;
            panel.style.paddingLeft = 12;
            panel.style.paddingRight = 12;
            panel.style.paddingTop = 10;
            panel.style.paddingBottom = 10;
            panel.style.marginBottom = 8;
            panel.style.borderBottomLeftRadius = 4;
            panel.style.borderBottomRightRadius = 4;
            panel.style.borderTopLeftRadius = 4;
            panel.style.borderTopRightRadius = 4;
            panel.style.borderLeftWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderTopWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftColor = new Color(0.28f, 0.28f, 0.28f);
            panel.style.borderRightColor = new Color(0.28f, 0.28f, 0.28f);
            panel.style.borderTopColor = new Color(0.28f, 0.28f, 0.28f);
            panel.style.borderBottomColor = new Color(0.28f, 0.28f, 0.28f);
            panel.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.19f, 0.19f, 0.19f) : new Color(0.84f, 0.84f, 0.84f);
            return panel;
        }

        /// <summary>
        /// 配置 Field Layout。
        /// </summary>
        /// <typeparam name="TValue">泛型类型参数。</typeparam>
        /// <param name="field">field 参数。</param>
        /// <returns>执行结果。</returns>
        private static BaseField<TValue> ConfigureField<TValue>(BaseField<TValue> field)
        {
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.style.minWidth = 0;
            field.style.marginBottom = 6;
            var labelWidth = string.IsNullOrEmpty(field.label) ? 0f : FieldLabelWidth;
            field.labelElement.style.minWidth = labelWidth;
            field.labelElement.style.maxWidth = labelWidth;
            field.labelElement.style.width = labelWidth;
            field.labelElement.style.flexShrink = 0;
            field.labelElement.style.whiteSpace = WhiteSpace.Normal;
            var input = field.Q<VisualElement>(className: BaseField<TValue>.inputUssClassName);
            if (input != null)
            {
                input.style.flexGrow = 1;
                input.style.flexShrink = 1;
                input.style.flexBasis = 0;
                input.style.minWidth = 0;
            }

            return field;
        }

        /// <summary>
        /// 创建 Text Field。
        /// </summary>
        /// <param name="label">label 参数。</param>
        /// <returns>执行结果。</returns>
        private static TextField CreateTextField(string label)
        {
            return (TextField)ConfigureField(new TextField(label));
        }

        /// <summary>
        /// 创建 Dropdown Field。
        /// </summary>
        /// <param name="label">label 参数。</param>
        /// <returns>执行结果。</returns>
        private static DropdownField CreateDropdownField(string label)
        {
            return (DropdownField)ConfigureField(new DropdownField(label));
        }

        /// <summary>
        /// 创建 Toggle Field。
        /// </summary>
        /// <param name="label">label 参数。</param>
        /// <returns>执行结果。</returns>
        private static Toggle CreateToggleField(string label)
        {
            return (Toggle)ConfigureField(new Toggle(label));
        }

        /// <summary>
        /// 创建 Section Header。
        /// </summary>
        /// <param name="text">text 参数。</param>
        /// <returns>执行结果。</returns>
        private static Label CreateSectionHeader(string text)
        {
            var header = new Label(text);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 14;
            header.style.marginBottom = 8;
            return header;
        }

        /// <summary>
        /// 创建 Field Header。
        /// </summary>
        /// <param name="text">text 参数。</param>
        /// <returns>执行结果。</returns>
        private static Label CreateFieldHeader(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 3;
            return label;
        }

        /// <summary>
        /// 创建 Button Row。
        /// </summary>
        /// <returns>执行结果。</returns>
        private static VisualElement CreateButtonRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.minWidth = 0;
            row.style.marginTop = 8;
            return row;
        }

        /// <summary>
        /// 添加 Row Button。
        /// </summary>
        /// <param name="row">row 参数。</param>
        /// <param name="button">button 参数。</param>
        private static void AddRowButton(VisualElement row, Button button)
        {
            button.style.marginRight = 6;
            button.style.marginBottom = 6;
            button.style.flexShrink = 0;
            row.Add(button);
        }

        /// <summary>
        /// 创建 Folder Select Row。
        /// </summary>
        /// <param name="field">field 参数。</param>
        /// <param name="button">button 参数。</param>
        /// <returns>执行结果。</returns>
        private static VisualElement CreateFolderSelectRow(TextField field, Button button)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexEnd;
            row.style.flexGrow = 1;
            row.style.flexShrink = 1;
            row.style.marginBottom = 6;
            row.style.minWidth = 0;

            field.isDelayed = true;
            field.isReadOnly = true;
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.style.flexBasis = 0;
            field.style.minWidth = 0;
            field.style.marginRight = 8;
            field.style.marginBottom = 0;
            row.Add(field);

            button.style.width = 74;
            button.style.minWidth = 74;
            button.style.maxWidth = 74;
            button.style.flexShrink = 0;
            row.Add(button);
            return row;
        }
    }
}
