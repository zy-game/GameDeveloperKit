using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using GameDeveloperKit.Combat;

namespace GameDeveloperKit.Editor.Combat
{
    public class EffectSelectorPopup : EditorWindow
    {
        private struct EffectInfo
        {
            public GameplayEffect Effect;
            public string Path;
            public string Name;
            public string Description;
        }

        private static List<EffectInfo> _cachedEffects = new();
        private static DateTime _lastScanTime = DateTime.MinValue;

        private GameplayEffect _currentValue;
        private string _searchText = "";
        private Action<GameplayEffect> _onSelect;
        private HashSet<GameplayEffect> _existingEffects = new();

        private VisualElement _root;
        private TextField _searchField;
        private ScrollView _listScroll;
        private VisualElement _listContainer;

        private List<EffectInfo> _filteredEffects = new();

        private static EffectSelectorPopup _currentInstance;

        public static void Show(GameplayEffect currentValue, Action<GameplayEffect> onSelect, IEnumerable<GameplayEffect> existingEffects = null)
        {
            if (_currentInstance != null)
            {
                _currentInstance.Close();
                _currentInstance = null;
            }

            ScanEffects();

            var window = CreateInstance<EffectSelectorPopup>();
            window._currentValue = currentValue;
            window._onSelect = onSelect;
            window._existingEffects = existingEffects != null 
                ? new HashSet<GameplayEffect>(existingEffects.Where(e => e != null)) 
                : new HashSet<GameplayEffect>();
            window.titleContent = new GUIContent("选择效果");
            window.minSize = new Vector2(400, 500);
            window.maxSize = new Vector2(500, 650);
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

        private static void ScanEffects()
        {
            if ((DateTime.Now - _lastScanTime).TotalSeconds < 30)
                return;

            _lastScanTime = DateTime.Now;
            _cachedEffects.Clear();

            var guids = AssetDatabase.FindAssets("t:GameplayEffect");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var effect = AssetDatabase.LoadAssetAtPath<GameplayEffect>(path);
                if (effect != null)
                {
                    _cachedEffects.Add(new EffectInfo
                    {
                        Effect = effect,
                        Path = path,
                        Name = effect.EffectName,
                        Description = effect.Description
                    });
                }
            }

            _cachedEffects = _cachedEffects.OrderBy(e => e.Name).ToList();
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

            // Current value display
            if (_currentValue != null)
            {
                var currentContainer = new VisualElement();
                currentContainer.style.flexDirection = FlexDirection.Row;
                currentContainer.style.alignItems = Align.Center;
                currentContainer.style.marginTop = 8;
                currentContainer.style.marginBottom = 8;
                currentContainer.style.paddingLeft = 10;
                currentContainer.style.paddingRight = 10;
                currentContainer.style.paddingTop = 8;
                currentContainer.style.paddingBottom = 8;
                currentContainer.style.backgroundColor = new Color(0.23f, 0.51f, 0.96f, 0.2f);
                currentContainer.style.borderTopLeftRadius = 6;
                currentContainer.style.borderTopRightRadius = 6;
                currentContainer.style.borderBottomLeftRadius = 6;
                currentContainer.style.borderBottomRightRadius = 6;

                var currentLabel = new Label($"当前: {_currentValue.EffectName}");
                currentLabel.style.flexGrow = 1;
                currentLabel.style.color = new Color(0.58f, 0.77f, 0.99f);
                currentLabel.style.fontSize = 12;
                currentContainer.Add(currentLabel);

                var clearBtn = new Button(() =>
                {
                    _onSelect?.Invoke(null);
                    Close();
                }) { text = "清除" };
                clearBtn.AddToClassList("btn");
                clearBtn.AddToClassList("btn-sm");
                clearBtn.AddToClassList("btn-danger");
                clearBtn.style.height = 22;
                clearBtn.style.marginLeft = 8;
                currentContainer.Add(clearBtn);

                _root.Add(currentContainer);
            }

            // List header
            var listLabel = new Label("选择效果");
            listLabel.style.marginTop = 8;
            listLabel.style.marginBottom = 4;
            listLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            listLabel.style.fontSize = 11;
            _root.Add(listLabel);

            // Legend
            var legendContainer = new VisualElement();
            legendContainer.style.flexDirection = FlexDirection.Row;
            legendContainer.style.marginBottom = 6;

            AddLegendItem(legendContainer, new Color(0.96f, 0.62f, 0.04f), "已添加");
            AddLegendItem(legendContainer, new Color(0.06f, 0.73f, 0.51f), "可选择");

            _root.Add(legendContainer);

            // List scroll
            _listScroll = new ScrollView(ScrollViewMode.Vertical);
            _listScroll.style.flexGrow = 1;
            _listScroll.style.backgroundColor = new Color(0, 0, 0, 0.2f);
            _listScroll.style.borderTopLeftRadius = 6;
            _listScroll.style.borderTopRightRadius = 6;
            _listScroll.style.borderBottomLeftRadius = 6;
            _listScroll.style.borderBottomRightRadius = 6;

            _listContainer = new VisualElement();
            _listContainer.style.paddingLeft = 4;
            _listContainer.style.paddingRight = 4;
            _listContainer.style.paddingTop = 4;
            _listContainer.style.paddingBottom = 4;
            _listScroll.Add(_listContainer);

            _root.Add(_listScroll);

            RefreshList();
        }

