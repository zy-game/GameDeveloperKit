namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 表示数据内容所属的层级。
    /// </summary>
    public enum DataContentLayer
    {
        /// <summary>
        /// 运行时配置层。
        /// </summary>
        RuntimeConfig = 0,

        /// <summary>
        /// 配置表层。
        /// </summary>
        ConfigTable = 1,

        /// <summary>
        /// 存档数据层。
        /// </summary>
        SaveData = 2
    }
}
