using Cysharp.Threading.Tasks;

namespace GameDeveloperKit
{
    public interface IGameFrameworkModule : IReference
    {
        UniTask Startup();

        UniTask Shutdown();
    }
}
