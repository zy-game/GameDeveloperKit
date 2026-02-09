using System.Collections.Generic;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// Web 请求响应结果
    /// </summary>
    /// <typeparam name="T">响应数据类型</typeparam>
    public class WebResult<T>
    {
        /// <summary>
        /// 请求是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// HTTP 状态码
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// 响应头
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }

        /// <summary>
        /// 响应数据
        /// </summary>
        public T Data { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string Error { get; set; }

        public WebResult()
        {
            Headers = new Dictionary<string, string>();
        }
    }
}
