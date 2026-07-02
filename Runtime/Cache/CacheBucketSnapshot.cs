using System;

namespace GameDeveloperKit.Cache
{
    public readonly struct CacheBucketSnapshot
    {
        internal CacheBucketSnapshot(
            string name,
            Type keyType,
            Type valueType,
            CacheEvictionMode evictionMode,
            int count,
            long hitCount,
            long missCount,
            long evictionCount,
            long finalizerExceptionCount,
            Exception lastException)
        {
            Name = name;
            KeyType = keyType;
            ValueType = valueType;
            EvictionMode = evictionMode;
            Count = count;
            HitCount = hitCount;
            MissCount = missCount;
            EvictionCount = evictionCount;
            FinalizerExceptionCount = finalizerExceptionCount;
            LastException = lastException;
        }

        public string Name { get; }

        public Type KeyType { get; }

        public Type ValueType { get; }

        public CacheEvictionMode EvictionMode { get; }

        public int Count { get; }

        public long HitCount { get; }

        public long MissCount { get; }

        public long EvictionCount { get; }

        public long FinalizerExceptionCount { get; }

        public Exception LastException { get; }
    }
}
