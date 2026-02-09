using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor.Combat
{
    /// <summary>
    /// 标签选择弹窗
    /// </summary>
    public class TagSelectorPopup : EditorWindow
    {
        private static readonly List<string> CommonTags = new()
        {
            // 状态标签
            "State.Alive",
            "State.Dead",
            "State.Stunned",
            "State.Silenced",
            "State.Rooted",
            "State.Invincible",
            "State.Invisible",
            
            // Buff标签
            "State.Buff.Attack",
            "State.Buff.Defense",
            "State.Buff.Speed",
            "State.Buff.Regen",
            "State.Buff.Shield",
            
            // Debuff标签
            "State.Debuff.Slow",
            "State.Debuff.Weak",
            "State.Debuff.Poison",
            "State.Debuff.Burn",
            "State.Debuff.Freeze",
            "State.Debuff.Bleed",
            
            // 技能标签
            "Ability.Attack",
            "Ability.Skill",
            "Ability.Ultimate",
            "Ability.Passive",
            "Ability.Movement",
            
            // 伤害标签
            "Damage.Physical",
            "Damage.Magical",
            "Damage.Fire",
            "Damage.Ice",
            "Damage.Lightning",
            "Damage.Poison",
            "Damage.True",
            
            // 表现标签
            "Cue.Hit",
            "Cue.Critical",
            "Cue.Heal",
            "Cue.Buff",
            "Cue.Debuff",
            "Cue.Death",
            "Cue.LevelUp",
        };

        private static HashSet<string> _projectTags = new();
        private static DateTime _lastScanTime;

        private string _currentValue;
        private HashSet<string> _existingTags = new();
        private string _searchText = "";
        private Action<string> _onSelect;
        private List<string> _filteredTags = new();

        private VisualElement _root;
        private TextField _searchField;
        private TextField _customTagField;
        private ScrollView _tagListScroll;
        private VisualElement _tagListContainer;

        private static TagSelectorPopup _currentInstance;

        public static void Show(string currentValue, Action<string> onSelect, IEnumerable<string> existingTags = null)
        {
            // 关闭已存在的窗口
            if (_currentInstance != null)
            {
                _currentInstance.Close();
                _currentInstance = null;
            }

            // 强制重新扫描项目标签
            _lastScanTime = DateTime.MinValue;
            ScanProjectTags();

            var window = CreateInstance<TagSelectorPopup>();
            window._currentValue = currentValue;
            window._onSelect = onSelect;
            window._existingTags = existingTags != null ? new HashSet<string>(existingTags) : new HashSet<string>();
            window.titleContent = new GUIContent("选择标签");
            window.minSize = new Vector2(350, 500);
            window.maxSize = new Vector2(400, 650);
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
            // 失去焦点时关闭窗口
            Close();
        }

        private static void ScanProjectTags()
        {
            if ((DateTime.Now - _lastScanTime).TotalSeconds < 30)
                return;

            _lastScanTime = DateTime.Now;
            _projectTags.Clear();

            // Scan AbilityBase assets
            var abilityGuids = AssetDatabase.FindAssets("t:AbilityBase");
            foreach (var guid in abilityGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ability = AssetDatabase.LoadAssetAtPath<GameDeveloperKit.Combat.AbilityBase>(path);
                if (ability != null)
                {
                    AddTagsFromArray(ability.AbilityTags);
                    AddTagsFromArray(ability.ActivationRequiredTags);
                    AddTagsFromArray(ability.ActivationBlockedTags);
                    AddTagsFromArray(ability.ActivationGrantedTags);
                    AddTagsFromArray(ability.CancelAbilitiesWithTags);
                    AddTagsFromArray(ability.BlockAbilitiesWithTags);
                    // ActivationCue 现在是 CueDefinition 资源，不再是标签
                }
            }

            // Scan GameplayEffect assets
            var effectGuids = AssetDatabase.FindAssets("t:GameplayEffect");
            foreach (var guid in effectGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var effect = AssetDatabase.LoadAssetAtPath<GameDeveloperKit.Combat.GameplayEffect>(path);
                if (effect != null)
                {
                    AddTagsFromArray(effect.GrantedTags);
                    AddTagsFromArray(effect.RequiredTags);
                    AddTagsFromArray(effect.BlockedTags);
                    AddTagsFromArray(effect.RemoveEffectsWithTags);
                    // Cues 现在是 CueDefinition[] 资源，不再是标签
                }
            }
        }

        private static void AddTagsFromArray(string[] tags)
        {
            if (tags == null) return;
            foreach (var tag in tags)
            {
                if (!string.IsNullOrEmpty(tag))
                    _projectTags.Add(tag);
            }
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
                RefreshTagList();
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

                var currentLabel = new Label($"当前: {_currentValue}");
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

            // Tag list
            var listLabel = new Label("选择标签");
            listLabel.style.marginTop = 8;
            listLabel.style.marginBottom = 4;
            listLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            listLabel.style.fontSize = 11;
            _root.Add(listLabel);

            // Legend
            var legendContainer = new VisualElement();
            legendContainer.style.flexDirection = FlexDirection.Row;
            legendContainer.style.marginBottom = 6;
            legendContainer.style.flexWrap = Wrap.Wrap;
            
            AddLegendItem(legendContainer, new Color(0.96f, 0.62f, 0.04f), "已添加");
            AddLegendItem(legendContainer, new Color(0.06f, 0.73f, 0.51f), "项目中使用");
            AddLegendItem(legendContainer, new Color(0.5f, 0.5f, 0.5f), "预设");
            
            _root.Add(legendContainer);

            _tagListScroll = new ScrollView(ScrollViewMode.Vertical);
            _tagListScroll.style.flexGrow = 1;
            _tagListScroll.style.flexShrink = 1;
            _tagListScroll.style.maxHeight = 280;
            _tagListScroll.style.backgroundColor = new Color(0, 0, 0, 0.2f);
            _tagListScroll.style.borderTopLeftRadius = 6;
            _tagListScroll.style.borderTopRightRadius = 6;
            _tagListScroll.style.borderBottomLeftRadius = 6;
            _tagListScroll.style.borderBottomRightRadius = 6;

            _tagListContainer = new VisualElement();
            _tagListContainer.style.paddingLeft = 4;
            _tagListContainer.style.paddingRight = 4;
            _tagListContainer.style.paddingTop = 4;
            _tagListContainer.style.paddingBottom = 4;
            _tagListScroll.Add(_tagListContainer);

            _root.Add(_tagListScroll);

            // Custom tag input
            var customContainer = new VisualElement();
            customContainer.style.marginTop = 12;

            var customLabel = new Label("自定义标签");
            customLabel.style.marginBottom = 4;
            customLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            customLabel.style.fontSize = 11;
            customContainer.Add(customLabel);

            var customRow = new VisualElement();
            customRow.style.flexDirection = FlexDirection.Row;

            _customTagField = new TextField();
            _customTagField.AddToClassList("custom-textfield");
            _customTagField.style.flexGrow = 1;
            _customTagField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    OnCustomTagConfirm();
                    evt.StopPropagation();
                }
            });
            customRow.Add(_customTagField);

            var addBtn = new Button(OnCustomTagConfirm) { text = "添加" };
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

            RefreshTagList();
            _searchField.Focus();
        }

        private void RefreshTagList()
        {
            _tagListContainer.Clear();

            var allTags = new HashSet<string>(CommonTags);
            allTags.UnionWith(_projectTags);

            _filteredTags = allTags
                .Where(t => string.IsNullOrEmpty(_searchText) || t.ToLower().Contains(_searchText.ToLower()))
                .OrderBy(t => t)
                .ToList();

            // Group by category
            var grouped = _filteredTags.GroupBy(t =>
            {
                var parts = t.Split('.');
                return parts.Length > 0 ? parts[0] : "Other";
            }).OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                var categoryLabel = new Label(GetCategoryDisplayName(group.Key));
                categoryLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                categoryLabel.style.fontSize = 10;
                categoryLabel.style.marginTop = 8;
                categoryLabel.style.marginBottom = 2;
                categoryLabel.style.marginLeft = 4;
                _tagListContainer.Add(categoryLabel);

                foreach (var tag in group)
                {
                    var tagItem = CreateTagItem(tag);
                    _tagListContainer.Add(tagItem);
                }
            }

            if (_filteredTags.Count == 0)
            {
                var hint = new Label("没有找到匹配的标签");
                hint.style.color = new Color(0.5f, 0.5f, 0.5f);
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                hint.style.marginTop = 20;
                _tagListContainer.Add(hint);
            }
        }

        private VisualElement CreateTagItem(string tag)
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

            var isCurrentValue = tag == _currentValue;
            var isAlreadySelected = _existingTags.Contains(tag);

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
                    _onSelect?.Invoke(tag);
                    Close();
                }
            });

            var isProjectTag = _projectTags.Contains(tag);
            var indicator = new VisualElement();
            indicator.style.width = 6;
            indicator.style.height = 6;
            indicator.style.borderTopLeftRadius = 3;
            indicator.style.borderTopRightRadius = 3;
            indicator.style.borderBottomLeftRadius = 3;
            indicator.style.borderBottomRightRadius = 3;
            indicator.style.marginRight = 8;
            
            // 颜色优先级：已添加(橙色) > 项目中使用(绿色) > 预设(灰色)
            if (isAlreadySelected)
            {
                indicator.style.backgroundColor = new Color(0.96f, 0.62f, 0.04f); // 橙色 - 当前字段已添加
                indicator.tooltip = "当前字段已添加";
            }
            else if (isProjectTag)
            {
                indicator.style.backgroundColor = new Color(0.06f, 0.73f, 0.51f); // 绿色 - 项目中其他地方使用
                indicator.tooltip = "项目中已使用";
            }
            else
            {
                indicator.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f); // 灰色 - 预设标签
                indicator.tooltip = "预设标签";
            }
            item.Add(indicator);

            var label = new Label(tag);
            label.style.flexGrow = 1;
            label.style.color = isAlreadySelected ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.86f, 0.86f, 0.86f);
            label.style.fontSize = 12;
            item.Add(label);

            if (isCurrentValue)
            {
                var checkmark = new Label("✓");
                checkmark.style.color = new Color(0.58f, 0.77f, 0.99f);
                item.Add(checkmark);
            }
            else if (isAlreadySelected)
            {
                var existsLabel = new Label("已添加");
                existsLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                existsLabel.style.fontSize = 10;
                item.Add(existsLabel);
            }

            return item;
        }

        private void OnCustomTagConfirm()
        {
            var customTag = _customTagField.value?.Trim();
            if (!string.IsNullOrEmpty(customTag))
            {
                _onSelect?.Invoke(customTag);
                Close();
            }
        }

        private string GetCategoryDisplayName(string category)
        {
            return category switch
            {
                "State" => "状态",
                "Ability" => "技能",
                "Damage" => "伤害",
                "Cue" => "表现",
                _ => category
            };
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
