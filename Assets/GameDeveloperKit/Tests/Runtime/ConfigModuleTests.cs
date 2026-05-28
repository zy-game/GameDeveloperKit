using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Config;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class ConfigModuleTests : RuntimeTestBase
    {
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

            try
            {
                Super.Unregister<ConfigModule>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }
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

                Assert.IsFalse(module.HasSource("missing"));
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenJsonRootArray_LoadsRowsAndQueriesByKey()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                module.RegisterSource(Source("items", ConfigFormat.Json, WriteTemp("[{\"Id\":1001,\"Name\":\"Sword\",\"Price\":120}]")));

                var table = await module.LoadTableAsync<ItemConfig>("items");

                Assert.AreEqual(typeof(int), table.KeyType);
                Assert.AreEqual("Sword", table.Get(1001).Name);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenJsonWrapper_LoadsRows()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                module.RegisterSource(Source("items", ConfigFormat.Json, WriteTemp("{\"rows\":[{\"Id\":1002,\"Name\":\"Shield\",\"Price\":90}]}")));

                var table = await module.LoadTableAsync<ItemConfig>("items");

                Assert.IsTrue(table.TryGet(1002, out var row));
                Assert.AreEqual("Shield", row.Name);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenCsv_LoadsRows()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                module.RegisterSource(Source("items", ConfigFormat.Csv, WriteTemp("Id,Name,Price\n1001,Sword,120\n1002,\"Iron, Shield\",90")));

                var table = await module.LoadTableAsync<ItemConfig>("items");

                Assert.AreEqual("Iron, Shield", table.Get(1002).Name);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenXml_LoadsRows()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                module.RegisterSource(Source("items", ConfigFormat.Xml, WriteTemp("<rows><row><Id>1001</Id><Name>Sword</Name><Price>120</Price></row></rows>")));

                var table = await module.LoadTableAsync<ItemConfig>("items");

                Assert.AreEqual(120, table.Get(1001).Price);
            });
        }

        [UnityTest]
        public IEnumerator ScriptableObjectSerializer_WhenAssetImplementsConfigAsset_LoadsRows()
        {
            return RunAsync(async () =>
            {
                var asset = ScriptableObject.CreateInstance<ItemConfigAsset>();
                asset.Rows.Add(new ItemConfig { Id = 1001, Name = "Sword", Price = 120 });
                var serializer = new GameDeveloperKit.Config.Serializers.ScriptableObjectConfigSerializer();
                var context = new ConfigSerializerContext(
                    Source("items", ConfigFormat.ScriptableObject, "ItemsAsset"),
                    ConfigSourcePayload.FromAsset(asset));

                var rows = await serializer.DeserializeAsync(context, typeof(ItemConfig));

                Assert.AreEqual(1, rows.Count);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenCalledTwice_UsesCachedTable()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                var serializer = new CountingSerializer(new[] { new ItemConfig { Id = 1001, Name = "Sword", Price = 120 } });
                module.RegisterSerializer(serializer);
                module.RegisterSource(Source("items", ConfigFormat.Json, WriteTemp("{}")));

                var first = await module.LoadTableAsync<ItemConfig>("items");
                var second = await module.LoadTableAsync<ItemConfig>("items");

                Assert.AreSame(first, second);
                Assert.AreEqual(1, serializer.Count);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenSameNameIsPending_UsesSingleLoad()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                var serializer = new YieldingCountingSerializer(new[] { new ItemConfig { Id = 1001, Name = "Sword", Price = 120 } });
                module.RegisterSerializer(serializer);
                module.RegisterSource(Source("items", ConfigFormat.Json, WriteTemp("{}")));

                var first = module.LoadTableAsync<ItemConfig>("items");
                var second = module.LoadTableAsync<ItemConfig>("items");
                await UniTask.WhenAll(first, second);

                Assert.AreEqual(1, serializer.Count);
            });
        }

        [UnityTest]
        public IEnumerator TryGetRow_WhenKeyMissing_ReturnsFalse()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                module.RegisterSource(Source("items", ConfigFormat.Json, WriteTemp("[{\"Id\":1001,\"Name\":\"Sword\",\"Price\":120}]")));
                await module.LoadTableAsync<ItemConfig>("items");

                Assert.IsFalse(module.TryGetRow<ItemConfig>("items", 9999, out _));
            });
        }

        [UnityTest]
        public IEnumerator Unload_WhenTableLoaded_RemovesTableAndAllowsReload()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                var serializer = new CountingSerializer(new[] { new ItemConfig { Id = 1001, Name = "Sword", Price = 120 } });
                module.RegisterSerializer(serializer);
                module.RegisterSource(Source("items", ConfigFormat.Json, WriteTemp("{}")));

                await module.LoadTableAsync<ItemConfig>("items");
                module.Unload("items");
                await module.LoadTableAsync<ItemConfig>("items");

                Assert.AreEqual(2, serializer.Count);
            });
        }

        [UnityTest]
        public IEnumerator Shutdown_WhenTableLoaded_ClearsTable()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                module.RegisterSource(Source("items", ConfigFormat.Json, WriteTemp("[{\"Id\":1001,\"Name\":\"Sword\",\"Price\":120}]")));
                await module.LoadTableAsync<ItemConfig>("items");

                await module.Shutdown();

                Assert.Throws<GameException>(() => module.GetTable<ItemConfig>("items"));
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenNameInvalid_Throws()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();

                await ThrowsAsync<ArgumentNullException>(async () => { await module.LoadTableAsync<ItemConfig>(null); });
                await ThrowsAsync<ArgumentException>(async () => { await module.LoadTableAsync<ItemConfig>(" "); });
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenSourceMissing_Throws()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();

                var exception = await ThrowsAsync<GameException>(async () => { await module.LoadTableAsync<ItemConfig>("missing"); });
                StringAssert.Contains("missing", exception.Message);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenSerializerMissing_Throws()
        {
            return RunAsync(async () =>
            {
                var module = new ConfigModule();
                module.RegisterSource(Source("items", ConfigFormat.Json, WriteTemp("{}")));

                var exception = await ThrowsAsync<GameException>(async () => { await module.LoadTableAsync<ItemConfig>("items"); });
                StringAssert.Contains("Json", exception.Message);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenKeyFieldMissing_Throws()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                module.RegisterSource(Source("items", ConfigFormat.Json, WriteTemp("[{\"Name\":\"Sword\"}]"), typeof(NoKeyConfig)));

                var exception = await ThrowsAsync<GameException>(async () => { await module.LoadTableAsync<NoKeyConfig>("items"); });
                StringAssert.Contains("does not contain key field", exception.Message);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenDuplicateKey_Throws()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                module.RegisterSource(Source("items", ConfigFormat.Json, WriteTemp("[{\"Id\":1001,\"Name\":\"Sword\"},{\"Id\":1001,\"Name\":\"Shield\"}]")));

                var exception = await ThrowsAsync<GameException>(async () => { await module.LoadTableAsync<ItemConfig>("items"); });
                StringAssert.Contains("duplicate key", exception.Message);
            });
        }

        [UnityTest]
        public IEnumerator Get_WhenKeyTypeDoesNotMatch_Throws()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                module.RegisterSource(Source("items", ConfigFormat.Json, WriteTemp("[{\"Id\":1001,\"Name\":\"Sword\"}]")));
                var table = await module.LoadTableAsync<ItemConfig>("items");

                var exception = Assert.Throws<GameException>(() => table.Get("1001"));
                StringAssert.Contains("key type mismatch", exception.Message);
            });
        }

        [UnityTest]
        public IEnumerator LoadTableAsync_WhenJsonInvalid_DoesNotCacheTable()
        {
            return RunAsync(async () =>
            {
                var module = await CreateStartedModuleAsync();
                module.RegisterSource(Source("items", ConfigFormat.Json, WriteTemp("{invalid")));

                await ThrowsAsync<GameException>(async () => { await module.LoadTableAsync<ItemConfig>("items"); });
                Assert.Throws<GameException>(() => module.GetTable<ItemConfig>("items"));
            });
        }

        [UnityTest]
        public IEnumerator ScriptableObjectSerializer_WhenAssetDoesNotImplementContract_Throws()
        {
            return RunAsync(async () =>
            {
                var asset = ScriptableObject.CreateInstance<PlainAsset>();
                var serializer = new GameDeveloperKit.Config.Serializers.ScriptableObjectConfigSerializer();
                var context = new ConfigSerializerContext(
                    Source("items", ConfigFormat.ScriptableObject, "PlainAsset"),
                    ConfigSourcePayload.FromAsset(asset));

                var exception = await ThrowsAsync<GameException>(async () => { await serializer.DeserializeAsync(context, typeof(ItemConfig)); });
                StringAssert.Contains("IConfigAsset", exception.Message);
            });
        }

        [UnityTest]
        public IEnumerator ScriptableObjectSerializer_WhenRowTypeDoesNotMatch_Throws()
        {
            return RunAsync(async () =>
            {
                var asset = ScriptableObject.CreateInstance<WrongTypeConfigAsset>();
                var serializer = new GameDeveloperKit.Config.Serializers.ScriptableObjectConfigSerializer();
                var context = new ConfigSerializerContext(
                    Source("items", ConfigFormat.ScriptableObject, "WrongTypeAsset"),
                    ConfigSourcePayload.FromAsset(asset));

                var exception = await ThrowsAsync<GameException>(async () => { await serializer.DeserializeAsync(context, typeof(ItemConfig)); });
                StringAssert.Contains("requested row type", exception.Message);
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

        private ConfigSourceDefinition Source(string name, ConfigFormat format, string location, Type rowType = null)
        {
            return new ConfigSourceDefinition
            {
                Name = name,
                Format = format,
                Location = location,
                RowTypeName = (rowType ?? typeof(ItemConfig)).AssemblyQualifiedName,
                KeyField = "Id",
            };
        }

        private string WriteTemp(string content)
        {
            var path = Path.GetTempFileName();
            System.IO.File.WriteAllText(path, content);
            m_TempFiles.Add(path);
            return path;
        }

        [Serializable]
        private sealed class ItemConfig
        {
            public int Id;
            public string Name;
            public int Price;
        }

        [Serializable]
        private sealed class NoKeyConfig
        {
            public string Name = string.Empty;
        }

        private sealed class ItemConfigAsset : ScriptableObject, IConfigAsset
        {
            public readonly List<ItemConfig> Rows = new List<ItemConfig>();

            public Type RowType => typeof(ItemConfig);

            public IList GetRows()
            {
                return Rows;
            }
        }

        private sealed class WrongTypeConfigAsset : ScriptableObject, IConfigAsset
        {
            public Type RowType => typeof(NoKeyConfig);

            public IList GetRows()
            {
                return new List<NoKeyConfig>();
            }
        }

        private sealed class PlainAsset : ScriptableObject
        {
        }

        private sealed class CountingSerializer : IConfigSerializer
        {
            private readonly IList m_Rows;

            public CountingSerializer(IEnumerable<ItemConfig> rows)
            {
                m_Rows = new List<ItemConfig>(rows);
            }

            public ConfigFormat Format => ConfigFormat.Json;

            public int Count { get; private set; }

            public UniTask<IList> DeserializeAsync(ConfigSerializerContext context, Type rowType)
            {
                Count++;
                return UniTask.FromResult(m_Rows);
            }
        }

        private sealed class YieldingCountingSerializer : IConfigSerializer
        {
            private readonly IList m_Rows;

            public YieldingCountingSerializer(IEnumerable<ItemConfig> rows)
            {
                m_Rows = new List<ItemConfig>(rows);
            }

            public ConfigFormat Format => ConfigFormat.Json;

            public int Count { get; private set; }

            public async UniTask<IList> DeserializeAsync(ConfigSerializerContext context, Type rowType)
            {
                Count++;
                await UniTask.Yield();
                return m_Rows;
            }
        }
    }
}
