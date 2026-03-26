using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 网络请求，封装HTTP请求的配置和数据
    /// </summary>
    public sealed class NetworkRequest
    {
        private readonly Dictionary<string, string> _headers = new(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置请求URL
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 获取或设置请求方法
        /// </summary>
        public NetworkMethod Method { get; set; } = NetworkMethod.Get;

        /// <summary>
        /// 获取或设置请求体
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// 获取或设置内容类型
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// 获取或设置超时时间（秒）
        /// </summary>
        public int TimeoutSeconds { get; set; }

        /// <summary>
        /// 获取或设置是否使用基础URL
        /// </summary>
        public bool UseBaseUrl { get; set; } = true;

        /// <summary>
        /// 获取或设置追踪ID
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// 获取或设置网络策略
        /// </summary>
        public NetworkPolicy Policy { get; set; }

        /// <summary>
        /// 获取请求头的只读集合
        /// </summary>
        public IReadOnlyDictionary<string, string> Headers => _headers;

        /// <summary>
        /// 设置请求头
        /// </summary>
        /// <param name="key">请求头键</param>
        /// <param name="value">请求头值</param>
        /// <exception cref="ArgumentException">请求头键为空</exception>
        public void SetHeader(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Header key can not be empty.", nameof(key));
            }

            _headers[key] = value ?? string.Empty;
        }

        /// <summary>
        /// 移除请求头
        /// </summary>
        /// <param name="key">请求头键</param>
        /// <returns>如果移除成功返回true，否则返回false</returns>
        public bool RemoveHeader(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return _headers.Remove(key);
        }

        /// <summary>
        /// 清除所有请求头
        /// </summary>
        public void ClearHeaders()
        {
            _headers.Clear();
        }
    }
}
