using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Cache
{
    internal interface ICacheBucket
    {
        string Name { get; }

        CacheBucketSnapshot Snapshot();

        UniTask<int> TrimAsync();

        UniTask ClearAsync();
    }
}
