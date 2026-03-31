using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// UI模块，提供UI窗口的管理、显示、隐藏、层级控制等功能
    /// </summary>
    public sealed partial class UIModule : IGameFrameworkLifecycleModule
    {
        private readonly Dictionary<UILayer, UIGroup> _groups = new();
        private readonly Dictionary<string, UIWindow> _windows = new(StringComparer.Ordinal);
        private readonly Dictionary<string, UIWindow> _cachedWindows = new(StringComparer.Ordinal);
        private readonly Dictionary<string, UniTask<UIWindow>> _loadingTasks = new(StringComparer.Ordinal);
        private readonly UIStack _stack = new();
        private readonly List<GameObject> _tips = new();

        private Canvas _uiRoot;
        private CanvasScaler _canvasScaler;
        private RectTransform _safeAreaRect;
        private UIModuleDriver _driver;
        private Rect _lastSafeArea;
        private bool _isInitialized;
        private GameObject _loadingOverlay;
        private Text _loadingText;
        private GameObject _dialogOverlay;
        private Text _dialogTitleText;
        private Text _dialogMessageText;
        private Button _dialogConfirmButton;
        private Text _dialogConfirmButtonText;
        private Button _dialogCancelButton;
        private Text _dialogCancelButtonText;
        private Action _dialogConfirmAction;
        private Action _dialogCancelAction;
        private GameObject _tipsPrefab;

        /// <summary>
        /// 初始化 UIModule 的新实例。
        /// </summary>
        public UIModule()
        {
            CreateRoot();
            InitializeGroups();
        }

        /// <summary>
        /// 获取UI根画布
        /// </summary>
        public Canvas UIRoot => _uiRoot;

        /// <summary>
        /// 获取安全区域根节点
        /// </summary>
        public RectTransform SafeAreaRoot => _safeAreaRect;

        /// <summary>
        /// 获取UI窗口栈
        /// </summary>
        public UIStack Stack => _stack;

        /// <summary>
        /// 获取已打开的窗口数量
        /// </summary>
        public int OpenCount => _windows.Count;

        /// <summary>
        /// 获取已缓存的窗口数量
        /// </summary>
        public int CachedCount => _cachedWindows.Count;

        /// <summary>
        /// 获取模块状态
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// UI窗口打开事件
        /// </summary>
        public event Action<UIWindow> Opened;

        /// <summary>
        /// UI窗口关闭事件
        /// </summary>
        public event Action<UIWindow> Closed;

        /// <summary>
        /// 获取是否显示加载界面
        /// </summary>
        public bool IsLoadingVisible => _loadingOverlay != null && _loadingOverlay.activeSelf;

        /// <summary>
        /// 获取是否显示对话框
        /// </summary>
        public bool IsDialogVisible => _dialogOverlay != null && _dialogOverlay.activeSelf;

        /// <summary>
        /// 异步初始化UI模块
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
            {
                return UniTask.CompletedTask;
            }

            try
            {
                _isInitialized = true;
                return UniTask.CompletedTask;
            }
            catch
            {
                _isInitialized = false;
                throw;
            }
        }

        /// <summary>
        /// 异步关闭UI模块
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public UniTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
            {
                return UniTask.CompletedTask;
            }

            Dispose();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 异步打开UI窗口
        /// </summary>
        /// <typeparam name="TWindow">窗口类型</typeparam>
        /// <param name="args">传递给窗口的参数</param>
        /// <returns>窗口实例的异步任务</returns>
        public UniTask<TWindow> OpenAsync<TWindow>(params object[] args)
            where TWindow : UIWindow, new()
        {
            return OpenAsync<TWindow>(CancellationToken.None, args);
        }

        /// <summary>
        /// 异步打开UI窗口
        /// </summary>
        /// <typeparam name="TWindow">窗口类型</typeparam>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="args">传递给窗口的参数</param>
        /// <returns>窗口实例的异步任务</returns>
        public async UniTask<TWindow> OpenAsync<TWindow>(CancellationToken cancellationToken, params object[] args)
            where TWindow : UIWindow, new()
        {
            var metadata = new TWindow();
            var key = GetWindowKey<TWindow>();
            if (_windows.TryGetValue(key, out var existingWindow))
            {
                var handledWindow = HandleExistingWindow(existingWindow, metadata.OpenStrategy, args);
                if (handledWindow != null)
                {
                    return (TWindow)handledWindow;
                }
            }

            if (_cachedWindows.TryGetValue(key, out var cachedWindow))
            {
                if (metadata.OpenStrategy == UIOpenStrategy.Recreate)
                {
                    _cachedWindows.Remove(key);
                    cachedWindow.DestroyInternal();
                }
                else
                {
                    _cachedWindows.Remove(key);
                    ReuseCachedWindow(cachedWindow, args, metadata.OpenStrategy);
                    return (TWindow)cachedWindow;
                }
            }

            if (_loadingTasks.TryGetValue(key, out var existingTask))
            {
                return (TWindow)await existingTask;
            }

            var createTask = CreateWindowAsync<TWindow>(cancellationToken, args);
            _loadingTasks[key] = WrapCreateTask(createTask);

            try
            {
                return await createTask;
            }
            finally
            {
                _loadingTasks.Remove(key);
            }
        }

        /// <summary>
        /// 关闭指定类型的UI窗口
        /// </summary>
        /// <typeparam name="TWindow">窗口类型</typeparam>
        public void Close<TWindow>()
            where TWindow : UIWindow
        {
            Close(GetWindowKey<TWindow>());
        }

        /// <summary>
        /// 通过窗口键关闭UI窗口
        /// </summary>
        /// <param name="windowKey">窗口键</param>
        /// <returns>如果成功关闭返回true，否则返回false</returns>
        public bool Close(string windowKey)
        {
            return !string.IsNullOrWhiteSpace(windowKey)
                && _windows.TryGetValue(windowKey, out var window)
                && Close(window);
        }

        /// <summary>
        /// 关闭指定的UI窗口
        /// </summary>
        /// <param name="window">窗口实例</param>
        /// <returns>如果成功关闭返回true，否则返回false</returns>
        public bool Close(UIWindow window)
        {
            if (window == null || !_windows.Remove(window.WindowKey))
            {
                return false;
            }

            _stack.Remove(window);
            if (_groups.TryGetValue(window.Layer, out var group))
            {
                group.Remove(window);
            }

            if (window.CacheOnClose)
            {
                window.Hide();
                _cachedWindows[window.WindowKey] = window;
                Closed?.Invoke(window);
                return true;
            }

            window.DestroyInternal();
            Closed?.Invoke(window);
            return true;
        }

        /// <summary>
        /// 检查指定类型的窗口是否已打开
        /// </summary>
        /// <typeparam name="TWindow">窗口类型</typeparam>
        /// <returns>如果已打开返回true，否则返回false</returns>
        public bool IsOpen<TWindow>()
            where TWindow : UIWindow
        {
            return _windows.ContainsKey(GetWindowKey<TWindow>());
        }

        /// <summary>
        /// 获取已打开的UI窗口
        /// </summary>
        /// <typeparam name="TWindow">窗口类型</typeparam>
        /// <returns>窗口实例</returns>
        /// <exception cref="InvalidOperationException">当窗口未打开时抛出</exception>
        public TWindow Get<TWindow>()
            where TWindow : UIWindow
        {
            var key = GetWindowKey<TWindow>();
            if (!_windows.TryGetValue(key, out var window))
            {
                throw new InvalidOperationException($"UI window '{key}' is not open.");
            }

            return (TWindow)window;
        }

        /// <summary>
        /// 尝试获取已打开的UI窗口
        /// </summary>
        /// <typeparam name="TWindow">窗口类型</typeparam>
        /// <param name="window">输出的窗口实例</param>
        /// <returns>如果获取成功返回true，否则返回false</returns>
        public bool TryGet<TWindow>(out TWindow window)
            where TWindow : UIWindow
        {
            if (_windows.TryGetValue(GetWindowKey<TWindow>(), out var existingWindow))
            {
                window = (TWindow)existingWindow;
                return true;
            }

            window = null;
            return false;
        }

        /// <summary>
        /// 显示指定层级的UI窗口
        /// </summary>
        /// <param name="layers">要显示的层级数组</param>
        public void Show(params UILayer[] layers)
        {
            for (var i = 0; i < layers.Length; i++)
            {
                if (_groups.TryGetValue(layers[i], out var group))
                {
                    group.Show();
                }
            }
        }

        /// <summary>
        /// 隐藏指定层级的UI窗口
        /// </summary>
        /// <param name="layers">要隐藏的层级数组</param>
        public void Hide(params UILayer[] layers)
        {
            for (var i = 0; i < layers.Length; i++)
            {
                if (_groups.TryGetValue(layers[i], out var group))
                {
                    group.Hide();
                }
            }
        }

        /// <summary>
        /// 返回到上一个UI窗口
        /// </summary>
        public void Back()
        {
            var window = _stack.Pop();
            if (window != null)
            {
                Close(window);
            }
        }

        /// <summary>
        /// 返回到指定类型的UI窗口
        /// </summary>
        /// <typeparam name="TWindow">窗口类型</typeparam>
        public void BackTo<TWindow>()
            where TWindow : UIWindow
        {
            var windows = _stack.PopUntil<TWindow>();
            for (var i = 0; i < windows.Count; i++)
            {
                Close(windows[i]);
            }
        }

        /// <summary>
        /// 显示加载界面
        /// </summary>
        /// <param name="message">加载消息</param>
        public void ShowLoading(string message = "Loading...")
        {
            EnsureLoadingOverlay();
            _loadingText.text = string.IsNullOrWhiteSpace(message) ? "Loading..." : message;
            _loadingOverlay.SetActive(true);
            _loadingOverlay.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 隐藏加载界面
        /// </summary>
        public void HideLoading()
        {
            if (_loadingOverlay != null)
            {
                _loadingOverlay.SetActive(false);
            }
        }

        /// <summary>
        /// 显示对话框
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <param name="message">对话框消息</param>
        /// <param name="confirmText">确认按钮文本</param>
        /// <param name="onConfirm">确认回调</param>
        /// <param name="cancelText">取消按钮文本</param>
        /// <param name="onCancel">取消回调</param>
        public void ShowDialog(
            string title,
            string message,
            string confirmText = "OK",
            Action onConfirm = null,
            string cancelText = null,
            Action onCancel = null)
        {
            EnsureDialogOverlay();

            _dialogTitleText.text = string.IsNullOrWhiteSpace(title) ? "Dialog" : title;
            _dialogMessageText.text = message ?? string.Empty;
            _dialogConfirmButtonText.text = string.IsNullOrWhiteSpace(confirmText) ? "OK" : confirmText;
            _dialogConfirmAction = onConfirm;
            _dialogCancelAction = onCancel;

            var showCancel = !string.IsNullOrWhiteSpace(cancelText) || onCancel != null;
            _dialogCancelButton.gameObject.SetActive(showCancel);
            _dialogCancelButtonText.text = string.IsNullOrWhiteSpace(cancelText) ? "Cancel" : cancelText;

            _dialogOverlay.SetActive(true);
            _dialogOverlay.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 隐藏对话框
        /// </summary>
        public void HideDialog()
        {
            if (_dialogOverlay == null)
            {
                return;
            }

            _dialogConfirmAction = null;
            _dialogCancelAction = null;
            _dialogOverlay.SetActive(false);
        }

        /// <summary>
        /// 显示提示信息
        /// </summary>
        /// <param name="message">提示消息</param>
        /// <param name="durationSeconds">显示持续时间（秒）</param>
        public void ShowTips(string message, float durationSeconds = 2f)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            EnsureTipsPrefab();
            var tip = Game.Pool.Spawn(_tipsPrefab, GetOrCreateGroup(UILayer.Popup).Root);
            var tipView = tip.GetComponent<BuiltinTipsView>();
            tipView.SetMessage(message);
            _tips.Add(tip);
            UpdateTipsLayout();
            AutoHideTipAsync(tip, durationSeconds <= 0f ? 2f : durationSeconds, _driver.GetCancellationTokenOnDestroy()).ForgetWithDiagnostics("UIModule.AutoHideTipFailed", nameof(UIModule), nameof(UIModule));
        }

        /// <summary>
        /// 释放UI模块占用的所有资源
        /// </summary>
        public void Dispose()
        {
            var windows = new List<UIWindow>(_windows.Values);
            for (var i = 0; i < windows.Count; i++)
            {
                Close(windows[i]);
            }

            var cachedWindows = new List<UIWindow>(_cachedWindows.Values);
            for (var i = 0; i < cachedWindows.Count; i++)
            {
                cachedWindows[i].DestroyInternal();
            }

            _windows.Clear();
            _cachedWindows.Clear();
            _loadingTasks.Clear();
            _stack.Clear();
            _groups.Clear();
            for (var i = _tips.Count - 1; i >= 0; i--)
            {
                ReleaseTip(_tips[i]);
            }

            _tips.Clear();
            Opened = null;
            Closed = null;
            _isInitialized = false;

            if (_driver != null)
            {
                UnityEngine.Object.Destroy(_driver.gameObject);
                _driver = null;
            }

            if (_tipsPrefab != null)
            {
                UnityEngine.Object.Destroy(_tipsPrefab);
                _tipsPrefab = null;
            }
        }

        private async UniTask<TWindow> CreateWindowAsync<TWindow>(CancellationToken cancellationToken, params object[] args)
            where TWindow : UIWindow, new()
        {
            var window = new TWindow();
            var group = GetOrCreateGroup(window.Layer);
            await window.CreateAsync(this, group.Root, cancellationToken, args);
            window.ApplySortingOrder(group.ReserveSortingOrder(window.SortingOrder));

            group.Add(window);
            _windows.Add(window.WindowKey, window);

            if (window.ToStack)
            {
                _stack.Push(window);
            }

            HandleWindowMode(window);
            Opened?.Invoke(window);
            return window;
        }

        private void ReuseCachedWindow(UIWindow window, params object[] args)
        {
            ReuseCachedWindow(window, UIOpenStrategy.RefreshExisting, args);
        }

        private void ReuseCachedWindow(UIWindow window, UIOpenStrategy openStrategy, params object[] args)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            var group = GetOrCreateGroup(window.Layer);
            group.Add(window);
            window.ApplySortingOrder(group.ReserveSortingOrder(window.SortingOrder));
            _windows[window.WindowKey] = window;

            if (window.ToStack)
            {
                _stack.Push(window);
            }

            if (openStrategy == UIOpenStrategy.RefreshExisting)
            {
                window.Refresh(args);
            }

            window.Show();
            HandleWindowMode(window);
            Opened?.Invoke(window);
        }

        private UIWindow HandleExistingWindow(UIWindow window, UIOpenStrategy openStrategy, params object[] args)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            if (openStrategy == UIOpenStrategy.Recreate)
            {
                Close(window);
                return null;
            }

            if (openStrategy == UIOpenStrategy.RefreshExisting)
            {
                window.Refresh(args);
            }

            window.Show();
            return window;
        }

        private async UniTask<UIWindow> WrapCreateTask<TWindow>(UniTask<TWindow> createTask)
            where TWindow : UIWindow
        {
            return await createTask;
        }

        private void CreateRoot()
        {
            var rootObject = new GameObject("[GameDeveloperKit.UI]");
            UnityEngine.Object.DontDestroyOnLoad(rootObject);

            _uiRoot = rootObject.AddComponent<Canvas>();
            _uiRoot.renderMode = RenderMode.ScreenSpaceOverlay;
            _uiRoot.sortingOrder = 0;

            _canvasScaler = rootObject.AddComponent<CanvasScaler>();
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            _canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _canvasScaler.matchWidthOrHeight = 0.5f;

            rootObject.AddComponent<GraphicRaycaster>();

            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystemObject = new GameObject("[GameDeveloperKit.EventSystem]");
                UnityEngine.Object.DontDestroyOnLoad(eventSystemObject);
                eventSystemObject.AddComponent<EventSystem>();
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }

            var safeAreaObject = new GameObject("SafeArea");
            _safeAreaRect = safeAreaObject.AddComponent<RectTransform>();
            _safeAreaRect.SetParent(_uiRoot.transform, false);
            _safeAreaRect.anchorMin = Vector2.zero;
            _safeAreaRect.anchorMax = Vector2.one;
            _safeAreaRect.offsetMin = Vector2.zero;
            _safeAreaRect.offsetMax = Vector2.zero;

            _driver = rootObject.AddComponent<UIModuleDriver>();
            _driver.Initialize(this);
            UpdateSafeArea(true);
        }

        private void InitializeGroups()
        {
            _groups.Clear();

            var baseSortingOrder = 0;
            var layers = (UILayer[])Enum.GetValues(typeof(UILayer));
            for (var i = 0; i < layers.Length; i++)
            {
                var layerObject = new GameObject(layers[i].ToString());
                var rectTransform = layerObject.AddComponent<RectTransform>();
                rectTransform.SetParent(_safeAreaRect, false);
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;

                _groups[layers[i]] = new UIGroup(layers[i], rectTransform, baseSortingOrder);
                baseSortingOrder += 100;
            }
        }

        private UIGroup GetOrCreateGroup(UILayer layer)
        {
            if (_groups.TryGetValue(layer, out var group))
            {
                return group;
            }

            var layerObject = new GameObject(layer.ToString());
            var rectTransform = layerObject.AddComponent<RectTransform>();
            rectTransform.SetParent(_safeAreaRect, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            group = new UIGroup(layer, rectTransform, _groups.Count * 100);
            _groups.Add(layer, group);
            return group;
        }

        private void HandleWindowMode(UIWindow window)
        {
            switch (window.Mode)
            {
                case UIMode.HideOthers:
                    if (_groups.TryGetValue(window.Layer, out var group))
                    {
                        foreach (var otherWindow in group.Windows.Values)
                        {
                            if (otherWindow != window && otherWindow.Status == UIStatus.Active)
                            {
                                otherWindow.Hide();
                            }
                        }
                    }

                    break;
                case UIMode.HideLower:
                    foreach (var pair in _groups)
                    {
                        if (pair.Key < window.Layer)
                        {
                            pair.Value.Hide();
                        }
                    }

                    break;
                case UIMode.Exclusive:
                    foreach (var openedWindow in _windows.Values)
                    {
                        if (openedWindow != window && openedWindow.Status == UIStatus.Active)
                        {
                            openedWindow.Hide();
                        }
                    }

                    break;
            }
        }

        private void UpdateSafeAreaIfNeeded()
        {
            if (Screen.safeArea != _lastSafeArea)
            {
                UpdateSafeArea(false);
            }
        }

        private void UpdateSafeArea(bool force)
        {
            var safeArea = Screen.safeArea;
            if (!force && safeArea == _lastSafeArea)
            {
                return;
            }

            _lastSafeArea = safeArea;

            var screenSize = new Vector2(Screen.width, Screen.height);
            if (screenSize.x <= 0f || screenSize.y <= 0f)
            {
                return;
            }

            _safeAreaRect.anchorMin = safeArea.position / screenSize;
            _safeAreaRect.anchorMax = (safeArea.position + safeArea.size) / screenSize;

            foreach (var window in _windows.Values)
            {
                window.UpdateFullScreenBackground();
            }
        }

        private void EnsureLoadingOverlay()
        {
            if (_loadingOverlay != null)
            {
                return;
            }

            _loadingOverlay = CreateFullscreenOverlay("BuiltinLoading", UILayer.Overlay, new Color(0f, 0f, 0f, 0.65f));
            _loadingText = CreateText("Message", (RectTransform)_loadingOverlay.transform, "Loading...", 32, TextAnchor.MiddleCenter);
            Stretch(_loadingText.rectTransform);
        }

        private void EnsureDialogOverlay()
        {
            if (_dialogOverlay != null)
            {
                return;
            }

            _dialogOverlay = CreateFullscreenOverlay("BuiltinDialog", UILayer.System, new Color(0f, 0f, 0f, 0.7f));

            var panel = CreatePanel("Panel", (RectTransform)_dialogOverlay.transform, new Vector2(480f, 260f), new Color(0.12f, 0.12f, 0.12f, 0.96f));
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;

            _dialogTitleText = CreateText("Title", panel, "Dialog", 28, TextAnchor.UpperCenter);
            _dialogTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _dialogTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _dialogTitleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _dialogTitleText.rectTransform.anchoredPosition = new Vector2(0f, -20f);
            _dialogTitleText.rectTransform.sizeDelta = new Vector2(-40f, 40f);

            _dialogMessageText = CreateText("Message", panel, string.Empty, 24, TextAnchor.UpperLeft);
            _dialogMessageText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _dialogMessageText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _dialogMessageText.rectTransform.offsetMin = new Vector2(24f, 80f);
            _dialogMessageText.rectTransform.offsetMax = new Vector2(-24f, -70f);

            _dialogConfirmButton = CreateButton("ConfirmButton", panel, new Vector2(150f, 46f), new Vector2(-84f, 28f), OnDialogConfirmClicked);
            _dialogConfirmButtonText = CreateButtonLabel(_dialogConfirmButton, "OK");

            _dialogCancelButton = CreateButton("CancelButton", panel, new Vector2(150f, 46f), new Vector2(84f, 28f), OnDialogCancelClicked);
            _dialogCancelButtonText = CreateButtonLabel(_dialogCancelButton, "Cancel");

            _dialogOverlay.SetActive(false);
        }

        private void EnsureTipsPrefab()
        {
            if (_tipsPrefab != null)
            {
                return;
            }

            _tipsPrefab = new GameObject("BuiltinTips", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(BuiltinTipsView));
            _tipsPrefab.SetActive(false);

            var rectTransform = _tipsPrefab.GetComponent<RectTransform>();
            rectTransform.SetParent(_uiRoot.transform, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(460f, 72f);

            var canvasGroup = _tipsPrefab.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var image = _tipsPrefab.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.78f);

            var text = CreateText("Message", rectTransform, string.Empty, 24, TextAnchor.MiddleCenter);
            text.rectTransform.offsetMin = new Vector2(20f, 12f);
            text.rectTransform.offsetMax = new Vector2(-20f, -12f);

            var tipView = _tipsPrefab.GetComponent<BuiltinTipsView>();
            tipView.Initialize(canvasGroup, image, text);

            Game.Pool.SetCapacity(_tipsPrefab, 16);
            Game.Pool.Warmup(_tipsPrefab, 2);
        }

        private async UniTask AutoHideTipAsync(GameObject tip, float durationSeconds, CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(durationSeconds), cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (tip != null)
                {
                    _tips.Remove(tip);
                    ReleaseTip(tip);
                    UpdateTipsLayout();
                }
            }
        }

        private void UpdateTipsLayout()
        {
            for (var i = 0; i < _tips.Count; i++)
            {
                if (_tips[i] == null)
                {
                    continue;
                }

                var rectTransform = _tips[i].GetComponent<RectTransform>();
                rectTransform.anchoredPosition = new Vector2(0f, 120f - (i * 84f));
            }
        }

        private void ReleaseTip(GameObject tip)
        {
            if (tip == null)
            {
                return;
            }

            if (!Game.HasModule<PoolModule>() || !Game.Pool.Despawn(tip))
            {
                UnityEngine.Object.Destroy(tip);
            }
        }

        private void OnDialogConfirmClicked()
        {
            var callback = _dialogConfirmAction;
            HideDialog();
            callback?.Invoke();
        }

        private void OnDialogCancelClicked()
        {
            var callback = _dialogCancelAction;
            HideDialog();
            callback?.Invoke();
        }

        private GameObject CreateFullscreenOverlay(string name, UILayer layer, Color color)
        {
            var overlay = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rectTransform = overlay.GetComponent<RectTransform>();
            rectTransform.SetParent(GetOrCreateGroup(layer).Root, false);
            Stretch(rectTransform);

            var image = overlay.GetComponent<Image>();
            image.color = color;
            return overlay;
        }

        private static RectTransform CreatePanel(string name, RectTransform parent, Vector2 size, Color color)
        {
            var panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rectTransform = panelObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.sizeDelta = size;

            var image = panelObject.GetComponent<Image>();
            image.color = color;
            return rectTransform;
        }

        private static Text CreateText(string name, RectTransform parent, string text, int fontSize, TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            Stretch(rectTransform);

            var label = textObject.GetComponent<Text>();
            label.font = LoadDefaultFont();
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = text ?? string.Empty;
            return label;
        }

        private static Button CreateButton(string name, RectTransform parent, Vector2 size, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rectTransform.anchorMax = new Vector2(0.5f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.24f, 0.45f, 0.85f, 1f);

            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            return button;
        }

        private static Text CreateButtonLabel(Button button, string text)
        {
            var label = CreateText("Label", (RectTransform)button.transform, text, 22, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            return label;
        }

        private static Font LoadDefaultFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static string GetWindowKey<TWindow>()
            where TWindow : UIWindow
        {
            return typeof(TWindow).FullName ?? typeof(TWindow).Name;
        }
    }
}

