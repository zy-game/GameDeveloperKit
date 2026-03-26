namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 网络服务结果类，表示网络服务的执行结果。
    /// </summary>
    public class NetworkServiceResult : FrameworkOperationResult
    {
        /// <summary>
        /// 获取服务名称。
        /// </summary>
        public string ServiceName { get; internal set; }

        /// <summary>
        /// 获取操作名称。
        /// </summary>
        public string OperationName { get; internal set; }

        /// <summary>
        /// 获取请求URL。
        /// </summary>
        public string Url { get; internal set; }

        /// <summary>
        /// 获取HTTP状态码。
        /// </summary>
        public long StatusCode { get; internal set; }

        /// <summary>
        /// 获取追踪ID。
        /// </summary>
        public string TraceId { get; internal set; }

        /// <summary>
        /// 获取网络响应。
        /// </summary>
        public NetworkResponse Response { get; internal set; }
    }
}
