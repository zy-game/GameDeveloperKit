using System;
using System.Collections.Generic;
using System.Text;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// HTTP 请求描述。
    /// </summary>
    public readonly struct HttpRequest
    {
        /// <summary>
        /// 初始化 Http Request。
        /// </summary>
        /// <param name="url">url 参数。</param>
        /// <param name="method">method 参数。</param>
        /// <param name="headers">headers 参数。</param>
        /// <param name="body">body 参数。</param>
        /// <param name="timeout">timeout 参数。</param>
        public HttpRequest(
            string url,
            NetworkHttpMethod method = NetworkHttpMethod.Get,
            IReadOnlyDictionary<string, string> headers = null,
            byte[] body = null,
            TimeSpan timeout = default)
        {
            Url = url;
            Method = method;
            Headers = headers;
            Body = body;
            Timeout = timeout;
        }

        public string Url { get; }

        public NetworkHttpMethod Method { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public byte[] Body { get; }

        public TimeSpan Timeout { get; }

        /// <summary>
        /// 获取 member。
        /// </summary>
        /// <param name="url">url 参数。</param>
        /// <returns>执行结果。</returns>
        public static HttpRequest Get(string url)
        {
            return new HttpRequest(url);
        }

        /// <summary>
        /// 执行 Post Json。
        /// </summary>
        /// <param name="url">url 参数。</param>
        /// <param name="json">json 参数。</param>
        /// <param name="timeout">timeout 参数。</param>
        /// <returns>执行结果。</returns>
        public static HttpRequest PostJson(string url, string json, TimeSpan timeout = default)
        {
            var headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            };
            var body = json == null ? null : Encoding.UTF8.GetBytes(json);
            return new HttpRequest(url, NetworkHttpMethod.Post, headers, body, timeout);
        }
    }
}
