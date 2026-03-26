using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 场景加载信息类，记录场景加载的相关信息。
    /// </summary>
    public sealed class SceneLoadInfo
    {
        /// <summary>
        /// 初始化场景加载信息实例。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <param name="location">资源位置。</param>
        /// <param name="loadMode">场景加载模式。</param>
        /// <param name="isAsync">是否异步加载。</param>
        public SceneLoadInfo(string packageName, ResourceLocation location, LoadSceneMode loadMode, bool isAsync)
        {
            PackageName = packageName;
            Location = location?.Clone() ?? new ResourceLocation();
            LoadMode = loadMode;
            IsAsync = isAsync;
        }

        /// <summary>
        /// 获取资源包名称。
        /// </summary>
        public string PackageName { get; }

        /// <summary>
        /// 获取资源位置。
        /// </summary>
        public ResourceLocation Location { get; }

        /// <summary>
        /// 获取场景加载模式。
        /// </summary>
        public LoadSceneMode LoadMode { get; }

        /// <summary>
        /// 获取是否异步加载。
        /// </summary>
        public bool IsAsync { get; }
    }
}
