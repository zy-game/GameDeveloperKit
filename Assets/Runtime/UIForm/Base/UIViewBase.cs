using UnityEngine;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// UI 视图基类（纯逻辑类，不继承 MonoBehaviour）
    /// 主View和Widget嵌套类都继承此类
    /// </summary>
    public abstract class UIViewBase
    {
        private GameObject _gameObject;
        protected UIBindData _bindData;

        /// <summary>
        /// GameObject
        /// </summary>
        public GameObject GameObject => _gameObject;

        /// <summary>
        /// RectTransform 组件
        /// </summary>
        public RectTransform RectTransform => _gameObject?.transform as RectTransform;

        /// <summary>
        /// 初始化（由 UIForm 或父View调用）
        /// </summary>
        /// <param name="gameObject">对应的GameObject</param>
        /// <param name="bindData">UIBindData引用（可选，如果为null则从GameObject获取）</param>
        /// <param name="groupName">分组名（主View为空字符串，Widget为d_xxx）</param>
        public void Startup(GameObject gameObject)
        {
            _gameObject = gameObject;
            _bindData = gameObject.GetComponent<UIBindData>();
            OnStartup();
        }

        /// <summary>
        /// 获取本分组内的组件
        /// </summary>
        protected T Get<T>(string goName) where T : Component
        {
            return _bindData?.Get<T>(goName);
        }

        /// <summary>
        /// 初始化时调用
        /// </summary>
        public virtual void OnStartup()
        {
        }

        /// <summary>
        /// 清理时调用
        /// </summary>
        public virtual void OnClearup()
        {
        }

        /// <summary>
        /// 显示
        /// </summary>
        public void Show()
        {
            _gameObject?.SetActive(true);
        }

        /// <summary>
        /// 隐藏
        /// </summary>
        public void Hide()
        {
            _gameObject?.SetActive(false);
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public void Destroy()
        {
            if (_gameObject != null)
            {
                Object.Destroy(_gameObject);
                _gameObject = null;
            }
        }
        
        /// <summary>
        /// 克隆当前View对应的Form（用于列表项等需要动态创建的场景）
        /// </summary>
        /// <typeparam name="TForm">Form类型，必须绑定当前View类型</typeparam>
        /// <returns>克隆的Form实例</returns>
        public TForm Instantiate<TForm>() where TForm : class, IUIForm, new()
        {
            if (_gameObject == null) return null;
            
            // 检查TForm绑定的View类型是否与当前View类型匹配
            var formType = typeof(TForm);
            var baseType = formType.BaseType;
            if (baseType != null && baseType.IsGenericType)
            {
                var genericArgs = baseType.GetGenericArguments();
                if (genericArgs.Length >= 2)
                {
                    var viewType = genericArgs[1]; // TView是第二个泛型参数
                    if (viewType != this.GetType())
                    {
                        Game.Debug.Error($"Instantiate failed: Form '{formType.Name}' is bound to View '{viewType.Name}', but current View is '{this.GetType().Name}'");
                        return null;
                    }
                }
            }
            
            var clonedGo = Object.Instantiate(_gameObject, _gameObject.transform.parent);
            clonedGo.name = _gameObject.name;
            
            var rect = clonedGo.transform as RectTransform;
            if (rect != null)
                rect.anchoredPosition = Vector2.zero;
            
            var form = new TForm();
            form.OnCreateFromClone(clonedGo);
            return form;
        }
    }
}
