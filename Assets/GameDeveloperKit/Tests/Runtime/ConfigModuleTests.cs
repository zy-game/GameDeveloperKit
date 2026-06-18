using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Config;
using GameDeveloperKit.Download;
using Luban.SimpleJSON;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class ConfigModuleTests : RuntimeTestBase
    {
        private const string AttributeTablePath = "ConfigModuleAttributePathTest.json";
        private const string GeneratedLubanTablePath = "Assets/GameDeveloperKit/Tests/Runtime/LubanGeneratedTableFixture.json";

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
            return RunAsync(async () =>
            {
                await App.Register<ConfigModule>();

                Assert.IsNotNull(App.Config);
            });
        }

        [UnityTest]
        public IEnumerator Startup_WhenSettingsMissing_DoesNotThrow()
        {
            return RunAsync(async () =>
            {
                var module = new ConfigModule();

                module.Startup();

                Assert.IsFalse(module.TryGetTable<ItemRow>(out _));
                if (module.TryGetTagGroup(TagCatalogAsset.AssetTagsGroupKey, out var group))
                {
                    Assert.AreEqual(TagCatalogAsset.AssetTagsGroupKey, group.Key);
                    Assert.IsTrue(group.Fixed);
                }
            });
        }

        [UnityTest]
        public IEnumerator TagCatalog_WhenAssetContainsTags_ReturnsReadonlySnapshot()
        {
            return RunAsync(async () =>
            {
                var asset = ScriptableObject.CreateInstance<TagCatalogAsset>();
                asset.EnsureDefaults();
                asset.Groups[0].Tags.Add(new TagDefinition
                {
                    Key = "weapon",
                    DisplayName = "Weapon"
                });

                var catalog = TagCatalog.FromAsset(asset, "test");

                Assert.IsTrue(catalog.TryGetGroup(TagCatalogAsset.AssetTagsGroupKey, out var group));
                Assert.AreEqual(TagCatalogAsset.AssetTagsDisplayName, group.DisplayName);
                Assert.IsTrue(catalog.HasTag(TagCatalogAsset.AssetTagsGroupKey, "weapon"));
                Assert.AreEqual("Weapon", catalog.GetTags(TagCatalogAsset.AssetTagsGroupKey)[0].DisplayName);

                UnityEngine.Object.DestroyImmediate(asset);
                await UniTask.CompletedTask;
            });
        }

        [UnityTest]
        public IEnumerator TagCatalog_WhenDuplicateTagKey_Throws()
        {
            return RunAsync(async () =>
            {
                var asset = ScriptableObject.CreateInstance<TagCatalogAsset>();
                asset.EnsureDefaults();
                asset.Groups[0].Tags.Add(new TagDefinition { Key = "enemy", DisplayName = "Enemy" });
                asset.Groups[0].Tags.Add(new TagDefinition { Key = "Enemy", DisplayName = "Enemy 2" });

                var exception = Assert.Throws<GameException>(() => TagCatalog.FromAsset(asset, "test"));
                StringAssert.Contains("duplicate tag key", exception.Message);

                UnityEngine.Object.DestroyImmediate(asset);
                await UniTask.CompletedTask;
            });
        }

        [UnityTest]
        public IEnumerator TagCatalog_WhenArgumentsInvalid_Throws()
        {
            return RunAsync(async () =>
            {
                Assert.Throws<ArgumentNullException>(() => TagCatalog.Empty.HasTag(null, "weapon"));
                Assert.Throws<ArgumentException>(() => TagCatalog.Empty.HasTag(TagCatalogAsset.AssetTagsGroupKey, " "));

                var exception = Assert.Throws<GameException>(() => TagCatalog.Empty.GetTags(TagCatalogAsset.AssetTagsGroupKey));
                StringAssert.Contains(TagCatalogAsset.AssetTagsGroupKey, exception.Message);

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
                Assert.AreEqual(GeneratedLubanTablePath, tableOption.Path);
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
                var json = JSON.Parse(System.IO.File.ReadAllText(GeneratedLubanTablePath));
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

        private static async UniTask<ConfigModule> CreateStartedModuleAsync()
        {
            var module = new ConfigModule();
            module.Startup();
            return module;
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
    }
}
