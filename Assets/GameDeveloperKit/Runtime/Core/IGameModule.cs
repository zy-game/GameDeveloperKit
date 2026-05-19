using Cysharp.Threading.Tasks;

namespace GameDeveloperKit
{
    public interface IGameModule : IReference
    {
        UniTask Startup();

        UniTask Shutdown();
    }
}
