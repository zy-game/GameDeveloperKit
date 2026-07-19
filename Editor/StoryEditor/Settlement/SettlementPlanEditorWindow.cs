using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Settlement;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.StoryEditor.Settlement
{
    internal sealed class SettlementPlanEditorWindow : EditorWindow
    {
        private readonly List<OperationEditor> m_Operations = new List<OperationEditor>();
        private readonly SettlementDefinitionCatalog m_Catalog = SettlementDefinitionCatalog.Shared;
        private Action<string> m_Confirmed;
        private string m_SettlementId = string.Empty;
        private ScrollView m_List;
        private Label m_Error;

        public static void Open(string currentValue, Action<string> confirmed)
        {
            var window = CreateInstance<SettlementPlanEditorWindow>();
            window.titleContent = new GUIContent("编辑剧情段结算");
            window.minSize = new Vector2(720f, 520f);
            window.m_Confirmed = confirmed;
            if (SettlementPlanCodec.TryDeserialize(currentValue, out var plan, out _))
            {
                window.m_SettlementId = plan.SettlementId;
                for (var i = 0; i < plan.Operations.Count; i++)
                {
                    window.m_Operations.Add(OperationEditor.From(plan.Operations[i]));
                }
            }

            window.BuildUi();
            window.ShowAuxWindow();
        }

        private void BuildUi()
        {
            m_Error = new Label();
            rootVisualElement.Add(m_Error);
            var settlementId = new TextField("结算 ID") { value = m_SettlementId };
            settlementId.RegisterValueChangedCallback(evt => m_SettlementId = evt.newValue);
            rootVisualElement.Add(settlementId);
            m_List = new ScrollView { style = { flexGrow = 1f } };
            rootVisualElement.Add(m_List);
            var footer = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            footer.Add(new Button(() =>
            {
                m_Operations.Add(new OperationEditor());
                Refresh();
            }) { text = "添加操作" });
            footer.Add(new Button(Close) { text = "取消" });
            footer.Add(new Button(Save) { text = "保存计划" });
            rootVisualElement.Add(footer);
            Refresh();
        }

        private void Refresh()
        {
            m_List.Clear();
            for (var i = 0; i < m_Operations.Count; i++)
            {
                var index = i;
                var editor = m_Operations[i];
                var box = new Box();
                var operationId = new TextField("操作 ID") { value = editor.OperationId };
                operationId.RegisterValueChangedCallback(evt => editor.OperationId = evt.newValue);
                box.Add(operationId);
                var kinds = new List<string>();
                for (var definitionIndex = 0; definitionIndex < m_Catalog.Definitions.Count; definitionIndex++)
                {
                    kinds.Add(m_Catalog.Definitions[definitionIndex].Kind);
                }

                if (kinds.Count == 0)
                {
                    box.Add(new HelpBox("没有可用的业务结算定义。", HelpBoxMessageType.Error));
                }
                else
                {
                    var selected = Math.Max(0, kinds.IndexOf(editor.Kind));
                    editor.Kind = kinds[selected];
                    var kind = new PopupField<string>("业务 Kind", kinds, selected);
                    kind.RegisterValueChangedCallback(evt =>
                    {
                        editor.Kind = evt.newValue;
                        editor.Arguments.Clear();
                        Refresh();
                    });
                    box.Add(kind);
                    if (m_Catalog.TryGet(editor.Kind, out var definition))
                    {
                        for (var argumentIndex = 0; argumentIndex < definition.Arguments.Count; argumentIndex++)
                        {
                            AddArgumentField(box, editor, definition.Arguments[argumentIndex]);
                        }
                    }
                }
                box.Add(new Button(() =>
                {
                    m_Operations.RemoveAt(index);
                    Refresh();
                }) { text = "删除" });
                m_List.Add(box);
            }
        }

        private static void AddArgumentField(
            VisualElement parent,
            OperationEditor editor,
            SettlementArgumentDefinition definition)
        {
            editor.Arguments.TryGetValue(definition.Key, out var current);
            switch (definition.ValueType)
            {
                case GameDeveloperKit.Story.Authoring.ParameterValueType.Boolean:
                    var toggle = new Toggle(definition.Label) { value = current.Kind == ValueKind.Boolean && current.BooleanValue };
                    toggle.RegisterValueChangedCallback(evt => editor.Arguments[definition.Key] = Value.FromBoolean(evt.newValue));
                    parent.Add(toggle);
                    editor.Arguments[definition.Key] = Value.FromBoolean(toggle.value);
                    break;
                case GameDeveloperKit.Story.Authoring.ParameterValueType.Number:
                    var number = new DoubleField(definition.Label) { value = current.Kind == ValueKind.Number ? current.NumberValue : 0d };
                    number.RegisterValueChangedCallback(evt => editor.Arguments[definition.Key] = Value.FromNumber(evt.newValue));
                    parent.Add(number);
                    editor.Arguments[definition.Key] = Value.FromNumber(number.value);
                    break;
                case GameDeveloperKit.Story.Authoring.ParameterValueType.Option:
                    var options = new List<string>(definition.Options);
                    if (options.Count == 0)
                    {
                        parent.Add(new HelpBox($"{definition.Label} 没有选项。", HelpBoxMessageType.Error));
                        break;
                    }

                    var selected = Math.Max(0, options.IndexOf(current.StringValue));
                    var option = new PopupField<string>(definition.Label, options, selected);
                    option.RegisterValueChangedCallback(evt => editor.Arguments[definition.Key] = Value.FromString(evt.newValue));
                    parent.Add(option);
                    editor.Arguments[definition.Key] = Value.FromString(option.value);
                    break;
                default:
                    var text = new TextField(definition.Label) { value = current.StringValue ?? string.Empty };
                    text.RegisterValueChangedCallback(evt => editor.Arguments[definition.Key] = Value.FromString(evt.newValue));
                    parent.Add(text);
                    editor.Arguments[definition.Key] = Value.FromString(text.value);
                    break;
            }
        }

        private void Save()
        {
            try
            {
                var operations = new List<SettlementOperation>();
                for (var i = 0; i < m_Operations.Count; i++)
                {
                    operations.Add(m_Operations[i].Build());
                }

                var plan = new SettlementPlan(
                    m_SettlementId,
                    SettlementPlan.CurrentVersion,
                    operations);
                m_Confirmed?.Invoke(SettlementPlanCodec.Serialize(plan));
                Close();
            }
            catch (Exception exception)
            {
                m_Error.text = exception.Message;
            }
        }

        private sealed class OperationEditor
        {
            public string OperationId = string.Empty;
            public string Kind = string.Empty;
            public readonly Dictionary<string, Value> Arguments = new Dictionary<string, Value>(StringComparer.Ordinal);

            public SettlementOperation Build()
            {
                return new SettlementOperation(OperationId, Kind, new ArgumentBag(Arguments));
            }

            public static OperationEditor From(SettlementOperation operation)
            {
                var editor = new OperationEditor
                {
                    OperationId = operation.OperationId,
                    Kind = operation.Kind
                };
                foreach (var pair in operation.Arguments.Values)
                {
                    editor.Arguments.Add(pair.Key, pair.Value);
                }

                return editor;
            }
        }
    }
}
