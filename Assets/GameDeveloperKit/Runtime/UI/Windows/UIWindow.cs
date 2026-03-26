using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// UI窗口基类，提供UI窗口的生命周期管理和显示控制功能
    /// </summary>
    public abstract class UIWindow : IDisposable
    {
        private UIModule _module;
        private GameObject _gameObject;
        private Canvas _canvas;
        private Animator _animator;
        private UIDocument _document;
        private IUIDesign _design;
        private UIDataBase _data;
        private UIStatus _status = UIStatus.Closed;

        /// <summary>
        /// 获取窗口键
        /// </summary>
        public string WindowKey => GetType().FullName ?? GetType().Name;

        /// <summary>
        /// 获取窗口游戏对象
        /// </summary>
        public GameObject GameObject => _gameObject;

        /// <summary>
        /// 获取窗口变换组件
        /// </summary>
        public Transform Transform => _gameObject == null ? null : _gameObject.transform;

        /// <summary>
        /// 获取画布组件
        /// </summary>
        public Canvas Canvas => _canvas;

        /// <summary>
        /// 获取动画组件
        /// </summary>
        public Animator Animator => _animator;

        /// <summary>
        /// 获取UI文档
        /// </summary>
        public UIDocument Document => _document;

        /// <summary>
        /// 获取窗口状态
        /// </summary>
        public UIStatus Status => _status;

        /// <summary>
        /// 获取是否已创建
        /// </summary>
        public bool IsCreated => _gameObject != null && _status != UIStatus.Closed;

        /// <summary>
        /// 获取是否可见
        /// </summary>
        public bool IsVisible => _status == UIStatus.Active || _status == UIStatus.Opening;

        /// <summary>
        /// 获取窗口层级
        /// </summary>
        public UILayer Layer => Metadata.Layer;

        /// <summary>
        /// 获取窗口模式
        /// </summary>
        public UIMode Mode => Metadata.Mode;

        /// <summary>
        /// 获取是否加入栈
        /// </summary>
        public bool ToStack => Metadata.ToStack;

        /// <summary>
        /// 获取排序顺序
        /// </summary>
        public int SortingOrder => Metadata.SortingOrder;

        /// <summary>
        /// 获取关闭时是否缓存
        /// </summary>
        public bool CacheOnClose => Metadata.CacheOnClose;

        /// <summary>
        /// 获取打开策略
        /// </summary>
        public UIOpenStrategy OpenStrategy => Metadata.OpenStrategy;

        /// <summary>
        /// 窗口创建事件
        /// </summary>
        public event Action<UIWindow> Created;

        /// <summary>
        /// 窗口刷新事件
        /// </summary>
        public event Action<UIWindow> Refreshed;

        /// <summary>
        /// 窗口显示事件
        /// </summary>
        public event Action<UIWindow> Shown;

        /// <summary>
        /// 窗口隐藏事件
        /// </summary>
        public event Action<UIWindow> Hidden;

        /// <summary>
        /// 窗口关闭中事件
        /// </summary>
        public event Action<UIWindow> Closing;

        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        public event Action<UIWindow> Closed;

        /// <summary>
        /// 异步创建窗口
        /// </summary>
        /// <param name="module">UI模块</param>
        /// <param name="parent">父节点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="args">传递给窗口的参数</param>
        /// <returns>异步任务</returns>
        internal async UniTask CreateAsync(UIModule module, Transform parent, CancellationToken cancellationToken, params object[] args)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
            _status = UIStatus.Loading;

            cancellationToken.ThrowIfCancellationRequested();
            ValidateMetadata();

            var assetHandle = await Game.Resource.Provider.LoadAssetAsync<GameObject>(Metadata.AssetPath, cancellationToken);
            try
            {
                _gameObject = await assetHandle.InstantiateAsync(parent, false, cancellationToken);
            }
            finally
            {
                assetHandle.Release();
            }

            if (_gameObject == null)
            {
                throw new InvalidOperationException($"Failed to instantiate UI window '{WindowKey}'.");
            }

            _gameObject.name = GetType().Name;
            _animator = _gameObject.GetComponent<Animator>();
            _canvas = _gameObject.GetComponent<Canvas>();
            _document = _gameObject.GetComponent<UIDocument>();
            ValidateDocumentMetadata();

            if (_canvas == null)
            {
                _canvas = _gameObject.AddComponent<Canvas>();
                _canvas.overrideSorting = true;
                _gameObject.AddComponent<GraphicRaycaster>();
            }

            _data = CreateData();
            _data?.OnInitialize();

            _design = CreateDesign();
            _design?.Load(_gameObject);

            ChangeStatus(UIStatus.Opening);
            OnCreate(args);
            Created?.Invoke(this);
            Show();
        }

        /// <summary>
        /// 刷新窗口
        /// </summary>
        /// <param name="args">传递给窗口的参数</param>
        public void Refresh(params object[] args)
        {
            EnsureCreated();
            OnRefresh(args);
            Refreshed?.Invoke(this);
        }

        /// <summary>
        /// 显示窗口
        /// </summary>
        public void Show()
        {
            if (_gameObject == null || _status == UIStatus.Active)
            {
                return;
            }

            ChangeStatus(UIStatus.Opening);
            _gameObject.SetActive(true);
            UpdateFullScreenBackground();
            OnShow();
            ChangeStatus(UIStatus.Active);
            Shown?.Invoke(this);
        }

        /// <summary>
        /// 隐藏窗口
        /// </summary>
        public void Hide()
        {
            if (_gameObject == null || _status != UIStatus.Active)
            {
                return;
            }

            ChangeStatus(UIStatus.Closing);
            OnHide();
            _gameObject.SetActive(false);
            ChangeStatus(UIStatus.Paused);
            Hidden?.Invoke(this);
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        public void Close()
        {
            _module?.Close(this);
        }

        /// <summary>
        /// 获取UI设计
        /// </summary>
        /// <typeparam name="TDesign">设计类型</typeparam>
        /// <returns>设计实例</returns>
        public TDesign GetDesign<TDesign>()
            where TDesign : class, IUIDesign
        {
            return _design as TDesign;
        }

        /// <summary>
        /// 获取UI数据
        /// </summary>
        /// <typeparam name="TData">数据类型</typeparam>
        /// <returns>数据实例</returns>
        public TData GetData<TData>()
            where TData : UIDataBase
        {
            return _data as TData;
        }

        /// <summary>
        /// 应用排序顺序
        /// </summary>
        /// <param name="sortingOrder">排序顺序</param>
        internal void ApplySortingOrder(int sortingOrder)
        {
            if (_canvas == null)
            {
                return;
            }

            _canvas.overrideSorting = true;
            _canvas.sortingOrder = sortingOrder;
        }

        /// <summary>
        /// 更新全屏背景
        /// </summary>
        internal void UpdateFullScreenBackground()
        {
            if (_document?.FullScreenBackground == null || _canvas == null)
            {
                return;
            }

            var safeArea = Screen.safeArea;
            var screenSize = new Vector2(Screen.width, Screen.height);
            var rootCanvas = _canvas.rootCanvas;
            var canvasScaler = rootCanvas == null ? null : rootCanvas.GetComponent<CanvasScaler>();

            var scaleFactor = rootCanvas == null ? 1f : rootCanvas.scaleFactor;
            if (canvasScaler != null && canvasScaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                var refResolution = canvasScaler.referenceResolution;
                var logWidth = Mathf.Log(screenSize.x / refResolution.x, 2f);
                var logHeight = Mathf.Log(screenSize.y / refResolution.y, 2f);
                var logScale = Mathf.Lerp(logWidth, logHeight, canvasScaler.matchWidthOrHeight);
                scaleFactor = Mathf.Pow(2f, logScale);
            }

            var background = _document.FullScreenBackground;
            background.offsetMin = new Vector2(-safeArea.x / scaleFactor, -safeArea.y / scaleFactor);
            background.offsetMax = new Vector2((screenSize.x - safeArea.xMax) / scaleFactor, (screenSize.y - safeArea.yMax) / scaleFactor);
        }

        /// <summary>
        /// 内部销毁窗口
        /// </summary>
        internal void DestroyInternal()
        {
            if (_status == UIStatus.Closed)
            {
                return;
            }

            ChangeStatus(UIStatus.Closing);
            Closing?.Invoke(this);
            OnClose();

            _design?.Dispose();
            _design = null;

            _data?.Dispose();
            _data = null;

            if (_gameObject != null)
            {
                UnityEngine.Object.Destroy(_gameObject);
                _gameObject = null;
            }

            _document = null;
            _canvas = null;
            _animator = null;
            ChangeStatus(UIStatus.Closed);
            Closed?.Invoke(this);
            Created = null;
            Refreshed = null;
            Shown = null;
            Hidden = null;
            Closing = null;
            Closed = null;
        }

        /// <summary>
        /// 释放窗口占用的资源
        /// </summary>
        public void Dispose()
        {
            DestroyInternal();
        }

        /// <summary>
        /// 窗口创建时的回调
        /// </summary>
        /// <param name="args">传递给窗口的参数</param>
        protected virtual void OnCreate(params object[] args)
        {
        }

        /// <summary>
        /// 窗口刷新时的回调
        /// </summary>
        /// <param name="args">传递给窗口的参数</param>
        protected virtual void OnRefresh(params object[] args)
        {
        }

        /// <summary>
        /// 窗口显示时的回调
        /// </summary>
        protected virtual void OnShow()
        {
        }

        /// <summary>
        /// 窗口隐藏时的回调
        /// </summary>
        protected virtual void OnHide()
        {
        }

        /// <summary>
        /// 窗口关闭时的回调
        /// </summary>
        protected virtual void OnClose()
        {
        }

        /// <summary>
        /// 解析资源路径
        /// </summary>
        /// <returns>资源路径</returns>
        protected virtual string ResolveAssetPath()
        {
            return GetType().Name;
        }

        private UIWindowAttribute Metadata
        {
            get
            {
                if (_metadata == null)
                {
                    _metadata = ResolveMetadata();
                }

                return _metadata;
            }
        }

        private UIWindowAttribute _metadata;

        private UIWindowAttribute ResolveMetadata()
        {
            var attribute = GetType().GetCustomAttribute<UIWindowAttribute>(true);
            if (attribute != null)
            {
                var assetPath = string.IsNullOrWhiteSpace(attribute.AssetPath) ? ResolveAssetPath() : attribute.AssetPath;
                return new UIWindowAttribute(assetPath, attribute.Layer, attribute.Mode, attribute.ToStack, attribute.SortingOrder, attribute.CacheOnClose, attribute.OpenStrategy);
            }

            return new UIWindowAttribute(ResolveAssetPath());
        }

        private void ValidateDocumentMetadata()
        {
            if (_document == null)
            {
                return;
            }

            var mismatch = _document.GetRuntimeMetadataMismatch(Metadata);
            if (!string.IsNullOrWhiteSpace(mismatch))
            {
                Game.Diagnostics.LogWarning(mismatch, WindowKey);
            }
        }

        private void ValidateMetadata()
        {
            if (string.IsNullOrWhiteSpace(Metadata.AssetPath))
            {
                throw new FrameworkException(FrameworkError.Create("UIWindowAssetPathMissing", $"UI window '{WindowKey}' requires a valid asset path.", FrameworkFailureCategory.Configuration, context: WindowKey, stage: FrameworkOperationStage.Validating));
            }
        }

        private void EnsureCreated()
        {
            if (!IsCreated)
            {
                throw new InvalidOperationException($"UI window '{WindowKey}' has not been created.");
            }
        }

        private void ChangeStatus(UIStatus status)
        {
            _status = status;
        }

        private IUIDesign CreateDesign()
        {
            var designType = GetType().GetNestedType("Design", BindingFlags.Public | BindingFlags.NonPublic);
            if (designType == null || !typeof(IUIDesign).IsAssignableFrom(designType))
            {
                return null;
            }

            return Activator.CreateInstance(designType) as IUIDesign;
        }

        private UIDataBase CreateData()
        {
            var dataType = GetType().GetNestedType("Data", BindingFlags.Public | BindingFlags.NonPublic);
            if (dataType == null || !typeof(UIDataBase).IsAssignableFrom(dataType))
            {
                return null;
            }

            return Activator.CreateInstance(dataType) as UIDataBase;
        }
    }
}
