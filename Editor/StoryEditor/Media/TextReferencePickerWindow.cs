using System;
using GameDeveloperKit.LocalizationEditor;
using GameDeveloperKit.Story.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.StoryEditor.Media
{
    internal sealed class TextReferencePickerWindow : EditorWindow
    {
        private Action<string> m_Confirmed;
        private LocalizationPickerView m_Picker;
        private Button m_LiteralButton;
        private Button m_KeyButton;

        public static void Open(string currentValue, Action<string> confirmed)
        {
            var window = CreateInstance<TextReferencePickerWindow>();
            window.titleContent = new GUIContent("编辑剧情文本");
            window.minSize = new Vector2(680f, 500f);
            window.m_Confirmed = confirmed;
            window.BuildUi(currentValue);
            window.ShowAuxWindow();
        }

        private void BuildUi(string currentValue)
        {
            rootVisualElement.Clear();
            TextReferenceCodec.TryDeserialize(currentValue, out var current, out _, out _);
            rootVisualElement.style.paddingLeft = 12;
            rootVisualElement.style.paddingRight = 12;
            rootVisualElement.style.paddingTop = 12;
            rootVisualElement.style.paddingBottom = 12;

            m_Picker = new LocalizationPickerView(
                new LocalizationPickerRequest(
                    current.Mode == TextMode.LocalizationKey ? current.Value : string.Empty,
                    allowCreate: true,
                    initialQuery: current.Value),
                ConfirmKey);
            m_Picker.SelectionChanged += _ => RefreshActions();
            rootVisualElement.Add(m_Picker);

            var footer = new VisualElement { name = "story-text-picker-footer" };
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.FlexEnd;
            footer.style.marginTop = 8;
            footer.Add(new Button(Close) { text = "取消" });
            m_LiteralButton = new Button(ConfirmLiteral)
            {
                name = "story-text-save-literal",
                text = "保存为直接文本"
            };
            footer.Add(m_LiteralButton);
            m_KeyButton = new Button(() => m_Picker.ConfirmSelected())
            {
                name = "story-text-save-key",
                text = "保存为多语言 Key"
            };
            footer.Add(m_KeyButton);
            rootVisualElement.Add(footer);
            RefreshActions();
        }

        private void RefreshActions()
        {
            m_LiteralButton?.SetEnabled(string.IsNullOrWhiteSpace(m_Picker?.CurrentInput) is false);
            m_KeyButton?.SetEnabled(m_Picker?.CanConfirm == true);
        }

        private void ConfirmLiteral()
        {
            var value = m_Picker?.CurrentInput;
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            m_Confirmed?.Invoke(TextReferenceCodec.Serialize(new TextReference(TextMode.Literal, value)));
            Close();
        }

        private void ConfirmKey(LocalizationSelection selection)
        {
            m_Confirmed?.Invoke(TextReferenceCodec.Serialize(
                new TextReference(TextMode.LocalizationKey, selection.Key)));
            Close();
        }
    }
}
