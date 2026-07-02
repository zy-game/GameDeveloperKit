using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Cache
{
    public readonly struct CacheSnapshot
    {
        internal CacheSnapshot(IReadOnlyList<CacheBucketSnapshot> buckets, Exception lastException)
        {
            Buckets = buckets ?? Array.Empty<CacheBucketSnapshot>();
            LastException = lastException;
            var entryCount = 0;
            for (var i = 0; i < Buckets.Count; i++)
            {
                entryCount += Buckets[i].Count;
            }

            BucketCount = Buckets.Count;
            EntryCount = entryCount;
        }

        public IReadOnlyList<CacheBucketSnapshot> Buckets { get; }

        public int BucketCount { get; }

        public int EntryCount { get; }

        public Exception LastException { get; }
    }
}
