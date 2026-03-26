namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 表示带响应值的网络服务结果。
    /// </summary>
    /// <typeparam name="TResponse">响应值类型。</typeparam>
    public sealed class NetworkServiceResult<TResponse> : NetworkServiceResult
    {
        /// <summary>
        /// 获取响应值。
        /// </summary>
        public TResponse Value { get; internal set; }
    }
}
