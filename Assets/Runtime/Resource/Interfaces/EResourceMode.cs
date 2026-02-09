namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源模式
    /// </summary>
    public enum EResourceMode
    {
        /// <summary>
        /// 未设置
        /// </summary>
        None,
        
        /// <summary>
        /// 编辑器模拟模式（使用 AssetDatabase 加载，无需打包）
        /// </summary>
        EditorSimulator,

        /// <summary>
        /// 离线模式（仅从 StreamingAssets 加载）
        /// </summary>
        Offline,

        /// <summary>
        /// 在线模式（支持 StreamingAssets 母包资源 + 网络下载热更新）
        /// </summary>
        Online,
    }
}