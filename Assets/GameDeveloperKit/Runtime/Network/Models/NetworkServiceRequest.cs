using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 网络服务请求类，封装网络请求的配置和数据。
    /// </summary>
    public sealed class NetworkServiceRequest
    {
        private readonly Dictionary<string, string> _headers = new(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置请求路径。
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 获取或设置操作名称。
        /// </summary>
        public string OperationName { get; set; }

        /// <summary>
        /// 获取或设置HTTP方法。
        /// </summary>
        public NetworkMethod Method { get; set; } = NetworkMethod.Get;

        /// <summary>
        /// 获取或设置内容类型。
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// 获取或设置超时时间（秒）。
        /// </summary>
        public int TimeoutSeconds { get; set; }

        /// <summary>
        /// 获取或设置是否使用基础URL。
        /// </summary>
        public bool UseBaseUrl { get; set; } = true;

        /// <summary>
        /// 获取或设置追踪ID。
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// 获取或设置网络策略。
        /// </summary>
        public NetworkPolicy Policy { get; set; }

        /// <summary>
        /// 获取请求头的只读字典。
        /// </summary>
        public IReadOnlyDictionary<string, string> Headers => _headers;

        /// <summary>
        /// 设置请求头。
        /// </summary>
        /// <param name="key">请求头键。</param>
        /// <param name="value">请求头值。</param>
        /// <exception cref="ArgumentException">当键为空时抛出。</exception>
        public void SetHeader(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Header key can not be empty.", nameof(key));
            }

            _headers[key] = value ?? string.Empty;
        }

        /// <summary>
        /// 移除请求头。
        /// </summary>
        /// <param name="key">请求头键。</param>
        /// <returns>如果成功移除则返回true，否则返回false。</returns>
        public bool RemoveHeader(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return _headers.Remove(key);
        }

        /// <summary>
        /// 清除所有请求头。
        /// </summary>
        public void ClearHeaders()
        {
            _headers.Clear();
        }
    }
}
