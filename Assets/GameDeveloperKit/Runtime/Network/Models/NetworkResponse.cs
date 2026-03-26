namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 网络响应类，封装HTTP响应的数据和信息。
    /// </summary>
    public sealed class NetworkResponse : FrameworkOperationResult
    {
        /// <summary>
        /// 获取请求的URL。
        /// </summary>
        public string Url { get; internal set; }

        /// <summary>
        /// 获取HTTP状态码。
        /// </summary>
        public long StatusCode { get; internal set; }

        /// <summary>
        /// 获取追踪ID，用于请求追踪。
        /// </summary>
        public string TraceId { get; internal set; }

        /// <summary>
        /// 获取响应是否成功。
        /// </summary>
        public bool IsSuccess { get; internal set; }

        /// <summary>
        /// 获取响应文本内容。
        /// </summary>
        public string Text { get; internal set; }

        /// <summary>
        /// 获取响应二进制数据。
        /// </summary>
        public byte[] Data { get; internal set; }

        /// <summary>
        /// 获取请求耗时（毫秒）。
        /// </summary>
        public long DurationMilliseconds { get; internal set; }
    }
}
