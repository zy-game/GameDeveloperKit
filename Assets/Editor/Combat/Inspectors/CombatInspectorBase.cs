using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using GameDeveloperKit.Combat;

namespace GameDeveloperKit.Editor.Combat
{
    /// <summary>
    /// 战斗系统Inspector基类
    /// 提供通用的UI渲染方法，子类可以调用base.OnDraw()来绘制父类内容
    /// </summary>
    public abstract class CombatInspectorBase
    {
        protected ScriptableObject Target { get; private set; }
        protected SerializedObject SerializedObject { get; private set; }
        protected VisualElement RootElement { get; private set; }

        /// <summary>
        /// 已绘制的属性名称集合，用于扩展字段检测
        /// </summary>
        protected HashSet<string> DrawnProperties { get; } = new();

        /// <summary>
        /// 初始化Inspector
        /// </summary>
        public void Initialize(ScriptableObject target, VisualElement root)
        {
            Target = target;
            SerializedObject = new SerializedObject(target);
            RootElement = root;
            DrawnProperties.Clear();
        }

        /// <summary>
        /// 绘制Inspector内容
        /// 子类重写此方法，可以调用base.OnDraw()来绘制父类内容
        /// </summary>
        public virtual void OnDraw()
        {
            // 基类默认不绘制任何内容
        }

        /// <summary>
        /// 绘制未被显式绘制的扩展字段
        /// 通常在OnDraw()末尾调用
        /// </summary>
        protected void DrawExtensionFields(string sectionTitle = "扩展字段")
        {
            var extensionFields = new List<SerializedProperty>();
            var prop = SerializedObject.GetIterator();
            prop.Next(true);

            while (prop.NextVisible(false))
            {
                if (!DrawnProperties.Contains(prop.name) && prop.name != "m_Script")
                {
                    extensionFields.Add(prop.Copy());
                }
            }

            if (extensionFields.Count > 0)
            {
                var section = CreateSection(sectionTitle);
                AddHelpBox(section, "以下是扩展类型定义的额外字段。", HelpBoxType.Info);

                foreach (var field in extensionFields)
                {
                    var fieldElement = new PropertyField(field);
                    fieldElement.Bind(SerializedObject);
                    fieldElement.RegisterValueChangeCallback(evt => SaveChanges());
                    section.Add(fieldElement);
                }

                RootElement.Add(section);
            }
        }

        #region UI Helper Methods

        protected enum HelpBoxType
        {
            Info,
            Warning,
            Error
        }

        protected VisualElement CreateDetailHeader(string title, string subtitle, Color accentColor)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.paddingTop = 12;
            header.style.paddingBottom = 12;
            header.style.backgroundColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.1f);
            header.style.borderLeftWidth = 3;
            header.style.borderLeftColor = accentColor;
            header.style.borderTopLeftRadius = 8;
            header.style.borderTopRightRadius = 8;
            header.style.borderBottomLeftRadius = 8;
            header.style.borderBottomRightRadius = 8;
            header.style.marginBottom = 12;

            var titleContainer = new VisualElement();

