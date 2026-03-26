namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源播放模式枚举，定义不同的资源加载模式。
    /// </summary>
    public enum ResourcePlayMode
    {
        /// <summary>
        /// 编辑器模拟模式，在编辑器中直接加载资源。
        /// </summary>
        EditorSimulate,
        /// <summary>
        /// 离线模式，从本地缓存加载资源。
        /// </summary>
        Offline,
        /// <summary>
        /// 主机模式，从服务器下载并缓存资源。
        /// </summary>
        Host,
        /// <summary>
        /// Web模式，适用于Web平台的资源加载。
        /// </summary>
        Web
    }
}
