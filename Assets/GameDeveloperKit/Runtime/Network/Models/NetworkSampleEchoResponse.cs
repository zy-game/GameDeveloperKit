using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 网络示例回显响应类，用于接收网络测试的响应。
    /// </summary>
    [Serializable]
    public sealed class NetworkSampleEchoResponse
    {
        /// <summary>
        /// 获取或设置消息内容。
        /// </summary>
        public string Message;
    }
}
