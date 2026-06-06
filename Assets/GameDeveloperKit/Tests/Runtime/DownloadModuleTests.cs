using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Download;
using GameDeveloperKit.File;
using GameDeveloperKit.Operation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class DownloadModuleTests : RuntimeTestBase
    {
        private const string TestDownloadUrl = "https://saltgame-1251268098.cos.ap-chengdu.myqcloud.com/common.game";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                try
                {
                    await App.Unregister<DownloadModule>();
                }
                catch (GameException)
                {
                }

                try
                {
                    await App.Unregister<OperationModule>();
                }
                catch (GameException)
                {
                }

                try
                {
                    await App.Unregister<FileModule>();
                }
                catch (GameException)
                {
                }
            });
        }

        [UnityTest]
        public IEnumerator Register_WhenDownloadModuleIsRegistered_ReturnsDownload()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await EnsureOperationAsync();
                await App.Register<DownloadModule>();

                Assert.IsNotNull(App.Download);
            });
        }

        [UnityTest]
        public IEnumerator DownloadAsync_WhenSameUrlRequested_ReturnsCachedHandler()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await EnsureOperationAsync();
                var module = await CreateStartedModuleAsync();
                var url = "https://example.com/cached.bin";

                var first = module.DownloadAsync(url);
                var second = module.DownloadAsync(url);

                Assert.AreSame(first, second);
                Assert.IsTrue(module.HasDownload(url));
                Assert.AreSame(first, module.GetDownload(url));

                await module.Cancel(url);
            });
        }

        [UnityTest]
        public IEnumerator DownloadAsync_WhenRemoteFileResponds_WritesDownloadedBytesIntoFileModule()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await EnsureOperationAsync();
                var download = await CreateStartedModuleAsync();
                var file = await FileModuleTests.CreateStartedModuleAsync();

                var handler = download.DownloadAsync(TestDownloadUrl);
                await handler.WaitCompletionAsync();

                Assert.AreEqual(OperationStatus.Succeeded, handler.Status);
                Assert.IsTrue(System.IO.File.Exists(handler.TempPath));

                var downloaded = await System.IO.File.ReadAllBytesAsync(handler.TempPath);
                Assert.Greater(downloaded.Length, 0);

                var vfsPath = System.IO.Path.GetFileName(TestDownloadUrl);
                await file.WriteAsync(vfsPath, "downloaded", downloaded);
                CollectionAssert.AreEqual(downloaded, await file.ReadAsync(vfsPath));

                await file.DeleteAsync(vfsPath);
                await download.Cancel(TestDownloadUrl);
                await file.Shutdown();
            });
        }

        [UnityTest]
        public IEnumerator CancelAll_WhenDownloadsExist_CancelsAndClearsHandlers()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await EnsureOperationAsync();
                var module = await CreateStartedModuleAsync();
                var first = "https://example.com/a.bin";
                var second = "https://example.com/b.bin";

                module.DownloadAsync(first);
                module.DownloadAsync(second);

                await module.CancelAll();

                Assert.IsFalse(module.HasDownload(first));
                Assert.IsFalse(module.HasDownload(second));
            });
        }

        [Test]
        public void DownloadAsync_WhenUrlInvalid_Throws()
        {
            var module = new DownloadModule();

            Assert.Throws<ArgumentNullException>(() => module.DownloadAsync(null));
            Assert.Throws<ArgumentException>(() => module.DownloadAsync(" "));
            Assert.Throws<ArgumentException>(() => module.DownloadAsync("file:///tmp/test.bin"));
        }

        private static async UniTask EnsureOperationAsync()
        {
            try
            {
                await App.Register<OperationModule>();
            }
            catch (GameException)
            {
            }
        }

        private static async UniTask<DownloadModule> CreateStartedModuleAsync()
        {
            var module = new DownloadModule();
            await module.Startup();
            return module;
        }
    }
}
