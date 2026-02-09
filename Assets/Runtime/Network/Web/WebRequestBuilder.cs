using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// Web 请求构建器
    /// 提供链式调用的 API 来配置 Web 请求
    /// </summary>
    public class WebRequestBuilder
    {
        private readonly string _method;
        private readonly string _url;
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>();
        private string _contentType = "application/json";
        private string _body;
        private Dictionary<string, string> _query;
        private TimeSpan? _timeout;
        private int _retryCount = 0;
        private TimeSpan _retryDelay = TimeSpan.FromSeconds(1);

        internal WebRequestBuilder(string method, string url)
        {
            _method = method;
            _url = url;
        }

        /// <summary>
        /// 设置请求头
        /// </summary>
        public WebRequestBuilder SetHeaders(IDictionary<string, string> headers)
        {
            if (headers != null)
            {
                foreach (var kv in headers)
                {
                    _headers[kv.Key] = kv.Value;
                }
            }
            return this;
        }

        /// <summary>
        /// 添加单个请求头
        /// </summary>
        public WebRequestBuilder AddHeader(string name, string value)
        {
            _headers[name] = value;
            return this;
        }

        /// <summary>
        /// 设置 Content-Type
        /// </summary>
        public WebRequestBuilder SetContentType(string contentType)
        {
            _contentType = contentType;
            return this;
        }

        /// <summary>
        /// 设置请求体（字符串）
        /// </summary>
        public WebRequestBuilder SetBody(string body)
        {
            _body = body;
            return this;
        }

        /// <summary>
        /// 设置请求体（JSON 对象）
        /// </summary>
        public WebRequestBuilder SetBodyJson(object obj)
        {
            _body = JsonConvert.SerializeObject(obj);
            return this;
        }

        /// <summary>
        /// 设置查询参数
        /// </summary>
        public WebRequestBuilder SetQuery(IDictionary<string, string> query)
        {
            if (_query == null)
            {
                _query = new Dictionary<string, string>();
            }

            if (query != null)
            {
                foreach (var kv in query)
                {
                    _query[kv.Key] = kv.Value;
                }
            }
            return this;
        }

        /// <summary>
        /// 添加单个查询参数
        /// </summary>
        public WebRequestBuilder AddQuery(string name, string value)
        {
            if (_query == null)
            {
                _query = new Dictionary<string, string>();
            }
            _query[name] = value;
            return this;
        }

        /// <summary>
        /// 设置超时时间
        /// </summary>
        public WebRequestBuilder SetTimeout(TimeSpan timeout)
        {
            _timeout = timeout;
            return this;
        }

        /// <summary>
        /// 设置超时时间（秒）
        /// </summary>
        public WebRequestBuilder SetTimeout(int seconds)
        {
            _timeout = TimeSpan.FromSeconds(seconds);
            return this;
        }

        /// <summary>
        /// 设置重试次数
        /// </summary>
        public WebRequestBuilder SetRetryCount(int count)
        {
            _retryCount = count;
            return this;
        }

        /// <summary>
        /// 设置重试延迟
        /// </summary>
        public WebRequestBuilder SetRetryDelay(TimeSpan delay)
        {
            _retryDelay = delay;
            return this;
        }

        /// <summary>
        /// 设置重试延迟（秒）
        /// </summary>
        public WebRequestBuilder SetRetryDelay(int seconds)
        {
            _retryDelay = TimeSpan.FromSeconds(seconds);
            return this;
        }

        /// <summary>
        /// 构建并返回 Web 请求处理器
        /// </summary>
        /// <returns>Web 请求处理器</returns>
        public WebRequestHandle Build()
        {
            return new WebRequestHandle(
                _method,
                _url,
                _headers,
                _contentType,
                _body,
                _query,
                _timeout,
                _retryCount,
                _retryDelay);
        }
    }
}
