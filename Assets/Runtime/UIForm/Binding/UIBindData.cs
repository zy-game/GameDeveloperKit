using UnityEngine;
using System;
using System.Collections.Generic;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// UI 组件绑定数据容器（挂载在 UI Prefab 根节点上）
    /// </summary>
    public class UIBindData : MonoBehaviour
    {
        [Serializable]
        public class ComponentBinding
        {
            [SerializeField] public GameObject target;             // 目标GameObject
            public List<Component> components = new List<Component>(); // 组件引用列表
        }

        [Header("UI Form Settings")]
        [Tooltip("UI层级")]
        [SerializeField] private EUILayer uiLayer = EUILayer.Window;
        
        [Tooltip("UI显示模式")]
        [SerializeField] private EUIMode uiMode = EUIMode.Normal;

        [Header("Full Screen Background")]
        [Tooltip("需要扩展到全屏的背景RectTransform（用于填充安全区域外的镂空部分）")]
        [SerializeField] private RectTransform fullScreenBackground;

        [Header("Component Bindings")]
        [SerializeField] private List<ComponentBinding> bindings = new List<ComponentBinding>();

        /// <summary>
        /// UI层级
        /// </summary>
        public EUILayer UILayer => uiLayer;
        
        /// <summary>
        /// UI显示模式
        /// </summary>
        public EUIMode UIMode => uiMode;

        /// <summary>
        /// 全屏背景RectTransform（用于填充安全区域外的镂空部分）
        /// </summary>
        public RectTransform FullScreenBackground => fullScreenBackground;

        // 缓存：goName -> Components
        private Dictionary<string, List<Component>> _cache;

        private void Awake()
        {
            BuildCache();
        }

        private void BuildCache()
        {
            _cache = new Dictionary<string, List<Component>>();
            
            foreach (var binding in bindings)
            {
                if (binding.target == null || binding.components == null || binding.components.Count == 0)
                    continue;

                _cache[binding.target.name] = binding.components;
            }
        }

        /// <summary>
        /// 获取组件（通过GameObject名称和类型）
        /// </summary>
        public T Get<T>(string goName) where T : Component
        {
            if (_cache == null)
                BuildCache();

            if (_cache.TryGetValue(goName, out var components))
            {
                foreach (var comp in components)
                {
                    if (comp is T typedComp)
                        return typedComp;
                }
            }
            Game.Debug.Warning($"Component '{typeof(T).Name}' with name '{goName}' not found");
            return null;
        }

        /// <summary>
        /// 尝试获取组件（不输出警告）
        /// </summary>
        public bool TryGet<T>(string goName, out T component) where T : Component
        {
            if (_cache == null)
                BuildCache();

            if (_cache.TryGetValue(goName, out var components))
            {
                foreach (var comp in components)
                {
                    if (comp is T typedComp)
                    {
                        component = typedComp;
                        return true;
                    }
                }
            }
            component = null;
            return false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器辅助：获取所有绑定（供编辑器使用）
        /// </summary>
        public List<ComponentBinding> GetBindings()
        {
            return bindings;
        }
#endif
    }
}
