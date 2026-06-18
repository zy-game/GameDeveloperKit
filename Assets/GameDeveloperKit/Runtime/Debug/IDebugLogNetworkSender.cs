using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Logger
{
    /// <summary>
    /// 定义 Debug Log Network Sender 接口。
    /// </summary>
    public interface IDebugLogNetworkSender
    {
        /// <summary>
        /// 发送 Debug Log。
        /// </summary>
        /// <param name="payload">payload 参数。</param>
        /// <returns>操作完成任务。</returns>
        UniTask SendDebugLogAsync(DebugLogPayload payload);
    }
}
