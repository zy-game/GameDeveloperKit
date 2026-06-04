using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Data;
using GameDeveloperKit.Data.Internal;
using GameDeveloperKit.File;
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
                await module.Startup();

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

                var slot = DataSlot.Create<ExampleData>("slot-a");
                var indexPath = DataPathUtility.GetIndexPath(slot);
                Assert.IsTrue(m_FileModule.Exists(indexPath));
                Assert.IsTrue(m_FileModule.Exists(DataPathUtility.GetVersionPath(slot, first.Version)));
                Assert.IsTrue(m_FileModule.Exists(DataPathUtility.GetVersionPath(slot, second.Version)));

                await module.DeleteDataAsync<ExampleData>("slot-a");

                Assert.IsFalse(module.TryGetData<ExampleData>("slot-a", out _));
                Assert.IsFalse(m_FileModule.Exists(indexPath));
                Assert.IsFalse(m_FileModule.Exists(DataPathUtility.GetVersionPath(slot, first.Version)));
                Assert.IsFalse(m_FileModule.Exists(DataPathUtility.GetVersionPath(slot, second.Version)));
                Assert.IsFalse(m_FileModule.Exists(DataPathUtility.GetVersionPath(slot, third.Version)));
            });
        }

        [UnityTest]
        public IEnumerator LoadDataAsync_WhenIndexMissing_ReturnsDefaultAndDoesNotWrite()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                var module = new DataModule();
                await module.Startup();

                var data = await module.LoadDataAsync<ExampleData>("missing");

                Assert.AreEqual(0, data.Value);
                Assert.IsTrue(module.TryGetData<ExampleData>("missing", out var cached));
                Assert.AreSame(data, cached);
                Assert.IsFalse(m_FileModule.Exists(DataPathUtility.GetIndexPath(DataSlot.Create<ExampleData>("missing"))));
            });
        }

        [UnityTest]
        public IEnumerator DataKeyAttribute_WhenSaved_UsesStableTypeKeyInPathAndDocument()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await RegisterIsolatedFileModuleAsync();
                var module = new DataModule();
                await module.Startup();
                module.SetData("profile", new ExampleData { Value = 9 });

                var version = await module.SaveDataAsync<ExampleData>("profile");

                var slot = DataSlot.Create<ExampleData>("profile");
                var indexPath = DataPathUtility.GetIndexPath(slot);
                var versionPath = DataPathUtility.GetVersionPath(slot, version.Version);
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
                await module.Startup();
                module.SetSerializer(new CustomDataSerializer());
                module.SetData("custom", new ExampleData { Value = 42 });

                await module.SaveDataAsync<ExampleData>("custom");
                await module.Shutdown();
                await module.Startup();
                module.SetSerializer(new CustomDataSerializer());

                var loaded = await module.LoadDataAsync<ExampleData>("custom");

                Assert.AreEqual(42, loaded.Value);
            });
        }

        [Test]
        public void Register_WhenDataModuleIsRegistered_ReturnsData()
        {
            Super.Register<DataModule>().GetAwaiter().GetResult();

            Assert.IsNotNull(Super.Data);
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
                await module.Startup();

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
                await module.Startup();
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
                await module.Startup();
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
                await module.Startup();
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
                await module.Startup();
                module.SetData("broken", new ExampleData { Value = 1 });
                var version = await module.SaveDataAsync<ExampleData>("broken");
                module.SetData("broken", new ExampleData { Value = 9 });

                var slot = DataSlot.Create<ExampleData>("broken");
                await m_FileModule.WriteAsync(DataPathUtility.GetVersionPath(slot, version.Version), version.Version, Encoding.UTF8.GetBytes("{broken-json"));

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
                await module.Startup();
                module.SetData("mismatch", new ExampleData { Value = 1 });
                var version = await module.SaveDataAsync<ExampleData>("mismatch");

                var slot = DataSlot.Create<ExampleData>("mismatch");
                var versionPath = DataPathUtility.GetVersionPath(slot, version.Version);
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
                await module.Startup();
                module.SetSerializer(new FailingDataSerializer());
                module.SetData("fail", new ExampleData { Value = 1 });
                var slot = DataSlot.Create<ExampleData>("fail");

                var exception = await ThrowsAsync<GameException>(async () => { await module.SaveDataAsync<ExampleData>("fail"); });

                StringAssert.Contains("fail", exception.Message);
                Assert.IsFalse(m_FileModule.Exists(DataPathUtility.GetIndexPath(slot)));
            });
        }

        [Test]
        public void Shutdown_WhenCalled_ClearsCacheWithoutSaving()
        {
            var module = new DataModule();
            module.SetData("temp", new ExampleData { Value = 1 });

            module.Shutdown().GetAwaiter().GetResult();

            Assert.IsFalse(module.TryGetData<ExampleData>("temp", out _));
        }

        private async UniTask RegisterIsolatedFileModuleAsync()
        {
            m_RootPath = Path.Combine(UnityEngine.Application.temporaryCachePath, "data-module-tests", Guid.NewGuid().ToString("N"));
            var module = new FileModule(m_RootPath);
            await RegisterModuleAsync(module);
            m_FileModule = module;
        }

        private static async UniTask RegisterModuleAsync<T>(T module)
            where T : class, IGameModule
        {
            await UnregisterIfRegisteredAsync<T>();
            GetModules().Add(typeof(T), module);
            await module.Startup();
        }

        private static async UniTask UnregisterIfRegisteredAsync<T>()
            where T : IGameModule
        {
            if (GetModules().ContainsKey(typeof(T)))
            {
                await Super.Unregister<T>();
            }
        }

        private static Dictionary<Type, IGameModule> GetModules()
        {
            var field = typeof(Super).GetField("_modules", BindingFlags.NonPublic | BindingFlags.Static);
            return (Dictionary<Type, IGameModule>)field.GetValue(null);
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

        [DataKey("example-data")]
        private sealed class ExampleData
        {
            public int Value;
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
                var example = (ExampleData)(object)data;
                return Encoding.UTF8.GetBytes(example.Value.ToString());
            }

            public T Deserialize<T>(byte[] bytes)
            {
                return (T)(object)new ExampleData { Value = int.Parse(Encoding.UTF8.GetString(bytes)) };
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
        }
    }
}
