using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Localization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class LocalizationModuleTests
    {
        [Test]
        public void Startup_WhenModuleStarts_HasEmptySnapshot()
        {
            var loader = new TestLocalizationAssetLoader();
            var module = new LocalizationModule(loader);

            module.Startup();
            var snapshot = module.Snapshot();

            Assert.IsNull(module.CatalogLocation);
            Assert.IsNull(snapshot.CurrentLocale);
            Assert.IsNull(snapshot.FallbackLocale);
            Assert.AreEqual(0, snapshot.LoadedLocales.Count);
            Assert.AreEqual(0, snapshot.MissingEntries.Count);
        }

        [Test]
        public void InitializeAsync_WhenAssetsAreValid_LoadsOnlyRequestedClosure()
        {
            using var fixture = new LocalizationFixture();

            fixture.Module.InitializeAsync(LocalizationFixture.CatalogPath, "en-US").GetAwaiter().GetResult();

            Assert.AreEqual(LocalizationFixture.CatalogPath, fixture.Module.CatalogLocation);
            Assert.AreEqual("en-US", fixture.Module.CurrentLocale);
            Assert.AreEqual("zh-CN", fixture.Module.FallbackLocale);
            CollectionAssert.AreEqual(
                new[]
                {
                    LocalizationFixture.CatalogPath,
                    LocalizationFixture.EnUsPath,
                    LocalizationFixture.ZhCnPath
                },
                fixture.Loader.LoadedLocations);
            CollectionAssert.DoesNotContain(fixture.Loader.LoadedLocations, LocalizationFixture.JaJpPath);
            Assert.AreEqual("Start", fixture.Module.GetText("ui.start"));
            Assert.AreEqual("仅中文", fixture.Module.GetText("ui.fallback"));
            Assert.AreEqual(string.Empty, fixture.Module.GetText("ui.empty"));
            Assert.IsTrue(fixture.Module.HasText("ui.empty"));
        }

        [Test]
        public void GetText_WhenAllLocalesMiss_ReturnsKeyAndRecordsClosureMissing()
        {
            using var fixture = new LocalizationFixture();
            fixture.Module.InitializeAsync(LocalizationFixture.CatalogPath, "en-US").GetAwaiter().GetResult();

            Assert.AreEqual("ui.missing", fixture.Module.GetText("ui.missing"));

            var missing = fixture.Module.Snapshot().MissingEntries;
            AssertMissing(missing, "en-US", "ui.missing");
            AssertMissing(missing, "zh-CN", "ui.missing");
            Assert.AreEqual(2, missing.Count);
        }

        [Test]
        public void SetLocaleAsync_WhenCandidateFails_PreservesOldState()
        {
            using var fixture = new LocalizationFixture();
            fixture.Module.InitializeAsync(LocalizationFixture.CatalogPath, "en-US").GetAwaiter().GetResult();
            fixture.Loader.Fail(LocalizationFixture.JaJpPath);

            Assert.Throws<GameException>(() =>
                fixture.Module.SetLocaleAsync("ja-JP").GetAwaiter().GetResult());

            Assert.AreEqual("en-US", fixture.Module.CurrentLocale);
            Assert.AreEqual("Start", fixture.Module.GetText("ui.start"));
        }

        [Test]
        public void SetLocaleAsync_WhenCancelled_PreservesOldState()
        {
            using var fixture = new LocalizationFixture();
            fixture.Module.InitializeAsync(LocalizationFixture.CatalogPath, "en-US").GetAwaiter().GetResult();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                fixture.Module.SetLocaleAsync("ja-JP", cancellation.Token).GetAwaiter().GetResult());

            Assert.AreEqual("en-US", fixture.Module.CurrentLocale);
            Assert.AreEqual("Start", fixture.Module.GetText("ui.start"));
        }

        [UnityTest]
        public IEnumerator SetLocaleAsync_WhenRequestsOverlap_OnlyLatestRequestCommits()
        {
            return UniTask.ToCoroutine(async () =>
            {
                using var fixture = new LocalizationFixture();
                await fixture.Module.InitializeAsync(LocalizationFixture.CatalogPath, "zh-CN");
                fixture.Loader.Gate(LocalizationFixture.EnUsPath);

                var superseded = fixture.Module.SetLocaleAsync("en-US");
                await fixture.Module.SetLocaleAsync("ja-JP");
                fixture.Loader.OpenGate(LocalizationFixture.EnUsPath);

                var wasSuperseded = false;
                try
                {
                    await superseded;
                }
                catch (OperationCanceledException)
                {
                    wasSuperseded = true;
                }

                Assert.IsTrue(wasSuperseded);
                Assert.AreEqual("ja-JP", fixture.Module.CurrentLocale);
                Assert.AreEqual("開始", fixture.Module.GetText("ui.start"));
                Assert.AreEqual(
                    fixture.Loader.LoadCount(LocalizationFixture.CatalogPath),
                    fixture.Loader.ReleaseCount(LocalizationFixture.CatalogPath) + 1);
            });
        }

        [Test]
        public void ReloadAsync_WhenContentChanges_AtomicallyRefreshesAndNotifies()
        {
            using var fixture = new LocalizationFixture();
            var events = 0;
            fixture.Module.LocaleChanged += _ => events++;
            fixture.Module.InitializeAsync(LocalizationFixture.CatalogPath, "en-US").GetAwaiter().GetResult();
            fixture.EnUs.Replace(
                "en-US",
                new[]
                {
                    new LocalizationValueEntry(1, "Continue"),
                    new LocalizationValueEntry(3, string.Empty),
                    new LocalizationValueEntry(4, "Damage {0}")
                },
                fixture.EnUs.Revision + 1);

            Assert.AreEqual("Start", fixture.Module.GetText("ui.start"));
            fixture.Module.ReloadAsync().GetAwaiter().GetResult();

            Assert.AreEqual("Continue", fixture.Module.GetText("ui.start"));
            Assert.AreEqual(2, events);
            Assert.AreEqual(1, fixture.Loader.ReleaseCount(LocalizationFixture.CatalogPath));
            Assert.AreEqual(1, fixture.Loader.ReleaseCount(LocalizationFixture.EnUsPath));
            Assert.AreEqual(1, fixture.Loader.ReleaseCount(LocalizationFixture.ZhCnPath));
        }

        [Test]
        public void ReloadAsync_WhenSchemaIsInvalid_PreservesOldState()
        {
            using var fixture = new LocalizationFixture();
            fixture.Module.InitializeAsync(LocalizationFixture.CatalogPath, "en-US").GetAwaiter().GetResult();
            fixture.Catalog.Replace(
                fixture.Catalog.CatalogId,
                fixture.Catalog.DefaultLocale,
                fixture.Catalog.Keys,
                fixture.Catalog.Locales,
                LocalizationCatalogAsset.CurrentSchemaVersion + 1);

            var exception = Assert.Throws<GameException>(() =>
                fixture.Module.ReloadAsync().GetAwaiter().GetResult());

            StringAssert.Contains("catalog_schema_unsupported", exception.Message);
            Assert.AreEqual("en-US", fixture.Module.CurrentLocale);
            Assert.AreEqual("Start", fixture.Module.GetText("ui.start"));
        }

        [Test]
        public void Shutdown_WhenCalled_ReleasesLeasesAndClearsStateAndEvents()
        {
            using var fixture = new LocalizationFixture();
            var events = 0;
            fixture.Module.LocaleChanged += _ => events++;
            fixture.Module.InitializeAsync(LocalizationFixture.CatalogPath, "en-US").GetAwaiter().GetResult();

            fixture.Module.Shutdown();

            Assert.IsNull(fixture.Module.CatalogLocation);
            Assert.IsNull(fixture.Module.CurrentLocale);
            Assert.AreEqual(0, fixture.Module.Snapshot().LoadedLocales.Count);
            Assert.AreEqual(1, fixture.Loader.ReleaseCount(LocalizationFixture.CatalogPath));
            Assert.AreEqual(1, fixture.Loader.ReleaseCount(LocalizationFixture.EnUsPath));
            Assert.AreEqual(1, fixture.Loader.ReleaseCount(LocalizationFixture.ZhCnPath));
            fixture.Module.Startup();
            fixture.Module.InitializeAsync(LocalizationFixture.CatalogPath, "zh-CN").GetAwaiter().GetResult();
            Assert.AreEqual(1, events);
        }

        [Test]
        public void Format_WhenArgumentsMatch_ReturnsFormattedText()
        {
            using var fixture = new LocalizationFixture();
            fixture.Module.InitializeAsync(LocalizationFixture.CatalogPath, "en-US").GetAwaiter().GetResult();

            Assert.AreEqual("Damage 120", fixture.Module.Format("battle.damage", 120));
            Assert.Throws<GameException>(() => fixture.Module.Format("battle.damage"));
        }

        [Test]
        public void ArgumentsAndUninitializedOperations_WhenInvalid_ThrowExpectedExceptions()
        {
            var module = new LocalizationModule(new TestLocalizationAssetLoader());
            module.Startup();

            Assert.Throws<ArgumentNullException>(() =>
                module.InitializeAsync(null, "en-US").GetAwaiter().GetResult());
            Assert.Throws<ArgumentException>(() =>
                module.InitializeAsync(" ", "en-US").GetAwaiter().GetResult());
            Assert.Throws<ArgumentNullException>(() =>
                module.SetLocaleAsync(null).GetAwaiter().GetResult());
            Assert.Throws<GameException>(() =>
                module.SetLocaleAsync("en-US").GetAwaiter().GetResult());
            Assert.Throws<GameException>(() => module.ReloadAsync().GetAwaiter().GetResult());
            Assert.Throws<ArgumentNullException>(() => module.GetText(null));
            Assert.Throws<ArgumentException>(() => module.GetText(" "));
        }

        private static void AssertMissing(
            IReadOnlyList<MissingLocalizationEntry> entries,
            string locale,
            string key)
        {
            Assert.IsTrue(entries.Any(entry => entry.Locale == locale && entry.Key == key),
                $"Missing entry was not found: {locale}/{key}");
        }

        private sealed class LocalizationFixture : IDisposable
        {
            public const string CatalogPath = "Localization/catalog";
            public const string ZhCnPath = "Localization/zh-CN";
            public const string EnUsPath = "Localization/en-US";
            public const string JaJpPath = "Localization/ja-JP";

            public LocalizationFixture()
            {
                Catalog = ScriptableObject.CreateInstance<LocalizationCatalogAsset>();
                ZhCn = ScriptableObject.CreateInstance<LocalizationLocaleAsset>();
                EnUs = ScriptableObject.CreateInstance<LocalizationLocaleAsset>();
                JaJp = ScriptableObject.CreateInstance<LocalizationLocaleAsset>();
                Catalog.Replace(
                    "catalog-main",
                    "zh-CN",
                    new[]
                    {
                        new LocalizationKeyEntry(1, "ui.start"),
                        new LocalizationKeyEntry(2, "ui.fallback"),
                        new LocalizationKeyEntry(3, "ui.empty"),
                        new LocalizationKeyEntry(4, "battle.damage")
                    },
                    new[]
                    {
                        new LocalizationLocaleDescriptor("zh-CN", ZhCnPath),
                        new LocalizationLocaleDescriptor("en-US", EnUsPath, "zh-CN"),
                        new LocalizationLocaleDescriptor("ja-JP", JaJpPath)
                    });
                ZhCn.Replace(
                    "zh-CN",
                    new[]
                    {
                        new LocalizationValueEntry(1, "开始"),
                        new LocalizationValueEntry(2, "仅中文"),
                        new LocalizationValueEntry(4, "伤害 {0}")
                    },
                    1);
                EnUs.Replace(
                    "en-US",
                    new[]
                    {
                        new LocalizationValueEntry(1, "Start"),
                        new LocalizationValueEntry(3, string.Empty),
                        new LocalizationValueEntry(4, "Damage {0}")
                    },
                    1);
                JaJp.Replace("ja-JP", new[] { new LocalizationValueEntry(1, "開始") }, 1);
                Loader = new TestLocalizationAssetLoader();
                Loader.Add(CatalogPath, Catalog);
                Loader.Add(ZhCnPath, ZhCn);
                Loader.Add(EnUsPath, EnUs);
                Loader.Add(JaJpPath, JaJp);
                Module = new LocalizationModule(Loader);
                Module.Startup();
            }

            public LocalizationCatalogAsset Catalog { get; }

            public LocalizationLocaleAsset ZhCn { get; }

            public LocalizationLocaleAsset EnUs { get; }

            public LocalizationLocaleAsset JaJp { get; }

            public TestLocalizationAssetLoader Loader { get; }

            public LocalizationModule Module { get; }

            public void Dispose()
            {
                Module.Shutdown();
                UnityEngine.Object.DestroyImmediate(Catalog);
                UnityEngine.Object.DestroyImmediate(ZhCn);
                UnityEngine.Object.DestroyImmediate(EnUs);
                UnityEngine.Object.DestroyImmediate(JaJp);
            }
        }

        private sealed class TestLocalizationAssetLoader : ILocalizationAssetLoader
        {
            private readonly Dictionary<string, UnityEngine.Object> m_Assets =
                new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> m_LoadCounts =
                new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> m_ReleaseCounts =
                new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, UniTaskCompletionSource> m_Gates =
                new Dictionary<string, UniTaskCompletionSource>(StringComparer.Ordinal);
            private readonly HashSet<string> m_Failures = new HashSet<string>(StringComparer.Ordinal);

            public List<string> LoadedLocations { get; } = new List<string>();

            public void Add(string location, UnityEngine.Object asset)
            {
                m_Assets[location] = asset;
            }

            public void Fail(string location)
            {
                m_Failures.Add(location);
            }

            public void Gate(string location)
            {
                m_Gates[location] = new UniTaskCompletionSource();
            }

            public void OpenGate(string location)
            {
                m_Gates[location].TrySetResult();
                m_Gates.Remove(location);
            }

            public int LoadCount(string location)
            {
                return m_LoadCounts.TryGetValue(location, out var count) ? count : 0;
            }

            public int ReleaseCount(string location)
            {
                return m_ReleaseCounts.TryGetValue(location, out var count) ? count : 0;
            }

            public async UniTask<LocalizationAssetLease> LoadAsync(string location)
            {
                LoadedLocations.Add(location);
                m_LoadCounts[location] = LoadCount(location) + 1;
                if (m_Gates.TryGetValue(location, out var gate))
                {
                    await gate.Task;
                }

                if (m_Failures.Contains(location) || m_Assets.TryGetValue(location, out var asset) is false)
                {
                    throw new GameException($"Test localization asset is missing: {location}");
                }

                return new LocalizationAssetLease(location, asset, () =>
                {
                    m_ReleaseCounts[location] = ReleaseCount(location) + 1;
                    return UniTask.CompletedTask;
                });
            }
        }
    }
}
