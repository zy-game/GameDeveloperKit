using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using GameDeveloperKit.Combat;
using System.Collections.Generic;

namespace GameDeveloperKit.Editor.Combat
{
    [CustomPropertyDrawer(typeof(EffectModifierDef))]
    public class EffectModifierDefDrawer : PropertyDrawer
    {
        private static readonly List<string> OperationChoices = new()
        {
            "加法 (+)",
            "百分比加成 (%+)",
            "乘法 (×)",
            "覆盖 (=)"
        };

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.AddToClassList("modifier-item");
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            var attrNameProp = property.FindPropertyRelative("AttributeName");
            var operationProp = property.FindPropertyRelative("Operation");
            var valueProp = property.FindPropertyRelative("Value");
            var priorityProp = property.FindPropertyRelative("Priority");

            // Attribute Name Button (click to select)
            var attrBtn = new Button(() =>
            {
                AttributeSelectorPopup.Show(attrNameProp.stringValue, selectedAttr =>
                {
                    attrNameProp.stringValue = selectedAttr;
                    property.serializedObject.ApplyModifiedProperties();
                    SaveAsset(property.serializedObject);
                });
            });
            attrBtn.text = string.IsNullOrEmpty(attrNameProp.stringValue) ? "(选择属性)" : attrNameProp.stringValue;
            attrBtn.AddToClassList("btn");
            attrBtn.AddToClassList("btn-sm");
            attrBtn.AddToClassList("btn-secondary");
            attrBtn.style.flexGrow = 1;
            attrBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
            
            // Track property changes
            attrBtn.TrackPropertyValue(attrNameProp, p =>
            {
                attrBtn.text = string.IsNullOrEmpty(p.stringValue) ? "(选择属性)" : p.stringValue;
            });
            container.Add(attrBtn);

            // Operation Dropdown
            var opDropdown = new DropdownField();
            opDropdown.choices = OperationChoices;
            opDropdown.index = operationProp.enumValueIndex;
            opDropdown.style.width = 110;
            opDropdown.style.marginLeft = 4;
            opDropdown.AddToClassList("custom-dropdown");
            opDropdown.RegisterValueChangedCallback(evt =>
            {
                var index = OperationChoices.IndexOf(evt.newValue);
                operationProp.enumValueIndex = index;
                property.serializedObject.ApplyModifiedProperties();
                SaveAsset(property.serializedObject);
            });
            container.Add(opDropdown);

            // Value with label
            var valueLabel = new Label("值");
            valueLabel.style.marginLeft = 8;
            valueLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            valueLabel.style.fontSize = 11;
            container.Add(valueLabel);

            var valueField = new FloatField();
            valueField.value = valueProp.floatValue;
            valueField.style.width = 50;
            valueField.style.marginLeft = 2;
            valueField.RegisterValueChangedCallback(evt =>
            {
                valueProp.floatValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                SaveAsset(property.serializedObject);
            });
            container.Add(valueField);

            // Priority with label
            var priorityLabel = new Label("优先级");
            priorityLabel.style.marginLeft = 8;
            priorityLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            priorityLabel.style.fontSize = 11;
            container.Add(priorityLabel);

            var priorityField = new IntegerField();
            priorityField.value = priorityProp.intValue;
            priorityField.style.width = 35;
            priorityField.style.marginLeft = 2;
            priorityField.RegisterValueChangedCallback(evt =>
            {
                priorityProp.intValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                SaveAsset(property.serializedObject);
            });
            container.Add(priorityField);

            return container;
        }

        private static void SaveAsset(SerializedObject serializedObject)
        {
            if (serializedObject.targetObject != null)
            {
                EditorUtility.SetDirty(serializedObject.targetObject);
                AssetDatabase.SaveAssetIfDirty(serializedObject.targetObject);
            }
        }
    }

    [CustomPropertyDrawer(typeof(AbilityCost))]
    public class AbilityCostDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.AddToClassList("cost-item");
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            var attrNameProp = property.FindPropertyRelative("AttributeName");
            var costProp = property.FindPropertyRelative("Cost");

            // Attribute Name Button (click to select)
            var attrBtn = new Button(() =>
            {
                AttributeSelectorPopup.Show(attrNameProp.stringValue, selectedAttr =>
                {
                    attrNameProp.stringValue = selectedAttr;
                    property.serializedObject.ApplyModifiedProperties();
                    SaveAsset(property.serializedObject);
                });
            });
            attrBtn.text = string.IsNullOrEmpty(attrNameProp.stringValue) ? "(选择属性)" : attrNameProp.stringValue;
            attrBtn.AddToClassList("btn");
            attrBtn.AddToClassList("btn-sm");
            attrBtn.AddToClassList("btn-secondary");
            attrBtn.style.flexGrow = 1;
            attrBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
            
            // Track property changes
            attrBtn.TrackPropertyValue(attrNameProp, p =>
            {
                attrBtn.text = string.IsNullOrEmpty(p.stringValue) ? "(选择属性)" : p.stringValue;
            });
            container.Add(attrBtn);

            // Cost Value with label
            var costLabel = new Label("消耗");
            costLabel.style.marginLeft = 8;
            costLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            costLabel.style.fontSize = 11;
            container.Add(costLabel);

            var costField = new FloatField();
            costField.value = costProp.floatValue;
            costField.style.width = 50;
            costField.style.marginLeft = 2;
            costField.RegisterValueChangedCallback(evt =>
            {
                costProp.floatValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                SaveAsset(property.serializedObject);
            });
            container.Add(costField);

            return container;
        }

        private static void SaveAsset(SerializedObject serializedObject)
        {
            if (serializedObject.targetObject != null)
            {
                EditorUtility.SetDirty(serializedObject.targetObject);
                AssetDatabase.SaveAssetIfDirty(serializedObject.targetObject);
            }
        }
    }
}
