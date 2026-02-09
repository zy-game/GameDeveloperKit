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
    /// 战斗系统编辑器窗口
    /// 使用可扩展的Inspector架构，支持子类自定义编辑器
    /// </summary>
    public class CombatEditorWindow : EditorWindow
    {
        private enum TabType
        {
            Abilities,
            Effects,
            Cues,
            Characters,
            Items
        }

        private TabType _currentTab = TabType.Abilities;
        private List<ScriptableObject> _currentItems = new();
        private ScriptableObject _selectedItem;
        private CombatInspectorBase _currentInspector;

        // 记录每个标签页上次选中的项
        private readonly Dictionary<TabType, ScriptableObject> _lastSelectedItems = new();

        private VisualElement _root;
        private VisualElement _listContainer;
        private VisualElement _detailPanel;
        private ScrollView _detailScroll;
        private Label _emptyHint;

        [MenuItem("GameDeveloperKit/战斗系统编辑器")]
        public static void ShowWindow()
        {
            var window = GetWindow<CombatEditorWindow>("战斗系统编辑器");
            window.minSize = new Vector2(900, 600);
        }

        private void OnEnable()
        {
            // 刷新Inspector注册表，确保加载所有程序集中的Inspector
            CombatInspectorRegistry.Refresh();
            RefreshAssetList();
        }

        private void CreateGUI()
        {
            _root = new VisualElement();
            _root.style.flexGrow = 1;
            _root.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(_root);

            LoadStyleSheets();
            CreateToolbar();
            CreateMainContent();
            RefreshList();
            SelectFirstItem();
        }

        private void LoadStyleSheets()
        {
            var commonStyle = EditorAssetLoader.LoadStyleSheet("Common/Style/EditorCommonStyle.uss");
            if (commonStyle != null) _root.styleSheets.Add(commonStyle);

            var combatStyle = EditorAssetLoader.LoadStyleSheet("Combat/CombatEditorStyle.uss");
            if (combatStyle != null) _root.styleSheets.Add(combatStyle);
        }

        private void CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");

            var titleLabel = new Label("战斗系统编辑器");
            titleLabel.AddToClassList("toolbar-title");
            toolbar.Add(titleLabel);

            var spacer = new VisualElement();
            spacer.AddToClassList("toolbar-spacer");
            toolbar.Add(spacer);

            // Tab buttons
            var tabContainer = new VisualElement();
            tabContainer.style.flexDirection = FlexDirection.Row;

            CreateTabButton(tabContainer, "tab-abilities", "技能", TabType.Abilities, true);
            CreateTabButton(tabContainer, "tab-effects", "效果", TabType.Effects, false);
            CreateTabButton(tabContainer, "tab-cues", "Cue定义", TabType.Cues, false);
            CreateTabButton(tabContainer, "tab-characters", "角色", TabType.Characters, false);
            CreateTabButton(tabContainer, "tab-items", "道具", TabType.Items, false);

            toolbar.Add(tabContainer);

            // Create button
            var createBtn = new Button(OnCreateClicked) { text = "+ 新建" };
            createBtn.AddToClassList("btn");
            createBtn.AddToClassList("btn-success");
            createBtn.style.marginLeft = 16;
            toolbar.Add(createBtn);

            // Refresh button
            var refreshBtn = new Button(OnRefreshClicked) { text = "刷新" };
            refreshBtn.AddToClassList("btn");
            refreshBtn.AddToClassList("btn-secondary");
            refreshBtn.style.marginLeft = 8;
            toolbar.Add(refreshBtn);

            _root.Add(toolbar);
        }

        private void CreateTabButton(VisualElement container, string name, string text, TabType tab, bool isPrimary)
        {
            var btn = new Button(() => SwitchTab(tab)) { text = text };
            btn.AddToClassList("btn");
            btn.AddToClassList(isPrimary ? "btn-primary" : "btn-secondary");
            btn.name = name;
            if (container.childCount > 0)
                btn.style.marginLeft = 4;
            container.Add(btn);
        }

        private void CreateMainContent()
        {
            var contentArea = new VisualElement();
            contentArea.AddToClassList("content-area");
            contentArea.style.flexGrow = 1;
            contentArea.style.flexDirection = FlexDirection.Row;

            // Left panel - List
            var leftPanel = new VisualElement();
            leftPanel.AddToClassList("left-panel");
            leftPanel.style.width = 280;

            var listScroll = new ScrollView(ScrollViewMode.Vertical);
            listScroll.style.flexGrow = 1;

            _listContainer = new VisualElement();
            _listContainer.AddToClassList("package-list-container");
            listScroll.Add(_listContainer);

            leftPanel.Add(listScroll);
            contentArea.Add(leftPanel);

            // Splitter
            var splitter = new VisualElement();
            splitter.AddToClassList("splitter");
            contentArea.Add(splitter);

            // Right panel - Detail
            var rightPanel = new VisualElement();
            rightPanel.AddToClassList("right-panel");
            rightPanel.style.flexGrow = 1;

            _detailScroll = new ScrollView(ScrollViewMode.Vertical);
            _detailScroll.style.flexGrow = 1;

            _detailPanel = new VisualElement();
            _detailPanel.AddToClassList("detail-content");
            _detailScroll.Add(_detailPanel);

            _emptyHint = new Label("请从左侧列表选择一个项目进行编辑");
            _emptyHint.AddToClassList("empty-state-text");
            _emptyHint.style.alignSelf = Align.Center;
            _emptyHint.style.marginTop = 100;

            rightPanel.Add(_detailScroll);
            contentArea.Add(rightPanel);

            _root.Add(contentArea);

            ShowEmptyState();
        }

        private void SwitchTab(TabType tab)
        {
            // 保存当前标签页的选中项
            if (_selectedItem != null)
            {
                _lastSelectedItems[_currentTab] = _selectedItem;
            }

            _currentTab = tab;
            _selectedItem = null;
            _currentInspector = null;

            UpdateTabButtonStyles();
            RefreshAssetList();
            RefreshList();
            RestoreLastSelection();
        }

        private void UpdateTabButtonStyles()
        {
            var tabs = new[] { "tab-abilities", "tab-effects", "tab-cues", "tab-characters", "tab-items" };
            var tabTypes = new[] { TabType.Abilities, TabType.Effects, TabType.Cues, TabType.Characters, TabType.Items };

            for (int i = 0; i < tabs.Length; i++)
            {
                var btn = _root.Q<Button>(tabs[i]);
                if (btn == null) continue;

                btn.RemoveFromClassList("btn-primary");
                btn.RemoveFromClassList("btn-secondary");
                btn.AddToClassList(tabTypes[i] == _currentTab ? "btn-primary" : "btn-secondary");
            }
        }

        private void SelectFirstItem()
        {
            if (_currentItems.Count > 0)
            {
                SelectItem(_currentItems[0]);
            }
            else
            {
                ShowEmptyState();
            }
        }

        private void RestoreLastSelection()
        {
            if (_lastSelectedItems.TryGetValue(_currentTab, out var lastSelected) &&
                lastSelected != null &&
                _currentItems.Contains(lastSelected))
            {
                SelectItem(lastSelected);
            }
            else
            {
                SelectFirstItem();
            }
        }

        private void RefreshAssetList()
        {
            _currentItems.Clear();

            var searchType = _currentTab switch
            {
                TabType.Abilities => typeof(AbilityBase),
                TabType.Effects => typeof(GameplayEffect),
                TabType.Cues => typeof(CueDefinition),
                TabType.Characters => typeof(CharacterConfig),
                TabType.Items => typeof(ItemConfig),
                _ => null
            };

            if (searchType == null) return;

            // 使用 FindAssets 搜索所有该类型及其子类的资源
            var guids = AssetDatabase.FindAssets($"t:{searchType.Name}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset != null && searchType.IsInstanceOfType(asset))
                {
                    _currentItems.Add(asset);
                }
            }

            // 排序
            _currentItems = _currentItems.OrderBy(GetItemDisplayName).ToList();
        }

        private void RefreshList()
        {
            _listContainer.Clear();

            if (_currentItems.Count == 0)
            {
                var hint = new Label($"暂无{GetTabDisplayName()}，点击\"新建\"创建");
                hint.AddToClassList("empty-list-hint");
                _listContainer.Add(hint);
                return;
            }

            foreach (var item in _currentItems)
            {
                var listItem = CreateListItem(item);
                _listContainer.Add(listItem);
            }
        }

        private VisualElement CreateListItem(ScriptableObject obj)
        {
            var item = new VisualElement();
            item.AddToClassList("package-item");
            item.userData = obj;

            if (_selectedItem == obj)
            {
                item.AddToClassList("package-item--selected");
            }

            // Badge - 显示实际类型名
            var typeName = obj.GetType().Name;
            var badgeLabel = new Label(GetShortTypeName(typeName));
            badgeLabel.AddToClassList("package-type-badge");
            badgeLabel.AddToClassList(GetBadgeClass());
            item.Add(badgeLabel);

            // Name
            var nameLabel = new Label(GetItemDisplayName(obj));
            nameLabel.AddToClassList("package-name");
            item.Add(nameLabel);

            item.RegisterCallback<ClickEvent>(evt => SelectItem(obj));

            return item;
        }

        private string GetShortTypeName(string typeName)
        {
            // 缩短常见类型名
            if (typeName.EndsWith("Ability")) return "技能";
            if (typeName.EndsWith("Effect")) return "效果";
            if (typeName.EndsWith("Definition")) return "Cue";
            if (typeName == "CharacterConfig") return "角色";
            if (typeName == "ItemConfig" || typeName.EndsWith("Item")) return "道具";
            return typeName;
        }

        private string GetBadgeClass()
        {
            return _currentTab switch
            {
                TabType.Abilities => "package-type-badge--base",
                TabType.Effects => "package-type-badge--hotfix",
                TabType.Cues => "package-type-badge--base",
                TabType.Characters => "package-type-badge--hotfix",
                TabType.Items => "package-type-badge--base",
                _ => "package-type-badge--base"
            };
        }

        private void SelectItem(ScriptableObject obj)
        {
            _selectedItem = obj;

            // Update list selection
            foreach (var child in _listContainer.Children())
            {
                child.RemoveFromClassList("package-item--selected");
                if (child.userData == obj)
                {
                    child.AddToClassList("package-item--selected");
                }
            }

            // 获取对应的Inspector
            _currentInspector = CombatInspectorRegistry.CreateInspector(obj.GetType());

            if (_currentInspector != null)
            {
                _detailPanel.Clear();
                _currentInspector.Initialize(obj, _detailPanel);
                _currentInspector.OnDraw();
                AddDeleteButton();
            }
            else
            {
                // 没有找到对应的Inspector，显示默认的PropertyField
                ShowDefaultInspector(obj);
            }
        }

        private void ShowDefaultInspector(ScriptableObject obj)
        {
            _detailPanel.Clear();

            var serializedObject = new SerializedObject(obj);
            var prop = serializedObject.GetIterator();
            prop.Next(true);

            while (prop.NextVisible(false))
            {
                if (prop.name == "m_Script") continue;

                var field = new UnityEditor.UIElements.PropertyField(prop);
                field.Bind(serializedObject);
                field.RegisterValueChangeCallback(evt =>
                {
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(obj);
                    AssetDatabase.SaveAssetIfDirty(obj);
                });
                _detailPanel.Add(field);
            }

            AddDeleteButton();
        }

        private void ShowEmptyState()
        {
            _detailPanel.Clear();
            _detailPanel.Add(_emptyHint);
        }

        private void AddDeleteButton()
        {
            var deleteBtn = new Button(() =>
            {
                if (_selectedItem == null) return;

                var itemName = GetItemDisplayName(_selectedItem);
                if (!EditorUtility.DisplayDialog("确认删除", $"确定要删除 \"{itemName}\" 吗？", "删除", "取消"))
                    return;

                var path = AssetDatabase.GetAssetPath(_selectedItem);

                // 清除记录
                if (_lastSelectedItems.TryGetValue(_currentTab, out var last) && last == _selectedItem)
                {
                    _lastSelectedItems.Remove(_currentTab);
                }

                AssetDatabase.DeleteAsset(path);
                _selectedItem = null;
                _currentInspector = null;
                RefreshAssetList();
                RefreshList();
                SelectFirstItem();
            }) { text = "删除" };
            deleteBtn.AddToClassList("btn");
            deleteBtn.AddToClassList("btn-danger");
            deleteBtn.style.marginTop = 16;
            deleteBtn.style.alignSelf = Align.FlexEnd;
            _detailPanel.Add(deleteBtn);
        }

        private void OnCreateClicked()
        {
            var title = $"创建{GetTabDisplayName()}";
            var defaultName = GetDefaultAssetName();

            var path = EditorUtility.SaveFilePanelInProject(title, defaultName, "asset", "选择保存位置");
            if (string.IsNullOrEmpty(path)) return;

            var asset = CreateAssetForCurrentTab(path);
            if (asset == null) return;

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            RefreshAssetList();
            RefreshList();
            SelectItem(asset);
        }

        private ScriptableObject CreateAssetForCurrentTab(string path)
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(path);

            return _currentTab switch
            {
                TabType.Abilities => CreateAbility(fileName),
                TabType.Effects => CreateEffect(fileName),
                TabType.Cues => CreateCue(fileName),
                TabType.Characters => CreateCharacter(fileName),
                TabType.Items => CreateItem(fileName),
                _ => null
            };
        }

        private ScriptableObject CreateAbility(string name)
        {
            var asset = CreateInstance<AbilityBase>();
            asset.AbilityName = name;
            return asset;
        }

        private ScriptableObject CreateEffect(string name)
        {
            var asset = CreateInstance<GameplayEffect>();
            asset.EffectName = name;
            return asset;
        }

        private ScriptableObject CreateCue(string name)
        {
            var asset = CreateInstance<CueDefinition>();
            asset.CueName = name;
            return asset;
        }

        private ScriptableObject CreateCharacter(string name)
        {
            var asset = CreateInstance<CharacterConfig>();
            asset.CharacterName = name;
            return asset;
        }

        private ScriptableObject CreateItem(string name)
        {
            var asset = CreateInstance<ItemConfig>();
            asset.ItemName = name;
            return asset;
        }

        private void OnRefreshClicked()
        {
            CombatInspectorRegistry.Refresh();
            RefreshAssetList();
            RefreshList();

            if (_selectedItem != null && _currentItems.Contains(_selectedItem))
            {
                SelectItem(_selectedItem);
            }
            else
            {
                SelectFirstItem();
            }
        }

        private string GetTabDisplayName()
        {
            return _currentTab switch
            {
                TabType.Abilities => "技能",
                TabType.Effects => "效果",
                TabType.Cues => "Cue定义",
                TabType.Characters => "角色",
                TabType.Items => "道具",
                _ => "资源"
            };
        }

        private string GetDefaultAssetName()
        {
            return _currentTab switch
            {
                TabType.Abilities => "NewAbility",
                TabType.Effects => "NewEffect",
                TabType.Cues => "NewCue",
                TabType.Characters => "NewCharacter",
                TabType.Items => "NewItem",
                _ => "NewAsset"
            };
        }

        private string GetItemDisplayName(ScriptableObject obj)
        {
            var name = obj switch
            {
                AbilityBase ability => ability.AbilityName,
                GameplayEffect effect => effect.EffectName,
                CueDefinition cue => cue.CueName,
                CharacterConfig character => character.CharacterName,
                ItemConfig item => item.ItemName,
                _ => null
            };

            return string.IsNullOrEmpty(name) ? "(未命名)" : name;
        }
    }
}
