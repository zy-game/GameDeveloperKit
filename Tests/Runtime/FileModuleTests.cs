using System;
using System.Collections;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.File;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class FileModuleTests : RuntimeTestBase
    {
        private FileModule m_Module;
        private string m_RootPath;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                if (m_Module != null)
                {
                    m_Module.Shutdown();
                    m_Module = null;
                }

                try
                {
                    await App.Unregister<FileModule>();
                }
                catch (GameException)
                {
                }

                if (!string.IsNullOrEmpty(m_RootPath) && Directory.Exists(m_RootPath))
                {
                    Directory.Delete(m_RootPath, true);
                    m_RootPath = null;
                }
            });
        }

        [UnityTest]
        public IEnumerator Register_WhenFileModuleIsRegistered_ReturnsFile()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Register<FileModule>();

                Assert.IsNotNull(App.File);
            });
        }

        [UnityTest]
        public IEnumerator WriteReadDelete_WhenDataWritten_RoundTripsThroughVfs()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var path = UniquePath("roundtrip");
                var data = Encoding.UTF8.GetBytes("hello-vfs");

                await module.WriteAsync(path, "1.0.0", data);

                Assert.IsTrue(module.Exists(path));
                Assert.IsTrue(module.Exists(path, "1.0.0"));
                Assert.IsFalse(module.Exists(path, "2.0.0"));
                Assert.IsTrue(module.TryGetFileInfo(path, out var entry));
                Assert.AreEqual(data.Length, entry.Size);

                var read = await module.ReadAsync(path);
                CollectionAssert.AreEqual(data, read);

                await module.DeleteAsync(path);

                Assert.IsFalse(module.Exists(path));
                Assert.IsNull(await module.ReadAsync(path));
                Assert.IsFalse(module.TryGetFileInfo(path, out _));
            });
        }

        [UnityTest]
        public IEnumerator DeleteAsync_WhenLastEntryUsesBundle_RemovesBundleFile()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var path = UniquePath("delete-bundle");

                await module.WriteAsync(path, "1.0.0", Encoding.UTF8.GetBytes("delete-bundle-file"));
                Assert.IsTrue(module.TryGetFileInfo(path, out var entry));

                var bundlePath = GetBundlePath(module, entry.BundlePath);
                Assert.IsTrue(System.IO.File.Exists(bundlePath));

                await module.DeleteAsync(path);

                Assert.IsFalse(System.IO.File.Exists(bundlePath));
                Assert.IsFalse(module.TryGetFileInfo(path, out _));
                Assert.IsFalse(BundlePathExists(module, entry.BundlePath));
                Assert.IsFalse(ManifestBundlePathExists(module, entry.BundlePath));
            });
        }

        [UnityTest]
        public IEnumerator WriteAsync_WhenOverwritingPath_RemovesPreviousBundleFile()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var path = UniquePath("overwrite-bundle");

                await module.WriteAsync(path, "1.0.0", Encoding.UTF8.GetBytes("first"));
                Assert.IsTrue(module.TryGetFileInfo(path, out var firstEntry));
                var firstBundlePath = GetBundlePath(module, firstEntry.BundlePath);
                Assert.IsTrue(System.IO.File.Exists(firstBundlePath));

                await module.WriteAsync(path, "2.0.0", Encoding.UTF8.GetBytes("second"));

                Assert.IsFalse(System.IO.File.Exists(firstBundlePath));
                Assert.IsFalse(BundlePathExists(module, firstEntry.BundlePath));
                Assert.IsFalse(ManifestBundlePathExists(module, firstEntry.BundlePath));
                Assert.IsTrue(module.TryGetFileInfo(path, out var secondEntry));
                Assert.AreEqual("2.0.0", secondEntry.Version);
                Assert.IsFalse(string.IsNullOrEmpty(secondEntry.BundlePath));
                CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("second"), await module.ReadAsync(path));
            });
        }

        [UnityTest]
        public IEnumerator WriteAsync_WhenDataIsNull_Throws()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;

                var exception = await ThrowsAsync<ArgumentNullException>(async () => { await module.WriteAsync(UniquePath("null"), "1", null); });
                Assert.AreEqual("data", exception.ParamName);
            });
        }

        internal static async UniTask<FileModule> CreateStartedModuleAsync()
        {
            var module = new FileModule();
            module.Startup();
            return module;
        }

        private async UniTask<FileModule> CreateIsolatedStartedModuleAsync()
        {
            m_RootPath = Path.Combine(UnityEngine.Application.temporaryCachePath, "vfs-tests", Guid.NewGuid().ToString("N"));
            var module = new FileModule(m_RootPath);
            module.Startup();
            return module;
        }

        internal static string UniquePath(string prefix)
        {
            return $"tests/{prefix}-{Guid.NewGuid():N}.bin";
        }

        private static string GetBundlePath(FileModule module, string bundlePath)
        {
            return Path.Combine(module.RootPath, bundlePath);
        }

        private static bool BundlePathExists(FileModule module, string bundlePath)
        {
            foreach (var entry in module.ListFiles())
            {
                if (entry.BundlePath == bundlePath)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ManifestBundlePathExists(FileModule module, string bundlePath)
        {
            var manifestPath = Path.Combine(module.RootPath, VfsConstants.ManifestFileName);
            return System.IO.File.ReadAllText(manifestPath).Contains($"\"BundlePath\": \"{bundlePath}\"");
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
