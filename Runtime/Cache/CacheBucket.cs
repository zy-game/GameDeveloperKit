using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Cache
{
    public sealed class CacheBucket<TKey, TValue> : ICacheBucket
    {
        private readonly CacheModule m_Module;
        private readonly CacheBucketOptions<TKey, TValue> m_Options;
        private readonly Dictionary<TKey, CacheEntry> m_Entries = new Dictionary<TKey, CacheEntry>();
        private long m_HitCount;
        private long m_MissCount;
        private long m_EvictionCount;
        private long m_FinalizerExceptionCount;
        private Exception m_LastException;

        internal CacheBucket(CacheModule module, CacheBucketOptions<TKey, TValue> options)
        {
            m_Module = module ?? throw new ArgumentNullException(nameof(module));
            m_Options = ValidateOptions(options);
        }

        public string Name => m_Options.Name;

        public bool TryPut(TKey key, TValue value)
        {
            ValidateKey(key);
            ValidateValue(value);
            if (m_Entries.ContainsKey(key))
            {
                return false;
            }

            var now = GetCurrentTime();
            m_Entries.Add(key, new CacheEntry(key, value, now));
            return true;
        }

        public bool TryGet(TKey key, out TValue value)
        {
            ValidateKey(key);
            if (m_Entries.TryGetValue(key, out var entry))
            {
                entry.Touch(GetCurrentTime());
                m_HitCount++;
                value = entry.Value;
                return true;
            }

            m_MissCount++;
            value = default;
            return false;
        }

        public bool TryTake(TKey key, out TValue value)
        {
            ValidateKey(key);
            if (m_Entries.TryGetValue(key, out var entry) is false)
            {
                m_MissCount++;
                value = default;
                return false;
            }

            entry.Touch(GetCurrentTime());
            m_HitCount++;
            m_Entries.Remove(key);
            value = entry.Value;
            return true;
        }

        public async UniTask<bool> RemoveAsync(TKey key)
        {
            ValidateKey(key);
            if (m_Entries.TryGetValue(key, out var entry) is false)
            {
                return false;
            }

            m_Entries.Remove(key);
            await FinalizeEntryAsync(entry);
            return true;
        }

        public async UniTask<int> TrimAsync()
        {
            var victims = CollectVictims(GetCurrentTime());
            for (var i = 0; i < victims.Count; i++)
            {
                m_Entries.Remove(victims[i].Key);
            }

            for (var i = 0; i < victims.Count; i++)
            {
                await FinalizeEntryAsync(victims[i]);
            }

            return victims.Count;
        }

        public async UniTask ClearAsync()
        {
            var entries = m_Entries.Values.ToList();
            m_Entries.Clear();
            for (var i = 0; i < entries.Count; i++)
            {
                await FinalizeEntryAsync(entries[i]);
            }
        }

        CacheBucketSnapshot ICacheBucket.Snapshot()
        {
            return Snapshot();
        }

        public CacheBucketSnapshot Snapshot()
        {
            return new CacheBucketSnapshot(
                m_Options.Name,
                typeof(TKey),
                typeof(TValue),
                m_Options.EvictionMode,
                m_Entries.Count,
                m_HitCount,
                m_MissCount,
                m_EvictionCount,
                m_FinalizerExceptionCount,
                m_LastException);
        }

        private List<CacheEntry> CollectVictims(double now)
        {
            var victims = new List<CacheEntry>();
            if (m_Options.TimeToLive > 0f || m_Options.TimeToLiveProvider != null)
            {
                foreach (var entry in m_Entries.Values)
                {
                    var timeToLive = GetTimeToLive(entry);
                    if (timeToLive > 0f && now - entry.LastAccessTime >= timeToLive)
                    {
                        victims.Add(entry);
                    }
                }
            }

            if (m_Options.EvictionMode == CacheEvictionMode.Heat && m_Options.Capacity > 0)
            {
                var overflow = m_Entries.Count - victims.Count - m_Options.Capacity;
                if (overflow > 0)
                {
                    var victimKeys = new HashSet<TKey>(victims.Select(x => x.Key));
                    var coldEntries = m_Entries.Values
                        .Where(x => victimKeys.Contains(x.Key) is false)
                        .OrderBy(x => x.AccessCount)
                        .ThenBy(x => x.LastAccessTime)
                        .ThenBy(x => x.CreatedTime)
                        .Take(overflow);
                    victims.AddRange(coldEntries);
                }
            }

            return victims;
        }

        private async UniTask FinalizeEntryAsync(CacheEntry entry)
        {
            try
            {
                await m_Options.Finalizer(entry.Key, entry.Value);
            }
            catch (Exception exception)
            {
                m_LastException = exception;
                m_FinalizerExceptionCount++;
            }
            finally
            {
                m_EvictionCount++;
            }
        }

        private double GetCurrentTime()
        {
            return m_Module.GetCurrentTime(m_Options.UseUnscaledTime);
        }

        private float GetTimeToLive(CacheEntry entry)
        {
            return m_Options.TimeToLiveProvider != null
                ? m_Options.TimeToLiveProvider(entry.Key, entry.Value)
                : m_Options.TimeToLive;
        }

        private static CacheBucketOptions<TKey, TValue> ValidateOptions(CacheBucketOptions<TKey, TValue> options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.Name))
            {
                throw new ArgumentException("Cache bucket name cannot be empty.", nameof(options));
            }

            if (options.Finalizer == null)
            {
                throw new ArgumentException("Cache bucket finalizer cannot be null.", nameof(options));
            }

            if (options.TimeToLive < 0f)
            {
                throw new ArgumentException("Cache bucket TTL cannot be negative.", nameof(options));
            }

            switch (options.EvictionMode)
            {
                case CacheEvictionMode.Time:
                    if (options.TimeToLive <= 0f && options.TimeToLiveProvider == null)
                    {
                        throw new ArgumentException("Time cache bucket TTL or TTL provider is required.", nameof(options));
                    }

                    break;
                case CacheEvictionMode.Heat:
                    if (options.Capacity <= 0)
                    {
                        throw new ArgumentException("Heat cache bucket capacity must be greater than zero.", nameof(options));
                    }

                    break;
                default:
                    throw new ArgumentException("Cache eviction mode is not supported.", nameof(options));
            }

            return options;
        }

        private static void ValidateKey(TKey key)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }
        }

        private static void ValidateValue(TValue value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }
        }

        private sealed class CacheEntry
        {
            public CacheEntry(TKey key, TValue value, double now)
            {
                Key = key;
                Value = value;
                CreatedTime = now;
                LastAccessTime = now;
                AccessCount = 1;
            }

            public TKey Key { get; }

            public TValue Value { get; }

            public double CreatedTime { get; }

            public double LastAccessTime { get; private set; }

            public long AccessCount { get; private set; }

            public void Touch(double now)
            {
                LastAccessTime = now;
                AccessCount++;
            }
        }
    }
}
