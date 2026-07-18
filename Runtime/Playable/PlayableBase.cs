using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Playable
{
    public abstract class PlayableBase<TOptions, THandle> : IPlayable
        where THandle : PlayableHandle
    {
        public abstract UniTask<THandle> PlayAsync(
            TOptions options,
            CancellationToken cancellationToken = default);

        public abstract void Dispose();
    }
}
