using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using GameDeveloperKit.Combat;

namespace GameDeveloperKit.Editor.Combat
{
    public class HandlerTypeSelectorPopup : EditorWindow
    {
        private static List<Type> _cachedHandlerTypes = new();
        private static DateTime _lastScanTime = DateTime.MinValue;

        private string _currentValue;
        private string _searchText = "";
        private Action<string> _onSelect;

        private VisualElement _root;
        private TextField _searchField;
        private ScrollView _listScroll;
        private VisualElement _listContainer;

        private List<Type> _filteredTypes = new();

        private static HandlerTypeSelectorPopup _currentInstance;

        public static void Show(string currentValue, Action<string> onSelect)
        {
            if (_currentInstance != null)
            {
                _currentInstance.Close();
                _currentInstance = null;
            }

            ScanHandlerTypes();

            var window = CreateInstance<HandlerTypeSelectorPopup>();
            window._currentValue = currentValue;
            window._onSelect = onSelect;
            window.titleContent = new GUIContent("选择处理器类型");
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

        private static void ScanHandlerTypes()
        {
            if ((DateTime.Now - _lastScanTime).TotalSeconds < 30)
                return;

            _lastScanTime = DateTime.Now;
            _cachedHandlerTypes.Clear();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => !t.IsInterface && !t.IsAbstract && typeof(ICueHandler).IsAssignableFrom(t));
                    _cachedHandlerTypes.AddRange(types);
                }
                catch { }
            }

            _cachedHandlerTypes = _cachedHandlerTypes.OrderBy(t => t.Name).ToList();
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
            if (!string.IsNullOrEmpty(_currentValue))
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

                var currentLabel = new Label($"当前: {GetShortTypeName(_currentValue)}");
                currentLabel.style.flexGrow = 1;
                currentLabel.style.color = new Color(0.58f, 0.77f, 0.99f);
                currentLabel.style.fontSize = 12;
                currentContainer.Add(currentLabel);

                var clearBtn = new Button(() =>
                {
                    _onSelect?.Invoke("");
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
            var listLabel = new Label("选择处理器类型");
            listLabel.style.marginTop = 8;
            listLabel.style.marginBottom = 4;
            listLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            listLabel.style.fontSize = 11;
            _root.Add(listLabel);

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

            _filteredTypes = _cachedHandlerTypes
                .Where(t => string.IsNullOrEmpty(_searchText) ||
                           t.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                           t.FullName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                           (t.Namespace != null && t.Namespace.Contains(_searchText, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (_filteredTypes.Count == 0)
            {
                var hint = new Label("没有找到匹配的处理器类型");
                hint.style.color = new Color(0.5f, 0.5f, 0.5f);
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                hint.style.marginTop = 20;
                _listContainer.Add(hint);
                return;
            }

            foreach (var type in _filteredTypes)
            {
                _listContainer.Add(CreateTypeItem(type));
            }
        }

        private VisualElement CreateTypeItem(Type type)
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

            var isCurrentValue = type.FullName == _currentValue;

            if (isCurrentValue)
            {
                item.style.backgroundColor = new Color(0.23f, 0.51f, 0.96f, 0.3f);
            }

            item.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (!isCurrentValue)
                    item.style.backgroundColor = new Color(1, 1, 1, 0.05f);
            });

            item.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                if (isCurrentValue)
                    item.style.backgroundColor = new Color(0.23f, 0.51f, 0.96f, 0.3f);
                else
                    item.style.backgroundColor = Color.clear;
            });

            item.RegisterCallback<ClickEvent>(evt =>
            {
                _onSelect?.Invoke(type.FullName);
                Close();
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
            indicator.style.backgroundColor = new Color(0.06f, 0.73f, 0.51f);
            item.Add(indicator);

            // Content container
            var contentContainer = new VisualElement();
            contentContainer.style.flexGrow = 1;
            contentContainer.style.flexShrink = 1;
            contentContainer.style.overflow = Overflow.Hidden;

            // Name
            var nameLabel = new Label(type.Name);
            nameLabel.style.color = new Color(0.86f, 0.86f, 0.86f);
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            contentContainer.Add(nameLabel);

            // Namespace
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                var nsLabel = new Label(type.Namespace);
                nsLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                nsLabel.style.fontSize = 10;
                nsLabel.style.whiteSpace = WhiteSpace.NoWrap;
                nsLabel.style.overflow = Overflow.Hidden;
                nsLabel.style.textOverflow = TextOverflow.Ellipsis;
                contentContainer.Add(nsLabel);
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

            return item;
        }

        private string GetShortTypeName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName)) return "";
            
            var lastDot = fullTypeName.LastIndexOf('.');
            return lastDot >= 0 ? fullTypeName.Substring(lastDot + 1) : fullTypeName;
        }
    }
}
