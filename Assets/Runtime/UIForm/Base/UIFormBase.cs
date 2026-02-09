using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using UnityEngine;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// UI 表单基类（泛型，MVP 模式）
    /// </summary>
    /// <typeparam name="TData">数据类型</typeparam>
    /// <typeparam name="TView">视图类型</typeparam>
    public abstract class UIFormBase<TData, TView> : IUIForm where TData : UIDataBase, new() where TView : UIViewBase
    {
        private TView _view;
        private TData _data;
        private GameObject _gameObject;
        private UIFormAttribute _attribute;
        private Animator _animator;
        private Canvas _canvas;
        private UIStatus _status = UIStatus.Closed;

        /// <summary>
        /// 构造函数，从View类型获取UIFormAttribute
        /// </summary>
        protected UIFormBase()
        {
            var viewType = typeof(TView);
            var attributes = viewType.GetCustomAttributes(typeof(UIFormAttribute), true);
            if (attributes.Length > 0)
                _attribute = attributes[0] as UIFormAttribute;
        }

        /// <summary>
        /// 游戏对象
        /// </summary>
        public GameObject GameObject => _gameObject;

        /// <summary>
        /// Transform 组件
        /// </summary>
        public Transform Transform => _gameObject.transform;

        /// <summary>
        /// UI 层级
        /// </summary>
        public EUILayer Layer => _attribute?.Layer ?? EUILayer.Window;

        /// <summary>
        /// UI 模式
        /// </summary>
        public EUIMode Mode => _attribute?.Mode ?? EUIMode.Normal;

        /// <summary>
        /// 是否进入堆栈
        /// </summary>
        public bool ToStack => _attribute?.ToStack ?? false;

        /// <summary>
        /// UI 状态
        /// </summary>
        public UIStatus Status => _status;

        /// <summary>
        /// Canvas 组件
        /// </summary>
        public Canvas Canvas => _canvas;

        /// <summary>
        /// Animator 组件
        /// </summary>
        public Animator Animator => _animator;

        /// <summary>
        /// 数据（Model）
        /// </summary>
        public TData Data => _data;

        /// <summary>
        /// 视图（View）
        /// </summary>
        public TView View => _view;

        /// <summary>
        /// 内部创建（由 UIModule 调用，负责资源加载和 GameObject 实例化）
        /// </summary>
        /// <param name="parent">父节点</param>
        /// <param name="args">初始化参数</param>
        public async UniTask<bool> OnCreate(Transform parent, params object[] args)
        {
            _status = UIStatus.Loading;

            // 确保Attribute已获取
            Throw.Asserts(_attribute != null, $"View type '{typeof(TView).Name}' must have UIFormAttribute");

            // 1. 确定资源名称
            var finalName = _attribute.Name;

            // 2. 加载资源
            AssetHandle<GameObject> prefabHandle = null;
            try
            {
                prefabHandle = await Game.Resource.LoadAssetAsync<GameObject>(finalName);

                if (prefabHandle == null || prefabHandle.Asset == null)
                {
                    Game.Debug.Error($"Failed to load UI prefab '{finalName}': Asset is null");
                    _status = UIStatus.Closed;
                    return false;
                }
            }
            catch (Exception ex)
            {
                Game.Debug.Error($"Failed to load UI prefab '{finalName}': {ex.Message}");
                _status = UIStatus.Closed;
                return false;
            }

            // 3. 实例化 GameObject
            _gameObject = prefabHandle.Instantiate(parent, Vector3.zero, Quaternion.identity, Vector3.one, false);
            _gameObject.name = finalName;

            // 4. 获取组件
            _animator = _gameObject.GetComponent<Animator>();
            _canvas = _gameObject.GetComponent<Canvas>();

            // 5. 获取或创建 Data（从 ReferencePool）
            _data = ReferencePool.Acquire<TData>();
            _data.OnStartup();

            // 6. 创建 View 实例（纯逻辑对象，不再是 MonoBehaviour 组件）
            _view = System.Activator.CreateInstance<TView>();
            _view.Startup(_gameObject);
            if (_canvas == null)
            {
                _canvas = _gameObject.AddComponent<Canvas>();
                _canvas.overrideSorting = true;
                _gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = _attribute.SortingOrder;
            var rect = _gameObject.transform as RectTransform;
            if (rect != null)
            {
                rect.anchoredPosition = Vector2.zero;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
            }

            // 7. 调整全屏背景（填充安全区域外的镂空部分）
            SetFullScreenBackground();

            // 8. 调用子类初始化
            OnStartup(args);

            _status = UIStatus.Active;
            return true;
        }

        /// <summary>
        /// 从克隆的GameObject创建UI（用于列表项等动态创建场景）
        /// </summary>
        /// <param name="clonedGameObject">已克隆的GameObject</param>
        /// <param name="args">初始化参数</param>
        public void OnCreateFromClone(GameObject clonedGameObject, params object[] args)
        {
            _gameObject = clonedGameObject;
            
            // 获取组件
            _animator = _gameObject.GetComponent<Animator>();
            _canvas = _gameObject.GetComponent<Canvas>();
            
            // 获取或创建 Data
            _data = ReferencePool.Acquire<TData>();
            _data.OnStartup();
            
            // 创建 View 实例
            _view = System.Activator.CreateInstance<TView>();
            _view.Startup(_gameObject);
            
            // 调用子类初始化
            OnStartup(args);
            
            _status = UIStatus.Active;
        }

        /// <summary>
        /// 调整全屏背景大小（填充安全区域外的镂空部分）
        /// 由UIModule在SafeArea变化时调用
        /// </summary>
        public void SetFullScreenBackground()
        {
            var bindData = _gameObject.GetComponent<UIBindData>();
            if (bindData?.FullScreenBackground == null) return;

            var bg = bindData.FullScreenBackground;
            var safeArea = Screen.safeArea;
            var screenSize = new Vector2(Screen.width, Screen.height);

            // 计算需要扩展的边距（相对于安全区域）
            float leftExtend = safeArea.x;
            float rightExtend = screenSize.x - (safeArea.x + safeArea.width);
            float bottomExtend = safeArea.y;
            float topExtend = screenSize.y - (safeArea.y + safeArea.height);

            // 获取CanvasScaler来计算正确的缩放因子
            var rootCanvas = _canvas.rootCanvas;
            var canvasScaler = rootCanvas?.GetComponent<UnityEngine.UI.CanvasScaler>();

            float scaleFactorX = 1f;
            float scaleFactorY = 1f;

            if (canvasScaler != null && canvasScaler.uiScaleMode == UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                var refResolution = canvasScaler.referenceResolution;
                float matchWidthOrHeight = canvasScaler.matchWidthOrHeight;

                // 计算宽度和高度的缩放比例
                float logWidth = Mathf.Log(screenSize.x / refResolution.x, 2);
                float logHeight = Mathf.Log(screenSize.y / refResolution.y, 2);
                float logScale = Mathf.Lerp(logWidth, logHeight, matchWidthOrHeight);
                float scaleFactor = Mathf.Pow(2, logScale);

                scaleFactorX = scaleFactor;
                scaleFactorY = scaleFactor;
            }
            else if (rootCanvas != null)
            {
                scaleFactorX = rootCanvas.scaleFactor;
                scaleFactorY = rootCanvas.scaleFactor;
            }

            // 扩展RectTransform的边距
            bg.offsetMin = new Vector2(-leftExtend / scaleFactorX, -bottomExtend / scaleFactorY);
            bg.offsetMax = new Vector2(rightExtend / scaleFactorX, topExtend / scaleFactorY);
        }


        /// <summary>
        /// 内部刷新（由 UIModule 调用，不播放动画）
        /// </summary>
        public void Refresh(params object[] args)
        {
            OnRefresh(args);
        }

        /// <summary>
        /// 内部清理（由 UIModule 调用）
        /// </summary>
        public void Destory()
        {
            _status = UIStatus.Closing;
            // 清理 View
            if (_view != null)
            {
                _view.OnClearup();
                _view = null;
            }

            // 清理 Data（释放回 ReferencePool）
            if (_data != null)
            {
                _data.OnClearup();
                ReferencePool.Release(_data);
                _data = null;
            }
            OnClearup();
            _status = UIStatus.Closed;
        }

        /// <summary>
        /// 显示
        /// </summary>
        public void Show()
        {
            if (_status == UIStatus.Paused)
            {
                _status = UIStatus.Opening;
                _gameObject.SetActive(true);

                // 播放显示动画（带异常处理）
                PlayShowAnimationAsync().Forget();

                OnEnable();
                _status = UIStatus.Active;
            }
        }

        /// <summary>
        /// 隐藏
        /// </summary>
        public void Hide()
        {
            if (_status == UIStatus.Active)
            {
                _status = UIStatus.Closing;

                // 播放隐藏动画（带异常处理）
                PlayHideAnimationAsync().Forget();

                OnDisable();
                _gameObject.SetActive(false);
                _status = UIStatus.Paused;
            }
        }

        /// <summary>
        /// 播放显示动画（异步，不阻塞）
        /// </summary>
        private async UniTaskVoid PlayShowAnimationAsync()
        {
            if (_animator == null)
            {
                OnShowAnimationCompleted();
                return;
            }

            var clips = _animator.runtimeAnimatorController?.animationClips;
            if (clips == null || clips.Length == 0)
            {
                OnShowAnimationCompleted();
                return;
            }

            AnimationClip targetClip = null;
            foreach (var clip in clips)
            {
                if (clip.name == "Show")
                {
                    targetClip = clip;
                    break;
                }
            }

            if (targetClip == null)
            {
                OnShowAnimationCompleted();
                return;
            }

            _animator.Play("Show");
            await UniTask.Delay(TimeSpan.FromSeconds(targetClip.length));

            // 动画播放完成，触发回调
            OnShowAnimationCompleted();
        }

        /// <summary>
        /// 播放隐藏动画（异步，不阻塞）
        /// </summary>
        private async UniTaskVoid PlayHideAnimationAsync()
        {
            if (_animator == null)
            {
                OnHideAnimationCompleted();
                return;
            }

            var clips = _animator.runtimeAnimatorController?.animationClips;
            if (clips == null || clips.Length == 0)
            {
                OnHideAnimationCompleted();
                return;
            }

            AnimationClip targetClip = null;
            foreach (var clip in clips)
            {
                if (clip.name == "Hide")
                {
                    targetClip = clip;
                    break;
                }
            }

            if (targetClip == null)
            {
                OnHideAnimationCompleted();
                return;
            }

            _animator.Play("Hide");
            await UniTask.Delay(TimeSpan.FromSeconds(targetClip.length));

            // 动画播放完成，触发回调
            OnHideAnimationCompleted();
        }

        #region 生命周期（子类重写）

        /// <summary>
        /// 初始化（打开时调用一次）
        /// </summary>
        protected virtual void OnStartup(params object[] args)
        {
        }

        /// <summary>
        /// 刷新（重复打开时调用，不播放动画）
        /// </summary>
        protected virtual void OnRefresh(params object[] args)
        {
        }

        /// <summary>
        /// 启用（显示时调用）
        /// </summary>
        protected virtual void OnEnable()
        {
        }

        /// <summary>
        /// 禁用（隐藏时调用）
        /// </summary>
        protected virtual void OnDisable()
        {
        }

        /// <summary>
        /// 清理（关闭时调用一次）
        /// </summary>
        protected virtual void OnClearup()
        {
        }

        /// <summary>
        /// 显示动画播放完成（子类重写处理动画后逻辑）
        /// </summary>
        protected virtual void OnShowAnimationCompleted()
        {
        }

        /// <summary>
        /// 隐藏动画播放完成（子类重写处理动画后逻辑）
        /// </summary>
        protected virtual void OnHideAnimationCompleted()
        {
        }

        #endregion
    }
}