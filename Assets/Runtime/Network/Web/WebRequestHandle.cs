using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// Web 请求处理器
    /// 封装 HTTP 请求的执行逻辑
    /// </summary>
    public class WebRequestHandle
    {
        private readonly string _method;
        private readonly string _url;
        private readonly Dictionary<string, string> _headers;
        private readonly string _contentType;
        private readonly string _body;
        private readonly Dictionary<string, string> _query;
        private readonly TimeSpan? _timeout;
        private readonly int _retryCount;
        private readonly TimeSpan _retryDelay;

        internal WebRequestHandle(
            string method,
            string url,
            Dictionary<string, string> headers,
            string contentType,
            string body,
            Dictionary<string, string> query,
            TimeSpan? timeout,
            int retryCount,
            TimeSpan retryDelay)
        {
            _method = method;
            _url = url;
            _headers = headers;
            _contentType = contentType;
            _body = body;
            _query = query;
            _timeout = timeout;
            _retryCount = retryCount;
            _retryDelay = retryDelay;
        }

        /// <summary>
        /// 执行请求并返回字符串结果
        /// </summary>
        public async UniTask<WebResult<string>> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            return await SendRequestAsync(cancellationToken);
        }

        /// <summary>
        /// 执行请求并返回泛型结果（自动 JSON 反序列化）
        /// </summary>
        public async UniTask<WebResult<T>> ExecuteAsync<T>(CancellationToken cancellationToken = default)
        {
            var stringResult = await SendRequestAsync(cancellationToken);

            var result = new WebResult<T>
            {
                IsSuccess = stringResult.IsSuccess,
                StatusCode = stringResult.StatusCode,
                Headers = stringResult.Headers,
                Error = stringResult.Error
            };

            if (stringResult.IsSuccess && !string.IsNullOrEmpty(stringResult.Data))
            {
                if (typeof(T) == typeof(string))
                {
                    result.Data = (T)(object)stringResult.Data;
                }
                else
                {
                    try
                    {
                        result.Data = JsonConvert.DeserializeObject<T>(stringResult.Data);
                    }
                    catch (Exception ex)
                    {
                        result.IsSuccess = false;
                        result.Error = $"Failed to deserialize response: {ex.Message}";
                    }
                }
            }

            return result;
        }

        private async UniTask<WebResult<string>> SendRequestAsync(CancellationToken cancellationToken)
        {
            Throw.Asserts(!string.IsNullOrEmpty(_method), "Method is null or empty");
            Throw.Asserts(!string.IsNullOrEmpty(_url), "URL is null or empty");

            var result = new WebResult<string>();
            int attemptCount = 0;
            int maxAttempts = _retryCount + 1;

            while (attemptCount < maxAttempts)
            {
                attemptCount++;
                cancellationToken.ThrowIfCancellationRequested();

                var finalUrl = ApplyQuery(_url, _query);

                using (var request = CreateRequest(_method, finalUrl, _headers, _contentType, _body))
                {
                    bool timedOut = false;
                    CancellationTokenSource timeoutCts = null;

                    try
                    {
                        if (_timeout.HasValue && _timeout.Value > TimeSpan.Zero)
                        {
                            timeoutCts = new CancellationTokenSource();
                            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                            var delayTask = UniTask.Delay(_timeout.Value, cancellationToken: timeoutCts.Token);
                            var requestTask = request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

                            var completedTask = await UniTask.WhenAny(requestTask, delayTask);

                            if (!completedTask.hasResultLeft)
                            {
                                timedOut = true;
                                request.Abort();
                            }
                        }
                        else
                        {
                            using (cancellationToken.Register(() => request.Abort()))
                            {
                                await request.SendWebRequest();
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (timedOut)
                        {
                            result.Error = "Request timeout";
                        }
                        else
                        {
                            throw;
                        }
                    }
                    finally
                    {
                        timeoutCts?.Cancel();
                        timeoutCts?.Dispose();
                    }

                    result.StatusCode = (int)request.responseCode;
                    result.Headers = request.GetResponseHeaders() ?? new Dictionary<string, string>();

#if UNITY_2020_2_OR_NEWER
                    bool isSuccess = request.result == UnityWebRequest.Result.Success;
#else
                    bool isSuccess = !(request.isHttpError || request.isNetworkError);
#endif

                    if (isSuccess)
                    {
                        result.IsSuccess = true;
                        result.Data = request.downloadHandler?.text;
                        return result;
                    }

                    result.Error = timedOut ? "Request timeout" : request.error;

                    if (attemptCount < maxAttempts)
                    {
                        if (_retryDelay > TimeSpan.Zero)
                        {
                            await UniTask.Delay(_retryDelay, cancellationToken: cancellationToken);
                        }

                        continue;
                    }

                    result.IsSuccess = false;
                    return result;
                }
            }

            return result;
        }

        private UnityWebRequest CreateRequest(string method, string url, IDictionary<string, string> headers, string contentType, string body)
        {
            UnityWebRequest request;

            if (method == UnityWebRequest.kHttpVerbGET)
            {
                request = UnityWebRequest.Get(url);
            }
            else if (method == UnityWebRequest.kHttpVerbDELETE)
            {
                request = UnityWebRequest.Delete(url);
            }
            else if (method == UnityWebRequest.kHttpVerbHEAD)
            {
                request = UnityWebRequest.Head(url);
            }
            else
            {
                request = new UnityWebRequest(url, method);
                var bytes = string.IsNullOrEmpty(body) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(body);
                request.uploadHandler = new UploadHandlerRaw(bytes);
            }

            request.downloadHandler = new DownloadHandlerBuffer();

            if (headers != null)
            {
                foreach (var kv in headers)
                {
                    request.SetRequestHeader(kv.Key, kv.Value);
                }
            }

            if (!string.IsNullOrEmpty(contentType))
            {
                request.SetRequestHeader("Content-Type", contentType);
            }

            return request;
        }

        private string ApplyQuery(string baseUrl, IDictionary<string, string> query)
        {
            if (query == null || query.Count == 0)
            {
                return baseUrl;
            }

            var sb = new StringBuilder();
            sb.Append(baseUrl);
            sb.Append(baseUrl.Contains("?") ? "&" : "?");

            bool first = true;
            foreach (var kv in query)
            {
                if (!first)
                {
                    sb.Append('&');
                }

                first = false;

                var k = Uri.EscapeDataString(kv.Key);
                var v = Uri.EscapeDataString(kv.Value);
                sb.Append(k);
                sb.Append('=');
                sb.Append(v);
            }

            return sb.ToString();
        }
    }
}