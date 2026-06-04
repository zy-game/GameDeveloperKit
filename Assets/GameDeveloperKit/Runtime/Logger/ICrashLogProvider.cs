using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Logger
{
    public interface ICrashLogProvider
    {
        UniTask<IReadOnlyList<CrashLogArtifact>> CollectAsync();
    }
}
