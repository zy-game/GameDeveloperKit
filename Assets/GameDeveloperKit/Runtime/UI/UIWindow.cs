using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.UI
{
    public abstract class UIWindow : IReference
    {
        public UIDocument Document { get; private set; }

        public GameObject GameObject { get; private set; }

        public UILayer Layer { get; private set; }

        internal void Initialize(UIDocument document, GameObject gameObject, UILayer layer)
        {
            Document = document;
            GameObject = gameObject;
            Layer = layer;
        }

        public virtual UniTask OnAwakeAsync()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask OnOpenAsync()
        {
            return UniTask.CompletedTask;
        }

        public virtual void OnEnable()
        {
        }

        public virtual void OnDisable()
        {
        }

        public virtual void Release()
        {
            Document = null;
            GameObject = null;
            Layer = default;
        }
    }
}
