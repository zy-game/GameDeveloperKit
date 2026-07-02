using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Cache
{
    public sealed class CacheBucketOptions<TKey, TValue>
    {
        public string Name { get; set; }

        public CacheEvictionMode EvictionMode { get; set; }

        public float TimeToLive { get; set; }

        public Func<TKey, TValue, float> TimeToLiveProvider { get; set; }

        public int Capacity { get; set; }

        public bool UseUnscaledTime { get; set; } = true;

        public Func<TKey, TValue, UniTask> Finalizer { get; set; }
    }
}
