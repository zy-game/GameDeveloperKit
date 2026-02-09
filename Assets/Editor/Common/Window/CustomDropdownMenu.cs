using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor
{
    /// <summary>
    /// UIToolkit自定义下拉菜单
    /// 用于替代Unity的GenericMenu，支持自定义USS样式
    /// </summary>
    public class CustomDropdownMenu
    {
        private List<MenuItem> _items = new List<MenuItem>();
        private static VisualElement _currentOpenMenu = null; // 跟踪当前打开的菜单
        private static VisualElement _currentRoot = null; // 跟踪当前菜单的root
        private static EventCallback<MouseDownEvent> _currentClickCallback = null; // 跟踪当前的点击回调
        private static EventCallback<WheelEvent> _currentScrollCallback = null; // 跟踪当前的滚动回调
        private static EventCallback<FocusOutEvent> _currentFocusOutCallback = null; // 跟踪当前的焦点回调
        internal bool _keepOpenOnClick = false; // 是否在点击菜单项后保持打开（用于多选）
        
        private class MenuItem
        {
            public string Text { get; set; }
            public Action Callback { get; set; }
            public bool IsSeparator { get; set; }
            public bool IsChecked { get; set; }
            public bool IsDisabled { get; set; }
        }
        
        public void AddItem(string text, bool isChecked, Action callback)
        {
            _items.Add(new MenuItem 
            { 
                Text = text, 
                IsChecked = isChecked, 
                Callback = callback 
            });
        }
        
        public void AddDisabledItem(string text)
        {
            _items.Add(new MenuItem 
            { 
                Text = text, 
                IsDisabled = true 
            });
        }
        
        public void AddSeparator()
        {
            _items.Add(new MenuItem { IsSeparator = true });
        }
        
        /// <summary>
        /// 在按钮下方显示菜单
        /// </summary>
        public void ShowAsDropdown(VisualElement button, VisualElement root)
        {
            // 如果 root 为空，尝试使用 button 所在的 panel 的根节点
            if (root == null)
            {
                root = button.panel?.visualTree;
            }
            
            if (root == null)
            {
                Debug.LogError("CustomDropdownMenu: Root is null and cannot find panel root.");
                return;
            }

            // 获取按钮在root坐标系中的位置
            var buttonWorldPos = button.LocalToWorld(Vector2.zero);
            var buttonPosInRoot = root.WorldToLocal(buttonWorldPos);
            var menuPos = new Vector2(buttonPosInRoot.x, buttonPosInRoot.y + button.resolvedStyle.height);
            ShowAtPosition(menuPos, root);
        }
        
        /// <summary>
        /// 在鼠标位置显示菜单（右键菜单）
        /// </summary>
        public void ShowAsContext(VisualElement root)
        {
            // Event.current.mousePosition是相对于窗口的坐标
            var mousePos = Event.current.mousePosition;
            ShowAtPosition(mousePos, root);
        }
        
        /// <summary>
        /// 创建自定义多选下拉框（替代MaskField）
        /// </summary>
        public static VisualElement CreateMaskDropdown(string label, List<string> choices, int currentMask, Action<int> onValueChanged, VisualElement root)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.AddToClassList("custom-dropdown");
            
            // Label
            if (!string.IsNullOrEmpty(label))
            {
                var labelElement = new Label(label);
                labelElement.style.minWidth = 80;
                labelElement.style.color = new Color(0.7f, 0.7f, 0.7f);
                container.Add(labelElement);
            }
            
            // 值显示按钮（使用 VisualElement 避免 Button 内部 TextElement 占用空间）
            var button = new VisualElement();
            button.AddToClassList("custom-dropdown-button");
            button.focusable = true;
            // 在添加 class 之后设置样式，确保不被 CSS 覆盖
            button.style.flexGrow = 1;
            button.style.flexShrink = 1;
            button.style.flexDirection = FlexDirection.Row;
            button.style.justifyContent = Justify.SpaceBetween;
            button.style.alignItems = Align.Center;
            
            // 文本标签
            var textLabel = new Label();
            textLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            textLabel.style.overflow = Overflow.Hidden;
            textLabel.style.textOverflow = TextOverflow.Ellipsis;
            button.Add(textLabel);
            
            // 下拉箭头
            var arrow = new Label("▼");
            arrow.style.fontSize = 8;
            arrow.style.color = new Color(0.7f, 0.7f, 0.7f);
            arrow.style.flexShrink = 0;
            arrow.style.marginLeft = 4;
            arrow.style.minWidth = 8;
            button.Add(arrow);
            
            // 更新按钮文本
            void UpdateButtonText(int mask)
            {
                var selectedItems = new List<string>();
                for (int i = 0; i < choices.Count; i++)
                {
                    if ((mask & (1 << i)) != 0)
                    {
                        selectedItems.Add(choices[i]);
                    }
                }
                textLabel.text = selectedItems.Count > 0 ? string.Join(", ", selectedItems) : "无";
            }
            UpdateButtonText(currentMask);
            
            // 点击显示菜单
            button.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                
                var menu = new CustomDropdownMenu();
                menu._keepOpenOnClick = true; // 多选模式：点击不关闭菜单
                
                for (int i = 0; i < choices.Count; i++)
                {
                    var index = i; // 捕获变量
                    var isChecked = (currentMask & (1 << index)) != 0;
                    
                    menu.AddItem(choices[index], isChecked, () =>
                    {
                        // 切换选中状态
                        currentMask ^= (1 << index);
                        UpdateButtonText(currentMask);
                        onValueChanged?.Invoke(currentMask);
                        
                        // 重新创建菜单以更新选中状态显示
                        // （因为需要保持菜单打开，所以需要刷新）
                    });
                }
                
                menu.ShowAsDropdown(button, root);
            });
            
            container.Add(button);
            return container;
        }
        
        /// <summary>
        /// 创建自定义枚举下拉框（替代EnumField）
        /// </summary>
        public static VisualElement CreateEnumDropdown<T>(string label, T currentValue, Action<T> onValueChanged, VisualElement root) where T : System.Enum
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.AddToClassList("custom-dropdown");
            
            // Label
            if (!string.IsNullOrEmpty(label))
            {
                var labelElement = new Label(label);
                labelElement.style.minWidth = 80;
                labelElement.style.color = new Color(0.7f, 0.7f, 0.7f);
                container.Add(labelElement);
            }
            
            // 值显示按钮（使用 VisualElement 避免 Button 内部 TextElement 占用空间）
            var button = new VisualElement();
            button.AddToClassList("custom-dropdown-button");
            button.focusable = true;
            // 在添加 class 之后设置样式，确保不被 CSS 覆盖
            button.style.flexGrow = 1;
            button.style.flexShrink = 1;
            button.style.flexDirection = FlexDirection.Row;
            button.style.justifyContent = Justify.SpaceBetween;
            button.style.alignItems = Align.Center;
            
            // 文本标签
            var textLabel = new Label();
            textLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            textLabel.style.overflow = Overflow.Hidden;
            textLabel.style.textOverflow = TextOverflow.Ellipsis;
            button.Add(textLabel);
            
            // 下拉箭头
            var arrow = new Label("▼");
            arrow.style.fontSize = 8;
            arrow.style.color = new Color(0.7f, 0.7f, 0.7f);
            arrow.style.flexShrink = 0;
            arrow.style.marginLeft = 4;
            arrow.style.minWidth = 8;
            button.Add(arrow);
            
            // 更新按钮文本
            void UpdateButtonText(T value)
            {
                textLabel.text = value.ToString();
            }
            UpdateButtonText(currentValue);
            
            // 点击显示菜单
            button.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                
                var menu = new CustomDropdownMenu();
                var enumValues = System.Enum.GetValues(typeof(T));
                
                foreach (T enumValue in enumValues)
                {
                    var value = enumValue; // 捕获变量
                    var isChecked = System.Collections.Generic.EqualityComparer<T>.Default.Equals(value, currentValue);
                    
                    menu.AddItem(value.ToString(), isChecked, () =>
                    {
                        currentValue = value;
                        UpdateButtonText(value);
                        onValueChanged?.Invoke(value);
                    });
                }
                
                menu.ShowAsDropdown(button, root);
            });
            
            container.Add(button);
            return container;
        }
        
        private void ShowAtPosition(Vector2 position, VisualElement root)
        {
            // 强制设置root为相对定位
            if (root.style.position != Position.Relative)
            {
                root.style.position = Position.Relative;
            }
            
            // 计算可用的最大高度（从position到窗口底部的距离）
            var rootHeight = root.resolvedStyle.height;
            var availableHeight = rootHeight - position.y - 20; // 留20px底部边距
            var maxMenuHeight = Mathf.Max(150, availableHeight); // 至少150px高度
            
            // 创建菜单容器
            var container = new VisualElement();
            
            // 先设置基本样式（不包括position和left/top）
            container.style.backgroundColor = new Color(0.165f, 0.165f, 0.165f); // rgb(42, 42, 42)
            container.style.borderTopLeftRadius = 8;
            container.style.borderTopRightRadius = 8;
            container.style.borderBottomLeftRadius = 8;
            container.style.borderBottomRightRadius = 8;
            container.style.borderLeftWidth = 1;
            container.style.borderRightWidth = 1;
            container.style.borderTopWidth = 1;
            container.style.borderBottomWidth = 1;
            container.style.borderLeftColor = new Color(0.231f, 0.51f, 0.965f, 0.3f); // rgba(59, 130, 246, 0.3)
            container.style.borderRightColor = new Color(0.231f, 0.51f, 0.965f, 0.3f);
            container.style.borderTopColor = new Color(0.231f, 0.51f, 0.965f, 0.3f);
            container.style.borderBottomColor = new Color(0.231f, 0.51f, 0.965f, 0.3f);
            container.style.paddingLeft = 4;
            container.style.paddingRight = 4;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 4;
            container.style.minWidth = 200;
            container.style.maxHeight = maxMenuHeight; // 设置最大高度
            
            // 创建ScrollView用于菜单项列表
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.style.maxHeight = maxMenuHeight - 8; // 减去padding
            
            // 添加菜单项到ScrollView
            foreach (var item in _items)
            {
                if (item.IsSeparator)
                {
                    var separator = new VisualElement();
                    separator.AddToClassList("custom-menu-separator");
                    scrollView.Add(separator);
                }
                else
                {
                    var menuItem = CreateMenuItem(item, container, root);
                    scrollView.Add(menuItem);
                }
            }
            
            container.Add(scrollView);
            
            // 注意：不再需要StopPropagation，因为我们使用位置判断
            
            // 如果已有打开的菜单，先完全清理它
            if (_currentOpenMenu != null)
            {
                // 注销所有旧的事件监听器
                if (_currentRoot != null && _currentClickCallback != null)
                {
                    _currentRoot.UnregisterCallback<MouseDownEvent>(_currentClickCallback, TrickleDown.TrickleDown);
                    if (_currentScrollCallback != null)
                        _currentRoot.UnregisterCallback<WheelEvent>(_currentScrollCallback, TrickleDown.TrickleDown);
                    if (_currentFocusOutCallback != null)
                        _currentRoot.UnregisterCallback<FocusOutEvent>(_currentFocusOutCallback);
                }
                
                // 移除菜单元素
                if (_currentOpenMenu.parent != null)
                {
                    _currentOpenMenu.parent.Remove(_currentOpenMenu);
                }
                
                _currentOpenMenu = null;
                _currentRoot = null;
                _currentClickCallback = null;
                _currentScrollCallback = null;
                _currentFocusOutCallback = null;
            }
            
            // 添加到根元素
            root.Add(container);
            _currentOpenMenu = container; // 记录当前打开的菜单
            
            // 在下一帧设置position和位置（等待Unity完成初始布局）
            container.schedule.Execute(() =>
            {
                // 现在设置绝对定位和位置
                container.style.position = Position.Absolute;
                container.style.left = new StyleLength(new Length(position.x, LengthUnit.Pixel));
                container.style.top = new StyleLength(new Length(position.y, LengthUnit.Pixel));
                
                // 再等一帧检查是否生效
                container.schedule.Execute(() =>
                {
                    AdjustMenuPosition(container, root);
                }).ExecuteLater(0);
            }).ExecuteLater(0);
            
            // 点击外部关闭菜单 - 必须在TrickleDown阶段判断（因为ScrollView会StopPropagation）
            void OnRootClick(MouseDownEvent evt)
            {
                // 重要：检查这是否是当前活动的菜单
                if (_currentOpenMenu != container || container.parent == null)
                {
                    return;  // 不是当前菜单，忽略此事件
                }
                
                // 检查点击的目标元素
                var target = evt.target as VisualElement;
                
                // 检查目标是否是container或其子元素
                bool isInsideMenu = false;
                if (target != null)
                {
                    // 向上遍历父元素链，查找是否包含container
                    var current = target;
                    while (current != null)
                    {
                        if (current == container)
                        {
                            isInsideMenu = true;
                            break;
                        }
                        current = current.parent;
                    }
                }
                
                if (!isInsideMenu)
                {
                    // 点击在菜单外部，关闭菜单
                    CloseMenu(container, root, OnRootClick, OnScroll, OnFocusOut);
                }
            }
            
            // 关键：必须使用TrickleDown，因为ScrollView会阻止事件冒泡
            root.RegisterCallback<MouseDownEvent>(OnRootClick, TrickleDown.TrickleDown);
            
            // 监听滚动事件 - 只有在菜单外部滚动时才关闭菜单
            void OnScroll(WheelEvent evt)
            {
                // 检查是否是当前菜单
                if (_currentOpenMenu != container || container.parent == null)
                {
                    return;
                }
                
                // 如果滚动发生在菜单内部，不关闭菜单
                if (container.Contains((VisualElement)evt.target))
                {
                    return; // 允许菜单内部的ScrollView正常滚动
                }
                
                // 如果滚动发生在菜单外部，关闭菜单
                CloseMenu(container, root, OnRootClick, OnScroll, OnFocusOut);
            }
            root.RegisterCallback<WheelEvent>(OnScroll, TrickleDown.TrickleDown);
            
            // 监听焦点丢失事件 - 窗口失去焦点时关闭菜单
            void OnFocusOut(FocusOutEvent evt)
            {
                // 检查是否是当前菜单
                if (_currentOpenMenu != container)
                {
                    return;
                }
                
                // 检查焦点是否移动到了菜单内部的其他元素
                // relatedTarget是接收焦点的元素
                if (evt.relatedTarget != null)
                {
                    var relatedElement = evt.relatedTarget as VisualElement;
                    if (relatedElement != null)
                    {
                        // 向上遍历，检查relatedTarget是否在container内
                        var current = relatedElement;
                        while (current != null)
                        {
                            if (current == container)
                            {
                                return; // 焦点还在菜单内，不关闭
                            }
                            current = current.parent;
                        }
                    }
                }
                
                CloseMenu(container, root, OnRootClick, OnScroll, OnFocusOut);
            }
            root.RegisterCallback<FocusOutEvent>(OnFocusOut);
            
            // 保存当前菜单和回调的引用
            _currentRoot = root;
            _currentClickCallback = OnRootClick;
            _currentScrollCallback = OnScroll;
            _currentFocusOutCallback = OnFocusOut;
        }
        
        private void CloseMenu(VisualElement container, VisualElement root, 
            EventCallback<MouseDownEvent> clickCallback, 
            EventCallback<WheelEvent> scrollCallback,
            EventCallback<FocusOutEvent> focusOutCallback)
        {
            if (container.parent != null)
            {
                root.Remove(container);
                
                // 注销事件时必须使用与注册时相同的参数
                root.UnregisterCallback<MouseDownEvent>(clickCallback, TrickleDown.TrickleDown);
                root.UnregisterCallback<WheelEvent>(scrollCallback, TrickleDown.TrickleDown);
                root.UnregisterCallback<FocusOutEvent>(focusOutCallback);
                
                // 清理静态引用
                if (_currentOpenMenu == container)
                {
                    _currentOpenMenu = null;
                    _currentRoot = null;
                    _currentClickCallback = null;
                    _currentScrollCallback = null;
                    _currentFocusOutCallback = null;
                }
            }
        }
        
        private VisualElement CreateMenuItem(MenuItem item, VisualElement menuContainer, VisualElement root)
        {
            var itemElement = new VisualElement();
            itemElement.AddToClassList("custom-menu-item");
            
            if (item.IsDisabled)
            {
                itemElement.AddToClassList("custom-menu-item--disabled");
            }
            
            // 选中标记
            var checkmark = new Label(item.IsChecked ? "✓" : "");
            checkmark.AddToClassList("custom-menu-checkmark");
            itemElement.Add(checkmark);
            
            // 文本
            var text = new Label(item.Text);
            text.AddToClassList("custom-menu-text");
            itemElement.Add(text);
            
            // 点击事件
            if (!item.IsDisabled)
            {
                itemElement.RegisterCallback<MouseDownEvent>(evt =>
                {
                    item.Callback?.Invoke();
                    
                    // 如果不是保持打开模式，则关闭菜单
                    if (!_keepOpenOnClick)
                    {
                        // 必须注销事件监听器！
                        if (_currentOpenMenu == menuContainer && _currentRoot != null)
                        {
                            if (_currentClickCallback != null)
                                _currentRoot.UnregisterCallback<MouseDownEvent>(_currentClickCallback, TrickleDown.TrickleDown);
                            if (_currentScrollCallback != null)
                                _currentRoot.UnregisterCallback<WheelEvent>(_currentScrollCallback, TrickleDown.TrickleDown);
                            if (_currentFocusOutCallback != null)
                                _currentRoot.UnregisterCallback<FocusOutEvent>(_currentFocusOutCallback);
                            
                            _currentRoot = null;
                            _currentClickCallback = null;
                            _currentScrollCallback = null;
                            _currentFocusOutCallback = null;
                        }
                        
                        // 移除菜单元素
                        if (menuContainer.parent != null)
                        {
                            menuContainer.parent.Remove(menuContainer);
                        }
                        
                        if (_currentOpenMenu == menuContainer)
                        {
                            _currentOpenMenu = null;
                        }
                    }
                    else
                    {
                        // 多选模式：切换复选标记
                        item.IsChecked = !item.IsChecked;
                        checkmark.text = item.IsChecked ? "✓" : "";
                    }
                    
                    evt.StopPropagation();
                });
            }
            
            return itemElement;
        }
        
        private void AdjustMenuPosition(VisualElement menu, VisualElement root)
        {
            // 确保菜单不超出窗口边界
            var menuWidth = menu.resolvedStyle.width;
            var menuHeight = menu.resolvedStyle.height;
            var rootWidth = root.resolvedStyle.width;
            var rootHeight = root.resolvedStyle.height;
            
            var left = menu.resolvedStyle.left;
            var top = menu.resolvedStyle.top;
            
            if (left + menuWidth > rootWidth)
            {
                left = rootWidth - menuWidth - 10;
            }
            
            if (top + menuHeight > rootHeight)
            {
                top = rootHeight - menuHeight - 10;
            }
            
            menu.style.left = Mathf.Max(10, left);
            menu.style.top = Mathf.Max(10, top);
        }
    }
}
