using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Cache;
using GameDeveloperKit.Timer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Tests
{
    public sealed class CacheModuleTests : RuntimeTestBase
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Shutdown();
                while (true)
                {
                    var timerObject = GameObject.Find("Timer");
                    if (timerObject == null)
                    {
                        break;
                    }

                    Object.DestroyImmediate(timerObject);
                    await UniTask.Yield();
                }
            });
        }

        [Test]
        public void Register_WhenCacheModuleIsResolved_StartsTimerDependency()
        {
            var cache = App.Cache;

            Assert.IsNotNull(cache);
            Assert.IsTrue(App.TryGetRegistered<TimerModule>(out var timer));
            Assert.IsTrue(timer.Snapshot().Updates.Any(x => ReferenceEquals(x.Owner, cache) && x.Tag == "CacheModule.Update"));
        }

        [UnityTest]
        public IEnumerator TimeBucket_WhenTrimBeforeTtl_KeepsEntry()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var cache = App.Cache;
                var finalizerCount = 0;
                var bucket = cache.GetOrCreateBucket(new CacheBucketOptions<string, string>
                {
                    Name = UniqueName(),
                    EvictionMode = CacheEvictionMode.Time,
                    TimeToLive = 1f,
                    Finalizer = (_, _) =>
                    {
                        finalizerCount++;
                        return UniTask.CompletedTask;
                    },
                });

                Assert.IsTrue(bucket.TryPut("key", "value"));
                App.Timer.Update(0.5f, 0.5f);

                var trimmed = await cache.TrimAsync(bucket.Name);

                Assert.AreEqual(0, trimmed);
                Assert.AreEqual(0, finalizerCount);
                Assert.IsTrue(bucket.TryTake("key", out var value));
                Assert.AreEqual("value", value);
            });
        }

        [UnityTest]
        public IEnumerator TimeBucket_WhenTtlElapsed_EvictsEntryOnce()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var cache = App.Cache;
                var finalized = new List<string>();
                var bucket = cache.GetOrCreateBucket(new CacheBucketOptions<string, string>
                {
                    Name = UniqueName(),
                    EvictionMode = CacheEvictionMode.Time,
                    TimeToLive = 0.25f,
                    Finalizer = (key, value) =>
                    {
                        finalized.Add($"{key}:{value}");
                        return UniTask.CompletedTask;
                    },
                });

                Assert.IsTrue(bucket.TryPut("key", "value"));
                App.Timer.Update(0.3f, 0.3f);

                var trimmed = await bucket.TrimAsync();

                Assert.AreEqual(1, trimmed);
                CollectionAssert.AreEqual(new[] { "key:value" }, finalized);
                Assert.IsFalse(bucket.TryTake("key", out _));
                Assert.AreEqual(1, bucket.Snapshot().EvictionCount);
            });
        }

        [UnityTest]
        public IEnumerator TimeBucket_WhenProviderSuppliesTtl_EvictsByProviderValue()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var cache = App.Cache;
                var finalized = 0;
                var bucket = cache.GetOrCreateBucket(new CacheBucketOptions<string, float>
                {
                    Name = UniqueName(),
                    EvictionMode = CacheEvictionMode.Time,
                    TimeToLiveProvider = (_, value) => value,
                    Finalizer = (_, _) =>
                    {
                        finalized++;
                        return UniTask.CompletedTask;
                    },
                });

                Assert.IsTrue(bucket.TryPut("key", 0.2f));
                App.Timer.Update(0.25f, 0.25f);

                Assert.AreEqual(1, await bucket.TrimAsync());
                Assert.AreEqual(1, finalized);
            });
        }

        [UnityTest]
        public IEnumerator HeatBucket_WhenCapacityExceeded_EvictsColdestEntry()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var cache = App.Cache;
                var finalized = new List<string>();
                var bucket = cache.GetOrCreateBucket(new CacheBucketOptions<string, string>
                {
                    Name = UniqueName(),
                    EvictionMode = CacheEvictionMode.Heat,
                    Capacity = 2,
                    Finalizer = (_, value) =>
                    {
                        finalized.Add(value);
                        return UniTask.CompletedTask;
                    },
                });

                Assert.IsTrue(bucket.TryPut("hot", "hot-value"));
                Assert.IsTrue(bucket.TryPut("cold", "cold-value"));
                Assert.IsTrue(bucket.TryGet("hot", out _));
                App.Timer.Update(0.1f, 0.1f);
                Assert.IsTrue(bucket.TryPut("new", "new-value"));

                var trimmed = await bucket.TrimAsync();

                Assert.AreEqual(1, trimmed);
                CollectionAssert.AreEqual(new[] { "cold-value" }, finalized);
                Assert.IsTrue(bucket.TryTake("hot", out _));
                Assert.IsTrue(bucket.TryTake("new", out _));
                Assert.IsFalse(bucket.TryTake("cold", out _));
            });
        }

        [UnityTest]
        public IEnumerator Finalizer_WhenThrows_RecordsExceptionAndContinues()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var cache = App.Cache;
                var finalized = new List<string>();
                var bucket = cache.GetOrCreateBucket(new CacheBucketOptions<string, string>
                {
                    Name = UniqueName(),
                    EvictionMode = CacheEvictionMode.Time,
                    TimeToLive = 0.1f,
                    Finalizer = (key, _) =>
                    {
                        finalized.Add(key);
                        if (key == "bad")
                        {
                            throw new InvalidOperationException("finalizer failed");
                        }

                        return UniTask.CompletedTask;
                    },
                });

                Assert.IsTrue(bucket.TryPut("bad", "bad-value"));
                Assert.IsTrue(bucket.TryPut("good", "good-value"));
                App.Timer.Update(0.2f, 0.2f);

                var trimmed = await bucket.TrimAsync();
                var snapshot = bucket.Snapshot();

                Assert.AreEqual(2, trimmed);
                CollectionAssert.AreEquivalent(new[] { "bad", "good" }, finalized);
                Assert.AreEqual(2, snapshot.EvictionCount);
                Assert.AreEqual(1, snapshot.FinalizerExceptionCount);
                Assert.IsInstanceOf<InvalidOperationException>(snapshot.LastException);
            });
        }

        [Test]
        public void GetOrCreateBucket_WhenOptionsInvalid_Throws()
        {
            var cache = App.Cache;

            Assert.Throws<ArgumentException>(() => cache.GetOrCreateBucket(new CacheBucketOptions<string, string>
            {
                Name = "",
                EvictionMode = CacheEvictionMode.Time,
                TimeToLive = 1f,
                Finalizer = (_, _) => UniTask.CompletedTask,
            }));
            Assert.Throws<ArgumentException>(() => cache.GetOrCreateBucket(new CacheBucketOptions<string, string>
            {
                Name = UniqueName(),
                EvictionMode = CacheEvictionMode.Time,
                TimeToLive = 1f,
            }));
            Assert.Throws<ArgumentException>(() => cache.GetOrCreateBucket(new CacheBucketOptions<string, string>
            {
                Name = UniqueName(),
                EvictionMode = CacheEvictionMode.Time,
                TimeToLive = 0f,
                Finalizer = (_, _) => UniTask.CompletedTask,
            }));
            Assert.Throws<ArgumentException>(() => cache.GetOrCreateBucket(new CacheBucketOptions<string, string>
            {
                Name = UniqueName(),
                EvictionMode = CacheEvictionMode.Heat,
                Capacity = 0,
                Finalizer = (_, _) => UniTask.CompletedTask,
            }));
        }

        private static string UniqueName()
        {
            return $"CacheModuleTests.{Guid.NewGuid():N}";
        }
    }
}
