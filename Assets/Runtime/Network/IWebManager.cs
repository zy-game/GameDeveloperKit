using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    public interface IWebManager : IModule
    {
        /// <summary>
        /// 执行 GET 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="timeoutSeconds">超时时间（秒）</param>
        /// <param name="cancellationToken">取消令牌</param>
        UniTask<WebResult<string>> GetAsync(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 执行 GET 请求（泛型版本）
        /// </summary>
        UniTask<WebResult<T>> GetAsync<T>(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 执行 POST 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="timeoutSeconds">超时时间（秒）</param>
        /// <param name="cancellationToken">取消令牌</param>
        UniTask<WebResult<string>> PostAsync(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 执行 POST 请求（泛型版本）
        /// </summary>
        UniTask<WebResult<T>> PostAsync<T>(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 执行 PUT 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="timeoutSeconds">超时时间（秒）</param>
        /// <param name="cancellationToken">取消令牌</param>
        UniTask<WebResult<string>> PutAsync(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 执行 PUT 请求（泛型版本）
        /// </summary>
        UniTask<WebResult<T>> PutAsync<T>(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 执行 DELETE 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="timeoutSeconds">超时时间（秒）</param>
        /// <param name="cancellationToken">取消令牌</param>
        UniTask<WebResult<string>> DeleteAsync(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 执行 DELETE 请求（泛型版本）
        /// </summary>
        UniTask<WebResult<T>> DeleteAsync<T>(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 执行 HEAD 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="timeoutSeconds">超时时间（秒）</param>
        /// <param name="cancellationToken">取消令牌</param>
        UniTask<WebResult<string>> HeadAsync(string url, int? timeoutSeconds = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 创建 GET 请求构建器
        /// </summary>
        WebRequestBuilder Get(string url);

        /// <summary>
        /// 创建 POST 请求构建器
        /// </summary>
        WebRequestBuilder Post(string url);

        /// <summary>
        /// 创建 PUT 请求构建器
        /// </summary>
        WebRequestBuilder Put(string url);

        /// <summary>
        /// 创建 DELETE 请求构建器
        /// </summary>
        WebRequestBuilder Delete(string url);

        /// <summary>
        /// 创建 HEAD 请求构建器
        /// </summary>
        WebRequestBuilder Head(string url);
    }
}