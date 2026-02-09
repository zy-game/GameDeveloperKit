using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// Web 模块
    /// 提供 HTTP 请求的 Builder 模式和直接执行两种方式
    /// </summary>
    public sealed class WebModule : IModule, IWebManager
    {
        public void OnStartup()
        {
        }

        public void OnUpdate(float elapseSeconds)
        {
        }

        public void OnClearup()
        {
        }


        /// <summary>
        /// 执行 GET 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="timeoutSeconds">超时时间（秒）</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async UniTask<WebResult<string>> GetAsync(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default)
        {
            var builder = new WebRequestBuilder(UnityWebRequest.kHttpVerbGET, url);
            if (timeoutSeconds.HasValue) builder.SetTimeout(timeoutSeconds.Value);
            return await builder.Build().ExecuteAsync(cancellationToken);
        }

        /// <summary>
        /// 执行 GET 请求（泛型版本）
        /// </summary>
        public async UniTask<WebResult<T>> GetAsync<T>(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default)
        {
            var builder = new WebRequestBuilder(UnityWebRequest.kHttpVerbGET, url);
            if (timeoutSeconds.HasValue) builder.SetTimeout(timeoutSeconds.Value);
            return await builder.Build().ExecuteAsync<T>(cancellationToken);
        }

        /// <summary>
        /// 执行 POST 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="timeoutSeconds">超时时间（秒）</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async UniTask<WebResult<string>> PostAsync(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default)
        {
            var builder = new WebRequestBuilder(UnityWebRequest.kHttpVerbPOST, url);
            if (timeoutSeconds.HasValue) builder.SetTimeout(timeoutSeconds.Value);
            return await builder.Build().ExecuteAsync(cancellationToken);
        }

        /// <summary>
        /// 执行 POST 请求（泛型版本）
        /// </summary>
        public async UniTask<WebResult<T>> PostAsync<T>(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default)
        {
            var builder = new WebRequestBuilder(UnityWebRequest.kHttpVerbPOST, url);
            if (timeoutSeconds.HasValue) builder.SetTimeout(timeoutSeconds.Value);
            return await builder.Build().ExecuteAsync<T>(cancellationToken);
        }

        /// <summary>
        /// 执行 PUT 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="timeoutSeconds">超时时间（秒）</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async UniTask<WebResult<string>> PutAsync(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default)
        {
            var builder = new WebRequestBuilder(UnityWebRequest.kHttpVerbPUT, url);
            if (timeoutSeconds.HasValue) builder.SetTimeout(timeoutSeconds.Value);
            return await builder.Build().ExecuteAsync(cancellationToken);
        }

        /// <summary>
        /// 执行 PUT 请求（泛型版本）
        /// </summary>
        public async UniTask<WebResult<T>> PutAsync<T>(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default)
        {
            var builder = new WebRequestBuilder(UnityWebRequest.kHttpVerbPUT, url);
            if (timeoutSeconds.HasValue) builder.SetTimeout(timeoutSeconds.Value);
            return await builder.Build().ExecuteAsync<T>(cancellationToken);
        }

        /// <summary>
        /// 执行 DELETE 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="timeoutSeconds">超时时间（秒）</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async UniTask<WebResult<string>> DeleteAsync(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default)
        {
            var builder = new WebRequestBuilder(UnityWebRequest.kHttpVerbDELETE, url);
            if (timeoutSeconds.HasValue) builder.SetTimeout(timeoutSeconds.Value);
            return await builder.Build().ExecuteAsync(cancellationToken);
        }

        /// <summary>
        /// 执行 DELETE 请求（泛型版本）
        /// </summary>
        public async UniTask<WebResult<T>> DeleteAsync<T>(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default)
        {
            var builder = new WebRequestBuilder(UnityWebRequest.kHttpVerbDELETE, url);
            if (timeoutSeconds.HasValue) builder.SetTimeout(timeoutSeconds.Value);
            return await builder.Build().ExecuteAsync<T>(cancellationToken);
        }

        /// <summary>
        /// 执行 HEAD 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="timeoutSeconds">超时时间（秒）</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async UniTask<WebResult<string>> HeadAsync(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default)
        {
            var builder = new WebRequestBuilder(UnityWebRequest.kHttpVerbHEAD, url);
            if (timeoutSeconds.HasValue) builder.SetTimeout(timeoutSeconds.Value);
            return await builder.Build().ExecuteAsync(cancellationToken);
        }

        /// <summary>
        /// 创建 GET 请求构建器
        /// </summary>
        public WebRequestBuilder Get(string url) => new WebRequestBuilder(UnityWebRequest.kHttpVerbGET, url);

        /// <summary>
        /// 创建 POST 请求构建器
        /// </summary>
        public WebRequestBuilder Post(string url) => new WebRequestBuilder(UnityWebRequest.kHttpVerbPOST, url);

        /// <summary>
        /// 创建 PUT 请求构建器
        /// </summary>
        public WebRequestBuilder Put(string url) => new WebRequestBuilder(UnityWebRequest.kHttpVerbPUT, url);

        /// <summary>
        /// 创建 DELETE 请求构建器
        /// </summary>
        public WebRequestBuilder Delete(string url) => new WebRequestBuilder(UnityWebRequest.kHttpVerbDELETE, url);

        /// <summary>
        /// 创建 HEAD 请求构建器
        /// </summary>
        public WebRequestBuilder Head(string url) => new WebRequestBuilder(UnityWebRequest.kHttpVerbHEAD, url);
    }
}