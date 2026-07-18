using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.File;
using GameDeveloperKit.Resource;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class ResourceBundleCacheTests : RuntimeTestBase
    {
        private const string Sha1A = "0123456789abcdef0123456789abcdef01234567";
        private const string Sha1B = "89abcdef0123456789abcdef0123456789abcdef";
        private FileModule m_FileModule;
        private string m_RootPath;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                if (m_FileModule != null)
                {
                    await ((IAsyncShutdownParticipant)m_FileModule).PrepareShutdownAsync();
                    m_FileModule.Shutdown();
                    m_FileModule = null;
                }

                if (string.IsNullOrWhiteSpace(m_RootPath) is false && Directory.Exists(m_RootPath))
                {
                    Directory.Delete(m_RootPath, true);
                    m_RootPath = null;
                }
            });
        }

        [Test]
        public void CacheIdentity_WhenInputsDiffer_IsDeterministicAndUnambiguous()
        {
            var first = CreateBundle("hot.bundle", Sha1A, 10);
            var second = CreateBundle("hot.bundle", Sha1B, 10);

            Assert.AreEqual(
                "resource/bundles/hot.bundle",
                BundleAssetProvider.CreateBundleCacheKey(first));
            Assert.AreEqual(
                "2:1240:" + Sha1A,
                BundleAssetProvider.CreateBundleCacheVersion("12", first));
            Assert.AreNotEqual(
                BundleAssetProvider.CreateBundleCacheVersion("12", first),
                BundleAssetProvider.CreateBundleCacheVersion("1", second));
        }

        [UnityTest]
        public IEnumerator ValidateBundleIdentityAsync_WhenSizeAndSha1Match_Succeeds()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var bytes = Encoding.UTF8.GetBytes("verified-bundle-bytes");
                var bundle = CreateBundle("verified.bundle", ComputeSha1(bytes), bytes.Length);
                using (var stream = new MemoryStream(bytes, false))
                {
                    await BundleAssetProvider.ValidateBundleIdentityAsync(stream, bundle, "test");
                    Assert.AreEqual(stream.Length, stream.Position);
                }
            });
        }

        [UnityTest]
        public IEnumerator ValidateBundleIdentityAsync_WhenSizeOrSha1Differs_Throws()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var bytes = Encoding.UTF8.GetBytes("corrupt-bundle-bytes");
                using (var stream = new MemoryStream(bytes, false))
                {
                    var sizeMismatch = CreateBundle("size.bundle", ComputeSha1(bytes), bytes.Length + 1);
                    var sizeException = await ThrowsAsync<InvalidDataException>(async () =>
                    {
                        await BundleAssetProvider.ValidateBundleIdentityAsync(stream, sizeMismatch, "test");
                    });
                    StringAssert.Contains("size mismatch", sizeException.Message);
                }

                using (var stream = new MemoryStream(bytes, false))
                {
                    var hashMismatch = CreateBundle("hash.bundle", Sha1A, bytes.Length);
                    var hashException = await ThrowsAsync<InvalidDataException>(async () =>
                    {
                        await BundleAssetProvider.ValidateBundleIdentityAsync(stream, hashMismatch, "test");
                    });
                    StringAssert.Contains("SHA-1 mismatch", hashException.Message);
                }
            });
        }

        [UnityTest]
        public IEnumerator PruneBundleCacheAsync_WhenEntriesAreCurrentStaleOrRemoved_KeepsOnlyCurrent()
        {
            return UniTask.ToCoroutine(async () =>
            {
                m_RootPath = Path.Combine(
                    Application.temporaryCachePath,
                    "resource-bundle-cache-tests",
                    Guid.NewGuid().ToString("N"));
                m_FileModule = new FileModule(m_RootPath);
                m_FileModule.Startup();

                var current = CreateBundle("current.bundle", Sha1A, 1);
                var stale = CreateBundle("stale.bundle", Sha1B, 1);
                var manifest = new ManifestInfo
                {
                    Version = "v2",
                    Packages = new List<PackageInfo>
                    {
                        new PackageInfo
                        {
                            Name = "Remote",
                            Bundles = new List<BundleInfo> { current, stale }
                        }
                    }
                };
                var index = new ResourceManifestIndex(
                    manifest,
                    new[] { current.Name, stale.Name });
                var currentKey = BundleAssetProvider.CreateBundleCacheKey(current);
                var staleKey = BundleAssetProvider.CreateBundleCacheKey(stale);
                var removedKey = BundleAssetProvider.BundleCachePrefix + "removed.bundle";
                await m_FileModule.WriteAsync(
                    currentKey,
                    BundleAssetProvider.CreateBundleCacheVersion(manifest.Version, current),
                    new byte[] { 1 });
                await m_FileModule.WriteAsync(staleKey, "old-version", new byte[] { 2 });
                await m_FileModule.WriteAsync(removedKey, "old-version", new byte[] { 3 });
                await m_FileModule.WriteAsync("other/module.bin", "v1", new byte[] { 4 });

                CollectionAssert.AreEquivalent(
                    new[] { currentKey, staleKey, removedKey },
                    m_FileModule.ListPaths(BundleAssetProvider.BundleCachePrefix));

                await ResourceModule.PruneBundleCacheAsync(m_FileModule, index);

                Assert.IsTrue(m_FileModule.Exists(
                    currentKey,
                    BundleAssetProvider.CreateBundleCacheVersion(manifest.Version, current)));
                Assert.IsFalse(m_FileModule.Exists(staleKey));
                Assert.IsFalse(m_FileModule.Exists(removedKey));
                Assert.IsTrue(m_FileModule.Exists("other/module.bin", "v1"));
            });
        }

        private static BundleInfo CreateBundle(string name, string hash, long size)
        {
            return new BundleInfo
            {
                Name = name,
                Hash = hash,
                Size = size,
                ProviderId = ResourceProviderIds.AssetBundle,
                Assets = new List<AssetInfo>()
            };
        }

        private static string ComputeSha1(byte[] bytes)
        {
            using (var sha1 = SHA1.Create())
            {
                return BitConverter.ToString(sha1.ComputeHash(bytes))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private static async UniTask<TException> ThrowsAsync<TException>(Func<UniTask> action)
            where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException exception)
            {
                return exception;
            }

            Assert.Fail($"Expected exception of type {typeof(TException).Name}.");
            return null;
        }
    }
}
