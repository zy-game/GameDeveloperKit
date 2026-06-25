using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Debugger
{
    public interface IDebugLogNetworkSender
    {
        /// <summary>
        /// 发送 Debug Log。
        /// </summary>
        UniTask SendDebugLogAsync(DebugLogPayload payload);
    }
}
