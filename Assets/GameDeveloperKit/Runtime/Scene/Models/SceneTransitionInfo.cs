using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 场景过渡信息类，记录场景切换的详细信息。
    /// </summary>
    public sealed class SceneTransitionInfo
    {
        /// <summary>
        /// 初始化场景过渡信息实例。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <param name="location">资源位置。</param>
        /// <param name="requestedLoadMode">请求的加载模式。</param>
        /// <param name="actualLoadMode">实际的加载模式。</param>
        /// <param name="isAsync">是否异步加载。</param>
        /// <param name="preservePersistentScenes">是否保留持久场景。</param>
        /// <param name="durationMilliseconds">加载耗时（毫秒）。</param>
        /// <param name="sceneKey">场景键。</param>
        public SceneTransitionInfo(
            string packageName,
            ResourceLocation location,
            LoadSceneMode requestedLoadMode,
            LoadSceneMode actualLoadMode,
            bool isAsync,
            bool preservePersistentScenes,
            long durationMilliseconds = 0,
            string sceneKey = null)
        {
            PackageName = packageName;
            Location = location?.Clone() ?? new ResourceLocation();
            RequestedLoadMode = requestedLoadMode;
            ActualLoadMode = actualLoadMode;
            IsAsync = isAsync;
            PreservePersistentScenes = preservePersistentScenes;
            DurationMilliseconds = durationMilliseconds;
            SceneKey = sceneKey;
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
        /// 获取请求的加载模式。
        /// </summary>
        public LoadSceneMode RequestedLoadMode { get; }

        /// <summary>
        /// 获取实际的加载模式。
        /// </summary>
        public LoadSceneMode ActualLoadMode { get; }

        /// <summary>
        /// 获取是否异步加载。
        /// </summary>
        public bool IsAsync { get; }

        /// <summary>
        /// 获取是否保留持久场景。
        /// </summary>
        public bool PreservePersistentScenes { get; }

        /// <summary>
        /// 获取加载耗时（毫秒）。
        /// </summary>
        public long DurationMilliseconds { get; }

        /// <summary>
        /// 获取场景键。
        /// </summary>
        public string SceneKey { get; }

        /// <summary>
        /// 完成场景过渡并返回完成后的信息。
        /// </summary>
        /// <param name="durationMilliseconds">加载耗时（毫秒）。</param>
        /// <param name="sceneKey">场景键。</param>
        /// <returns>完成后的场景过渡信息。</returns>
        public SceneTransitionInfo Complete(long durationMilliseconds, string sceneKey)
        {
            return new SceneTransitionInfo(
                PackageName,
                Location,
                RequestedLoadMode,
                ActualLoadMode,
                IsAsync,
                PreservePersistentScenes,
                durationMilliseconds,
                sceneKey);
        }
    }
}
