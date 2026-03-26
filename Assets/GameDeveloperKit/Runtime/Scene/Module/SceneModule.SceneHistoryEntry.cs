namespace GameDeveloperKit.Runtime
{
    public sealed partial class SceneModule
    {
        /// <summary>
        /// 场景历史条目类，记录已加载场景的信息。
        /// </summary>
        private sealed class SceneHistoryEntry
        {
            /// <summary>
            /// 初始化场景历史条目的新实例。
            /// </summary>
            /// <param name="sceneKey">场景键。</param>
            /// <param name="packageName">资源包名称。</param>
            /// <param name="location">资源位置。</param>
            /// <param name="useResource">是否使用资源系统。</param>
            public SceneHistoryEntry(string sceneKey, string packageName, ResourceLocation location, bool useResource)
            {
                SceneKey = sceneKey;
                PackageName = packageName;
                Location = location?.Clone() ?? new ResourceLocation();
                UseResource = useResource;
            }

            /// <summary>
            /// 获取场景键。
            /// </summary>
            public string SceneKey { get; }

            /// <summary>
            /// 获取资源包名称。
            /// </summary>
            public string PackageName { get; }

            /// <summary>
            /// 获取资源位置。
            /// </summary>
            public ResourceLocation Location { get; }

            /// <summary>
            /// 获取是否使用资源系统。
            /// </summary>
            public bool UseResource { get; }
        }
    }
}
