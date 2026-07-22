using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.EditorConfiguration;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.LocalizationEditor
{
    public readonly struct LocalizationPickerRequest
    {
        public LocalizationPickerRequest(
            string currentKey,
            string previewLocale = null,
            bool allowCreate = true,
            string initialQuery = null)
        {
            CurrentKey = currentKey ?? string.Empty;
            PreviewLocale = previewLocale ?? string.Empty;
            AllowCreate = allowCreate;
            InitialQuery = initialQuery ?? CurrentKey;
        }

        public string CurrentKey { get; }

        public string PreviewLocale { get; }

        public bool AllowCreate { get; }

        public string InitialQuery { get; }
    }

    public readonly struct LocalizationSelection
    {
        public LocalizationSelection(long keyId, string key, string previewText, bool isMissing)
        {
            KeyId = keyId;
            Key = key ?? string.Empty;
            PreviewText = previewText ?? string.Empty;
            IsMissing = isMissing;
        }

        public long KeyId { get; }

        public string Key { get; }

        public string PreviewText { get; }

        public bool IsMissing { get; }
    }

    public sealed class LocalizationPickerWindow : EditorWindow
    {
        private Action<LocalizationSelection> m_Selected;
        private LocalizationPickerView m_View;
        private Button m_ConfirmButton;

        public static void Open(LocalizationPickerRequest request, Action<LocalizationSelection> selected)
        {
            var window = CreateInstance<LocalizationPickerWindow>();
            window.titleContent = new GUIContent("选择多语言 Key");
            window.minSize = new Vector2(680f, 480f);
            window.m_Selected = selected;
            window.Build(request);
            window.ShowAuxWindow();
        }

        internal void Build(LocalizationPickerRequest request)
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 12;
            rootVisualElement.style.paddingRight = 12;
            rootVisualElement.style.paddingTop = 12;
            rootVisualElement.style.paddingBottom = 12;
            m_View = new LocalizationPickerView(request, Confirm);
            m_View.SelectionChanged += _ => RefreshActions();
            rootVisualElement.Add(m_View);

            var footer = new VisualElement { name = "localization-picker-footer" };
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.FlexEnd;
            footer.style.marginTop = 8;
            footer.Add(new Button(Close) { text = "取消" });
            m_ConfirmButton = new Button(() => m_View.ConfirmSelected())
            {
                name = "localization-picker-confirm",
                text = "选择 Key"
            };
            footer.Add(m_ConfirmButton);
            rootVisualElement.Add(footer);
            RefreshActions();
        }

        private void RefreshActions()
        {
            m_ConfirmButton?.SetEnabled(m_View?.CanConfirm == true);
        }

        private void Confirm(LocalizationSelection selection)
        {
            m_Selected?.Invoke(selection);
            Close();
        }
    }

    internal sealed class LocalizationPickerView : VisualElement
    {
        private readonly LocalizationPickerRequest m_Request;
        private readonly ILocalizationEditorCatalog m_Catalog;
        private readonly ILocalizationAuthoringService m_Authoring;
        private readonly Action<LocalizationSelection> m_Confirmed;
        private readonly string m_PreviewLocale;
        private readonly Dictionary<long, VisualElement> m_ResultRows =
            new Dictionary<long, VisualElement>();

        private LocalizationCatalogSnapshot m_Snapshot;
        private LocalizationSelection? m_Selected;
        private TextField m_Search;
        private ScrollView m_Results;
        private Label m_Status;
        private VisualElement m_CreateArea;
        private TextField m_CreateKey;
        private TextField m_CreateText;
        private Button m_CreateButton;

        public LocalizationPickerView(
            LocalizationPickerRequest request,
            Action<LocalizationSelection> confirmed,
            ILocalizationEditorCatalog catalog = null,
            ILocalizationAuthoringService authoring = null)
        {
            m_Request = request;
            m_Catalog = catalog ?? LocalizationEditorCatalog.Shared;
            m_Authoring = authoring ?? LocalizationAuthoringService.Shared;
            m_Confirmed = confirmed;
            var configuredPreview = string.IsNullOrWhiteSpace(request.PreviewLocale)
                ? EditorGlobalConfig.LoadOrCreate().Localization.PreviewLocale
                : string.Empty;
            m_PreviewLocale = string.IsNullOrWhiteSpace(request.PreviewLocale)
                ? configuredPreview
                : request.PreviewLocale.Trim();
            name = "localization-picker-view";
            style.flexGrow = 1;
            style.minHeight = 0;
            Build();
        }

        public event Action<LocalizationSelection?> SelectionChanged;

        public string CurrentInput => m_Search?.value ?? string.Empty;

        public bool CanConfirm => m_Selected.HasValue;

        public void ConfirmSelected()
        {
            if (m_Selected.HasValue)
            {
                m_Confirmed?.Invoke(m_Selected.Value);
            }
        }

        private void Build()
        {
            m_Search = new TextField
            {
                name = "localization-picker-search",
                value = m_Request.InitialQuery ?? string.Empty
            };
            m_Search.style.marginBottom = 8;
            m_Search.RegisterValueChangedCallback(evt =>
            {
                m_Selected = null;
                PrefillCreate(evt.newValue);
                RefreshResults();
                SelectionChanged?.Invoke(m_Selected);
            });
            Add(m_Search);

            m_Status = new Label { name = "localization-picker-status" };
            m_Status.style.whiteSpace = WhiteSpace.Normal;
            m_Status.style.marginBottom = 8;
            Add(m_Status);

            m_Results = new ScrollView(ScrollViewMode.Vertical) { name = "localization-picker-results" };
            m_Results.style.flexGrow = 1;
            m_Results.style.minHeight = 180;
            m_Results.style.borderTopWidth = 1;
            m_Results.style.borderBottomWidth = 1;
            m_Results.style.borderTopColor = DividerColor();
            m_Results.style.borderBottomColor = DividerColor();
            Add(m_Results);

            m_CreateArea = new VisualElement { name = "localization-picker-create" };
            m_CreateArea.style.flexDirection = FlexDirection.Row;
            m_CreateArea.style.alignItems = Align.FlexEnd;
            m_CreateArea.style.marginTop = 10;
            m_CreateKey = CreateField("localization-picker-create-key", "新 Key");
            m_CreateKey.RegisterValueChangedCallback(_ => RefreshCreateAction());
            m_CreateArea.Add(m_CreateKey);
            m_CreateText = CreateField("localization-picker-create-text", "预览文本");
            m_CreateText.RegisterValueChangedCallback(_ => RefreshCreateAction());
            m_CreateArea.Add(m_CreateText);
            m_CreateButton = new Button(CreateKey)
            {
                name = "localization-picker-create-button",
                text = "新增并选择"
            };
            m_CreateButton.style.height = 22;
            m_CreateButton.style.marginLeft = 6;
            m_CreateArea.Add(m_CreateButton);
            Add(m_CreateArea);

            m_Snapshot = m_Catalog.Refresh();
            PrefillCreate(m_Search.value);
            RefreshResults();
            m_Search.Focus();
        }

        private void RefreshResults()
        {
            m_Snapshot = m_Catalog.Refresh();
            m_Results.Clear();
            m_ResultRows.Clear();
            var query = m_Search.value?.Trim() ?? string.Empty;
            var matches = m_Catalog.Search(query, m_PreviewLocale);
            foreach (var match in matches)
            {
                var selection = new LocalizationSelection(
                    match.KeyId,
                    match.Key,
                    match.Text,
                    match.IsMissing);
                var row = CreateResultRow(selection);
                m_ResultRows.Add(selection.KeyId, row);
                m_Results.Add(row);
                if (string.Equals(match.Key, query, StringComparison.Ordinal))
                {
                    SetSelection(selection, false);
                }
            }

            var errors = m_Snapshot.Diagnostics
                .Where(diagnostic => diagnostic.Severity == LocalizationCatalogDiagnosticSeverity.Error)
                .Select(diagnostic => diagnostic.Message)
                .ToArray();
            if (errors.Length > 0)
            {
                m_Status.text = string.Join(Environment.NewLine, errors);
                m_Status.style.color = new Color(0.95f, 0.35f, 0.3f);
            }
            else
            {
                m_Status.text = $"{m_PreviewLocale} · {matches.Count} / {m_Snapshot.Entries.Count}";
                m_Status.style.color = SecondaryTextColor();
            }

            var hasExact = m_Snapshot.Entries.ContainsKey(query);
            m_CreateArea.style.display = m_Request.AllowCreate && hasExact is false
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            RefreshCreateAction();
            SelectionChanged?.Invoke(m_Selected);
        }

        private VisualElement CreateResultRow(LocalizationSelection selection)
        {
            var row = new VisualElement { name = $"localization-picker-result-{selection.KeyId}" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 48;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.paddingTop = 5;
            row.style.paddingBottom = 5;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = DividerColor();
            row.focusable = true;

            var key = new Label(selection.Key);
            key.style.width = Length.Percent(42);
            key.style.unityFontStyleAndWeight = FontStyle.Bold;
            key.pickingMode = PickingMode.Ignore;
            row.Add(key);

            var text = new Label(selection.IsMissing ? "缺翻译" : selection.PreviewText);
            text.style.flexGrow = 1;
            text.style.whiteSpace = WhiteSpace.Normal;
            if (selection.IsMissing)
            {
                text.style.color = new Color(0.95f, 0.55f, 0.2f);
            }
            text.pickingMode = PickingMode.Ignore;
            row.Add(text);

            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                SetSelection(selection, true);
                if (evt.clickCount >= 2)
                {
                    m_Confirmed?.Invoke(selection);
                }
            });
            return row;
        }

        private void SetSelection(LocalizationSelection selection, bool notify)
        {
            m_Selected = selection;
            foreach (var pair in m_ResultRows)
            {
                pair.Value.style.backgroundColor = pair.Key == selection.KeyId
                    ? SelectionColor()
                    : Color.clear;
            }

            if (notify)
            {
                SelectionChanged?.Invoke(m_Selected);
            }
        }

        private void CreateKey()
        {
            var result = m_Authoring.CreateKey(m_CreateKey.value, m_PreviewLocale, m_CreateText.value);
            if (result.Succeeded is false)
            {
                m_Status.text = result.Message;
                m_Status.style.color = new Color(0.95f, 0.35f, 0.3f);
                return;
            }

            m_Snapshot = m_Catalog.Refresh();
            var key = m_CreateKey.value.Trim();
            var match = m_Catalog.Search(key, m_PreviewLocale, 1)
                .FirstOrDefault(candidate => string.Equals(candidate.Key, key, StringComparison.Ordinal));
            if (match == null)
            {
                m_Status.text = "Key 已创建，但刷新后未能在 Catalog 中定位。";
                m_Status.style.color = new Color(0.95f, 0.35f, 0.3f);
                return;
            }

            var selection = new LocalizationSelection(match.KeyId, match.Key, match.Text, match.IsMissing);
            m_Selected = selection;
            m_Confirmed?.Invoke(selection);
        }

        private void PrefillCreate(string input)
        {
            input = input?.Trim() ?? string.Empty;
            if (LooksLikeKey(input))
            {
                m_CreateKey?.SetValueWithoutNotify(input);
                m_CreateText?.SetValueWithoutNotify(string.Empty);
            }
            else
            {
                m_CreateKey?.SetValueWithoutNotify(string.Empty);
                m_CreateText?.SetValueWithoutNotify(input);
            }
        }

        private void RefreshCreateAction()
        {
            if (m_CreateButton == null)
            {
                return;
            }

            var key = m_CreateKey.value?.Trim() ?? string.Empty;
            var text = m_CreateText.value ?? string.Empty;
            m_CreateButton.SetEnabled(
                m_Request.AllowCreate &&
                m_CreateArea.style.display.value != DisplayStyle.None &&
                key.Length > 0 &&
                text.Length > 0 &&
                string.IsNullOrWhiteSpace(m_PreviewLocale) is false &&
                m_Snapshot.Diagnostics.All(diagnostic =>
                    diagnostic.Severity != LocalizationCatalogDiagnosticSeverity.Error));
        }

        private static TextField CreateField(string name, string label)
        {
            var field = new TextField(label) { name = name, isDelayed = true };
            field.style.flexGrow = 1;
            field.style.minWidth = 180;
            field.labelElement.style.width = 70;
            return field;
        }

        private static bool LooksLikeKey(string value)
        {
            if (value.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character > 127 ||
                    (char.IsLetterOrDigit(character) is false && character != '.' && character != '_' && character != '-'))
                {
                    return false;
                }
            }

            return true;
        }

        private static Color DividerColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.27f, 0.28f, 0.3f)
                : new Color(0.76f, 0.77f, 0.79f);
        }

        private static Color SecondaryTextColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.68f, 0.69f, 0.71f)
                : new Color(0.35f, 0.36f, 0.38f);
        }

        private static Color SelectionColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.18f, 0.38f, 0.62f, 0.75f)
                : new Color(0.25f, 0.5f, 0.85f, 0.35f);
        }
    }
}