        private void RefreshList()
        {
            _listContainer.Clear();

            _filteredEffects = _cachedEffects
                .Where(e => string.IsNullOrEmpty(_searchText) ||
                           e.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                           e.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                           e.Path.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (_filteredEffects.Count == 0)
            {
                var hint = new Label("没有找到匹配的效果");
                hint.style.color = new Color(0.5f, 0.5f, 0.5f);
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                hint.style.marginTop = 20;
                _listContainer.Add(hint);
                return;
            }

            foreach (var effectInfo in _filteredEffects)
            {
                _listContainer.Add(CreateEffectItem(effectInfo));
            }
        }

        private VisualElement CreateEffectItem(EffectInfo effectInfo)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingLeft = 8;
            item.style.paddingRight = 8;
            item.style.paddingTop = 6;
            item.style.paddingBottom = 6;
            item.style.marginBottom = 2;
            item.style.borderTopLeftRadius = 4;
            item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = 4;
            item.style.borderBottomRightRadius = 4;

            var isCurrentValue = effectInfo.Effect == _currentValue;
            var isAlreadySelected = _existingEffects.Contains(effectInfo.Effect);

            if (isCurrentValue)
            {
                item.style.backgroundColor = new Color(0.23f, 0.51f, 0.96f, 0.3f);
            }
            else if (isAlreadySelected)
            {
                item.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
            }

            item.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (!isCurrentValue && !isAlreadySelected)
                    item.style.backgroundColor = new Color(1, 1, 1, 0.05f);
            });

            item.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                if (isCurrentValue)
                    item.style.backgroundColor = new Color(0.23f, 0.51f, 0.96f, 0.3f);
                else if (isAlreadySelected)
                    item.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
                else
                    item.style.backgroundColor = Color.clear;
            });

            item.RegisterCallback<ClickEvent>(evt =>
            {
                if (!isAlreadySelected)
                {
                    _onSelect?.Invoke(effectInfo.Effect);
                    Close();
                }
            });

            // Indicator
            var indicator = new VisualElement();
            indicator.style.width = 6;
            indicator.style.height = 6;
            indicator.style.borderTopLeftRadius = 3;
            indicator.style.borderTopRightRadius = 3;
            indicator.style.borderBottomLeftRadius = 3;
            indicator.style.borderBottomRightRadius = 3;
            indicator.style.marginRight = 8;
            indicator.style.flexShrink = 0;

            if (isAlreadySelected)
            {
                indicator.style.backgroundColor = new Color(0.96f, 0.62f, 0.04f);
                indicator.tooltip = "已添加到列表";
            }
            else
            {
                indicator.style.backgroundColor = new Color(0.06f, 0.73f, 0.51f);
                indicator.tooltip = "可选择";
            }
            item.Add(indicator);

            // Content container
            var contentContainer = new VisualElement();
            contentContainer.style.flexGrow = 1;
            contentContainer.style.flexShrink = 1;
            contentContainer.style.overflow = Overflow.Hidden;

            // Name
            var nameLabel = new Label(string.IsNullOrEmpty(effectInfo.Name) ? effectInfo.Effect.name : effectInfo.Name);
            nameLabel.style.color = isAlreadySelected ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.86f, 0.86f, 0.86f);
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            contentContainer.Add(nameLabel);

            // Description
            if (!string.IsNullOrEmpty(effectInfo.Description))
            {
                var descLabel = new Label(effectInfo.Description);
                descLabel.style.color = isAlreadySelected ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.6f, 0.6f, 0.6f);
                descLabel.style.fontSize = 10;
                descLabel.style.whiteSpace = WhiteSpace.NoWrap;
                descLabel.style.overflow = Overflow.Hidden;
                descLabel.style.textOverflow = TextOverflow.Ellipsis;
                contentContainer.Add(descLabel);
            }

            item.Add(contentContainer);

            // Status label
            if (isCurrentValue)
            {
                var checkmark = new Label("✓");
                checkmark.style.color = new Color(0.58f, 0.77f, 0.99f);
                checkmark.style.flexShrink = 0;
                item.Add(checkmark);
            }
            else if (isAlreadySelected)
            {
                var existsLabel = new Label("已添加");
                existsLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                existsLabel.style.fontSize = 10;
                existsLabel.style.flexShrink = 0;
                item.Add(existsLabel);
            }

            return item;
        }

        private void AddLegendItem(VisualElement parent, Color color, string text)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.marginRight = 12;

            var dot = new VisualElement();
            dot.style.width = 6;
            dot.style.height = 6;
            dot.style.borderTopLeftRadius = 3;
            dot.style.borderTopRightRadius = 3;
            dot.style.borderBottomLeftRadius = 3;
            dot.style.borderBottomRightRadius = 3;
            dot.style.marginRight = 4;
            dot.style.backgroundColor = color;
            item.Add(dot);

            var label = new Label(text);
            label.style.fontSize = 10;
            label.style.color = new Color(0.6f, 0.6f, 0.6f);
            item.Add(label);

            parent.Add(item);
        }
    }
}
