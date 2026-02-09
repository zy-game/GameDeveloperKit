using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ZLinq;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// UI 模块，管理所有 UI 表单
    /// </summary>
    public sealed class UIModule : IModule, IUIManager
    {
        private Canvas _uiRoot;
        private CanvasScaler _canvasScaler;
        private RectTransform _safeAreaRect;
        private readonly Dictionary<EUILayer, UIGroup> _groups = new Dictionary<EUILayer, UIGroup>();
        private readonly UIStack _stack = new UIStack();
        private readonly Dictionary<string, IUIForm> _allForms = new Dictionary<string, IUIForm>();
        private readonly Dictionary<string, object> _loadingTasks = new Dictionary<string, object>();
        private Rect _lastSafeArea;

        /// <summary>
        /// UI 根节点 Canvas
        /// </summary>
        public Canvas UIRoot => _uiRoot;

        /// <summary>
        /// 导航栈
        /// </summary>
        public UIStack Stack => _stack;

        /// <summary>
        /// 模块初始化
        /// </summary>
        public void OnStartup()
        {
            CreateDefaultUIRoot();
            InitializeGroups();
        }

        /// <summary>
        /// 模块轮询
        /// </summary>
        public void OnUpdate(float elapseSeconds)
        {
            // 仅在 SafeArea 变化时更新
            var currentSafeArea = Screen.safeArea;
            if (currentSafeArea != _lastSafeArea)
            {
                UpdateSafeArea();
                UpdateAllFormsFullScreenBackground();
                _lastSafeArea = currentSafeArea;
            }
        }

        /// <summary>
        /// 更新所有UI的全屏背景
        /// </summary>
        private void UpdateAllFormsFullScreenBackground()
        {
            foreach (var form in _allForms.Values)
            {
                form.SetFullScreenBackground();
            }
        }

        /// <summary>
        /// 模块关闭
        /// </summary>
        public void OnClearup()
        {
            // 清理所有层级
            foreach (var group in _groups.Values)
            {
                group.Clearup();
            }

            _groups.Clear();
            _stack.Clear();
            _allForms.Clear();

            // 销毁 UIRoot
            if (_uiRoot != null && _uiRoot.gameObject != null)
            {
                UnityEngine.Object.Destroy(_uiRoot.gameObject);
                _uiRoot = null;
            }
        }

        #region UIRoot 管理

        /// <summary>
        /// 创建默认 UIRoot
        /// </summary>
        private void CreateDefaultUIRoot()
        {
            var rootObj = new GameObject("UIRoot");
            UnityEngine.Object.DontDestroyOnLoad(rootObj);

            // 添加 Canvas
            _uiRoot = rootObj.AddComponent<Canvas>();
            _uiRoot.renderMode = RenderMode.ScreenSpaceOverlay;
            _uiRoot.sortingOrder = 0;

            _uiRoot.gameObject.AddComponent<EventSystem>();
            _uiRoot.gameObject.AddComponent<StandaloneInputModule>();

            // 添加 CanvasScaler
            _canvasScaler = rootObj.AddComponent<CanvasScaler>();
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _canvasScaler.referenceResolution = new Vector2(1920, 1080);
            _canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _canvasScaler.matchWidthOrHeight = 0.5f;

            // 添加 GraphicRaycaster
            rootObj.AddComponent<GraphicRaycaster>();

            // 设置 SafeArea
            SetupSafeArea();
        }

        /// <summary>
        /// 设置 SafeArea（刘海屏适配）
        /// </summary>
        public void SetupSafeArea()
        {
            var safeAreaObj = new GameObject("SafeArea");
            safeAreaObj.transform.SetParent(_uiRoot.transform, false);

            _safeAreaRect = safeAreaObj.AddComponent<RectTransform>();
            _safeAreaRect.anchorMin = Vector2.zero;
            _safeAreaRect.anchorMax = Vector2.one;
            _safeAreaRect.sizeDelta = Vector2.zero;

            UpdateSafeArea();
        }

        /// <summary>
        /// 更新 SafeArea
        /// </summary>
        public void UpdateSafeArea()
        {
            if (_safeAreaRect == null)
            {
                return;
            }

            var safeArea = Screen.safeArea;
            var screenSize = new Vector2(Screen.width, Screen.height);

            // 计算 SafeArea 的 anchorMin 和 anchorMax
            var anchorMin = safeArea.position / screenSize;
            var anchorMax = (safeArea.position + safeArea.size) / screenSize;

            _safeAreaRect.anchorMin = anchorMin;
            _safeAreaRect.anchorMax = anchorMax;
        }

        /// <summary>
        /// 初始化层级组
        /// </summary>
        private void InitializeGroups()
        {
            _groups.Clear();

            // 为每个层级创建一个父节点
            var layers = Enum.GetValues(typeof(EUILayer));
            int baseSortingOrder = 0;

            foreach (EUILayer layer in layers)
            {
                var layerObj = new GameObject();
                var rectTransform = layerObj.AddComponent<RectTransform>();
                rectTransform.SetParent(_safeAreaRect ?? _uiRoot.transform, false);
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.sizeDelta = Vector2.zero;

                var group = new UIGroup(layer, rectTransform, baseSortingOrder);
                _groups[layer] = group;

                // 每个层级预留 100 个排序顺序空间
                baseSortingOrder += 100;
            }
        }

        #endregion

        #region 打开/关闭 UI

        /// <summary>
        /// 打开 UI（按类型）
        /// </summary>
        public async UniTask<T> OpenFormAsync<T>(params object[] args) where T : class, IUIForm, new()
        {
            // 使用Form类型名作为Key（支持多个Form共用同一个View）
            var formTypeName = typeof(T).FullName;
            
            // 检查是否已存在
            if (_allForms.TryGetValue(formTypeName, out var existingForm))
            {
                existingForm.Refresh(args);
                return (T)existingForm;
            }

            // 检查是否正在加载（防止重复加载）
            if (_loadingTasks.TryGetValue(formTypeName, out var loadingTask))
            {
                var result = await (UniTask<T>)loadingTask;
                return result;
            }

            // 创建加载任务
            var createTask = CreateFormInternalAsync<T>(formTypeName, args);
            _loadingTasks[formTypeName] = createTask;

            try
            {
                var form = await createTask;
                return form;
            }
            finally
            {
                _loadingTasks.Remove(formTypeName);
            }
        }

        /// <summary>
        /// 内部创建UI的实现（分离出来以支持并发控制）
        /// </summary>
        private async UniTask<T> CreateFormInternalAsync<T>(string formTypeName, object[] args) where T : class, IUIForm, new()
        {
            // 创建 UIFormBase 实例
            var form = Activator.CreateInstance<T>();

            // UIFormBase 自己负责资源加载和 GameObject 创建
            var success = await form.OnCreate(_uiRoot.transform, args);
            if (!success)
            {
                return default;
            }

            // 添加到对应层级
            var layer = form.Layer;
            if (_groups.TryGetValue(layer, out var group))
            {
                group.Add(form);
            }

            // 添加到字典（使用Form类型名作为Key）
            _allForms[formTypeName] = form;

            // 如果需要进入堆栈，压入
            if (form.ToStack)
            {
                _stack.Push(form);
            }

            // 处理显示模式
            HandleUIMode(form);

            return form;
        }

        /// <summary>
        /// 关闭 UI
        /// </summary>
        public void CloseForm<T>() where T : IUIForm
        {
            var targetType = typeof(T);
            var formTypeName = targetType.FullName;
            
            // 先尝试直接匹配
            if (_allForms.ContainsKey(formTypeName))
            {
                CloseFormByKey(formTypeName);
                return;
            }
            
            // 如果是接口类型，查找实现该接口的 Form
            if (targetType.IsInterface)
            {
                foreach (var kvp in _allForms)
                {
                    if (targetType.IsAssignableFrom(kvp.Value.GetType()))
                    {
                        CloseFormByKey(kvp.Key);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 关闭 UI（按类型名）
        /// </summary>
        public void CloseForm(string formTypeName)
        {
            CloseFormByKey(formTypeName);
        }
        
        /// <summary>
        /// 内部关闭UI实现
        /// </summary>
        private void CloseFormByKey(string key)
        {
            if (!_allForms.TryGetValue(key, out var form))
            {
                return;
            }

            _allForms.Remove(key);

            // 从栈中移除
            if (form.ToStack)
            {
                _stack.Remove(form);
            }

            // 从层级移除
            if (_groups.TryGetValue(form.Layer, out var group))
            {
                group.Remove(form);
            }

            // 清理
            form.Destory();
        }

        /// <summary>
        /// 检查 UI 是否打开
        /// </summary>
        public bool IsOpen<T>() where T : IUIForm
        {
            var formTypeName = typeof(T).FullName;
            return _allForms.ContainsKey(formTypeName);
        }

        /// <summary>
        /// 检查 UI 是否打开（按类型名）
        /// </summary>
        public bool IsOpen(string formTypeName)
        {
            return _allForms.ContainsKey(formTypeName);
        }

        /// <summary>
        /// 获取已打开的 UI（按类型）
        /// </summary>
        public T GetForm<T>() where T : IUIForm
        {
            var formTypeName = typeof(T).FullName;
            if (_allForms.TryGetValue(formTypeName, out var form))
            {
                return (T)form;
            }
            return default;
        }

        #endregion

        #region 层级控制

        /// <summary>
        /// 显示指定层级
        /// </summary>
        public void Show(params EUILayer[] layers)
        {
            foreach (var layer in layers)
            {
                if (_groups.TryGetValue(layer, out var group))
                {
                    group.Show();
                }
            }
        }

        /// <summary>
        /// 隐藏指定层级
        /// </summary>
        public void Hide(params EUILayer[] layers)
        {
            foreach (var layer in layers)
            {
                if (_groups.TryGetValue(layer, out var group))
                {
                    group.Hide();
                }
            }
        }

        /// <summary>
        /// 清理指定层级
        /// </summary>
        public void Clearup(params EUILayer[] layers)
        {
            foreach (var layer in layers)
            {
                if (_groups.TryGetValue(layer, out var group))
                {
                    // 移除该层级的所有 UI 从全局字典
                    var formNames = group.Forms.Keys.AsValueEnumerable().ToList();
                    foreach (var name in formNames)
                    {
                        _allForms.Remove(name);
                    }

                    group.Clearup();
                }
            }
        }

        #endregion

        #region 导航栈

        /// <summary>
        /// 返回到指定 UI
        /// </summary>
        public void BackTo<T>() where T : IUIForm
        {
            var poppedForms = _stack.BackTo<T>();

            // 关闭所有弹出的 UI
            foreach (var form in poppedForms)
            {
                // 查找Form对应的Key
                string keyToRemove = null;
                foreach (var kvp in _allForms)
                {
                    if (kvp.Value == form)
                    {
                        keyToRemove = kvp.Key;
                        break;
                    }
                }
                if (keyToRemove != null)
                {
                    CloseFormByKey(keyToRemove);
                }
            }
        }

        /// <summary>
        /// 返回上一个 UI
        /// </summary>
        public void Back()
        {
            var top = _stack.Pop();
            if (top != null)
            {
                // 查找Form对应的Key
                string keyToRemove = null;
                foreach (var kvp in _allForms)
                {
                    if (kvp.Value == top)
                    {
                        keyToRemove = kvp.Key;
                        break;
                    }
                }
                if (keyToRemove != null)
                {
                    CloseFormByKey(keyToRemove);
                }
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 处理 UI 显示模式
        /// </summary>
        private void HandleUIMode(IUIForm form)
        {
            switch (form.Mode)
            {
                case EUIMode.Normal:
                    // 正常模式，不做特殊处理
                    break;

                case EUIMode.HideOthers:
                    // 隐藏同层级其他 UI
                    if (_groups.TryGetValue(form.Layer, out var group))
                    {
                        foreach (var other in group.Forms.Values)
                        {
                            if (other != form && other.Status == UIStatus.Active)
                            {
                                other.Hide();
                            }
                        }
                    }

                    break;

                case EUIMode.HideLower:
                    // 隐藏低层级 UI
                    foreach (var kvp in _groups)
                    {
                        if (kvp.Key < form.Layer)
                        {
                            kvp.Value.Hide();
                        }
                    }

                    break;

                case EUIMode.Exclusive:
                    // 独占模式，隐藏所有其他 UI
                    foreach (var other in _allForms.Values)
                    {
                        if (other != form && other.Status == UIStatus.Active)
                        {
                            other.Hide();
                        }
                    }

                    break;
            }
        }

        #endregion

        #region 默认UI便捷方法

        /// <summary>
        /// 默认提示（调用CommonTips）
        /// </summary>
        public void DefaultNotice(string message, float duration = 2f)
        {
            OpenFormAsync<CommonTipsForm>(message, duration).Forget();
        }

        /// <summary>
        /// 默认弹窗（调用CommonDialog）
        /// </summary>
        public async UniTask<bool> DefaultDialogAsync(string title, string content,
            string confirmText = "确定", string cancelText = "取消", bool showCancel = true)
        {
            var form = await OpenFormAsync<CommonDialogForm>(title, content, confirmText, cancelText, showCancel);
            if (form == null) return false;
            return await form.WaitForResultAsync();
        }

        /// <summary>
        /// 默认信息弹窗（仅确认按钮）
        /// </summary>
        public async UniTask DefaultAlertAsync(string title, string content, string confirmText = "确定")
        {
            await DefaultDialogAsync(title, content, confirmText, "", false);
        }

        #endregion
    }
}