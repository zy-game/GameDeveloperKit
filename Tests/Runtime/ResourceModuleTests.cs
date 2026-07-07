using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
                await WithoutDefaultStartupManifest(async () =>
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
        public IEnumerator ResourceManifestReader_WhenLocalManifestExists_LoadsManifest()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var path = WriteTemp("{\"Version\":\"test-version\",\"BuildTime\":1,\"Packages\":[]}");

                var manifest = await ResourceManifestReader.ReadAsync(path);

                Assert.IsNotNull(manifest);
                Assert.AreEqual("test-version", manifest.Version);
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
        public IEnumerator InitializeAsync_WhenDefaultPackageIsMissing_DoesNotInitializePackages()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = App.Resource;
                var settings = CreateSettings(CreateManifestPath("missing-package", Array.Empty<PackageInfo>()));
                settings.DefaultPackages = new[] { "Main" };

                await module.InitializeAsync(settings);

                Assert.IsTrue(module.IsInitialized);
                Assert.AreEqual(ResourceInitializeState.Initialized, module.InitializeState);
                Assert.Throws<GameException>(() => module.InitializePackageAsync("Main").GetAwaiter().GetResult());
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
                    Name = ResourceConstants.BUILTIN_PACKAGE_NAME,
                    Bundles = new List<BundleInfo>
                    {
                        new BundleInfo
                        {
                            Name = "Resources",
                            ProviderId = ResourceProviderIds.Resources,
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
                var operation = await module.InitializePackageAsync(ResourceConstants.BUILTIN_PACKAGE_NAME);
                var handle = await module.LoadAssetAsync("Resources/DefaultGUISkin");

                Assert.IsTrue(module.IsInitialized);
                Assert.AreEqual(OperationStatus.Succeeded, operation.Status);
                Assert.IsNotNull(handle);
                Assert.AreEqual(ResourceStatus.Succeeded, handle.Status);
                Assert.IsNotNull(handle.GetAsset<GUISkin>());
            });
        }

        [UnityTest]
        public IEnumerator Startup_WhenDefaultManifestContainsBuiltinResources_InitializesStartupResourcesOnly()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var manifest = CreateManifest(
                    "startup-builtin",
                    new[]
                    {
                        CreateGuiSkinResourcesPackage(ResourceConstants.BUILTIN_PACKAGE_NAME, "Resources", "Resources/DefaultGUISkin"),
                        CreateGuiSkinResourcesPackage("Base", "BaseResources", "Resources/DefaultGUISkin")
                    });

                await WithDefaultStartupManifest(manifest, async () =>
                {
                    var module = App.Resource;

                    Assert.IsTrue(module.IsStartupReady);
                    Assert.IsTrue(module.IsLocalInitialized);
                    Assert.IsFalse(module.IsInitialized);
                    Assert.AreEqual(ResourceInitializeState.LocalInitialized, module.InitializeState);
                    Assert.IsNotNull(module.Manifest);
                    Assert.AreEqual("startup-builtin", module.Manifest.Version);
                    Assert.IsFalse(module.HasPackage("Base"));

                    var handle = await module.LoadAssetAsync("Resources/DefaultGUISkin");

                    Assert.IsNotNull(handle);
                    Assert.AreEqual(ResourceStatus.Succeeded, handle.Status);
                    Assert.IsNotNull(handle.GetAsset<GUISkin>());
                });
            });
        }

        [UnityTest]
        public IEnumerator PreloadDefaultPackagesAsync_WhenDefaultPackagesConfigured_InitializesAfterStartup()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var manifest = CreateManifest(
                    "startup-default-package",
                    new[]
                    {
                        CreateGuiSkinResourcesPackage(ResourceConstants.BUILTIN_PACKAGE_NAME, "Resources", "Resources/DefaultGUISkin"),
                        CreateGuiSkinResourcesPackage("Base", "BaseResources", "Resources/DefaultGUISkin")
                    });
                var settings = new ResourceSettings
                {
                    Mode = ResourceMode.Offline,
                    ManifestName = ResourceSettings.MANIFEST_NAME,
                    DefaultPackages = new[] { "Base" }
                };

                await WithDefaultStartupManifest(manifest, async () =>
                {
                    var module = App.Resource;
                    Assert.IsFalse(module.HasPackage("Base"));

                    await module.InitializeAsync(settings);

                    Assert.IsTrue(module.IsInitialized);
                    Assert.AreEqual(ResourceInitializeState.Initialized, module.InitializeState);
                    Assert.IsFalse(module.HasPackage("Base"));

                    await module.PreloadDefaultPackagesAsync();
                    Assert.IsTrue(module.HasPackage("Base"));
                });
            });
        }

        [UnityTest]
        public IEnumerator PreloadDefaultPackagesAsync_WhenDefaultPackageMissingAfterStartup_FailsWithoutResettingResource()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var manifest = CreateManifest(
                    "startup-restore",
                    new[] { CreateGuiSkinResourcesPackage(ResourceConstants.BUILTIN_PACKAGE_NAME, "Resources", "Resources/DefaultGUISkin") });
                var settings = new ResourceSettings
                {
                    Mode = ResourceMode.Offline,
                    ManifestName = ResourceSettings.MANIFEST_NAME,
                    DefaultPackages = new[] { "Missing" }
                };

                await WithDefaultStartupManifest(manifest, async () =>
                {
                    var module = App.Resource;

                    await module.InitializeAsync(settings);

                    Assert.IsTrue(module.IsLocalInitialized);
                    Assert.IsTrue(module.IsInitialized);
                    Assert.AreEqual(ResourceInitializeState.Initialized, module.InitializeState);
                    Assert.AreEqual("startup-restore", module.Manifest.Version);

                    var exception = await ThrowsAsync<GameException>(async () =>
                    {
                        await module.PreloadDefaultPackagesAsync();
                    });
                    StringAssert.Contains("Missing", exception.Message);

                    var handle = await module.LoadAssetAsync("Resources/DefaultGUISkin");
                    Assert.AreEqual(ResourceStatus.Succeeded, handle.Status);
                });
            });
        }

        [Test]
        public void ResourceProviderFactory_WhenProviderIdsDiffer_CreatesMatchingProvider()
        {
            var resourcesProvider = ResourceProviderFactory.Create(
                new BundleInfo { Name = "Resources", ProviderId = ResourceProviderIds.Resources },
                ResourceMode.Offline);
            var offlineBundleProvider = ResourceProviderFactory.Create(
                new BundleInfo { Name = "Base", ProviderId = ResourceProviderIds.AssetBundle },
                ResourceMode.Offline);
            var webBundleProvider = ResourceProviderFactory.Create(
                new BundleInfo { Name = "Hot", ProviderId = ResourceProviderIds.AssetBundle },
                ResourceMode.Web);
            var editorProvider = ResourceProviderFactory.Create(
                new BundleInfo { Name = "Editor", ProviderId = ResourceProviderIds.AssetBundle },
                ResourceMode.EditorSimulator);

            Assert.IsInstanceOf<BuiltinAssetProvider>(resourcesProvider);
            Assert.IsInstanceOf<BundleAssetProvider>(offlineBundleProvider);
            Assert.IsInstanceOf<BundleAssetProvider>(webBundleProvider);
            Assert.AreEqual(ResourceMode.Web, ((BundleAssetProvider)webBundleProvider).Mode);
            Assert.IsInstanceOf<EditorAssetProvider>(editorProvider);
        }

        [Test]
        public void ResourceSettings_WhenServerUrlExists_UsesPublisherRemoteLayout()
        {
            var settings = new ResourceSettings
            {
                ServerUrl = "https://cdn.example.com/root/",
                ChannelName = "qa channel",
            };
            const string version = "1.0 hot";
            var platform = ResolvePlatformSegmentFromAddress(settings.GetPublishAddress(), "qa-channel");
            var expectedRoot = $"https://cdn.example.com/root/qa-channel/{platform}";

            Assert.AreEqual($"{expectedRoot}/publish.json", settings.GetPublishAddress());
            Assert.AreEqual($"{expectedRoot}/1.0-hot/manifest.json", settings.GetManifestAddress(version));
            Assert.AreEqual($"{expectedRoot}/1.0-hot/hash.bundle", settings.GetAssetAddress("hash.bundle", version));
            Assert.AreEqual($"{expectedRoot}/1.0-hot/hash.bundle", settings.GetAssetAddress($"qa-channel/{platform}/1.0-hot/hash.bundle", version));
            Assert.AreEqual($"{expectedRoot}/1.0-hot/hash.bundle", settings.GetAssetAddress($"archive/qa-channel/{platform}/1.0-hot/hash.bundle", version));
            Assert.AreEqual($"{expectedRoot}/1.0-hot/hash.bundle", settings.GetAssetAddress($"{platform}/1.0-hot/hash.bundle", version));
        }

        [Test]
        public void ResourceModuleAddress_UsesSettingsFallback()
        {
            var module = new ResourceModule();
            var settings = new ResourceSettings
            {
                ServerUrl = "https://cdn.example.com/root/",
                ChannelName = "qa-channel"
            };

            Assert.AreEqual(settings.GetPublishAddress(), module.GetPublishAddress(settings));
            Assert.AreEqual(settings.GetManifestAddress("v1"), module.GetManifestAddress(settings, "v1"));
            Assert.AreEqual(settings.GetAssetAddress("bundle", "v1"), module.GetAssetAddress(settings, "bundle", "v1"));
        }

        [Test]
        public void HasPackage_WhenNoProviderInitialized_ReturnsFalse()
        {
            var manifest = new ManifestInfo
            {
                Packages = new List<PackageInfo>
                {
                    new PackageInfo
                    {
                        Name = "LOCAL",
                        Bundles = new List<BundleInfo>
                        {
                            new BundleInfo { Name = "Default", ProviderId = ResourceProviderIds.AssetBundle }
                        }
                    }
                }
            };
            var module = new ResourceModule();
            SetPrivateField(module, "_manifest", manifest);
            SetPrivateField(module, "_setting", new ResourceSettings { Mode = ResourceMode.EditorSimulator });

            Assert.IsFalse(module.HasPackage("LOCAL"));
        }

        [Test]
        public void ManifestMergeUtility_WhenLocalAndHotProvided_MergesWithoutOverwritingBuiltin()
        {
            var localManifest = new ManifestInfo
            {
                Version = "local",
                BuildTime = 1,
                Packages = new List<PackageInfo>
                {
                    new PackageInfo { Name = ResourceConstants.BUILTIN_PACKAGE_NAME },
                    new PackageInfo { Name = "Base" },
                }
            };
            var hotManifest = new ManifestInfo
            {
                Version = "hot",
                BuildTime = 2,
                Packages = new List<PackageInfo>
                {
                    new PackageInfo { Name = ResourceConstants.BUILTIN_PACKAGE_NAME },
                    new PackageInfo { Name = "Hot" },
                }
            };

            var merged = ManifestMergeUtility.Merge(localManifest, hotManifest);

            Assert.AreEqual("hot", merged.Version);
            Assert.AreEqual(2, merged.BuildTime);
            CollectionAssert.AreEqual(
                new[] { ResourceConstants.BUILTIN_PACKAGE_NAME, "Base", "Hot" },
                merged.Packages.ConvertAll(package => package.Name));
            Assert.AreSame(localManifest.Packages[0], merged.Packages[0]);
        }

        [Test]
        public void ManifestMergeUtility_WhenDuplicatePackageBetweenLocalAndHot_Throws()
        {
            var localManifest = new ManifestInfo
            {
                Packages = new List<PackageInfo> { new PackageInfo { Name = "Base" } }
            };
            var hotManifest = new ManifestInfo
            {
                Packages = new List<PackageInfo> { new PackageInfo { Name = "Base" } }
            };

            var exception = Assert.Throws<GameException>(() => ManifestMergeUtility.Merge(localManifest, hotManifest));

            StringAssert.Contains("Duplicate package", exception.Message);
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

        private static ManifestInfo CreateManifest(string version, IEnumerable<PackageInfo> packages)
        {
            return new ManifestInfo
            {
                Version = version,
                BuildTime = 1,
                Packages = new List<PackageInfo>(packages),
            };
        }

        private static PackageInfo CreateGuiSkinResourcesPackage(string packageName, string bundleName, string location)
        {
            return new PackageInfo
            {
                Name = packageName,
                Bundles = new List<BundleInfo>
                {
                    new BundleInfo
                    {
                        Name = bundleName,
                        ProviderId = ResourceProviderIds.Resources,
                        Assets = new List<AssetInfo>
                        {
                            new AssetInfo
                            {
                                Location = location,
                                TypeName = nameof(GUISkin),
                            }
                        }
                    }
                }
            };
        }

        private static async UniTask WithDefaultStartupManifest(ManifestInfo manifest, Func<UniTask> action)
        {
            var manifestPath = Path.Combine(Application.streamingAssetsPath, ResourceSettings.MANIFEST_NAME);
            var directory = Path.GetDirectoryName(manifestPath);
            if (string.IsNullOrWhiteSpace(directory) is false)
            {
                Directory.CreateDirectory(directory);
            }

            var existed = System.IO.File.Exists(manifestPath);
            var original = existed ? System.IO.File.ReadAllText(manifestPath) : null;
            System.IO.File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));
            try
            {
                await action();
            }
            finally
            {
                try
                {
                    await App.Shutdown();
                }
                catch (GameException)
                {
                }

                if (existed)
                {
                    System.IO.File.WriteAllText(manifestPath, original);
                }
                else if (System.IO.File.Exists(manifestPath))
                {
                    System.IO.File.Delete(manifestPath);
                }
            }
        }

        private static async UniTask WithoutDefaultStartupManifest(Func<UniTask> action)
        {
            var manifestPath = Path.Combine(Application.streamingAssetsPath, ResourceSettings.MANIFEST_NAME);
            var existed = System.IO.File.Exists(manifestPath);
            var original = existed ? System.IO.File.ReadAllText(manifestPath) : null;
            if (existed)
            {
                System.IO.File.Delete(manifestPath);
            }

            try
            {
                await action();
            }
            finally
            {
                try
                {
                    await App.Shutdown();
                }
                catch (GameException)
                {
                }

                if (existed)
                {
                    var directory = Path.GetDirectoryName(manifestPath);
                    if (string.IsNullOrWhiteSpace(directory) is false)
                    {
                        Directory.CreateDirectory(directory);
                    }

                    System.IO.File.WriteAllText(manifestPath, original);
                }
            }
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

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (T)method.Invoke(target, args);
        }

        private static string ResolvePlatformSegmentFromAddress(string publishAddress, string channelSegment)
        {
            var marker = "/" + channelSegment + "/";
            var markerIndex = publishAddress.IndexOf(marker, StringComparison.Ordinal);
            Assert.GreaterOrEqual(markerIndex, 0);
            var platformStart = markerIndex + marker.Length;
            var platformEnd = publishAddress.IndexOf('/', platformStart);
            Assert.Greater(platformEnd, platformStart);
            return publishAddress.Substring(platformStart, platformEnd - platformStart);
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
