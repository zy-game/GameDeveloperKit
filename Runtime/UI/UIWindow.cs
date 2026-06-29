using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.UI
{
    public abstract class UIWindow : IReference
    {
        public UIDocument Document { get; private set; }

        public GameObject GameObject { get; private set; }

        public UILayer Layer { get; private set; }

        /// <summary>
        /// 初始化 member。
        /// </summary>
        /// <param name="gameObject">game Object 参数。</param>
        internal void Initialize(UIDocument document, GameObject gameObject, UILayer layer)
        {
            Document = document;
            GameObject = gameObject;
            Layer = layer;
        }

        /// <summary>
        /// 处理 Awake Async 回调。
        /// </summary>
        public virtual UniTask OnAwakeAsync()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 处理 Open Async 回调。
        /// </summary>
        public virtual UniTask OnOpenAsync()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Unity OnEnable 回调。
        /// </summary>
        public virtual void OnEnable()
        {
        }

        /// <summary>
        /// Unity OnDisable 回调。
        /// </summary>
        public virtual void OnDisable()
        {
        }

        /// <summary>
        /// 执行 Release。
        /// </summary>
        public virtual void Release()
        {
            Document = null;
            GameObject = null;
            Layer = default;
        }
    }
}
