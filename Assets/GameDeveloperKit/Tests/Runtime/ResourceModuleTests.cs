using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Download;
using GameDeveloperKit.Operation;
using GameDeveloperKit.Resource;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class ResourceModuleTests : RuntimeTestBase
    {
        private readonly List<string> m_TempFiles = new List<string>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                try
                {
                    await Super.Unregister<ResourceModule>();
                }
                catch (GameException)
                {
                }

                try
                {
                    await Super.Unregister<OperationModule>();
                }
                catch (GameException)
                {
                }

                try
                {
                    await Super.Unregister<DownloadModule>();
                }
                catch (GameException)
                {
                }

                foreach (var path in m_TempFiles)
                {
                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Delete(path);
                    }
                }

                m_TempFiles.Clear();
            });
        }

        [UnityTest]
        public IEnumerator Startup_WhenOperationModuleIsUnavailable_ThrowsGameException()
        {
            return UniTask.ToCoroutine(async () =>
            {
                try
                {
                    await Super.Unregister<OperationModule>();
                }
                catch (GameException)
                {
                }

                var module = new ResourceModule();

                var exception = await ThrowsAsync<GameException>(async () => { await module.Startup(); });
                Assert.IsNotEmpty(exception.Message);
            });
        }

        [Test]
        public void LoadMethods_WhenNoModeAvailable_ThrowGameException()
        {
            var module = new ResourceModule();

            Assert.Throws<GameException>(() => module.LoadAssetAsync("asset").GetAwaiter().GetResult());
            Assert.Throws<GameException>(() => module.LoadRawAssetAsync("asset").GetAwaiter().GetResult());
            Assert.Throws<GameException>(() => module.LoadSceneAssetAsync("scene").GetAwaiter().GetResult());
        }

        [Test]
        public void LoadMethods_WhenKeyInvalid_ThrowArgumentExceptions()
        {
            var module = new ResourceModule();

            Assert.Throws<ArgumentNullException>(() => module.LoadAssetAsync(null).GetAwaiter().GetResult());
            Assert.Throws<ArgumentException>(() => module.LoadRawAssetAsync(" ").GetAwaiter().GetResult());
            Assert.Throws<ArgumentException>(() => module.InitializePackageAsync(" ").GetAwaiter().GetResult());
        }

        [UnityTest]
        public IEnumerator ManifestOperationHandle_WhenLocalManifestExists_LoadsManifest()
        {
            return UniTask.ToCoroutine(async () =>
            {
                try
                {
                    await Super.Register<OperationModule>();
                }
                catch (GameException)
                {
                }

                var path = WriteTemp("{\"Version\":\"test-version\",\"BuildTime\":1,\"Packages\":[]}");

                var operation = await Super.Operation.WaitCompletionAsync<ResourceModule.ManifestOperationHandle>(path, path);

                Assert.AreEqual(OperationStatus.Succeeded, operation.Status);
                Assert.IsNotNull(operation.Value);
                Assert.AreEqual("test-version", operation.Value.Version);
            });
        }

        private string WriteTemp(string content)
        {
            var path = Path.GetTempFileName();
            System.IO.File.WriteAllText(path, content);
            m_TempFiles.Add(path);
            return path;
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
    }
}
