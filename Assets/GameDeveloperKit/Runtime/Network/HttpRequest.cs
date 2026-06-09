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

        public static HttpRequest Get(string url)
        {
            return new HttpRequest(url);
        }

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
