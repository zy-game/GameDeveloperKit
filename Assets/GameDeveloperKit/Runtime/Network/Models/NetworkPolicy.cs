namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 网络策略类，定义网络请求的行为和配置。
    /// </summary>
    public sealed class NetworkPolicy
    {
        /// <summary>
        /// 获取或设置超时时间覆盖值（秒）。
        /// </summary>
        public int TimeoutSecondsOverride { get; set; }

        /// <summary>
        /// 获取或设置重试次数。
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// 获取或设置授权头值。
        /// </summary>
        public string AuthorizationHeaderValue { get; set; }

        /// <summary>
        /// 获取或设置是否生成追踪ID。
        /// </summary>
        public bool GenerateTraceId { get; set; } = true;
    }
}
