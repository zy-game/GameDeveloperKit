using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Config;
using GameDeveloperKit.Download;
using GameDeveloperKit.Media;
using GameDeveloperKit.Resource;
using Luban.SimpleJSON;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class ConfigModuleTests : RuntimeTestBase
    {
        private const string AttributeTablePath = "ConfigModuleAttributePathTest.json";
        private static string GeneratedLubanTablePath => FrameworkAssetPath("Tests/Runtime/LubanGeneratedTableFixture.json");

        private readonly List<string> m_TempFiles = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (var path in m_TempFiles)
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }

            m_TempFiles.Clear();
            TryUnregister<ConfigModule>();
        }

        [UnityTest]
        public IEnumerator Register_WhenConfigModuleIsRegistered_ReturnsConfig()
        {
            return RunAsync(() =>
            {
                App.Register<ConfigModule>();

                Assert.IsNotNull(App.Config);
                return UniTask.CompletedTask;
            });
        }

        [UnityTest]
        public IEnumerator Startup_WhenSettingsMissing_DoesNotThrow()
        {
            return RunAsync(() =>
            {
                var module = new ConfigModule();

                module.Startup();

                Assert.IsFalse(module.TryGetTable<ItemRow>(out _));
                if (module.TryGetTagGroup(TagCatalogSettings.AssetTagsGroupKey, out var group))
                {
                    Assert.AreEqual(TagCatalogSettings.AssetTagsGroupKey, group.Key);
                    Assert.IsTrue(group.Fixed);
                }

                return UniTask.CompletedTask;
            });
        }

        [Test]
        public void MediaDelivery_WhenLoadedAndShutdown_ExposesThenClearsSettings()
        {
            var settings = new MediaDeliverySettings();
            settings.SetPublicUrls("https://bucket.cos.ap-chengdu.myqcloud.com");
            var module = new ConfigModule();
            module.LoadMediaDeliverySettings(_ => settings, new GdkSettings());

            Assert.AreSame(settings, module.MediaDelivery);
            module.Shutdown();
            Assert.IsNull(module.MediaDelivery);
        }

        [UnityTest]
        public IEnumerator TagCatalog_WhenCatalogContainsTags_ReturnsReadonlySnapshot()
        {
            return RunAsync(async () =>
            {
                var catalogSettings = new TagCatalogSettings();
                catalogSettings.EnsureDefaults();
                catalogSettings.Groups[0].Tags.Add(new TagDefinition
                {
                    Key = "weapon",
                    DisplayName = "Weapon"
                });

                var catalog = TagCatalog.Build(catalogSettings.Groups, "test");

                Assert.IsTrue(catalog.TryGetGroup(TagCatalogSettings.AssetTagsGroupKey, out var group));
                Assert.AreEqual(TagCatalogSettings.AssetTagsDisplayName, group.DisplayName);
                Assert.IsTrue(catalog.HasTag(TagCatalogSettings.AssetTagsGroupKey, "weapon"));
                Assert.AreEqual("Weapon", catalog.GetTags(TagCatalogSettings.AssetTagsGroupKey)[0].DisplayName);

                await UniTask.CompletedTask;
            });
        }

        [UnityTest]
        public IEnumerator TagCatalog_WhenDuplicateTagKey_Throws()
        {
            return RunAsync(async () =>
            {
                var catalogSettings = new TagCatalogSettings();
                catalogSettings.EnsureDefaults();
                catalogSettings.Groups[0].Tags.Add(new TagDefinition { Key = "enemy", DisplayName = "Enemy" });
                catalogSettings.Groups[0].Tags.Add(new TagDefinition { Key = "Enemy", DisplayName = "Enemy 2" });

                var exception = Assert.Throws<GameException>(() => TagCatalog.Build(catalogSettings.Groups, "test"));
                StringAssert.Contains("duplicate tag key", exception.Message);

                await UniTask.CompletedTask;
            });
        }

        [UnityTest]
        public IEnumerator TagCatalog_WhenArgumentsInvalid_Throws()
        {
            return RunAsync(async () =>
            {
                Assert.Throws<ArgumentNullException>(() => TagCatalog.Empty.HasTag(null, "weapon"));
                Assert.Throws<ArgumentException>(() => TagCatalog.Empty.HasTag(TagCatalogSettings.AssetTagsGroupKey, " "));

                var exception = Assert.Throws<GameException>(() => TagCatalog.Empty.GetTags(TagCatalogSettings.AssetTagsGroupKey));
                StringAssert.Contains(TagCatalogSettings.AssetTagsGroupKey, exception.Message);

                await UniTask.CompletedTask;
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenJsonRootArray_LoadsRowsAndQueriesByKey()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                var location = WriteTemp("[{\"Id\":1001,\"Name\":\"Sword\",\"Price\":120}]");

                var table = await module.LoadTableAsync<ItemRow>(location);

                Assert.IsInstanceOf<Table<ItemRow>>(table);
                Assert.AreEqual("Sword", table.GetRowByKey(1001).Name);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenJsonWrapper_LoadsRows()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                var location = WriteTemp("{\"rows\":[{\"Id\":1002,\"Name\":\"Shield\",\"Price\":90}]}");

                var table = await module.LoadTableAsync<ItemRow>(location);

                Assert.AreEqual("Shield", table.GetRowByKey(1002).Name);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenCalledTwice_UsesCachedTable()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                var path = WriteTemp("[{\"Id\":1001,\"Name\":\"Sword\",\"Price\":120}]");

                var first = await module.LoadTableAsync<ItemRow>(path);
                System.IO.File.WriteAllText(path, "[{\"Id\":1001,\"Name\":\"Shield\",\"Price\":90}]");
                var second = await module.LoadTableAsync<ItemRow>(path);

                Assert.AreSame(first, second);
                Assert.AreEqual("Sword", second.GetRowByKey(1001).Name);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenLoadedSourceChanges_ThrowsConflict()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                var firstPath = WriteTemp("[{\"Id\":1001,\"Name\":\"Sword\",\"Price\":120}]");
                var secondPath = WriteTemp("[{\"Id\":1001,\"Name\":\"Shield\",\"Price\":90}]");
                await module.LoadTableAsync<ItemRow>(firstPath);

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await module.LoadTableAsync<ItemRow>(secondPath);
                });

                StringAssert.Contains("already loaded from source", exception.Message);
                StringAssert.Contains(firstPath.Replace('\\', '/'), exception.Message);
                StringAssert.Contains(secondPath.Replace('\\', '/'), exception.Message);
            });
        }

        [Test]
        public void ReadAndReleaseRawAssetText_WhenReadCompletes_ReleasesHandle()
        {
            var rawAsset = RawAssetHandle.Success(
                null,
                System.Text.Encoding.UTF8.GetBytes("[{\"Id\":1001}]"));

            var text = ConfigModule.ReadAndReleaseRawAssetText(rawAsset, "config/test.json");

            Assert.AreEqual("[{\"Id\":1001}]", text);
            Assert.AreEqual(ResourceStatus.Released, rawAsset.Status);
            Assert.AreEqual(0, rawAsset.ReferenceCount);
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenSameRowTypeIsPending_ReturnsSameTable()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                var location = WriteTemp("[{\"Id\":1001,\"Name\":\"Sword\",\"Price\":120}]");

                var firstTask = module.LoadTableAsync<ItemRow>(location);
                var secondTask = module.LoadTableAsync<ItemRow>(location);
                var results = await UniTask.WhenAll(firstTask, secondTask);

                Assert.AreSame(results.Item1, results.Item2);
            });
        }

        [UnityTest]
        public IEnumerator GetRowByKey_WhenKeyMissing_ReturnsDefault()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                var table = await module.LoadTableAsync<ItemRow>(WriteTemp("[{\"Id\":1001,\"Name\":\"Sword\",\"Price\":120}]"));

                Assert.IsNull(table.GetRowByKey(9999));
            });
        }

        [UnityTest]
        public IEnumerator GetTable_WhenTableLoaded_ReturnsTable()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                var loaded = await module.LoadTableAsync<ItemRow>(WriteTemp("[{\"Id\":1001,\"Name\":\"Sword\",\"Price\":120}]"));

                Assert.AreSame(loaded, module.GetTable<ItemRow>());
                Assert.IsTrue(module.TryGetTable<ItemRow>(out var table));
                Assert.AreSame(loaded, table);
            });
        }

        [UnityTest]
        public IEnumerator Unload_WhenTableLoaded_RemovesTableAndAllowsReload()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                var path = WriteTemp("[{\"Id\":1001,\"Name\":\"Sword\",\"Price\":120}]");

                var first = await module.LoadTableAsync<ItemRow>(path);
                module.Unload<ItemRow>();
                System.IO.File.WriteAllText(path, "[{\"Id\":1001,\"Name\":\"Shield\",\"Price\":90}]");
                var second = await module.LoadTableAsync<ItemRow>(path);

                Assert.AreNotSame(first, second);
                Assert.AreEqual("Shield", second.GetRowByKey(1001).Name);
            });
        }

        [UnityTest]
        public IEnumerator Shutdown_WhenTableLoaded_ClearsTable()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                await module.LoadTableAsync<ItemRow>(WriteTemp("[{\"Id\":1001,\"Name\":\"Sword\",\"Price\":120}]"));

                module.Shutdown();

                Assert.Throws<GameException>(() => module.GetTable<ItemRow>());
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenPathInvalid_Throws()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();

                await ThrowsAsync<ArgumentNullException>(async () => { await module.LoadTableAsync<ItemRow>(null); });
                await ThrowsAsync<ArgumentException>(async () => { await module.LoadTableAsync<ItemRow>(" "); });
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenTableOptionMissing_Throws()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();

                var exception = await ThrowsAsync<GameException>(async () => { await module.LoadTableAsync<NoTableOptionRow>(); });
                StringAssert.Contains(nameof(NoTableOptionRow), exception.Message);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenLocationMissing_Throws()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();

                var exception = await ThrowsAsync<GameException>(async () => { await module.LoadTableAsync<ItemRow>("missing-config"); });
                StringAssert.Contains("missing-config", exception.Message);
                StringAssert.Contains(nameof(ItemRow), exception.Message);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenHttpDownloadFails_Throws()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                TryUnregister<DownloadModule>();

                var exception = await ThrowsAsync<GameException>(async () => { await module.LoadTableAsync<ItemRow>("http://127.0.0.1/config.json"); });
                StringAssert.Contains("http://127.0.0.1/config.json", exception.Message);
                StringAssert.Contains(nameof(ItemRow), exception.Message);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenRowKeyMissing_Throws()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();

                var exception = await ThrowsAsync<GameException>(async () => { await module.LoadTableAsync<NoKeyRow>(WriteTemp("[{\"Name\":\"Sword\"}]")); });
                StringAssert.Contains("has no key", exception.Message);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenDuplicateKey_Throws()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();

                var exception = await ThrowsAsync<GameException>(async () => { await module.LoadTableAsync<ItemRow>(WriteTemp("[{\"Id\":1001,\"Name\":\"Sword\"},{\"Id\":1001,\"Name\":\"Shield\"}]")); });
                StringAssert.Contains("duplicate key", exception.Message);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenJsonInvalid_DoesNotCacheTable()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();

                await ThrowsAsync<GameException>(async () => { await module.LoadTableAsync<ItemRow>(WriteTemp("{invalid")); });
                Assert.Throws<GameException>(() => module.GetTable<ItemRow>());
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenTableOptionExists_UsesAttributePath()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                WriteFile(AttributeTablePath, "[{\"Id\":1003,\"Name\":\"Potion\",\"Price\":30}]");

                var table = await module.LoadTableAsync<AttributePathRow>();

                Assert.AreEqual("Potion", table.GetRowByKey(1003).Name);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenGeneratedLubanRowUsesExplicitPath_LoadsRows()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();

                var table = await module.LoadTableAsync<cfg.test>(GeneratedLubanTablePath);

                AssertGeneratedLubanTable("explicit path", table);
            });
        }

        [UnityTest]
        public IEnumerator GeneratedLubanRow_WhenTemplateGenerated_HasConfigModuleContract()
        {
            return RunAsync(async () =>
            {
                var type = typeof(cfg.test);
                var tableOption = (TableOptionAttribute)Attribute.GetCustomAttribute(type, typeof(TableOptionAttribute));

                Assert.IsTrue(typeof(IConfig).IsAssignableFrom(type));
                Assert.IsNotNull(tableOption);
                Assert.AreEqual(GeneratedLubanTablePath, ResolveFrameworkAssetPath(tableOption.Path));
                Assert.IsTrue(type.GetConstructors().Any(x => x.GetCustomAttributes(false).Any(attribute => attribute.GetType().FullName == "Newtonsoft.Json.JsonConstructorAttribute")));
                LogGeneratedLuban($"contract rowType={type.FullName}, tableOption={tableOption.Path}, jsonConstructor=true");
                await UniTask.CompletedTask;
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenGeneratedLubanRowHasTableOption_UsesGeneratedDataPath()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();

                var table = await module.LoadTableAsync<cfg.test>();

                AssertGeneratedLubanTable("TableOption", table);
            });
        }

        [UnityTest]
        public IEnumerator Query_WhenGeneratedLubanTableLoaded_UsesConfigModuleQueries()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                await module.LoadTableAsync<cfg.test>(GeneratedLubanTablePath);

                var found = module.Find<cfg.test>(x => x.Id == 1);
                var first = module.FirstOrDefault<cfg.test>();
                var count = module.Where<cfg.test>(x => x.Name == "xx").Count();
                LogGeneratedLuban($"query find={FormatGeneratedLubanRow(found)}, first={FormatGeneratedLubanRow(first)}, whereName=xx count={count}");

                Assert.AreEqual("xx", found.Name);
                Assert.AreEqual("xx", first.Desc);
                Assert.AreEqual(1, count);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenGeneratedLubanTableCalledTwice_UsesCachedTable()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();

                var first = await module.LoadTableAsync<cfg.test>(GeneratedLubanTablePath);
                var second = await module.LoadTableAsync<cfg.test>(GeneratedLubanTablePath);

                LogGeneratedLuban($"cache firstHash={first.GetHashCode()}, secondHash={second.GetHashCode()}, same={ReferenceEquals(first, second)}");
                Assert.AreSame(first, second);
            });
        }

        [UnityTest]
        public IEnumerator GeneratedLubanWrapper_WhenLoadedFromGeneratedJson_MapsSameRows()
        {
            return RunAsync(async () =>
            {
                var json = JSON.Parse(System.IO.File.ReadAllText(FrameworkFilePath("Tests/Runtime/LubanGeneratedTableFixture.json")));
                var tables = new cfg.Tables(key => key == "tbtest" ? json : throw new ArgumentException(key));

                LogGeneratedLuban($"wrapper dataKey=tbtest, rowCount={tables.Tbtest.DataList.Count}");
                foreach (var row in tables.Tbtest.DataList)
                {
                    LogGeneratedLuban($"wrapper row {FormatGeneratedLubanRow(row)}");
                }

                Assert.AreEqual(1, tables.Tbtest.DataList.Count);
                Assert.AreSame(tables.Tbtest.DataList[0], tables.Tbtest.GetOrDefault(1));
                Assert.AreEqual("xx", tables.Tbtest[1].Name);
                await UniTask.CompletedTask;
            });
        }

        [UnityTest]
        public IEnumerator Query_WhenTableLoaded_FindsRows()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                var table = await module.LoadTableAsync<ItemRow>(WriteTemp("[{\"Id\":1001,\"Name\":\"Sword\",\"Price\":120},{\"Id\":1002,\"Name\":\"Shield\",\"Price\":90}]"));

                Assert.AreEqual("Shield", table.Find(x => x.Price == 90).Name);
                Assert.AreEqual("Sword", module.Find<ItemRow>(x => x.Id == 1001).Name);
                Assert.AreEqual("Sword", module.FirstOrDefault<ItemRow>().Name);
                Assert.AreEqual("Shield", module.FirstOrDefault<ItemRow>(x => x.Price < 100).Name);
                Assert.AreEqual(2, module.Where<ItemRow>(x => x.Price >= 90).Count());
            });
        }

        [UnityTest]
        public IEnumerator Query_WhenTableNotLoaded_Throws()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();

                var exception = Assert.Throws<GameException>(() => module.Find<ItemRow>(x => x.Id == 1001));
                StringAssert.Contains(nameof(ItemRow), exception.Message);

                exception = Assert.Throws<GameException>(() => module.FirstOrDefault<ItemRow>());
                StringAssert.Contains(nameof(ItemRow), exception.Message);

                exception = Assert.Throws<GameException>(() => module.Where<ItemRow>(x => x.Id == 1001).Count());
                StringAssert.Contains(nameof(ItemRow), exception.Message);

                await UniTask.CompletedTask;
            });
        }

        [UnityTest]
        public IEnumerator LoadFromRows_WhenRowsDoNotImplementIConfig_RegistersAndQueries()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                var rows = new List<NonConfigRow>
                {
                    new NonConfigRow { Id = 2001, Name = "Phonograph" },
                    new NonConfigRow { Id = 2002, Name = "Calendar" },
                };

                var table = module.LoadFromRows(rows);

                Assert.AreEqual(2, table.Rows.Count);
                Assert.AreEqual("Phonograph", module.Find<NonConfigRow>(x => x.Id == 2001).Name);
                Assert.AreEqual("Calendar", module.FirstOrDefault<NonConfigRow>(x => x.Id == 2002).Name);
                Assert.AreEqual(2, module.Where<NonConfigRow>(_ => true).Count());

                await UniTask.CompletedTask;
            });
        }

        [UnityTest]
        public IEnumerator LoadFromRows_WhenRegisteredTwice_Throws()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                module.LoadFromRows(new List<NonConfigRow> { new NonConfigRow { Id = 2001 } });

                var exception = Assert.Throws<GameException>(() => module.LoadFromRows(new List<NonConfigRow> { new NonConfigRow { Id = 2002 } }));
                StringAssert.Contains(nameof(NonConfigRow), exception.Message);
                StringAssert.Contains("already loaded", exception.Message);

                await UniTask.CompletedTask;
            });
        }

        [UnityTest]
        public IEnumerator GetRowByKey_WhenRowsDoNotImplementIConfig_ThrowsNotSupportedException()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                var table = module.LoadFromRows(new List<NonConfigRow> { new NonConfigRow { Id = 2001 } });

                Assert.Throws<NotSupportedException>(() => table.GetRowByKey(2001));

                await UniTask.CompletedTask;
            });
        }

        private static IEnumerator RunAsync(Func<UniTask> action)
        {
            return UniTask.ToCoroutine(action);
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

        private static UniTask<ConfigModule> CreateStartedModuleAsync()
        {
            var module = new ConfigModule();
            module.Startup();
            return UniTask.FromResult(module);
        }

        private static void AssertGeneratedLubanTable(string source, Table<cfg.test> table)
        {
            Assert.IsInstanceOf<Table<cfg.test>>(table);
            LogGeneratedLuban($"loaded source={source}, path={GeneratedLubanTablePath}, rowType={typeof(cfg.test).FullName}, rowCount={table.Rows.Count}");
            foreach (var loadedRow in table.Rows)
            {
                LogGeneratedLuban($"config row {FormatGeneratedLubanRow(loadedRow)}");
            }

            Assert.AreEqual(1, table.Rows.Count);

            var row = table.GetRowByKey(1);
            Assert.IsNotNull(row);
            Assert.AreEqual(1, row.Id);
            Assert.AreEqual("xx", row.Name);
            Assert.AreEqual("xx", row.Desc);
            Assert.AreEqual("Id", row.key.Name);
            Assert.AreEqual(1, row.key.Value);
        }

        private static string FormatGeneratedLubanRow(cfg.test row)
        {
            if (row == null)
            {
                return "<null>";
            }

            return $"id={row.Id}, name={row.Name}, desc={row.Desc}, key={row.key.Name}:{row.key.Value}";
        }

        private static void LogGeneratedLuban(string message)
        {
            var text = $"[LubanConfigTest] {message}";
            TestContext.Progress.WriteLine(text);
            UnityEngine.Debug.Log(text);
        }

        private static void TryUnregister<T>() where T : IGameModule
        {
            try
            {
                App.Unregister<T>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }
        }

        private string WriteTemp(string content)
        {
            var path = Path.GetTempFileName();
            System.IO.File.WriteAllText(path, content);
            m_TempFiles.Add(path);
            return path;
        }

        private void WriteFile(string path, string content)
        {
            System.IO.File.WriteAllText(path, content);
            m_TempFiles.Add(path);
        }

        [Serializable]
        private sealed class ItemRow : IConfig
        {
            public int Id = default;
            public string Name = string.Empty;
            public int Price = default;

            public Key key => new Key(nameof(Id), Id);
        }

        [Serializable]
        [TableOption(AttributeTablePath)]
        private sealed class AttributePathRow : IConfig
        {
            public int Id = default;
            public string Name = string.Empty;
            public int Price = default;

            public Key key => new Key(nameof(Id), Id);
        }

        [Serializable]
        private sealed class NoTableOptionRow : IConfig
        {
            public int Id = default;

            public Key key => new Key(nameof(Id), Id);
        }

        [Serializable]
        private sealed class NoKeyRow : IConfig
        {
            public string Name = string.Empty;

            public Key key => null;
        }

        [Serializable]
        private sealed class NonConfigRow
        {
            public int Id = default;
            public string Name = string.Empty;
        }
    }
}
