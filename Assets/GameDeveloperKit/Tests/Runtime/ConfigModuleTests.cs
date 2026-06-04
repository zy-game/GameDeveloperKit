using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Config;
using GameDeveloperKit.Download;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class ConfigModuleTests : RuntimeTestBase
    {
        private const string AttributeTablePath = "ConfigModuleAttributePathTest.json";

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
                await Super.Register<ConfigModule>();

                Assert.IsNotNull(Super.Config);
            });
        }

        [UnityTest]
        public IEnumerator Startup_WhenSettingsMissing_DoesNotThrow()
        {
            return RunAsync(async () =>
            {
                var module = new ConfigModule();

                await module.Startup();

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

                await module.Shutdown();

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
            await module.Startup();
            return module;
        }

        private static void TryUnregister<T>() where T : IGameModule
        {
            try
            {
                Super.Unregister<T>().GetAwaiter().GetResult();
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
