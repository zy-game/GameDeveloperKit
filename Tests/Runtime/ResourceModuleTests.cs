using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
                var path = WriteTemp("{\"FormatVersion\":1,\"Version\":\"test-version\",\"BuildTime\":1,\"Packages\":[]}");

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
                Assert.IsNotNull(module.ManifestIndexInternal);
                Assert.AreEqual("init-success", module.ManifestIndexInternal.Version);
            });
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator InitializeAsync_EditorSimulator_DoesNotReadConfiguredPlayerManifest()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = App.Resource;
                var settings = new ResourceSettings
                {
                    Mode = ResourceMode.EditorSimulator,
                    ManifestName = Path.Combine(
                        Path.GetTempPath(),
                        "GameDeveloperKit.Tests",
                        Guid.NewGuid().ToString("N"),
                        "missing-manifest.json"),
                    DefaultPackages = Array.Empty<string>()
                };

                await module.InitializeAsync(settings);

                Assert.IsTrue(module.IsInitialized);
                Assert.AreSame(settings, module.Settings);
                Assert.AreEqual(ResourceMode.EditorSimulator, module.Mode);
                Assert.AreEqual("preview", module.ManifestIndexInternal.Version);
            });
        }
#endif

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
                Assert.AreEqual("first", module.ManifestIndexInternal.Version);
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
                Assert.AreEqual("concurrent", module.ManifestIndexInternal.Version);
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
                Assert.IsNull(module.ManifestIndexInternal);

                var retrySettings = CreateSettings(CreateManifestPath("retry", Array.Empty<PackageInfo>()));
                await module.InitializeAsync(retrySettings);

                Assert.IsTrue(module.IsInitialized);
                Assert.AreEqual(ResourceInitializeState.Initialized, module.InitializeState);
                Assert.AreEqual("retry", module.ManifestIndexInternal.Version);
            });
        }

        [UnityTest]
        public IEnumerator InitializeAsync_WhenManifestSemanticValidationFails_ClearsFirstAttemptAndAllowsRetry()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = App.Resource;
                var invalidManifest = new ManifestInfo
                {
                    Version = "invalid",
                    Packages = new List<PackageInfo>
                    {
                        new PackageInfo
                        {
                            Name = "Base",
                            Bundles = new List<BundleInfo>
                            {
                                new BundleInfo
                                {
                                    Name = "invalid.bundle",
                                    ProviderId = string.Empty,
                                    Assets = new List<AssetInfo>()
                                }
                            }
                        }
                    }
                };
                var failedSettings = CreateSettings(WriteManifest(invalidManifest));

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await module.InitializeAsync(failedSettings);
                });

                StringAssert.Contains("ProviderId is required", exception.Message);
                Assert.AreEqual(ResourceInitializeState.Failed, module.InitializeState);
                Assert.IsNull(module.Settings);
                Assert.IsNull(module.ManifestIndexInternal);
                Assert.IsEmpty(module.Providers);

                var retrySettings = CreateSettings(CreateManifestPath("semantic-retry", Array.Empty<PackageInfo>()));
                await module.InitializeAsync(retrySettings);

                Assert.AreEqual(ResourceInitializeState.Initialized, module.InitializeState);
                Assert.AreSame(retrySettings, module.Settings);
                Assert.AreEqual("semantic-retry", module.ManifestIndexInternal.Version);
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
                Assert.IsNull(module.ManifestIndexInternal);
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
                var operation = await module.InitializePackageAsync("Main");
                Assert.AreEqual(OperationStatus.Failed, operation.Status);
                StringAssert.Contains("Main not found", operation.Error.Message);
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
                        CreateGuiSkinResourcesPackage("Base", "BaseResources", "Resources/BaseGUISkin")
                    });

                await WithDefaultStartupManifest(manifest, async () =>
                {
                    var module = App.Resource;

                    Assert.IsTrue(module.IsStartupReady);
                    Assert.IsTrue(module.IsLocalInitialized);
                    Assert.IsFalse(module.IsInitialized);
                    Assert.AreEqual(ResourceInitializeState.LocalInitialized, module.InitializeState);
                    Assert.IsNotNull(module.ManifestIndexInternal);
                    Assert.AreEqual("startup-builtin", module.ManifestIndexInternal.Version);
                    Assert.IsFalse(module.HasPackage("Base"));

                    var handle = await module.LoadAssetAsync("Resources/DefaultGUISkin");

                    Assert.IsNotNull(handle);
                    Assert.AreEqual(ResourceStatus.Succeeded, handle.Status);
                    Assert.IsNotNull(handle.GetAsset<GUISkin>());
                });
            });
        }

        [UnityTest]
        public IEnumerator Startup_WhenManifestSemanticValidationFails_DoesNotCreateProviders()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var invalidManifest = CreateManifest(
                    "startup-invalid",
                    new[]
                    {
                        CreateGuiSkinResourcesPackage(ResourceConstants.BUILTIN_PACKAGE_NAME, "Resources", "Resources/DefaultGUISkin"),
                        new PackageInfo
                        {
                            Name = "Invalid",
                            Bundles = new List<BundleInfo>
                            {
                                new BundleInfo
                                {
                                    Name = "invalid.bundle",
                                    ProviderId = "unknown",
                                    Assets = new List<AssetInfo>()
                                }
                            }
                        }
                    });

                await WithDefaultStartupManifest(invalidManifest, () =>
                {
                    _ = App.File;
                    var module = new ResourceModule();

                    module.Startup();

                    Assert.AreEqual(ResourceInitializeState.NotInitialized, module.InitializeState);
                    Assert.IsNull(module.Settings);
                    Assert.IsNull(module.ManifestIndexInternal);
                    Assert.IsEmpty(module.Providers);
                    var exception = Assert.Throws<GameException>(() =>
                        module.LoadAssetAsync("Resources/DefaultGUISkin").GetAwaiter().GetResult());
                    StringAssert.Contains("startup resource initialization failed", exception.Message);
                    return UniTask.CompletedTask;
                });
            });
        }

        [UnityTest]
        public IEnumerator InitializeAsync_WhenPriorLocalStateExistsAndCandidateIsInvalid_PreservesPriorState()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var startupManifest = CreateManifest(
                    "startup-prior",
                    new[] { CreateGuiSkinResourcesPackage(ResourceConstants.BUILTIN_PACKAGE_NAME, "Resources", "Resources/DefaultGUISkin") });
                var invalidManifest = new ManifestInfo
                {
                    Version = "invalid-candidate",
                    Packages = new List<PackageInfo>
                    {
                        new PackageInfo
                        {
                            Name = "Invalid",
                            Bundles = new List<BundleInfo>
                            {
                                new BundleInfo
                                {
                                    Name = "invalid.bundle",
                                    ProviderId = string.Empty,
                                    Assets = new List<AssetInfo>()
                                }
                            }
                        }
                    }
                };

                await WithDefaultStartupManifest(startupManifest, async () =>
                {
                    var module = App.Resource;
                    var priorSettings = module.Settings;
                    var priorIndex = module.ManifestIndexInternal;
                    var priorMode = module.Mode;
                    var priorProvider = module.Providers.Single();
                    var invalidSettings = CreateSettings(WriteManifest(invalidManifest));

                    var exception = await ThrowsAsync<GameException>(async () =>
                    {
                        await module.InitializeAsync(invalidSettings);
                    });

                    StringAssert.Contains("ProviderId is required", exception.Message);
                    Assert.AreEqual(ResourceInitializeState.LocalInitialized, module.InitializeState);
                    Assert.AreSame(priorSettings, module.Settings);
                    Assert.AreSame(priorIndex, module.ManifestIndexInternal);
                    Assert.AreEqual(priorMode, module.Mode);
                    Assert.AreEqual(1, module.Providers.Count);
                    Assert.AreSame(priorProvider, module.Providers[0]);
                    Assert.AreEqual(1, priorProvider.ReferenceCount);

                    var handle = await module.LoadAssetAsync("Resources/DefaultGUISkin");
                    Assert.AreEqual(ResourceStatus.Succeeded, handle.Status);
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
                        CreateGuiSkinResourcesPackage("Base", "BaseResources", "Resources/BaseGUISkin")
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
                    Assert.AreEqual("startup-restore", module.ManifestIndexInternal.Version);

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
                ResourceMode.Offline,
                "v1",
                false);
            var offlineBundleProvider = ResourceProviderFactory.Create(
                new BundleInfo { Name = "Base", ProviderId = ResourceProviderIds.AssetBundle },
                ResourceMode.Offline,
                "v1",
                false);
            var webBundleProvider = ResourceProviderFactory.Create(
                new BundleInfo { Name = "Hot", ProviderId = ResourceProviderIds.AssetBundle },
                ResourceMode.Web,
                "v1",
                true);
            var editorProvider = ResourceProviderFactory.Create(
                new BundleInfo { Name = "Editor", ProviderId = ResourceProviderIds.AssetBundle },
                ResourceMode.EditorSimulator,
                "v1",
                false);

            Assert.IsInstanceOf<BuiltinAssetProvider>(resourcesProvider);
            Assert.IsInstanceOf<BundleAssetProvider>(offlineBundleProvider);
            Assert.IsInstanceOf<BundleAssetProvider>(webBundleProvider);
            Assert.AreEqual(ResourceMode.Web, ((BundleAssetProvider)webBundleProvider).Mode);
            Assert.IsTrue(((BundleAssetProvider)webBundleProvider).IsRemote);
            Assert.IsInstanceOf<EditorAssetProvider>(editorProvider);
        }

        [Test]
        public void ResourceProviders_DoNotDeclarePerLoadOperationHandles()
        {
            var providerTypes = new[]
            {
                typeof(BuiltinAssetProvider),
                typeof(BundleAssetProvider),
                typeof(EditorAssetProvider)
            };

            foreach (var providerType in providerTypes)
            {
                var loadingHandles = providerType
                    .GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(type => type.Name.StartsWith("Loading", StringComparison.Ordinal) &&
                                   typeof(OperationHandle).IsAssignableFrom(type))
                    .ToArray();

                Assert.IsEmpty(loadingHandles, providerType.FullName);
            }
        }

        [Test]
        public void ResourceModulePublicApi_DoesNotExposeManifestDto()
        {
            var manifestProperty = typeof(ResourceModule).GetProperty(
                "Manifest",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.IsNull(manifestProperty);
        }

        [Test]
        public void ResourceSettings_DoesNotExposeResourceOwnedCachePath()
        {
            Assert.IsNull(typeof(ResourceSettings).GetField("CachePath"));
        }

        [Test]
        public void ResolveBundleFileName_WhenHashExists_UsesNameOnly()
        {
            var bundle = new BundleInfo
            {
                Name = "ui.bundle",
                Hash = "0123456789abcdef"
            };

            Assert.AreEqual("ui.bundle", ProviderBase.ResolveBundleFileName(bundle));
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
        public void ResourceSettings_RemoteManifestName_DoesNotUseLocalManifestPath()
        {
            var settings = new ResourceSettings
            {
                ServerUrl = "https://cdn.example.com/root/",
                ChannelName = "release",
                ManifestName = "D:/local/build/manifest.json"
            };

            var platform = ResolvePlatformSegmentFromAddress(settings.GetPublishAddress(), "release");
            Assert.AreEqual(
                $"https://cdn.example.com/root/release/{platform}/v1/manifest.json",
                settings.GetManifestAddress("v1"));
        }

        [Test]
        public void ResourcePublishProtocol_WhenSignatureAndManifestAreValid_AcceptsCandidate()
        {
            using (var rsa = RSA.Create(2048))
            {
                var manifestBytes = CreateManifestBytes("release-v1");
                var settings = CreateSecureRemoteSettings(rsa, "release-key", 100);
                var pointer = CreateSignedPointer(rsa, manifestBytes, "release-v1", "release-key", 1, 200);

                Assert.DoesNotThrow(() => ResourcePublishProtocol.VerifyPointer(pointer, settings));
                Assert.DoesNotThrow(() => ResourcePublishProtocol.VerifyManifest(
                    pointer,
                    manifestBytes,
                    ResourceManifestReader.Deserialize(manifestBytes)));
            }
        }

        [Test]
        public void ResourcePublishProtocol_WhenSignedFieldIsTampered_RejectsPointer()
        {
            using (var rsa = RSA.Create(2048))
            {
                var manifestBytes = CreateManifestBytes("release-v1");
                var settings = CreateSecureRemoteSettings(rsa, "release-key", 100);
                var pointer = CreateSignedPointer(rsa, manifestBytes, "release-v1", "release-key", 1, 200);
                pointer.Version = "release-v2";

                var exception = Assert.Throws<GameException>(() =>
                    ResourcePublishProtocol.VerifyPointer(pointer, settings));
                StringAssert.Contains("signature verification failed", exception.Message);
            }
        }

        [Test]
        public void ResourcePublishProtocol_WhenManifestIsTampered_RejectsCandidate()
        {
            using (var rsa = RSA.Create(2048))
            {
                var manifestBytes = CreateManifestBytes("release-v1");
                var pointer = CreateSignedPointer(rsa, manifestBytes, "release-v1", "release-key", 1, 200);
                var tamperedBytes = CreateManifestBytes("release-v2");

                var exception = Assert.Throws<GameException>(() => ResourcePublishProtocol.VerifyManifest(
                    pointer,
                    tamperedBytes,
                    ResourceManifestReader.Deserialize(tamperedBytes)));
                StringAssert.Contains("SHA-256", exception.Message);
            }
        }

        [Test]
        public void ResourcePublishProtocol_WhenKeyIdIsUnknown_RejectsPointer()
        {
            using (var rsa = RSA.Create(2048))
            {
                var manifestBytes = CreateManifestBytes("release-v1");
                var settings = CreateSecureRemoteSettings(rsa, "trusted-key", 100);
                var pointer = CreateSignedPointer(rsa, manifestBytes, "release-v1", "other-key", 1, 200);

                var exception = Assert.Throws<GameException>(() =>
                    ResourcePublishProtocol.VerifyPointer(pointer, settings));
                StringAssert.Contains("not trusted", exception.Message);
            }
        }

        [Test]
        public void ResourcePublishProtocol_WhenClientBuildIsOutsideRange_RejectsPointer()
        {
            using (var rsa = RSA.Create(2048))
            {
                var manifestBytes = CreateManifestBytes("release-v1");
                var settings = CreateSecureRemoteSettings(rsa, "release-key", 201);
                var pointer = CreateSignedPointer(rsa, manifestBytes, "release-v1", "release-key", 1, 200);

                var exception = Assert.Throws<GameException>(() =>
                    ResourcePublishProtocol.VerifyPointer(pointer, settings));
                StringAssert.Contains("outside resource range", exception.Message);
            }
        }

        [Test]
        public void ResourceManifestReader_WhenRemoteTransportIsHttp_RejectsLocation()
        {
            var exception = Assert.Throws<GameException>(() =>
                ResourceManifestReader.ReadBytesAsync("http://cdn.example.com/manifest.json").GetAwaiter().GetResult());
            StringAssert.Contains("must use HTTPS", exception.Message);
        }

        [Test]
        public void ResourceManifestReader_WhenFormatVersionIsMissing_RejectsManifest()
        {
            var bytes = Encoding.UTF8.GetBytes("{\"Version\":\"v1\",\"BuildTime\":1,\"Packages\":[]}");
            Assert.Throws<GameException>(() => ResourceManifestReader.Deserialize(bytes));
        }

        [Test]
        public void ResourcePublishProtocol_WhenFormatVersionIsUnsupported_RejectsManifest()
        {
            var manifestBytes = Encoding.UTF8.GetBytes(
                "{\"FormatVersion\":2,\"Version\":\"v1\",\"BuildTime\":1,\"Packages\":[]}");
            var pointer = new ResourcePublishPointer
            {
                Version = "v1",
                ManifestSha256 = ResourcePublishProtocol.ComputeSha256(manifestBytes)
            };

            var exception = Assert.Throws<GameException>(() => ResourcePublishProtocol.VerifyManifest(
                pointer,
                manifestBytes,
                ResourceManifestReader.Deserialize(manifestBytes)));
            StringAssert.Contains("not supported", exception.Message);
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
            SetPrivateField(module, "_manifestIndex", ResourceManifestValidator.ValidateAndIndex(manifest, ResourceMode.Offline));
            SetPrivateField(module, "_setting", new ResourceSettings { Mode = ResourceMode.EditorSimulator });

            Assert.IsFalse(module.HasPackage("LOCAL"));
        }

        [UnityTest]
        public IEnumerator ResourceQueries_WhenLocationCollidesWithLabel_RouteThroughDistinctIndexes()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var locationAsset = new AssetInfo
                {
                    Location = "ui",
                    TypeName = nameof(GUISkin),
                    Labels = new List<string>()
                };
                var labelAsset = new AssetInfo
                {
                    Location = "ui/button",
                    TypeName = nameof(Texture2D),
                    Labels = new List<string> { "ui" }
                };
                var manifest = new ManifestInfo
                {
                    Packages = new List<PackageInfo>
                    {
                        new PackageInfo
                        {
                            Name = "Base",
                            Bundles = new List<BundleInfo>
                            {
                                new BundleInfo
                                {
                                    Name = "location.bundle",
                                    ProviderId = ResourceProviderIds.AssetBundle,
                                    Assets = new List<AssetInfo> { locationAsset }
                                },
                                new BundleInfo
                                {
                                    Name = "label.bundle",
                                    ProviderId = ResourceProviderIds.AssetBundle,
                                    Assets = new List<AssetInfo> { labelAsset }
                                }
                            }
                        }
                    }
                };
                var module = new ResourceModule();
                var locationProvider = new TestAssetProvider(manifest.Packages[0].Bundles[0]);
                var labelProvider = new TestAssetProvider(manifest.Packages[0].Bundles[1]);
                SetPrivateField(module, "_manifestIndex", ResourceManifestValidator.ValidateAndIndex(manifest, ResourceMode.Offline));
                SetPrivateField(module, "_setting", new ResourceSettings { Mode = ResourceMode.Offline });
                SetPrivateField(module, "_initializeState", ResourceInitializeState.LocalInitialized);
                module.Providers.Add(locationProvider);
                module.Providers.Add(labelProvider);

                var exact = await module.LoadAssetAsync("ui");
                var byLabel = await module.LoadAssetsByLabelAsync("ui");
                var byType = await module.LoadAssetsByTypeAsync<Texture2D>();

                Assert.AreEqual("ui", exact.Info.Location);
                CollectionAssert.AreEqual(new[] { "ui/button" }, byLabel.Select(handle => handle.Info.Location).ToArray());
                CollectionAssert.AreEqual(new[] { "ui/button" }, byType.Select(handle => handle.Info.Location).ToArray());
                Assert.AreEqual(1, locationProvider.LoadCount);
                Assert.AreEqual(1, labelProvider.LoadCount);
                Assert.IsTrue(locationProvider.HasAsset("ui"));
                Assert.IsFalse(labelProvider.HasAsset("ui"));
            });
        }

        [UnityTest]
        public IEnumerator LoadAssetsByLabelAsync_WhenLaterProviderFails_ReleasesEntireBatch()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var firstAsset = new AssetInfo
                {
                    Location = "batch/first",
                    TypeName = nameof(Texture2D),
                    Labels = new List<string> { "batch" }
                };
                var secondAsset = new AssetInfo
                {
                    Location = "batch/second",
                    TypeName = nameof(Texture2D),
                    Labels = new List<string> { "batch" }
                };
                var failingAsset = new AssetInfo
                {
                    Location = "batch/failing",
                    TypeName = nameof(Texture2D),
                    Labels = new List<string> { "batch" }
                };
                var firstBundle = new BundleInfo
                {
                    Name = "batch.first",
                    ProviderId = ResourceProviderIds.AssetBundle,
                    Assets = new List<AssetInfo> { firstAsset }
                };
                var secondBundle = new BundleInfo
                {
                    Name = "batch.second",
                    ProviderId = ResourceProviderIds.AssetBundle,
                    Assets = new List<AssetInfo> { secondAsset, failingAsset }
                };
                var manifest = new ManifestInfo
                {
                    Packages = new List<PackageInfo>
                    {
                        new PackageInfo
                        {
                            Name = "Batch",
                            Bundles = new List<BundleInfo> { firstBundle, secondBundle }
                        }
                    }
                };
                var module = new ResourceModule();
                var firstProvider = new BatchTestProvider(firstBundle);
                var secondProvider = new BatchTestProvider(secondBundle, failingAsset.Location);
                SetPrivateField(module, "_manifestIndex", ResourceManifestValidator.ValidateAndIndex(manifest, ResourceMode.Offline));
                SetPrivateField(module, "_setting", new ResourceSettings { Mode = ResourceMode.Offline });
                SetPrivateField(module, "_initializeState", ResourceInitializeState.LocalInitialized);
                module.Providers.Add(firstProvider);
                module.Providers.Add(secondProvider);

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await module.LoadAssetsByLabelAsync("batch");
                });

                StringAssert.Contains(failingAsset.Location, exception.Message);
                Assert.AreEqual(0, firstProvider.SuccessfulHandles.Single().ReferenceCount);
                Assert.AreEqual(0, secondProvider.SuccessfulHandles.Single().ReferenceCount);

                await firstProvider.UnloadUnusedAssetAsync();
                await secondProvider.UnloadUnusedAssetAsync();
                Assert.AreEqual(ResourceStatus.Released, firstProvider.SuccessfulHandles.Single().Status);
                Assert.AreEqual(ResourceStatus.Released, secondProvider.SuccessfulHandles.Single().Status);
            });
        }

        [UnityTest]
        public IEnumerator LoadAssetsByLabelAsync_WhenConcurrencyIsTwo_PreservesOrderAndBoundsActiveLoads()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var assets = Enumerable.Range(0, 6)
                    .Select(index => new AssetInfo
                    {
                        Location = $"batch/concurrent-{index}",
                        TypeName = nameof(Texture2D),
                        Labels = new List<string> { "concurrent" }
                    })
                    .ToList();
                var bundle = new BundleInfo
                {
                    Name = "batch.concurrent",
                    ProviderId = ResourceProviderIds.AssetBundle,
                    Assets = assets
                };
                var manifest = new ManifestInfo
                {
                    Packages = new List<PackageInfo>
                    {
                        new PackageInfo
                        {
                            Name = "ConcurrentBatch",
                            Bundles = new List<BundleInfo> { bundle }
                        }
                    }
                };
                var probe = new BatchConcurrencyProbe();
                var provider = new DelayedBatchTestProvider(bundle, probe);
                var module = new ResourceModule();
                SetPrivateField(module, "_manifestIndex", ResourceManifestValidator.ValidateAndIndex(manifest, ResourceMode.Offline));
                SetPrivateField(module, "_setting", new ResourceSettings
                {
                    Mode = ResourceMode.Offline,
                    MaxConcurrentBatchLoads = 2
                });
                SetPrivateField(module, "_initializeState", ResourceInitializeState.LocalInitialized);
                module.Providers.Add(provider);

                var handles = await module.LoadAssetsByLabelAsync("concurrent");

                CollectionAssert.AreEqual(
                    assets.Select(asset => asset.Location).ToArray(),
                    handles.Select(handle => handle.Info.Location).ToArray());
                Assert.AreEqual(2, probe.MaxActive);
                Assert.AreEqual(6, probe.Completed);
                foreach (var handle in handles)
                {
                    handle.Release();
                }

                await provider.UnloadUnusedAssetAsync();
            });
        }

        [TestCase(0)]
        [TestCase(ResourceSettings.MAX_CONCURRENT_BATCH_LOADS_LIMIT + 1)]
        public void ResourceSettings_WhenBatchConcurrencyIsOutsideLimit_RejectsValue(int value)
        {
            var exception = Assert.Throws<GameException>(() =>
                ResourceSettings.ValidateBatchLoadConcurrency(value));
            StringAssert.Contains("MaxConcurrentBatchLoads", exception.Message);
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

        private string WriteManifest(ManifestInfo manifest)
        {
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

        private static byte[] CreateManifestBytes(string version)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new ManifestInfo
            {
                FormatVersion = ManifestInfo.CurrentFormatVersion,
                Version = version,
                BuildTime = 1,
                Packages = new List<PackageInfo>()
            }));
        }

        private static ResourceSettings CreateSecureRemoteSettings(RSA rsa, string keyId, long clientBuild)
        {
            var publicKey = rsa.ExportParameters(false);
            return new ResourceSettings
            {
                Mode = ResourceMode.Online,
                ServerUrl = "https://cdn.example.com",
                ChannelName = "release",
                ClientBuild = clientBuild,
                TrustedKeys = new[]
                {
                    new ResourceTrustKey
                    {
                        KeyId = keyId,
                        Modulus = Convert.ToBase64String(publicKey.Modulus),
                        Exponent = Convert.ToBase64String(publicKey.Exponent)
                    }
                }
            };
        }

        private static ResourcePublishPointer CreateSignedPointer(
            RSA rsa,
            byte[] manifestBytes,
            string version,
            string keyId,
            long minimumClientBuild,
            long maximumClientBuild)
        {
            var pointer = new ResourcePublishPointer
            {
                ProtocolVersion = ResourcePublishProtocol.CurrentProtocolVersion,
                Channel = "release",
                Platform = ResourceSettings.ResolvePlatformSegment(),
                Version = version,
                ManifestSha256 = ResourcePublishProtocol.ComputeSha256(manifestBytes),
                MinimumClientBuild = minimumClientBuild,
                MaximumClientBuild = maximumClientBuild,
                KeyId = keyId
            };
            pointer.Signature = Convert.ToBase64String(rsa.SignData(
                ResourcePublishProtocol.BuildSigningPayload(pointer),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1));
            return pointer;
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

        private sealed class BatchTestProvider : ProviderBase
        {
            private readonly string m_FailingLocation;

            public BatchTestProvider(BundleInfo info, string failingLocation = null) : base(info)
            {
                m_FailingLocation = failingLocation;
            }

            public List<AssetHandle> SuccessfulHandles { get; } = new List<AssetHandle>();

            public override UniTask<OperationHandle<BundleHandle>> InitializeProviderAsync()
            {
                return UniTask.FromResult<OperationHandle<BundleHandle>>(
                    new TestBundleOperationHandle(BundleHandle.Success(Info, null)));
            }

            public override UniTask<OperationHandle> UninitializeProviderAsync()
            {
                return UniTask.FromResult<OperationHandle>(new TestOperationHandle());
            }

            protected override UniTask<AssetHandle> LoadAssetInternalAsync(AssetInfo asset)
            {
                if (string.Equals(asset.Location, m_FailingLocation, StringComparison.Ordinal))
                {
                    return UniTask.FromResult(AssetHandle.Failure(new InvalidOperationException("batch item failed")));
                }

                var handle = AssetHandle.Success(asset, null);
                SuccessfulHandles.Add(handle);
                return UniTask.FromResult(handle);
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

        private sealed class BatchConcurrencyProbe
        {
            public int Active;
            public int MaxActive;
            public int Completed;
        }

        private sealed class DelayedBatchTestProvider : ProviderBase
        {
            private readonly BatchConcurrencyProbe m_Probe;

            public DelayedBatchTestProvider(BundleInfo info, BatchConcurrencyProbe probe) : base(info)
            {
                m_Probe = probe;
            }

            public override UniTask<OperationHandle<BundleHandle>> InitializeProviderAsync()
            {
                return UniTask.FromResult<OperationHandle<BundleHandle>>(
                    new TestBundleOperationHandle(BundleHandle.Success(Info, null)));
            }

            public override UniTask<OperationHandle> UninitializeProviderAsync()
            {
                return UniTask.FromResult<OperationHandle>(new TestOperationHandle());
            }

            protected override async UniTask<AssetHandle> LoadAssetInternalAsync(AssetInfo asset)
            {
                m_Probe.Active++;
                m_Probe.MaxActive = Math.Max(m_Probe.MaxActive, m_Probe.Active);
                try
                {
                    await UniTask.DelayFrame(2);
                    m_Probe.Completed++;
                    return AssetHandle.Success(asset, null);
                }
                finally
                {
                    m_Probe.Active--;
                }
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
