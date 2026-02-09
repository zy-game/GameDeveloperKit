using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.PsdToUgui
{
    /// <summary>
    /// PSD 文档绑定组件 - 记录 PSD 图层与 Prefab 节点的映射关系
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("GameDeveloperKit/PSD Document Binding")]
    public class PsdDocumentBinding : MonoBehaviour
    {
        [Serializable]
        public class LayerBinding
        {
            public int LayerId;
            public string LayerName;
            public string GameObjectPath;
            public int LayerType;
            public bool IsOrphan;
        }

        [SerializeField] private string _psdFilePath;
        [SerializeField] private string _psdFileHash;
        [SerializeField] private int _psdWidth;
        [SerializeField] private int _psdHeight;
        [SerializeField] private List<LayerBinding> _bindings = new();

        public string PsdFilePath => _psdFilePath;
        public string PsdFileHash => _psdFileHash;
        public int PsdWidth => _psdWidth;
        public int PsdHeight => _psdHeight;
        public IReadOnlyList<LayerBinding> Bindings => _bindings;

        public void Initialize(string psdFilePath, string psdFileHash, int width, int height)
        {
            _psdFilePath = psdFilePath;
            _psdFileHash = psdFileHash;
            _psdWidth = width;
            _psdHeight = height;
            _bindings.Clear();
        }

        public void AddBinding(int layerId, string layerName, string gameObjectPath, int layerType)
        {
            _bindings.Add(new LayerBinding
            {
                LayerId = layerId,
                LayerName = layerName,
                GameObjectPath = gameObjectPath,
                LayerType = layerType,
                IsOrphan = false
            });
        }

        public LayerBinding FindBinding(int layerId)
        {
            return _bindings.Find(b => b.LayerId == layerId);
        }

        public LayerBinding FindBindingByPath(string path)
        {
            return _bindings.Find(b => b.GameObjectPath == path);
        }

        public GameObject FindGameObject(int layerId)
        {
            var binding = FindBinding(layerId);
            if (binding == null || string.IsNullOrEmpty(binding.GameObjectPath))
                return null;

            if (binding.GameObjectPath == "")
                return gameObject;

            // 首先尝试原始路径
            var go = transform.Find(binding.GameObjectPath)?.gameObject;
            if (go != null)
                return go;

            // 如果原始路径找不到，在整个层级中按名称搜索
            return FindGameObjectByNameRecursive(transform, binding.LayerName);
        }

        private GameObject FindGameObjectByNameRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child.gameObject;

                var found = FindGameObjectByNameRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        public void UpdateBinding(int layerId, string newPath)
        {
            var binding = FindBinding(layerId);
            if (binding != null)
            {
                binding.GameObjectPath = newPath;
            }
        }

        public void MarkOrphan(int layerId)
        {
            var binding = FindBinding(layerId);
            if (binding != null)
            {
                binding.IsOrphan = true;
            }
        }

        public void ClearOrphanMarks()
        {
            foreach (var binding in _bindings)
            {
                binding.IsOrphan = false;
            }
        }

        public List<LayerBinding> GetOrphanBindings()
        {
            return _bindings.FindAll(b => b.IsOrphan);
        }

        public void RemoveBinding(int layerId)
        {
            _bindings.RemoveAll(b => b.LayerId == layerId);
        }

        public void ClearBindings()
        {
            _bindings.Clear();
        }

#if UNITY_EDITOR
        public void SetPsdFileHash(string hash)
        {
            _psdFileHash = hash;
        }

        public List<LayerBinding> GetBindingsForEditor()
        {
            return _bindings;
        }
#endif
    }
}