            var titleLabel = new Label(string.IsNullOrEmpty(title) ? "(未命名)" : title);
            titleLabel.style.fontSize = 16;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.86f, 0.86f, 0.86f);
            titleContainer.Add(titleLabel);

            var subtitleLabel = new Label(subtitle);
            subtitleLabel.style.fontSize = 11;
            subtitleLabel.style.color = accentColor;
            subtitleLabel.style.marginTop = 2;
            titleContainer.Add(subtitleLabel);

            header.Add(titleContainer);
            return header;
        }

        protected VisualElement CreateSection(string title)
        {
            var section = new VisualElement();
            section.AddToClassList("combat-section");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("combat-section-title");
            section.Add(titleLabel);

            return section;
        }

        protected void AddHelpBox(VisualElement parent, string message, HelpBoxType type)
        {
            var helpBox = new VisualElement();
            helpBox.style.flexDirection = FlexDirection.Row;
            helpBox.style.paddingLeft = 10;
            helpBox.style.paddingRight = 10;
            helpBox.style.paddingTop = 8;
            helpBox.style.paddingBottom = 8;
            helpBox.style.marginBottom = 8;
            helpBox.style.borderTopLeftRadius = 6;
            helpBox.style.borderTopRightRadius = 6;
            helpBox.style.borderBottomLeftRadius = 6;
            helpBox.style.borderBottomRightRadius = 6;
            helpBox.style.borderLeftWidth = 3;

            Color bgColor, borderColor, textColor;
            switch (type)
            {
                case HelpBoxType.Warning:
                    bgColor = new Color(0.96f, 0.62f, 0.04f, 0.1f);
                    borderColor = new Color(0.96f, 0.62f, 0.04f);
                    textColor = new Color(0.99f, 0.90f, 0.54f);
                    break;
                case HelpBoxType.Error:
                    bgColor = new Color(0.94f, 0.27f, 0.27f, 0.1f);
                    borderColor = new Color(0.94f, 0.27f, 0.27f);
                    textColor = new Color(0.99f, 0.65f, 0.65f);
                    break;
                default:
                    bgColor = new Color(0.23f, 0.51f, 0.96f, 0.1f);
                    borderColor = new Color(0.23f, 0.51f, 0.96f);
                    textColor = new Color(0.58f, 0.77f, 0.99f);
                    break;
            }

            helpBox.style.backgroundColor = bgColor;
            helpBox.style.borderLeftColor = borderColor;

            var label = new Label(message);
            label.style.color = textColor;
            label.style.fontSize = 11;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexGrow = 1;
            helpBox.Add(label);

            parent.Add(helpBox);
        }

        protected void AddPropertyField(VisualElement parent, string propertyName, string label = null)
        {
            var prop = SerializedObject.FindProperty(propertyName);
            if (prop == null) return;

            DrawnProperties.Add(propertyName);

            var field = new PropertyField(prop, label ?? propertyName);
            field.Bind(SerializedObject);
            field.AddToClassList("custom-textfield");
            field.RegisterValueChangeCallback(evt => SaveChanges());
            parent.Add(field);
        }

        protected void AddArrayField(VisualElement parent, string propertyName, string label = null)
        {
            var prop = SerializedObject.FindProperty(propertyName);
            if (prop == null) return;

            DrawnProperties.Add(propertyName);

            var field = new PropertyField(prop, label ?? propertyName);
            field.Bind(SerializedObject);
            field.RegisterValueChangeCallback(evt => SaveChanges());
            parent.Add(field);
        }

        protected void AddEnumField<T>(VisualElement parent, string propertyName, string label, Func<T, string> displayNameGetter = null) where T : Enum
        {
            var prop = SerializedObject.FindProperty(propertyName);
            if (prop == null) return;

            DrawnProperties.Add(propertyName);

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginBottom = 4;

            var labelElement = new Label(label);
            labelElement.style.minWidth = 120;
            labelElement.style.color = new Color(0.7f, 0.7f, 0.7f);
            container.Add(labelElement);

            var enumValues = Enum.GetValues(typeof(T)).Cast<T>().ToList();
            var choices = displayNameGetter != null
                ? enumValues.Select(displayNameGetter).ToList()
                : enumValues.Select(e => e.ToString()).ToList();

            var dropdown = new DropdownField();
            dropdown.style.flexGrow = 1;
            dropdown.AddToClassList("custom-dropdown");
            dropdown.choices = choices;
            dropdown.index = prop.enumValueIndex;

            dropdown.RegisterValueChangedCallback(evt =>
            {
                var index = choices.IndexOf(evt.newValue);
                prop.enumValueIndex = index;
                SaveChanges();
            });

            container.Add(dropdown);
            parent.Add(container);
        }

        protected void AddTagArrayField(VisualElement parent, string propertyName, string label)
        {
            var prop = SerializedObject.FindProperty(propertyName);
            if (prop == null) return;

            DrawnProperties.Add(propertyName);

            var container = new VisualElement();
            container.style.marginBottom = 8;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 4;

            var labelElement = new Label(label);
            labelElement.style.flexGrow = 1;
            labelElement.style.color = new Color(0.7f, 0.7f, 0.7f);
            labelElement.style.fontSize = 11;
            headerRow.Add(labelElement);

            var tagsContainer = new VisualElement();

            var addBtn = new Button(() =>
            {
                var existingTags = new List<string>();
                for (int i = 0; i < prop.arraySize; i++)
                {
                    existingTags.Add(prop.GetArrayElementAtIndex(i).stringValue);
                }

                TagSelectorPopup.Show("", selectedTag =>
                {
                    if (!string.IsNullOrEmpty(selectedTag) && !existingTags.Contains(selectedTag))
                    {
                        prop.arraySize++;
                        prop.GetArrayElementAtIndex(prop.arraySize - 1).stringValue = selectedTag;
                        SaveChanges();
                        RefreshTagList(tagsContainer, prop);
                    }
                }, existingTags);
            }) { text = "+" };
            addBtn.AddToClassList("btn");
            addBtn.AddToClassList("btn-sm");
            addBtn.AddToClassList("btn-primary");
            addBtn.style.width = 24;
            addBtn.style.height = 24;
            headerRow.Add(addBtn);

            container.Add(headerRow);
            tagsContainer.AddToClassList("tag-list");
            RefreshTagList(tagsContainer, prop);
            container.Add(tagsContainer);

            parent.Add(container);
        }

        private void RefreshTagList(VisualElement container, SerializedProperty prop)
        {
            container.Clear();

            if (prop.arraySize == 0)
            {
                var hint = new Label("(无)");
                hint.style.color = new Color(0.5f, 0.5f, 0.5f);
                hint.style.fontSize = 11;
                hint.style.unityFontStyleAndWeight = FontStyle.Italic;
                container.Add(hint);
                return;
            }

            for (int i = 0; i < prop.arraySize; i++)
            {
                var index = i;
                var tagValue = prop.GetArrayElementAtIndex(i).stringValue;

                var tagItem = new VisualElement();
                tagItem.AddToClassList("tag-item");
                tagItem.style.flexDirection = FlexDirection.Row;
                tagItem.style.alignItems = Align.Center;

                var tagText = new Label(tagValue);
                tagText.AddToClassList("tag-item-text");
                tagItem.Add(tagText);

                var removeBtn = new Button(() =>
                {
                    prop.DeleteArrayElementAtIndex(index);
                    SaveChanges();
                    RefreshTagList(container, prop);
                }) { text = "×" };
                removeBtn.style.width = 16;
                removeBtn.style.height = 16;
                removeBtn.style.marginLeft = 4;
                removeBtn.style.paddingLeft = 0;
                removeBtn.style.paddingRight = 0;
                removeBtn.style.paddingTop = 0;
                removeBtn.style.paddingBottom = 0;
                removeBtn.style.fontSize = 10;
                removeBtn.style.backgroundColor = Color.clear;
                removeBtn.style.borderLeftWidth = 0;
                removeBtn.style.borderRightWidth = 0;
                removeBtn.style.borderTopWidth = 0;
                removeBtn.style.borderBottomWidth = 0;
                removeBtn.style.color = new Color(0.8f, 0.8f, 0.8f);
                tagItem.Add(removeBtn);

                container.Add(tagItem);
            }
        }

        protected void AddEffectArrayField(VisualElement parent, string propertyName, string label)
        {
            var prop = SerializedObject.FindProperty(propertyName);
            if (prop == null) return;

            DrawnProperties.Add(propertyName);

            var container = new VisualElement();
            container.style.marginBottom = 8;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 4;

            var labelElement = new Label(label);
            labelElement.style.flexGrow = 1;
            labelElement.style.color = new Color(0.7f, 0.7f, 0.7f);
            labelElement.style.fontSize = 11;
            headerRow.Add(labelElement);

            var effectsContainer = new VisualElement();

            var addBtn = new Button(() =>
            {
                var existingEffects = new List<GameplayEffect>();
                for (int i = 0; i < prop.arraySize; i++)
                {
                    var effect = prop.GetArrayElementAtIndex(i).objectReferenceValue as GameplayEffect;
                    if (effect != null)
                        existingEffects.Add(effect);
                }

                EffectSelectorPopup.Show(null, selectedEffect =>
                {
                    if (selectedEffect != null && !existingEffects.Contains(selectedEffect))
                    {
                        prop.arraySize++;
                        prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = selectedEffect;
                        SaveChanges();
                        RefreshEffectList(effectsContainer, prop);
                    }
                }, existingEffects);
            }) { text = "+" };
            addBtn.AddToClassList("btn");
            addBtn.AddToClassList("btn-sm");
            addBtn.AddToClassList("btn-primary");
            addBtn.style.width = 24;
            addBtn.style.height = 20;
            headerRow.Add(addBtn);

            container.Add(headerRow);

            effectsContainer.style.backgroundColor = new Color(0, 0, 0, 0.15f);
            effectsContainer.style.borderTopLeftRadius = 4;
            effectsContainer.style.borderTopRightRadius = 4;
            effectsContainer.style.borderBottomLeftRadius = 4;
            effectsContainer.style.borderBottomRightRadius = 4;
            effectsContainer.style.paddingLeft = 6;
            effectsContainer.style.paddingRight = 6;
            effectsContainer.style.paddingTop = 6;
            effectsContainer.style.paddingBottom = 6;

            RefreshEffectList(effectsContainer, prop);
            container.Add(effectsContainer);
            parent.Add(container);
        }

        private void RefreshEffectList(VisualElement container, SerializedProperty prop)
        {
            container.Clear();

            if (prop.arraySize == 0)
            {
                var hint = new Label("(无效果)");
                hint.style.color = new Color(0.5f, 0.5f, 0.5f);
                hint.style.fontSize = 11;
                hint.style.unityFontStyleAndWeight = FontStyle.Italic;
                container.Add(hint);
                return;
            }

            for (int i = 0; i < prop.arraySize; i++)
            {
                var index = i;
                var effect = prop.GetArrayElementAtIndex(i).objectReferenceValue as GameplayEffect;

                var effectItem = new VisualElement();
                effectItem.style.flexDirection = FlexDirection.Row;
                effectItem.style.alignItems = Align.Center;
                effectItem.style.paddingLeft = 6;
                effectItem.style.paddingRight = 6;
                effectItem.style.paddingTop = 4;
                effectItem.style.paddingBottom = 4;
                effectItem.style.marginBottom = 2;
                effectItem.style.backgroundColor = new Color(0, 0, 0, 0.2f);
                effectItem.style.borderTopLeftRadius = 4;
                effectItem.style.borderTopRightRadius = 4;
                effectItem.style.borderBottomLeftRadius = 4;
                effectItem.style.borderBottomRightRadius = 4;

                var contentContainer = new VisualElement();
                contentContainer.style.flexGrow = 1;
                contentContainer.style.flexShrink = 1;
                contentContainer.style.overflow = Overflow.Hidden;

                if (effect != null)
                {
                    var nameLabel = new Label(string.IsNullOrEmpty(effect.EffectName) ? effect.name : effect.EffectName);
                    nameLabel.style.color = new Color(0.86f, 0.86f, 0.86f);
                    nameLabel.style.fontSize = 12;
                    contentContainer.Add(nameLabel);

                    if (!string.IsNullOrEmpty(effect.Description))
                    {
                        var descLabel = new Label(effect.Description);
                        descLabel.style.color = new Color(0.55f, 0.55f, 0.55f);
                        descLabel.style.fontSize = 10;
                        descLabel.style.whiteSpace = WhiteSpace.NoWrap;
                        descLabel.style.overflow = Overflow.Hidden;
                        descLabel.style.textOverflow = TextOverflow.Ellipsis;
                        contentContainer.Add(descLabel);
                    }
                }
                else
                {
                    var nullLabel = new Label("(空引用)");
                    nullLabel.style.color = new Color(0.8f, 0.4f, 0.4f);
                    nullLabel.style.fontSize = 11;
                    nullLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                    contentContainer.Add(nullLabel);
                }

                effectItem.Add(contentContainer);

                var removeBtn = new Button(() =>
                {
                    prop.DeleteArrayElementAtIndex(index);
                    if (prop.arraySize > index && prop.GetArrayElementAtIndex(index).objectReferenceValue != null)
                    {
                        prop.DeleteArrayElementAtIndex(index);
                    }
                    SaveChanges();
                    RefreshEffectList(container, prop);
                }) { text = "×" };
                removeBtn.style.width = 20;
                removeBtn.style.height = 20;
                removeBtn.style.marginLeft = 4;
                removeBtn.style.paddingLeft = 0;
                removeBtn.style.paddingRight = 0;
                removeBtn.style.paddingTop = 0;
                removeBtn.style.paddingBottom = 0;
                removeBtn.style.fontSize = 12;
                removeBtn.style.backgroundColor = Color.clear;
                removeBtn.style.borderLeftWidth = 0;
                removeBtn.style.borderRightWidth = 0;
                removeBtn.style.borderTopWidth = 0;
                removeBtn.style.borderBottomWidth = 0;
                removeBtn.style.color = new Color(0.7f, 0.7f, 0.7f);
                effectItem.Add(removeBtn);

                container.Add(effectItem);
            }
        }

        protected void AddCueArrayField(VisualElement parent, string propertyName, string label)
        {
            var prop = SerializedObject.FindProperty(propertyName);
            if (prop == null) return;

            DrawnProperties.Add(propertyName);

            var container = new VisualElement();
            container.style.marginBottom = 8;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 4;

            var labelElement = new Label(label);
            labelElement.style.flexGrow = 1;
            labelElement.style.color = new Color(0.7f, 0.7f, 0.7f);
            labelElement.style.fontSize = 11;
            headerRow.Add(labelElement);

            var cuesContainer = new VisualElement();

            var addBtn = new Button(() =>
            {
                var existingCues = new List<CueDefinition>();
                for (int i = 0; i < prop.arraySize; i++)
                {
                    var cue = prop.GetArrayElementAtIndex(i).objectReferenceValue as CueDefinition;
                    if (cue != null)
                        existingCues.Add(cue);
                }

                CueSelectorPopup.Show(null, selectedCue =>
                {
                    if (selectedCue != null && !existingCues.Contains(selectedCue))
                    {
                        prop.arraySize++;
                        prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = selectedCue;
                        SaveChanges();
                        RefreshCueList(cuesContainer, prop);
                    }
                }, existingCues);
            }) { text = "+" };
            addBtn.AddToClassList("btn");
            addBtn.AddToClassList("btn-sm");
            addBtn.AddToClassList("btn-primary");
            addBtn.style.width = 24;
            addBtn.style.height = 20;
            headerRow.Add(addBtn);

            container.Add(headerRow);

            cuesContainer.style.backgroundColor = new Color(0, 0, 0, 0.15f);
            cuesContainer.style.borderTopLeftRadius = 4;
            cuesContainer.style.borderTopRightRadius = 4;
            cuesContainer.style.borderBottomLeftRadius = 4;
            cuesContainer.style.borderBottomRightRadius = 4;
            cuesContainer.style.paddingLeft = 6;
            cuesContainer.style.paddingRight = 6;
            cuesContainer.style.paddingTop = 6;
            cuesContainer.style.paddingBottom = 6;

            RefreshCueList(cuesContainer, prop);
            container.Add(cuesContainer);
            parent.Add(container);
        }

        private void RefreshCueList(VisualElement container, SerializedProperty prop)
        {
            container.Clear();

            if (prop.arraySize == 0)
            {
                var hint = new Label("(无表现)");
                hint.style.color = new Color(0.5f, 0.5f, 0.5f);
                hint.style.fontSize = 11;
                hint.style.unityFontStyleAndWeight = FontStyle.Italic;
                container.Add(hint);
                return;
            }

            for (int i = 0; i < prop.arraySize; i++)
            {
                var index = i;
                var cue = prop.GetArrayElementAtIndex(i).objectReferenceValue as CueDefinition;

                var cueItem = new VisualElement();
                cueItem.style.flexDirection = FlexDirection.Row;
                cueItem.style.alignItems = Align.Center;
                cueItem.style.paddingLeft = 6;
                cueItem.style.paddingRight = 6;
                cueItem.style.paddingTop = 4;
                cueItem.style.paddingBottom = 4;
                cueItem.style.marginBottom = 2;
                cueItem.style.backgroundColor = new Color(0, 0, 0, 0.2f);
                cueItem.style.borderTopLeftRadius = 4;
                cueItem.style.borderTopRightRadius = 4;
                cueItem.style.borderBottomLeftRadius = 4;
                cueItem.style.borderBottomRightRadius = 4;

                var contentContainer = new VisualElement();
                contentContainer.style.flexGrow = 1;
                contentContainer.style.flexShrink = 1;
                contentContainer.style.overflow = Overflow.Hidden;

                if (cue != null)
                {
                    var nameLabel = new Label(string.IsNullOrEmpty(cue.CueName) ? cue.name : cue.CueName);
                    nameLabel.style.color = new Color(0.86f, 0.86f, 0.86f);
                    nameLabel.style.fontSize = 12;
                    contentContainer.Add(nameLabel);

                    if (!string.IsNullOrEmpty(cue.Description))
                    {
                        var descLabel = new Label(cue.Description);
                        descLabel.style.color = new Color(0.55f, 0.55f, 0.55f);
                        descLabel.style.fontSize = 10;
                        descLabel.style.whiteSpace = WhiteSpace.NoWrap;
                        descLabel.style.overflow = Overflow.Hidden;
                        descLabel.style.textOverflow = TextOverflow.Ellipsis;
                        contentContainer.Add(descLabel);
                    }
                }
                else
                {
                    var nullLabel = new Label("(空引用)");
                    nullLabel.style.color = new Color(0.8f, 0.4f, 0.4f);
                    nullLabel.style.fontSize = 11;
                    nullLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                    contentContainer.Add(nullLabel);
                }

                cueItem.Add(contentContainer);

                var removeBtn = new Button(() =>
                {
                    prop.DeleteArrayElementAtIndex(index);
                    if (prop.arraySize > index && prop.GetArrayElementAtIndex(index).objectReferenceValue != null)
                    {
                        prop.DeleteArrayElementAtIndex(index);
                    }
                    SaveChanges();
                    RefreshCueList(container, prop);
                }) { text = "×" };
                removeBtn.style.width = 20;
                removeBtn.style.height = 20;
                removeBtn.style.marginLeft = 4;
                removeBtn.style.paddingLeft = 0;
                removeBtn.style.paddingRight = 0;
                removeBtn.style.paddingTop = 0;
                removeBtn.style.paddingBottom = 0;
                removeBtn.style.fontSize = 12;
                removeBtn.style.backgroundColor = Color.clear;
                removeBtn.style.borderLeftWidth = 0;
                removeBtn.style.borderRightWidth = 0;
                removeBtn.style.borderTopWidth = 0;
                removeBtn.style.borderBottomWidth = 0;
                removeBtn.style.color = new Color(0.7f, 0.7f, 0.7f);
                cueItem.Add(removeBtn);

                container.Add(cueItem);
            }
        }

        protected void AddHandlerTypeField(VisualElement parent, string propertyName, string label)
        {
            var prop = SerializedObject.FindProperty(propertyName);
            if (prop == null) return;

            DrawnProperties.Add(propertyName);

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginBottom = 4;

            var labelElement = new Label(label);
            labelElement.style.minWidth = 120;
            labelElement.style.color = new Color(0.7f, 0.7f, 0.7f);
            container.Add(labelElement);

            var valueLabel = new Label(string.IsNullOrEmpty(prop.stringValue) ? "(默认处理器)" : prop.stringValue);
            valueLabel.style.flexGrow = 1;
            valueLabel.style.color = new Color(0.86f, 0.86f, 0.86f);
            container.Add(valueLabel);

            var selectBtn = new Button(() =>
            {
                HandlerTypeSelectorPopup.Show(prop.stringValue, selectedType =>
                {
                    prop.stringValue = selectedType;
                    SaveChanges();
                    valueLabel.text = string.IsNullOrEmpty(selectedType) ? "(默认处理器)" : selectedType;
                });
            }) { text = "选择" };
            selectBtn.AddToClassList("btn");
            selectBtn.AddToClassList("btn-sm");
            selectBtn.AddToClassList("btn-secondary");
            container.Add(selectBtn);

            parent.Add(container);
        }

        protected void SaveChanges()
        {
            SerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(Target);
            AssetDatabase.SaveAssetIfDirty(Target);
        }

        #endregion
    }
}
