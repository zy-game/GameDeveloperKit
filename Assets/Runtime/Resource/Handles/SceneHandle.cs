namespace GameDeveloperKit.Resource
{
    

    /// <summary>
    /// 场景引用句柄
    /// </summary>
    public class SceneHandle : BaseHandle
    {
        private bool _active;
        private UnityEngine.SceneManagement.Scene _scene;
        private AssetInfo _manifest;

        /// <summary>
        /// 场景实例
        /// </summary>
        public UnityEngine.SceneManagement.Scene Scene => _scene;

        /// <summary>
        /// 是否激活场景
        /// </summary>
        public bool IsActive => _active;

        /// <summary>
        /// 资源名称
        /// </summary>
        public override string Name => _manifest.name;

        /// <summary>
        /// 资源地址
        /// </summary>
        public override string Address => _manifest.address;

        /// <summary>
        /// 资源GUID
        /// </summary>
        public override string GUID => _manifest.guid;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="scene">场景实例</param>
        /// <param name="manifest">资源清单</param>
        public SceneHandle(UnityEngine.SceneManagement.Scene scene, AssetInfo manifest)
        {
            _scene = scene;
            _manifest = manifest;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += (l, n) => { _active = n.name == Name; };
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += s =>
            {
                if (s == _scene)
                {
                    Game.Resource.Unload(this);
                }
            };
        }

        /// <summary>
        /// 构造函数（简化版本，用于直接传入场景名称）
        /// </summary>
        /// <param name="scene">场景实例</param>
        /// <param name="sceneName">场景名称</param>
        public SceneHandle(UnityEngine.SceneManagement.Scene scene, string sceneName) : this(scene, new AssetInfo { name = sceneName, address = sceneName, guid = sceneName })
        {

        }

        protected override void OnDispose()
        {
            Game.Resource.Unload(this);
        }

        public override void OnClearup()
        {
            base.OnClearup();
            _scene = default;
            _manifest = default;
        }

        /// <summary>
        /// 激活场景
        /// </summary>
        public void Inactive()
        {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(_scene);
        }
    }
}