using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    public interface IEventHandle
    {
        void Handle(IEventContext context);
    }

    public interface IAsyncEventHandle
    {
        UniTask HandleAsync(IEventContext context);
    }
}
