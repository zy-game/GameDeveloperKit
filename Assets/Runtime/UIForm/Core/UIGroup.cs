using System.Collections.Generic;
using UnityEngine;
using ZLinq;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// UI 层级组，管理同一层级的所有 UI
    /// </summary>
    public class UIGroup
    {
        private readonly EUILayer _layer;
        private readonly Transform _parent;
        private readonly Dictionary<string, IUIForm> _forms = new Dictionary<string, IUIForm>();
        private readonly int _baseSortingOrder;

        /// <summary>
        /// 层级
        /// </summary>
        public EUILayer Layer => _layer;

        /// <summary>
        /// 父节点
        /// </summary>
        public Transform Parent => _parent;

        /// <summary>
        /// 当前层级所有 UI
        /// </summary>
        public IReadOnlyDictionary<string, IUIForm> Forms => _forms;

        /// <summary>
        /// UI 数量
        /// </summary>
        public int Count => _forms.Count;

        /// <summary>
        /// 构造函数
        /// </summary>
        public UIGroup(EUILayer layer, Transform parent, int baseSortingOrder)
        {
            _layer = layer;
            _parent = parent;
            _baseSortingOrder = baseSortingOrder;

            // 设置父节点名称
            _parent.name = $"Layer_{layer}";
        }

        /// <summary>
        /// 添加 UI
        /// </summary>
        public void Add(IUIForm form)
        {
            if (form == null)
            {
                return;
            }
            
            var formTypeName = form.GetType().FullName;
            if (_forms.ContainsKey(formTypeName))
            {
                return;
            }

            _forms[formTypeName] = form;

            // 设置父节点
            form.Transform.SetParent(_parent, false);

            // 设置 Canvas 排序顺序（相对排序：基础值 + 当前索引）
            if (form.Canvas != null)
            {
                form.Canvas.sortingOrder = _baseSortingOrder + _forms.Count;
            }
        }

        /// <summary>
        /// 移除 UI
        /// </summary>
        public void Remove(string name)
        {
            if (_forms.TryGetValue(name, out var form))
            {
                _forms.Remove(name);

                // 销毁游戏对象
                if (form != null && form.GameObject != null)
                {
                    Object.Destroy(form.GameObject);
                }

                // 重新排序（防止间隙累积）
                RefreshSortingOrder();
            }
        }
        
        /// <summary>
        /// 移除 UI（按实例）
        /// </summary>
        public void Remove(IUIForm form)
        {
            if (form == null) return;
            
            string keyToRemove = null;
            foreach (var kvp in _forms)
            {
                if (kvp.Value == form)
                {
                    keyToRemove = kvp.Key;
                    break;
                }
            }
            
            if (keyToRemove != null)
            {
                _forms.Remove(keyToRemove);

                // 销毁游戏对象
                if (form.GameObject != null)
                {
                    Object.Destroy(form.GameObject);
                }

                // 重新排序（防止间隙累积）
                RefreshSortingOrder();
            }
        }

        /// <summary>
        /// 重新计算所有 UI 的排序
        /// </summary>
        private void RefreshSortingOrder()
        {
            int index = 0;
            foreach (var form in _forms.Values)
            {
                if (form.Canvas != null)
                {
                    form.Canvas.sortingOrder = _baseSortingOrder + index;
                    index++;
                }
            }
        }

        /// <summary>
        /// 将 UI 移到最前
        /// </summary>
        public void BringToFront(IUIForm form)
        {
            if (form == null) return;
            
            var formTypeName = form.GetType().FullName;
            if (!_forms.ContainsKey(formTypeName))
            {
                return;
            }

            if (form.Canvas != null)
            {
                form.Canvas.sortingOrder = _baseSortingOrder + _forms.Count;
            }
        }

        /// <summary>
        /// 获取 UI（按类型）
        /// </summary>
        public T Get<T>() where T : class, IUIForm
        {
            var formTypeName = typeof(T).FullName;
            return _forms.TryGetValue(formTypeName, out var form) ? form as T : null;
        }

        /// <summary>
        /// 检查是否包含 UI
        /// </summary>
        public bool Contains<T>() where T : IUIForm
        {
            var formTypeName = typeof(T).FullName;
            return _forms.ContainsKey(formTypeName);
        }

        /// <summary>
        /// 显示层级
        /// </summary>
        public void Show()
        {
            _parent.gameObject.SetActive(true);
        }

        /// <summary>
        /// 隐藏层级
        /// </summary>
        public void Hide()
        {
            _parent.gameObject.SetActive(false);
        }

        /// <summary>
        /// 清理层级
        /// </summary>
        public void Clearup()
        {
            // 清理所有 UI
            var formList = _forms.Values.AsValueEnumerable().ToList();
            foreach (var form in formList)
            {
                if (form != null)
                {
                    form.Destory();
                    if (form.GameObject != null)
                    {
                        Object.Destroy(form.GameObject);
                    }
                }
            }

            _forms.Clear();
        }
    }
}
