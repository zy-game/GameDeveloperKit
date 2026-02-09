namespace GameDeveloperKit.Config
{
    /// <summary>
    /// 配置项接口（单条配置记录）
    /// </summary>
    public interface IConfigData
    {
        /// <summary>
        /// 配置ID（唯一标识）
        /// </summary>
        string Id { get; }
    }
}
