using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Download;
using GameDeveloperKit.File;
using GameDeveloperKit.Debugger;
using GameDeveloperKit.Operation;
using GameDeveloperKit.Resource;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
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
                    await App.Unregister<ResourceModule>();
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
                    await App.Unregister<DownloadModule>();
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

                try
                {
                    await App.Unregister<DebugModule>();
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
        public IEnumerator Startup_WhenOperationModuleIsUnavailable_StartsShell()
        {
            return UniTask.ToCoroutine(async () =>
            {
                try
                {
                    await App.Unregister<OperationModule>();
                }
                catch (GameException)
                {
                }

                var module = new ResourceModule();

                Assert.DoesNotThrow(() => module.Startup());
                Assert.IsFalse(module.IsInitialized);
                Assert.AreEqual(ResourceInitializeState.NotInitialized, module.InitializeState);
                var exception = Assert.Throws<GameException>(() => module.LoadAssetAsync("asset").GetAwaiter().GetResult());
                StringAssert.Contains("Call InitializeAsync first", exception.Message);
            });
        }

        [Test]
        public void LoadMethods_WhenNotInitialized_ThrowGameException()
        {
            var module = new ResourceModule();

            var exception = Assert.Throws<GameException>(() => module.LoadAssetAsync("asset").GetAwaiter().GetResult());
            StringAssert.Contains("Call InitializeAsync first", exception.Message);
            StringAssert.Contains("Call InitializeAsync first", Assert.Throws<GameException>(() => module.LoadRawAssetAsync("asset").GetAwaiter().GetResult()).Message);
            StringAssert.Contains("Call InitializeAsync first", Assert.Throws<GameException>(() => module.LoadSceneAssetAsync("scene").GetAwaiter().GetResult()).Message);
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
                    await App.Register<OperationModule>();
                }
                catch (GameException)
                {
                }

                var path = WriteTemp("{\"Version\":\"test-version\",\"BuildTime\":1,\"Packages\":[]}");

                var operation = await App.Operation.WaitCompletionWithKeyAsync<ResourceModule.ManifestOperationHandle>(path, path);

                Assert.AreEqual(OperationStatus.Succeeded, operation.Status);
                Assert.IsNotNull(operation.Value);
                Assert.AreEqual("test-version", operation.Value.Version);
            });
        }

        [UnityTest]
        public IEnumerator InitializeAsync_WhenManifestIsValid_EntersInitializedState()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = App.Resource;
                var settings = CreateSettings(CreateManifestPath("init-success", Array.Empty<PackageInfo>()));

                await module.InitializeAsync(settings);

                Assert.IsTrue(module.IsInitialized);
                Assert.AreEqual(ResourceInitializeState.Initialized, module.InitializeState);
                Assert.AreSame(settings, module.Settings);
                Assert.IsNotNull(module.Manifest);
                Assert.AreEqual("init-success", module.Manifest.Version);
            });
        }

        [UnityTest]
        public IEnumerator InitializeAsync_WhenCalledAgain_ReturnsReadyState()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = App.Resource;
                var firstSettings = CreateSettings(CreateManifestPath("first", Array.Empty<PackageInfo>()));
                var secondSettings = CreateSettings(CreateManifestPath("second", Array.Empty<PackageInfo>()));

                await module.InitializeAsync(firstSettings);
                await module.InitializeAsync(secondSettings);

                Assert.IsTrue(module.IsInitialized);
                Assert.AreSame(firstSettings, module.Settings);
                Assert.AreEqual("first", module.Manifest.Version);
            });
        }

        [UnityTest]
        public IEnumerator InitializeAsync_WhenCalledConcurrently_ReusesInFlightInitialization()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = App.Resource;
                var settings = CreateSettings(CreateManifestPath("concurrent", Array.Empty<PackageInfo>()));

                var first = module.InitializeAsync(settings);
                var second = module.InitializeAsync(settings);
                await UniTask.WhenAll(first, second);

                Assert.IsTrue(module.IsInitialized);
                Assert.AreEqual(ResourceInitializeState.Initialized, module.InitializeState);
                Assert.AreSame(settings, module.Settings);
                Assert.AreEqual("concurrent", module.Manifest.Version);
            });
        }

        [UnityTest]
        public IEnumerator InitializeAsync_WhenManifestFails_AllowsRetry()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = App.Resource;
                var failedSettings = CreateSettings(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json"));

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await module.InitializeAsync(failedSettings);
                });
                StringAssert.Contains("Resource manifest initialize failed", exception.Message);
                Assert.IsFalse(module.IsInitialized);
                Assert.AreEqual(ResourceInitializeState.Failed, module.InitializeState);
                Assert.IsNull(module.Settings);
                Assert.IsNull(module.Manifest);

                var retrySettings = CreateSettings(CreateManifestPath("retry", Array.Empty<PackageInfo>()));
                await module.InitializeAsync(retrySettings);

                Assert.IsTrue(module.IsInitialized);
                Assert.AreEqual(ResourceInitializeState.Initialized, module.InitializeState);
                Assert.AreEqual("retry", module.Manifest.Version);
            });
        }

        [UnityTest]
        public IEnumerator UninitializeAsync_WhenInitialized_ReturnsToNotInitialized()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = App.Resource;
                var settings = CreateSettings(CreateManifestPath("uninit", Array.Empty<PackageInfo>()));

                await module.InitializeAsync(settings);
                await module.UninitializeAsync();

                Assert.IsFalse(module.IsInitialized);
                Assert.AreEqual(ResourceInitializeState.NotInitialized, module.InitializeState);
                Assert.IsNull(module.Settings);
                Assert.IsNull(module.Manifest);
                var exception = Assert.Throws<GameException>(() => module.LoadAssetAsync("asset").GetAwaiter().GetResult());
                StringAssert.Contains("Call InitializeAsync first", exception.Message);
            });
        }

        [UnityTest]
        public IEnumerator InitializeAsync_WhenDefaultPackageIsMissing_FailsWithPackageName()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = App.Resource;
                var settings = CreateSettings(CreateManifestPath("missing-package", Array.Empty<PackageInfo>()));
                settings.DefaultPackages = new[] { "Main" };

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await module.InitializeAsync(settings);
                });

                StringAssert.Contains("Main", exception.Message);
                Assert.IsFalse(module.IsInitialized);
                Assert.AreEqual(ResourceInitializeState.Failed, module.InitializeState);
            });
        }

        [UnityTest]
        public IEnumerator InitializeAsync_WhenManifestContainsBuiltin_InitializesBuiltinMode()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = App.Resource;
                var builtinPackage = new PackageInfo
                {
                    Name = BuiltinMode.BUILTIN_PACKAGE_NAME,
                    Bundles = new List<BundleInfo>
                    {
                        new BundleInfo
                        {
                            Name = BuiltinMode.BUILTIN_PACKAGE_NAME,
                            Assets = new List<AssetInfo>
                            {
                                new AssetInfo
                                {
                                    Location = "Resources/DefaultGUISkin",
                                    TypeName = nameof(GUISkin),
                                }
                            }
                        }
                    }
                };
                var settings = CreateSettings(CreateManifestPath("builtin", new[] { builtinPackage }));

                await module.InitializeAsync(settings);
                var handle = await module.LoadAssetAsync("Resources/DefaultGUISkin");

                Assert.IsTrue(module.IsInitialized);
                Assert.IsNotNull(handle);
                Assert.AreEqual(ResourceStatus.Succeeded, handle.Status);
                Assert.IsNotNull(handle.GetAsset<GUISkin>());
            });
        }

        [Test]
        public void ResourceHandles_WhenRetained_ReleaseOnlyOnLastReference()
        {
            var bundle = BundleHandle.Success(new BundleInfo { Name = "bundle" }, null);
            var asset = AssetHandle.Success(new AssetInfo { Location = "asset" }, null, bundle);

            Assert.AreEqual(1, asset.ReferenceCount);
            Assert.AreEqual(2, bundle.ReferenceCount);

            asset.Retain();
            asset.Release();

            Assert.AreEqual(ResourceStatus.Succeeded, asset.Status);
            Assert.AreEqual(1, asset.ReferenceCount);
            Assert.AreEqual(2, bundle.ReferenceCount);

            asset.Release();

            Assert.AreEqual(ResourceStatus.Released, asset.Status);
            Assert.AreEqual(0, asset.ReferenceCount);
            Assert.AreEqual(1, bundle.ReferenceCount);

            bundle.Release();

            Assert.AreEqual(ResourceStatus.Released, bundle.Status);
            Assert.AreEqual(0, bundle.ReferenceCount);
        }

        [UnityTest]
        public IEnumerator Provider_WhenLoadingSameAsset_ReusesHandleUntilUnusedUnload()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var assetInfo = new AssetInfo { Location = "shared-asset" };
                var provider = new TestAssetProvider(new BundleInfo
                {
                    Name = "bundle",
                    Assets = new List<AssetInfo> { assetInfo },
                });

                var first = await provider.LoadAssetAsync("shared-asset");
                var second = await provider.LoadAssetAsync("shared-asset");

                Assert.AreSame(first, second);
                Assert.AreEqual(1, provider.LoadCount);
                Assert.AreEqual(2, first.ReferenceCount);
                Assert.IsTrue(provider.HasLoadedAssets);

                await provider.UnloadAsset(first);
                Assert.AreEqual(1, first.ReferenceCount);
                Assert.IsTrue(provider.HasLoadedAssets);

                await provider.UnloadAsset(second);
                Assert.AreEqual(0, first.ReferenceCount);
                Assert.AreEqual(ResourceStatus.Succeeded, first.Status);
                Assert.IsTrue(provider.HasLoadedAssets);

                var revived = await provider.LoadAssetAsync("shared-asset");
                Assert.AreSame(first, revived);
                Assert.AreEqual(1, provider.LoadCount);
                Assert.AreEqual(1, revived.ReferenceCount);

                await provider.UnloadAsset(revived);
                await provider.UnloadUnusedAssetAsync();

                Assert.IsFalse(provider.HasLoadedAssets);
                Assert.AreEqual(ResourceStatus.Released, revived.Status);
            });
        }

        private string WriteTemp(string content)
        {
            var path = Path.GetTempFileName();
            System.IO.File.WriteAllText(path, content);
            m_TempFiles.Add(path);
            return path;
        }

        private string CreateManifestPath(string version, IEnumerable<PackageInfo> packages)
        {
            var manifest = new ManifestInfo
            {
                Version = version,
                BuildTime = 1,
                Packages = new List<PackageInfo>(packages),
            };

            return WriteTemp(JsonConvert.SerializeObject(manifest));
        }

        private static ResourceSettings CreateSettings(string manifestPath)
        {
            var settings = new ResourceSettings();
            settings.Mode = ResourceMode.Offline;
            settings.ManifestName = manifestPath;
            settings.DefaultPackages = Array.Empty<string>();
            return settings;
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

        private sealed class TestAssetProvider : ProviderBase
        {
            public int LoadCount { get; private set; }

            public TestAssetProvider(BundleInfo info) : base(info)
            {
            }

            public override UniTask<OperationHandle<BundleHandle>> InitializeProviderAsync()
            {
                return UniTask.FromResult<OperationHandle<BundleHandle>>(new TestBundleOperationHandle(BundleHandle.Success(Info, null)));
            }

            public override UniTask<OperationHandle> UninitializeProviderAsync()
            {
                return UniTask.FromResult<OperationHandle>(new TestOperationHandle());
            }

            protected override UniTask<AssetHandle> LoadAssetInternalAsync(AssetInfo asset)
            {
                LoadCount++;
                return UniTask.FromResult(AssetHandle.Success(asset, null));
            }

            protected override UniTask<RawAssetHandle> LoadRawAssetInternalAsync(AssetInfo asset)
            {
                return UniTask.FromResult(RawAssetHandle.Failure(new NotSupportedException()));
            }

            protected override UniTask<SceneAssetHandle> LoadSceneAssetInternalAsync(AssetInfo asset)
            {
                return UniTask.FromResult(SceneAssetHandle.Failure(new NotSupportedException()));
            }
        }

        private sealed class TestOperationHandle : OperationHandle
        {
            public TestOperationHandle()
            {
                SetResult();
            }

            public override void Execute(params object[] args)
            {
            }
        }

        private sealed class TestBundleOperationHandle : OperationHandle<BundleHandle>
        {
            public TestBundleOperationHandle(BundleHandle value)
            {
                SetResult(value);
            }

            public override void Execute(params object[] args)
            {
            }
        }

    }
}
