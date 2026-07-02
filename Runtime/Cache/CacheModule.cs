using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Timer;

namespace GameDeveloperKit.Cache
{
    [ModuleDependency(typeof(TimerModule))]
    public sealed class CacheModule : GameModuleBase
    {
        private const string UpdateTag = "CacheModule.Update";
        private readonly Dictionary<string, ICacheBucket> m_Buckets = new Dictionary<string, ICacheBucket>(StringComparer.Ordinal);
        private UpdateTimerHandle m_UpdateHandle;
        private bool m_IsTrimming;
        private Exception m_LastException;

        public override void Startup()
        {
            if (m_UpdateHandle != null && m_UpdateHandle.IsCancelled is false && m_UpdateHandle.IsCompleted is false)
            {
                return;
            }

            m_UpdateHandle = App.Timer.OnUpdate(OnCacheUpdate, this, UpdateTag);
        }

        public override void Shutdown()
        {
            if (m_UpdateHandle != null)
            {
                m_UpdateHandle.Cancel();
                m_UpdateHandle = null;
            }

            ClearAsync().Forget(exception => m_LastException = exception);
            m_Buckets.Clear();
            m_IsTrimming = false;
        }

        public CacheBucket<TKey, TValue> GetOrCreateBucket<TKey, TValue>(CacheBucketOptions<TKey, TValue> options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.Name))
            {
                throw new ArgumentException("Cache bucket name cannot be empty.", nameof(options));
            }

            if (m_Buckets.TryGetValue(options.Name, out var existing))
            {
                if (existing is CacheBucket<TKey, TValue> typed)
                {
                    return typed;
                }

                throw new GameException($"Cache bucket '{options.Name}' already exists with another key or value type.");
            }

            var bucket = new CacheBucket<TKey, TValue>(this, options);
            m_Buckets.Add(options.Name, bucket);
            return bucket;
        }

        public bool TryGetBucket<TKey, TValue>(string name, out CacheBucket<TKey, TValue> bucket)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Cache bucket name cannot be empty.", nameof(name));
            }

            if (m_Buckets.TryGetValue(name, out var existing) && existing is CacheBucket<TKey, TValue> typed)
            {
                bucket = typed;
                return true;
            }

            bucket = null;
            return false;
        }

        public async UniTask<int> TrimAsync(string name = null)
        {
            if (name != null)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("Cache bucket name cannot be empty.", nameof(name));
                }

                return m_Buckets.TryGetValue(name, out var bucket) ? await bucket.TrimAsync() : 0;
            }

            var count = 0;
            var buckets = new List<ICacheBucket>(m_Buckets.Values);
            for (var i = 0; i < buckets.Count; i++)
            {
                count += await buckets[i].TrimAsync();
            }

            return count;
        }

        public async UniTask ClearAsync(string name = null)
        {
            if (name != null)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("Cache bucket name cannot be empty.", nameof(name));
                }

                if (m_Buckets.TryGetValue(name, out var bucket))
                {
                    await bucket.ClearAsync();
                }

                return;
            }

            var buckets = new List<ICacheBucket>(m_Buckets.Values);
            for (var i = 0; i < buckets.Count; i++)
            {
                await buckets[i].ClearAsync();
            }
        }

        public CacheSnapshot Snapshot()
        {
            var snapshots = new List<CacheBucketSnapshot>(m_Buckets.Count);
            foreach (var bucket in m_Buckets.Values)
            {
                snapshots.Add(bucket.Snapshot());
            }

            return new CacheSnapshot(snapshots, m_LastException);
        }

        internal double GetCurrentTime(bool useUnscaledTime)
        {
            if (App.TryGetRegistered<TimerModule>(out var timer))
            {
                return useUnscaledTime ? timer.UnscaledTime : timer.Time;
            }

            return 0d;
        }

        private void OnCacheUpdate(TimerUpdateContext context)
        {
            if (m_IsTrimming)
            {
                return;
            }

            TrimFromUpdateAsync().Forget(exception => m_LastException = exception);
        }

        private async UniTask TrimFromUpdateAsync()
        {
            m_IsTrimming = true;
            try
            {
                await TrimAsync();
            }
            catch (Exception exception)
            {
                m_LastException = exception;
            }
            finally
            {
                m_IsTrimming = false;
            }
        }
    }
}
