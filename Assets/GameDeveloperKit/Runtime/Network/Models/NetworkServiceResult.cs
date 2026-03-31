namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 网络服务结果类，表示网络服务的执行结果。
    /// </summary>
    public class NetworkServiceResult
    {
        /// <summary>
        /// 获取或设置操作是否成功。
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 获取或设置当前操作阶段。
        /// </summary>
        public string Stage { get; set; } = "None";

        /// <summary>
        /// 获取或设置错误消息。
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 获取或设置详细错误信息。
        /// </summary>
        public GameFrameworkException Error { get; set; }

        /// <summary>
        /// 获取或设置失败类型标识。
        /// </summary>
        public string FailureKind { get; set; }

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



