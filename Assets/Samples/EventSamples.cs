using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    public enum SampleEventId
    {
        Login = 1
    }

    [GameEvent("Sample.User.Login")]
    public sealed partial class SampleUserLoginEvent
    {
    }

    [GameEvent(10001)]
    public sealed partial class SampleNumericEvent
    {
    }

    public sealed partial class SampleEnumEvent
    {
    }


    [GameEvent(typeof(SampleUserLoginEvent))]
    public sealed class SampleUserLoginAsyncHandle : IAsyncEventHandle
    {
        public UniTask HandleAsync(IEventContext context)
        {
            _ = context.EventName;
            return UniTask.CompletedTask;
        }
    }
}
