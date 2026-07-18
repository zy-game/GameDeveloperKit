using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Data;
using GameDeveloperKit.Data.Internal;
using GameDeveloperKit.File;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class DataModuleTests : RuntimeTestBase
    {
        private FileModule m_FileModule;
        private string m_RootPath;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await UnregisterIfRegisteredAsync<DataModule>();
                await UnregisterIfRegisteredAsync<FileModule>();
                m_FileModule = null;

                if (!string.IsNullOrEmpty(m_RootPath) && Directory.Exists(m_RootPath))
                {
                    Directory.Delete(m_RootPath, true);
                    m_RootPath = null;
                }
            });
        }

        [UnityTest]
        public IEnumerator SaveLoadRollbackDelete_WhenUsingJsonPersistence_TracksVersions()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                var module = new DataModule();
                module.Startup();

                var data = module.GetData<ExampleData>("slot-a");
                data.Value = 1;
                var first = await module.SaveDataAsync<ExampleData>("slot-a");

                data.Value = 2;
                var second = await module.SaveDataAsync<ExampleData>("slot-a");

                Assert.AreNotEqual(first.Version, second.Version);
                var versions = await module.GetVersionsAsync<ExampleData>("slot-a");
                Assert.AreEqual(2, versions.Count);
                Assert.IsFalse(versions[0].IsCurrent);
                Assert.IsTrue(versions[1].IsCurrent);

                var oldVersion = await module.LoadVersionAsync<ExampleData>("slot-a", first.Version);
                Assert.AreEqual(1, oldVersion.Value);
                versions = await module.GetVersionsAsync<ExampleData>("slot-a");
                Assert.IsTrue(versions[1].IsCurrent);

                var current = await module.LoadDataAsync<ExampleData>("slot-a");
                Assert.AreEqual(2, current.Value);

                var rollback = await module.RollbackDataAsync<ExampleData>("slot-a", first.Version);
                Assert.AreEqual(1, rollback.Value);
                Assert.AreEqual(1, module.GetData<ExampleData>("slot-a").Value);
                versions = await module.GetVersionsAsync<ExampleData>("slot-a");
                Assert.IsTrue(versions[0].IsCurrent);

                rollback.Value = 3;
                var third = await module.SaveDataAsync<ExampleData>("slot-a");
                versions = await module.GetVersionsAsync<ExampleData>("slot-a");
                Assert.AreEqual(3, versions.Count);
                Assert.AreEqual(third.Version, versions[2].Version);
                Assert.IsTrue(versions[2].IsCurrent);

                var slot = Slot.Create<ExampleData>("slot-a");
                var indexPath = PathUtility.GetIndexPath(slot);
                Assert.IsTrue(m_FileModule.Exists(indexPath));
                Assert.IsTrue(m_FileModule.Exists(PathUtility.GetVersionPath(slot, first.Version)));
                Assert.IsTrue(m_FileModule.Exists(PathUtility.GetVersionPath(slot, second.Version)));

                await module.DeleteDataAsync<ExampleData>("slot-a");

                Assert.IsFalse(module.TryGetData<ExampleData>("slot-a", out _));
                Assert.IsFalse(m_FileModule.Exists(indexPath));
                Assert.IsFalse(m_FileModule.Exists(PathUtility.GetVersionPath(slot, first.Version)));
                Assert.IsFalse(m_FileModule.Exists(PathUtility.GetVersionPath(slot, second.Version)));
                Assert.IsFalse(m_FileModule.Exists(PathUtility.GetVersionPath(slot, third.Version)));
            });
        }

        [UnityTest]
        public IEnumerator LoadDataAsync_WhenIndexMissing_ReturnsDefaultAndDoesNotWrite()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                var module = new DataModule();
                module.Startup();

                var data = await module.LoadDataAsync<ExampleData>("missing");

                Assert.AreEqual(0, data.Value);
                Assert.IsTrue(module.TryGetData<ExampleData>("missing", out var cached));
                Assert.AreSame(data, cached);
                Assert.IsFalse(m_FileModule.Exists(PathUtility.GetIndexPath(Slot.Create<ExampleData>("missing"))));
            });
        }

        [UnityTest]
        public IEnumerator DataKeyAttribute_WhenSaved_UsesStableTypeKeyInPathAndDocument()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                var module = new DataModule();
                module.Startup();
                module.SetData("profile", new ExampleData { Value = 9 });

                var version = await module.SaveDataAsync<ExampleData>("profile");

                var slot = Slot.Create<ExampleData>("profile");
                var indexPath = PathUtility.GetIndexPath(slot);
                var versionPath = PathUtility.GetVersionPath(slot, version.Version);
                Assert.AreEqual("data/example-data/profile/index.json", indexPath);
                Assert.IsTrue(m_FileModule.Exists(indexPath));

                var document = Encoding.UTF8.GetString(await m_FileModule.ReadAsync(versionPath));
                StringAssert.Contains("\"typeKey\": \"example-data\"", document);
                StringAssert.Contains("\"key\": \"profile\"", document);
            });
        }

        [UnityTest]
        public IEnumerator SetSerializer_WhenCustomSerializerIsUsed_RoundTrips()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                var module = new DataModule();
                module.Startup();
                module.SetSerializer(new CustomDataSerializer());
                module.SetData("custom", new ExampleData { Value = 42 });

                await module.SaveDataAsync<ExampleData>("custom");
                module.Shutdown();
                module.Startup();
                module.SetSerializer(new CustomDataSerializer());

                var loaded = await module.LoadDataAsync<ExampleData>("custom");

                Assert.AreEqual(42, loaded.Value);
            });
        }

        [Test]
        public void Register_WhenDataModuleIsRegistered_ReturnsData()
        {
            App.Register<DataModule>();

            Assert.IsNotNull(App.Data);
        }

        [Test]
        public void GetData_WhenCalledTwiceWithDefaultKey_ReturnsSameInstance()
        {
            var module = new DataModule();

            var first = module.GetData<ExampleData>();
            var second = module.GetData<ExampleData>();

            Assert.AreSame(first, second);
        }

        [Test]
        public void GetData_WhenCalledWithDifferentKeys_ReturnsDifferentInstances()
        {
            var module = new DataModule();

            var first = module.GetData<ExampleData>("a");
            var second = module.GetData<ExampleData>("b");

            Assert.AreNotSame(first, second);
        }

        [Test]
        public void SetData_WhenValueIsSet_TryGetDataReturnsValue()
        {
            var module = new DataModule();
            var value = new ExampleData { Value = 7 };

            module.SetData("a", value);

            Assert.IsTrue(module.TryGetData<ExampleData>("a", out var data));
            Assert.AreSame(value, data);
        }

        [Test]
        public void GetData_WhenKeyIsInvalid_Throws()
        {
            var module = new DataModule();

            Assert.Throws<ArgumentNullException>(() => module.GetData<ExampleData>(null));
            Assert.Throws<ArgumentException>(() => module.GetData<ExampleData>(""));
        }

        [Test]
        public void SetData_WhenDataIsNull_Throws()
        {
            var module = new DataModule();

            Assert.Throws<ArgumentNullException>(() => module.SetData<ExampleData>("a", null));
        }

        [Test]
        public void GetData_WhenDefaultConstructorMissing_ThrowsGameException()
        {
            var module = new DataModule();

            var exception = Assert.Throws<GameException>(() => module.GetData<NoDefaultConstructorData>());
            StringAssert.Contains("TypeKey", exception.Message);
            StringAssert.Contains("DataKey", exception.Message);
        }

        [UnityTest]
        public IEnumerator SaveDataAsync_WhenSlotIsNotCached_ThrowsGameException()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = new DataModule();
                module.Startup();

                var exception = await ThrowsAsync<GameException>(async () => { await module.SaveDataAsync<ExampleData>("missing"); });

                StringAssert.Contains("example-data", exception.Message);
                StringAssert.Contains("missing", exception.Message);
            });
        }

        [UnityTest]
        public IEnumerator Persistence_WhenFileModuleMissing_ThrowsButMemoryWorks()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = new DataModule();
                module.Startup();
                var data = module.GetData<ExampleData>("memory");
                data.Value = 5;

                Assert.AreEqual(5, module.GetData<ExampleData>("memory").Value);
                StringAssert.Contains("FileModule", (await ThrowsAsync<GameException>(async () => { await module.SaveDataAsync<ExampleData>("memory"); })).Message);
                StringAssert.Contains("FileModule", (await ThrowsAsync<GameException>(async () => { await module.LoadDataAsync<ExampleData>("memory"); })).Message);
            });
        }

        [UnityTest]
        public IEnumerator SaveDataAsync_WhenVersionExists_ThrowsAndPreservesDocument()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                var module = new DataModule();
                module.Startup();
                var data = module.GetData<ExampleData>("dup");
                data.Value = 1;
                await module.SaveDataAsync<ExampleData>("dup", "v1");

                data.Value = 2;
                var exception = await ThrowsAsync<GameException>(async () => { await module.SaveDataAsync<ExampleData>("dup", "v1"); });
                StringAssert.Contains("v1", exception.Message);

                var loaded = await module.LoadVersionAsync<ExampleData>("dup", "v1");
                Assert.AreEqual(1, loaded.Value);
            });
        }

        [UnityTest]
        public IEnumerator RollbackDataAsync_WhenVersionMissing_DoesNotChangeCurrent()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                var module = new DataModule();
                module.Startup();
                var data = module.GetData<ExampleData>("rollback-missing");
                data.Value = 1;
                await module.SaveDataAsync<ExampleData>("rollback-missing", "old");
                data.Value = 2;
                await module.SaveDataAsync<ExampleData>("rollback-missing", "new");

                await ThrowsAsync<GameException>(async () => { await module.RollbackDataAsync<ExampleData>("rollback-missing", "missing"); });

                var versions = await module.GetVersionsAsync<ExampleData>("rollback-missing");
                Assert.AreEqual("new", versions[1].Version);
                Assert.IsTrue(versions[1].IsCurrent);
                Assert.AreEqual(2, module.GetData<ExampleData>("rollback-missing").Value);
            });
        }

        [UnityTest]
        public IEnumerator LoadDataAsync_WhenDocumentJsonIsBroken_DoesNotOverwriteCache()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                var module = new DataModule();
                module.Startup();
                module.SetData("broken", new ExampleData { Value = 1 });
                var version = await module.SaveDataAsync<ExampleData>("broken");
                module.SetData("broken", new ExampleData { Value = 9 });

                var slot = Slot.Create<ExampleData>("broken");
                await m_FileModule.WriteAsync(PathUtility.GetVersionPath(slot, version.Version), version.Version, Encoding.UTF8.GetBytes("{broken-json"));

                var exception = await ThrowsAsync<GameException>(async () => { await module.LoadDataAsync<ExampleData>("broken"); });

                StringAssert.Contains(version.Version, exception.Message);
                Assert.AreEqual(9, module.GetData<ExampleData>("broken").Value);
            });
        }

        [UnityTest]
        public IEnumerator LoadDataAsync_WhenDocumentSlotMismatches_Throws()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                var module = new DataModule();
                module.Startup();
                module.SetData("mismatch", new ExampleData { Value = 1 });
                var version = await module.SaveDataAsync<ExampleData>("mismatch");

                var slot = Slot.Create<ExampleData>("mismatch");
                var versionPath = PathUtility.GetVersionPath(slot, version.Version);
                var document = Encoding.UTF8.GetString(await m_FileModule.ReadAsync(versionPath));
                var mismatchedDocument = ReplaceJsonStringValue(document, "typeKey", "example-data", "other-type");
                await m_FileModule.WriteAsync(versionPath, version.Version, Encoding.UTF8.GetBytes(mismatchedDocument));

                var exception = await ThrowsAsync<GameException>(async () => { await module.LoadDataAsync<ExampleData>("mismatch"); });

                StringAssert.Contains("other-type", mismatchedDocument);
                StringAssert.Contains("mismatch", exception.Message);
                StringAssert.Contains(versionPath, exception.Message);
            });
        }

        [UnityTest]
        public IEnumerator SaveDataAsync_WhenSerializerFails_DoesNotCreateIndex()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                var module = new DataModule();
                module.Startup();
                module.SetSerializer(new FailingDataSerializer());
                module.SetData("fail", new ExampleData { Value = 1 });
                var slot = Slot.Create<ExampleData>("fail");

                var exception = await ThrowsAsync<GameException>(async () => { await module.SaveDataAsync<ExampleData>("fail"); });

                StringAssert.Contains("fail", exception.Message);
                Assert.IsFalse(m_FileModule.Exists(PathUtility.GetIndexPath(slot)));
            });
        }

        [UnityTest]
        public IEnumerator LoadDataAsync_WhenSchemaOneGoldenDocumentExists_MigratesToSchemaTwo()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                await WriteDocumentAsync<MigratedProfile>(
                    "golden",
                    "v1",
                    1,
                    "legacy-profile",
                    new JValue(Convert.ToBase64String(Encoding.UTF8.GetBytes("Ada|17"))));
                var module = new DataModule();
                module.Startup();
                module.RegisterMigration<MigratedProfile>(new ProfileV1ToV2Migration());

                var loaded = await module.LoadDataAsync<MigratedProfile>("golden");

                Assert.AreEqual("Ada", loaded.DisplayName);
                Assert.AreEqual(17, loaded.Score);
            });
        }

        [UnityTest]
        public IEnumerator LoadDataAsync_WhenMigrationChainIsMissing_ThrowsWithoutReplacingCache()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                await WriteDocumentAsync<MigratedProfile>(
                    "missing-chain",
                    "v1",
                    1,
                    "legacy-profile",
                    new JValue(Convert.ToBase64String(Encoding.UTF8.GetBytes("Ada|17"))));
                var module = new DataModule();
                module.Startup();
                var cached = new MigratedProfile { DisplayName = "Current", Score = 99 };
                module.SetData("missing-chain", cached);

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await module.LoadDataAsync<MigratedProfile>("missing-chain");
                });

                StringAssert.Contains("1->2", exception.Message);
                Assert.AreSame(cached, module.GetData<MigratedProfile>("missing-chain"));
            });
        }

        [UnityTest]
        public IEnumerator LoadDataAsync_WhenDocumentSchemaIsNewer_ThrowsWithoutReplacingCache()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                await WriteDocumentAsync<MigratedProfile>(
                    "future",
                    "v3",
                    3,
                    "json",
                    JObject.FromObject(new { displayName = "Future", score = 3 }));
                var module = new DataModule();
                module.Startup();
                var cached = new MigratedProfile { DisplayName = "Current", Score = 2 };
                module.SetData("future", cached);

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await module.LoadDataAsync<MigratedProfile>("future");
                });

                StringAssert.Contains("newer than supported", exception.Message);
                Assert.AreSame(cached, module.GetData<MigratedProfile>("future"));
            });
        }

        [UnityTest]
        public IEnumerator LoadDataAsync_WhenMigrationFails_ThrowsWithoutReplacingCache()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                await WriteDocumentAsync<MigratedProfile>(
                    "migration-failure",
                    "v1",
                    1,
                    "legacy-profile",
                    new JValue(Convert.ToBase64String(Encoding.UTF8.GetBytes("invalid"))));
                var module = new DataModule();
                module.Startup();
                module.RegisterMigration<MigratedProfile>(new FailingProfileMigration());
                var cached = new MigratedProfile { DisplayName = "Current", Score = 2 };
                module.SetData("migration-failure", cached);

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await module.LoadDataAsync<MigratedProfile>("migration-failure");
                });

                StringAssert.Contains("migration '1->2' failed", exception.Message);
                Assert.AreSame(cached, module.GetData<MigratedProfile>("migration-failure"));
            });
        }

        [UnityTest]
        public IEnumerator SaveDataAsync_WhenSchemaIsDeclared_WritesCurrentContainerAndSchemaVersions()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                var module = new DataModule();
                module.Startup();
                module.SetData("schema", new MigratedProfile { DisplayName = "Current", Score = 2 });

                var version = await module.SaveDataAsync<MigratedProfile>("schema", "current");
                var slot = Slot.Create<MigratedProfile>("schema");
                var bytes = await m_FileModule.ReadAsync(PathUtility.GetVersionPath(slot, version.Version));
                var document = JsonConvert.DeserializeObject<Document>(Encoding.UTF8.GetString(bytes));

                Assert.AreEqual(2, document.FormatVersion);
                Assert.AreEqual(2, document.SchemaVersion);
                Assert.AreEqual("json", document.Serializer);
            });
        }

        [UnityTest]
        public IEnumerator SaveDataAsync_WhenWritesRunConcurrently_PreservesEveryCommittedVersionAfterRestart()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                var module = new DataModule();
                module.Startup();
                module.SetData("concurrent", new ExampleData { Value = 7 });

                var writes = new UniTask<DataVersionInfo>[6];
                for (var index = 0; index < writes.Length; index++)
                {
                    writes[index] = module.SaveDataAsync<ExampleData>("concurrent", $"v{index}");
                }

                await UniTask.WhenAll(writes);
                await RestartFileModuleAsync();
                module.Shutdown();
                module.Startup();

                var versions = await module.GetVersionsAsync<ExampleData>("concurrent");
                Assert.AreEqual(6, versions.Count);
                CollectionAssert.AreEquivalent(
                    Enumerable.Range(0, 6).Select(index => $"v{index}").ToArray(),
                    versions.Select(info => info.Version).ToArray());
                Assert.AreEqual(1, versions.Count(info => info.IsCurrent));
                Assert.AreEqual("v5", versions.Single(info => info.IsCurrent).Version);
                Assert.AreEqual(7, (await module.LoadDataAsync<ExampleData>("concurrent")).Value);
            });
        }

        [UnityTest]
        public IEnumerator SaveDataAsync_WhenRetentionLimitIsExceeded_KeepsLatestTenVersions()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                var module = new DataModule();
                module.Startup();
                var data = module.GetData<ExampleData>("retention");
                var slot = Slot.Create<ExampleData>("retention");

                for (var index = 0; index < 12; index++)
                {
                    data.Value = index;
                    await module.SaveDataAsync<ExampleData>("retention", $"v{index:00}");
                }

                var versions = await module.GetVersionsAsync<ExampleData>("retention");
                Assert.AreEqual(10, versions.Count);
                CollectionAssert.AreEqual(
                    Enumerable.Range(2, 10).Select(index => $"v{index:00}").ToArray(),
                    versions.Select(info => info.Version).ToArray());
                Assert.AreEqual("v11", versions.Single(info => info.IsCurrent).Version);
                Assert.IsFalse(m_FileModule.Exists(PathUtility.GetVersionPath(slot, "v00")));
                Assert.IsFalse(m_FileModule.Exists(PathUtility.GetVersionPath(slot, "v01")));

                var rollback = await module.RollbackDataAsync<ExampleData>("retention", "v02");
                Assert.AreEqual(2, rollback.Value);
                rollback.Value = 12;
                await module.SaveDataAsync<ExampleData>("retention", "v12");

                versions = await module.GetVersionsAsync<ExampleData>("retention");
                CollectionAssert.AreEqual(
                    Enumerable.Range(3, 10).Select(index => $"v{index:00}").ToArray(),
                    versions.Select(info => info.Version).ToArray());
                Assert.AreEqual("v12", versions.Single(info => info.IsCurrent).Version);
                Assert.IsFalse(m_FileModule.Exists(PathUtility.GetVersionPath(slot, "v02")));
            });
        }

        [UnityTest]
        public IEnumerator LoadDataAsync_WhenCrashLeavesUnindexedVersion_RemovesOrphanAndUsesCommittedCurrent()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                var module = new DataModule();
                module.Startup();
                var data = module.GetData<ExampleData>("orphan");
                data.Value = 1;
                var committed = await module.SaveDataAsync<ExampleData>("orphan", "committed");
                data.Value = 2;
                await module.SaveDataAsync<ExampleData>("orphan", "orphaned");

                var slot = Slot.Create<ExampleData>("orphan");
                var indexPath = PathUtility.GetIndexPath(slot);
                var orphanPath = PathUtility.GetVersionPath(slot, "orphaned");
                var crashIndex = new VersionIndex
                {
                    FormatVersion = 2,
                    TypeKey = slot.TypeKey,
                    Key = slot.Key,
                    CurrentVersion = committed.Version,
                    Versions = new List<DataVersionInfo> { committed },
                };
                var indexBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(crashIndex, Formatting.Indented));
                await m_FileModule.WriteAsync(indexPath, "index", indexBytes);
                Assert.IsTrue(m_FileModule.Exists(orphanPath));

                await RestartFileModuleAsync();
                module.Shutdown();
                module.Startup();
                var loaded = await module.LoadDataAsync<ExampleData>("orphan");

                Assert.AreEqual(1, loaded.Value);
                Assert.IsFalse(m_FileModule.Exists(orphanPath));
                var versions = await module.GetVersionsAsync<ExampleData>("orphan");
                Assert.AreEqual(1, versions.Count);
                Assert.AreEqual("committed", versions[0].Version);
                Assert.IsTrue(versions[0].IsCurrent);
            });
        }

        [UnityTest]
        public IEnumerator RollbackDataAsync_WhenIndexCommitFails_PreservesCommittedCurrentAndCache()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                var module = new DataModule();
                module.Startup();
                var data = module.GetData<ExampleData>("rollback-commit-failure");
                data.Value = 1;
                await module.SaveDataAsync<ExampleData>("rollback-commit-failure", "old");
                data.Value = 2;
                await module.SaveDataAsync<ExampleData>("rollback-commit-failure", "current");

                var manifestPath = Path.Combine(m_FileModule.RootPath, VfsConstants.ManifestFileName);
                using (new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    await ThrowsAsync<GameException>(async () =>
                    {
                        await module.RollbackDataAsync<ExampleData>("rollback-commit-failure", "old");
                    });
                }

                Assert.AreEqual(2, module.GetData<ExampleData>("rollback-commit-failure").Value);
                var versions = await module.GetVersionsAsync<ExampleData>("rollback-commit-failure");
                Assert.AreEqual("current", versions.Single(info => info.IsCurrent).Version);
                Assert.AreEqual(2, (await module.LoadDataAsync<ExampleData>("rollback-commit-failure")).Value);
            });
        }

        [UnityTest]
        public IEnumerator LoadVersionAsync_WhenVersionIsNotIndexed_DeletesOrphanAndRejectsLoad()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                var module = new DataModule();
                module.Startup();
                module.SetData("unindexed", new ExampleData { Value = 3 });
                var committed = await module.SaveDataAsync<ExampleData>("unindexed", "committed");
                var slot = Slot.Create<ExampleData>("unindexed");
                var orphanPath = PathUtility.GetVersionPath(slot, "unindexed");
                var committedBytes = await m_FileModule.ReadAsync(PathUtility.GetVersionPath(slot, committed.Version));
                await m_FileModule.WriteAsync(orphanPath, "unindexed", committedBytes);

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await module.LoadVersionAsync<ExampleData>("unindexed", "unindexed");
                });

                StringAssert.Contains("not recorded", exception.Message);
                Assert.IsFalse(m_FileModule.Exists(orphanPath));
                Assert.AreEqual(3, module.GetData<ExampleData>("unindexed").Value);
            });
        }

        [Test]
        public void Shutdown_WhenCalled_ClearsCacheWithoutSaving()
        {
            var module = new DataModule();
            module.SetData("temp", new ExampleData { Value = 1 });

            module.Shutdown();

            Assert.IsFalse(module.TryGetData<ExampleData>("temp", out _));
        }

        private async UniTask RegisterIsolatedFileModuleAsync()
        {
            m_RootPath = Path.Combine(UnityEngine.Application.temporaryCachePath, "data-module-tests", Guid.NewGuid().ToString("N"));
            var module = new FileModule(m_RootPath);
            await RegisterModuleAsync(module);
            m_FileModule = module;
        }

        private async UniTask RestartFileModuleAsync()
        {
            var module = new FileModule(m_RootPath);
            await RegisterModuleAsync(module);
            m_FileModule = module;
        }

        private static async UniTask RegisterModuleAsync<T>(T module)
            where T : class, IGameModule
        {
            await UnregisterIfRegisteredAsync<T>();
            var registry = GetRegistry();
            GetModules(registry).Add(typeof(T), module);
            module.Startup();
            registry.TrackModuleOrder(typeof(T));
        }

        private static async UniTask UnregisterIfRegisteredAsync<T>()
            where T : class, IGameModule
        {
            if (App.TryGetRegistered<T>(out _))
            {
                await App.Unregister<T>();
            }
        }

        private static ModuleRegistry GetRegistry()
        {
            var stateField = typeof(App).GetField("s_State", BindingFlags.NonPublic | BindingFlags.Static);
            var state = stateField?.GetValue(null);
            var registryProperty = state?.GetType().GetProperty("Registry", BindingFlags.Public | BindingFlags.Instance);
            return (ModuleRegistry)registryProperty?.GetValue(state)
                ?? throw new InvalidOperationException("App module registry is unavailable.");
        }

        private static Dictionary<Type, IGameModule> GetModules(ModuleRegistry registry)
        {
            var modulesField = typeof(ModuleRegistry).GetField("_modules", BindingFlags.NonPublic | BindingFlags.Instance);
            return (Dictionary<Type, IGameModule>)modulesField?.GetValue(registry)
                ?? throw new InvalidOperationException("Module registry storage is unavailable.");
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

            Assert.Fail($"Expected exception of type {typeof(TException).FullName}.");
            return null;
        }

        private static string ReplaceJsonStringValue(string json, string name, string oldValue, string newValue)
        {
            var spaced = $"\"{name}\": \"{oldValue}\"";
            if (json.Contains(spaced))
            {
                return json.Replace(spaced, $"\"{name}\": \"{newValue}\"");
            }

            var compact = $"\"{name}\":\"{oldValue}\"";
            if (json.Contains(compact))
            {
                return json.Replace(compact, $"\"{name}\":\"{newValue}\"");
            }

            Assert.Fail($"Expected JSON field '{name}' with value '{oldValue}'.");
            return json;
        }

        private async UniTask WriteDocumentAsync<T>(string key, string version, int schemaVersion, string serializer, JToken payload)
        {
            var slot = Slot.Create<T>(key);
            var savedAtUtc = DateTimeOffset.UtcNow;
            var index = new VersionIndex
            {
                FormatVersion = 2,
                TypeKey = slot.TypeKey,
                Key = slot.Key,
                CurrentVersion = version,
                Versions = new List<DataVersionInfo>
                {
                    new DataVersionInfo(version, savedAtUtc, true),
                },
            };
            var document = new Document
            {
                FormatVersion = 2,
                Serializer = serializer,
                SchemaVersion = schemaVersion,
                TypeKey = slot.TypeKey,
                Key = slot.Key,
                DataVersion = version,
                TypeName = typeof(T).AssemblyQualifiedName,
                SavedAtUtc = savedAtUtc,
                Payload = payload,
            };

            await m_FileModule.WriteAsync(
                PathUtility.GetVersionPath(slot, version),
                version,
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(document, Formatting.Indented)));
            await m_FileModule.WriteAsync(
                PathUtility.GetIndexPath(slot),
                "index",
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(index, Formatting.Indented)));
        }

        [DataKey("example-data")]
        [DataSchema(1)]
        private sealed class ExampleData
        {
            public int Value;
        }

        [DataKey("migrated-profile")]
        [DataSchema(2)]
        private sealed class MigratedProfile
        {
            public string DisplayName;

            public int Score;
        }

        private sealed class ProfileV1ToV2Migration : IDataMigration
        {
            public int FromVersion => 1;

            public int ToVersion => 2;

            public DataMigrationPayload Migrate(DataMigrationPayload payload)
            {
                if (payload.Serializer != "legacy-profile")
                {
                    throw new InvalidOperationException($"Unexpected serializer: {payload.Serializer}");
                }

                var fields = Encoding.UTF8.GetString(payload.Bytes).Split('|');
                var json = JsonConvert.SerializeObject(new
                {
                    displayName = fields[0],
                    score = int.Parse(fields[1]),
                });
                return new DataMigrationPayload("json", Encoding.UTF8.GetBytes(json));
            }
        }

        private sealed class FailingProfileMigration : IDataMigration
        {
            public int FromVersion => 1;

            public int ToVersion => 2;

            public DataMigrationPayload Migrate(DataMigrationPayload payload)
            {
                throw new InvalidOperationException("migration failure");
            }
        }

        private sealed class NoDefaultConstructorData
        {
            public NoDefaultConstructorData(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private sealed class CustomDataSerializer : IDataSerializer
        {
            public string Format => "custom";

            public byte[] Serialize<T>(T data)
            {
                return Serialize(typeof(T), data);
            }

            public T Deserialize<T>(byte[] bytes)
            {
                return (T)Deserialize(typeof(T), bytes);
            }

            public byte[] Serialize(Type type, object data)
            {
                if (type == null)
                {
                    throw new ArgumentNullException(nameof(type));
                }

                if (type != typeof(ExampleData))
                {
                    throw new InvalidOperationException($"Unsupported data type: {type.FullName}");
                }

                var example = (ExampleData)data;
                return Encoding.UTF8.GetBytes(example.Value.ToString());
            }

            public object Deserialize(Type type, byte[] bytes)
            {
                if (type == null)
                {
                    throw new ArgumentNullException(nameof(type));
                }

                if (bytes == null)
                {
                    throw new ArgumentNullException(nameof(bytes));
                }

                if (type != typeof(ExampleData))
                {
                    throw new InvalidOperationException($"Unsupported data type: {type.FullName}");
                }

                return new ExampleData { Value = int.Parse(Encoding.UTF8.GetString(bytes)) };
            }
        }

        private sealed class FailingDataSerializer : IDataSerializer
        {
            public string Format => "fail";

            public byte[] Serialize<T>(T data)
            {
                throw new InvalidOperationException("serializer failure");
            }

            public T Deserialize<T>(byte[] bytes)
            {
                throw new InvalidOperationException("serializer failure");
            }

            public byte[] Serialize(Type type, object data)
            {
                throw new InvalidOperationException("serializer failure");
            }

            public object Deserialize(Type type, byte[] bytes)
            {
                throw new InvalidOperationException("serializer failure");
            }
        }
    }
}
