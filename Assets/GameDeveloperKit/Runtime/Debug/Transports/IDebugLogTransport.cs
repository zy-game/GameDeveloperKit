using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Logger
{
    public interface IDebugLogTransport
    {
        UniTask SendAsync(DebugLogRecord record);
    }
}
