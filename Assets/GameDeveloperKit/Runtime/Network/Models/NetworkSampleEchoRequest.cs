using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 网络示例回显请求类，用于测试网络连接。
    /// </summary>
    [Serializable]
    public sealed class NetworkSampleEchoRequest
    {
        /// <summary>
        /// 获取或设置消息内容。
        /// </summary>
        public string Message;
    }
}
