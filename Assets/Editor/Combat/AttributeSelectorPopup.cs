using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor.Combat
{
    /// <summary>
    /// 属性选择器弹窗
    /// </summary>
    public class AttributeSelectorPopup : EditorWindow
    {
        private static readonly List<AttributeInfo> CommonAttributes = new()
        {
            // 生命属性
            new("Health", "生命值", "生命"),
            new("MaxHealth", "最大生命值", "生命"),
            new("HealthRegen", "生命恢复", "生命"),
            
            // 战斗属性
            new("Attack", "攻击力", "战斗"),
            new("Defense", "防御力", "战斗"),
            new("CritRate", "暴击率", "战斗"),
            new("CritDamage", "暴击伤害", "战斗"),
            new("AttackSpeed", "攻击速度", "战斗"),
            new("MoveSpeed", "移动速度", "战斗"),
            
            // 资源属性
            new("Mana", "法力值", "资源"),
            new("MaxMana", "最大法力值", "资源"),
            new("ManaRegen", "法力恢复", "资源"),
            new("Energy", "能量", "资源"),
            new("MaxEnergy", "最大能量", "资源"),
            
            // 抗性属性
            new("PhysicalResist", "物理抗性", "抗性"),
            new("MagicalResist", "魔法抗性", "抗性"),
            new("FireResist", "火焰抗性", "抗性"),
            new("IceResist", "冰霜抗性", "抗性"),
            new("LightningResist", "雷电抗性", "抗性"),
            new("PoisonResist", "毒素抗性", "抗性"),
        };

        private struct AttributeInfo
        {
            public string Name;
            public string DisplayName;
            public string Category;

            public AttributeInfo(string name, string displayName, string category)
            {
                Name = name;
                DisplayName = displayName;
                Category = category;
            }
        }

        private string _currentValue;
        private string _searchText = "";
        private Action<string> _onSelect;

        private VisualElement _root;
        private TextField _searchField;
        private TextField _customField;
        private VisualElement _listContainer;

        private static AttributeSelectorPopup _currentInstance;

        public static void Show(string currentValue, Action<string> onSelect)
        {
            // 关闭已存在的窗口
            if (_currentInstance != null)
            {
                _currentInstance.Close();
                _currentInstance = null;
            }

            var window = CreateInstance<AttributeSelectorPopup>();
            window._currentValue = currentValue;
            window._onSelect = onSelect;
            window.titleContent = new GUIContent("选择属性");
            window.minSize = new Vector2(320, 480);
            window.maxSize = new Vector2(380, 600);
            window.ShowUtility();
            
            _currentInstance = window;
        }

        private void OnDestroy()
        {
            if (_currentInstance == this)
            {
                _currentInstance = null;
            }
        }

        private void OnLostFocus()
        {
            Close();
        }

        private void CreateGUI()
        {
            _root = new VisualElement();
            _root.style.flexGrow = 1;
            _root.style.paddingLeft = 12;
            _root.style.paddingRight = 12;
            _root.style.paddingTop = 12;
            _root.style.paddingBottom = 12;
            rootVisualElement.Add(_root);

            var commonStyle = EditorAssetLoader.LoadStyleSheet("Common/Style/EditorCommonStyle.uss");
            if (commonStyle != null) _root.styleSheets.Add(commonStyle);

            var combatStyle = EditorAssetLoader.LoadStyleSheet("Combat/CombatEditorStyle.uss");
            if (combatStyle != null) _root.styleSheets.Add(combatStyle);

            // Search field
            _searchField = new TextField("搜索");
            _searchField.AddToClassList("custom-textfield");
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchText = evt.newValue;
                RefreshList();
            });
            _root.Add(_searchField);

            // List
            var listLabel = new Label("选择属性");
            listLabel.style.marginTop = 8;
            listLabel.style.marginBottom = 4;
            listLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            listLabel.style.fontSize = 11;
            _root.Add(listLabel);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.style.maxHeight = 280;
            scroll.style.backgroundColor = new Color(0, 0, 0, 0.2f);
            scroll.style.borderTopLeftRadius = 6;
            scroll.style.borderTopRightRadius = 6;
            scroll.style.borderBottomLeftRadius = 6;
            scroll.style.borderBottomRightRadius = 6;

            _listContainer = new VisualElement();
            _listContainer.style.paddingLeft = 4;
            _listContainer.style.paddingRight = 4;
            _listContainer.style.paddingTop = 4;
            _listContainer.style.paddingBottom = 4;
            scroll.Add(_listContainer);

            _root.Add(scroll);

            // Custom input
            var customContainer = new VisualElement();
            customContainer.style.marginTop = 12;

            var customLabel = new Label("自定义属性名");
            customLabel.style.marginBottom = 4;
            customLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            customLabel.style.fontSize = 11;
            customContainer.Add(customLabel);

            var customRow = new VisualElement();
            customRow.style.flexDirection = FlexDirection.Row;

            _customField = new TextField();
            _customField.AddToClassList("custom-textfield");
            _customField.style.flexGrow = 1;
            _customField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    OnCustomConfirm();
                    evt.StopPropagation();
                }
            });
            customRow.Add(_customField);

            var addBtn = new Button(OnCustomConfirm) { text = "添加" };
            addBtn.AddToClassList("btn");
            addBtn.AddToClassList("btn-primary");
            addBtn.style.marginLeft = 4;
            customRow.Add(addBtn);

            customContainer.Add(customRow);
            _root.Add(customContainer);

            // Cancel button
            var cancelBtn = new Button(Close) { text = "取消" };
            cancelBtn.AddToClassList("btn");
            cancelBtn.AddToClassList("btn-secondary");
            cancelBtn.style.marginTop = 12;
            _root.Add(cancelBtn);

            RefreshList();
            _searchField.Focus();
        }

        private void RefreshList()
        {
            _listContainer.Clear();

            var filtered = CommonAttributes
                .Where(a => string.IsNullOrEmpty(_searchText) ||
                           a.Name.ToLower().Contains(_searchText.ToLower()) ||
                           a.DisplayName.Contains(_searchText))
                .GroupBy(a => a.Category)
                .OrderBy(g => g.Key);

            foreach (var group in filtered)
            {
                var categoryLabel = new Label(group.Key);
                categoryLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                categoryLabel.style.fontSize = 10;
                categoryLabel.style.marginTop = 8;
                categoryLabel.style.marginBottom = 2;
                categoryLabel.style.marginLeft = 4;
                _listContainer.Add(categoryLabel);

                foreach (var attr in group)
                {
                    var item = CreateAttributeItem(attr);
                    _listContainer.Add(item);
                }
            }

            if (!filtered.Any())
            {
                var hint = new Label("没有找到匹配的属性");
                hint.style.color = new Color(0.5f, 0.5f, 0.5f);
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                hint.style.marginTop = 20;
                _listContainer.Add(hint);
            }
        }

        private VisualElement CreateAttributeItem(AttributeInfo attr)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingLeft = 8;
            item.style.paddingRight = 8;
            item.style.paddingTop = 4;
            item.style.paddingBottom = 4;
            item.style.marginBottom = 2;
            item.style.borderTopLeftRadius = 4;
            item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = 4;
            item.style.borderBottomRightRadius = 4;

            var isSelected = attr.Name == _currentValue;
            if (isSelected)
            {
                item.style.backgroundColor = new Color(0.06f, 0.73f, 0.51f, 0.3f);
            }

            item.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (!isSelected)
                    item.style.backgroundColor = new Color(1, 1, 1, 0.05f);
            });

            item.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                if (!isSelected)
                    item.style.backgroundColor = Color.clear;
            });

            item.RegisterCallback<ClickEvent>(evt =>
            {
                _onSelect?.Invoke(attr.Name);
                Close();
            });

            var nameLabel = new Label(attr.DisplayName);
            nameLabel.style.flexGrow = 1;
            nameLabel.style.color = new Color(0.86f, 0.86f, 0.86f);
            nameLabel.style.fontSize = 12;
            item.Add(nameLabel);

            var codeLabel = new Label(attr.Name);
            codeLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            codeLabel.style.fontSize = 10;
            item.Add(codeLabel);

            if (isSelected)
            {
                var checkmark = new Label("✓");
                checkmark.style.color = new Color(0.65f, 0.95f, 0.81f);
                checkmark.style.marginLeft = 8;
                item.Add(checkmark);
            }

            return item;
        }

        private void OnCustomConfirm()
        {
            var customValue = _customField.value?.Trim();
            if (!string.IsNullOrEmpty(customValue))
            {
                _onSelect?.Invoke(customValue);
                Close();
            }
        }
    }
}
