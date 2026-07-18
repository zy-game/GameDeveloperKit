using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using GameDeveloperKit.Resource;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class ResourcePackageSessionTests : RuntimeTestBase
    {
        [UnityTearDown]
        public IEnumerator TearDown()
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
            });
        }

        [UnityTest]
        public IEnumerator PackageSessions_TrackSharedDependenciesAndRepeatedReferences()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var index = ResourceManifestValidator.ValidateAndIndex(CreateSharedManifest(), ResourceMode.Offline);
                var providers = new List<ProviderBase>();
                var sessions = new Dictionary<string, PackageSession>(StringComparer.Ordinal);

                await ResourceModule.InitializePackageOperationHandle.InitializeAsync(
                    "A",
                    index,
                    providers,
                    ResourceMode.Offline,
                    sessions);

                Assert.IsTrue(sessions.ContainsKey("A"));
                Assert.IsFalse(sessions.ContainsKey("B"));
                CollectionAssert.AreEqual(new[] { "B.Core", "A.Main" }, providers.Select(provider => provider.Info.Name));
                Assert.AreEqual(1, FindProvider(providers, "B.Core").ReferenceCount);

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await ResourceModule.UninitializePackageOperationHandle.UninitializeAsync("B", providers, sessions);
                });
                StringAssert.Contains("Package not initialized", exception.Message);
                Assert.AreEqual(1, FindProvider(providers, "B.Core").ReferenceCount);

                await ResourceModule.InitializePackageOperationHandle.InitializeAsync(
                    "B",
                    index,
                    providers,
                    ResourceMode.Offline,
                    sessions);

                Assert.AreEqual(2, FindProvider(providers, "B.Core").ReferenceCount);
                Assert.AreEqual(1, FindProvider(providers, "B.Extra").ReferenceCount);
                Assert.AreEqual(1, sessions["B"].ReferenceCount);

                await ResourceModule.InitializePackageOperationHandle.InitializeAsync(
                    "B",
                    index,
                    providers,
                    ResourceMode.Offline,
                    sessions);

                Assert.AreEqual(2, sessions["B"].ReferenceCount);
                Assert.AreEqual(2, FindProvider(providers, "B.Core").ReferenceCount);
                Assert.AreEqual(1, FindProvider(providers, "B.Extra").ReferenceCount);

                await ResourceModule.UninitializePackageOperationHandle.UninitializeAsync("B", providers, sessions);

                Assert.AreEqual(1, sessions["B"].ReferenceCount);
                Assert.AreEqual(2, FindProvider(providers, "B.Core").ReferenceCount);

                await ResourceModule.UninitializePackageOperationHandle.UninitializeAsync("B", providers, sessions);

                Assert.IsFalse(sessions.ContainsKey("B"));
                Assert.AreEqual(1, FindProvider(providers, "B.Core").ReferenceCount);
                Assert.IsFalse(providers.Any(provider => provider.Info.Name == "B.Extra"));

                await ResourceModule.UninitializePackageOperationHandle.UninitializeAsync("A", providers, sessions);

                Assert.IsEmpty(sessions);
                Assert.IsEmpty(providers);
            });
        }

        [UnityTest]
        public IEnumerator InitializePackage_WhenFactoryThrows_RollsBackRetainedProviders()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var sharedInfo = CreateBundle("Shared", ResourceProviderIds.Resources);
                var existingProvider = new BuiltinAssetProvider(sharedInfo);
                await existingProvider.InitializeProviderAsync();
                var providers = new List<ProviderBase> { existingProvider };
                var sessions = new Dictionary<string, PackageSession>(StringComparer.Ordinal);
                var manifest = new ManifestInfo
                {
                    Version = "rollback",
                    Packages = new List<PackageInfo>
                    {
                        new PackageInfo
                        {
                            Name = "SharedPackage",
                            Bundles = new List<BundleInfo> { sharedInfo }
                        },
                        new PackageInfo
                        {
                            Name = "Target",
                            Bundles = new List<BundleInfo>
                            {
                                new BundleInfo
                                {
                                    Name = "Invalid",
                                    ProviderId = "unsupported",
                                    Dependencies = new List<string> { "Shared" },
                                    Assets = new List<AssetInfo>()
                                }
                            }
                        }
                    }
                };
                var index = new ResourceManifestIndex(manifest);

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await ResourceModule.InitializePackageOperationHandle.InitializeAsync(
                        "Target",
                        index,
                        providers,
                        ResourceMode.Offline,
                        sessions);
                });

                StringAssert.Contains("Unsupported resource provider", exception.Message);
                Assert.IsEmpty(sessions);
                Assert.AreEqual(1, existingProvider.ReferenceCount);
                Assert.AreEqual(1, providers.Count);
                Assert.AreSame(existingProvider, providers[0]);
                existingProvider.Release();
            });
        }

        [UnityTest]
        public IEnumerator PackageLifecycle_WhenUninitializeIsPending_SerializesReinitialize()
        {
            return UniTask.ToCoroutine(async () =>
            {
                _ = App.Operation;
                var bundle = CreateBundle("Main.Bundle", ResourceProviderIds.Resources);
                bundle.Assets.Add(new AssetInfo { Location = "main.asset" });
                var index = new ResourceManifestIndex(new ManifestInfo
                {
                    Version = "serialized",
                    Packages = new List<PackageInfo>
                    {
                        new PackageInfo
                        {
                            Name = "Main",
                            Bundles = new List<BundleInfo> { bundle }
                        }
                    }
                });
                var module = CreateReadyModule(index);
                var delayedProvider = new DelayedUninitializeProvider(bundle);
                module.Providers.Add(delayedProvider);
                module.PackageSessions.Add("Main", new PackageSession("Main", new[] { delayedProvider }));

                var uninitializeTask = module.UninitializePackageAsync("Main");
                var initializeTask = module.InitializePackageAsync("Main");
                var rejectedLoad = await delayedProvider.LoadAssetAsync("main.asset");

                Assert.AreEqual(UniTaskStatus.Pending, uninitializeTask.Status);
                Assert.AreEqual(UniTaskStatus.Pending, initializeTask.Status);
                Assert.AreEqual(ResourceStatus.Failed, rejectedLoad.Status);
                StringAssert.Contains("not owned", rejectedLoad.Error.Message);
                Assert.AreEqual(0, delayedProvider.LoadCount);

                delayedProvider.CompleteUninitialize();
                var uninitialize = await uninitializeTask;
                var initialize = await initializeTask;

                Assert.AreEqual(OperationStatus.Succeeded, uninitialize.Status);
                Assert.AreEqual(OperationStatus.Succeeded, initialize.Status);
                Assert.IsTrue(module.HasPackage("Main"));
                Assert.AreEqual(1, module.Providers.Count);
                Assert.AreNotSame(delayedProvider, module.Providers[0]);
                Assert.AreEqual(1, module.PackageSessions["Main"].ReferenceCount);

                rejectedLoad.Release();
                var cleanup = await module.UninitializePackageAsync("Main");
                Assert.AreEqual(OperationStatus.Succeeded, cleanup.Status);
                Assert.IsEmpty(module.Providers);
            });
        }

        private static ManifestInfo CreateSharedManifest()
        {
            var core = CreateBundle("B.Core", ResourceProviderIds.Resources);
            var extra = CreateBundle("B.Extra", ResourceProviderIds.Resources);
            var main = CreateBundle("A.Main", ResourceProviderIds.Resources);
            main.Dependencies.Add("B.Core");
            return new ManifestInfo
            {
                Version = "shared",
                Packages = new List<PackageInfo>
                {
                    new PackageInfo
                    {
                        Name = "A",
                        Bundles = new List<BundleInfo> { main }
                    },
                    new PackageInfo
                    {
                        Name = "B",
                        Bundles = new List<BundleInfo> { core, extra }
                    }
                }
            };
        }

        private static BundleInfo CreateBundle(string name, string providerId)
        {
            return new BundleInfo
            {
                Name = name,
                ProviderId = providerId,
                Dependencies = new List<string>(),
                Assets = new List<AssetInfo>()
            };
        }

        private static ProviderBase FindProvider(IEnumerable<ProviderBase> providers, string name)
        {
            return providers.Single(provider => string.Equals(provider.Info.Name, name, StringComparison.Ordinal));
        }

        private static ResourceModule CreateReadyModule(ResourceManifestIndex index)
        {
            var module = new ResourceModule();
            SetPrivateField(module, "_manifestIndex", index);
            SetPrivateField(module, "_setting", new ResourceSettings
            {
                Mode = ResourceMode.Offline,
                DefaultPackages = Array.Empty<string>()
            });
            SetPrivateField(module, "_mode", ResourceMode.Offline);
            SetPrivateField(module, "_initializeState", ResourceInitializeState.Initialized);
            return module;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(target, value);
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

        private sealed class DelayedUninitializeProvider : ProviderBase
        {
            private readonly UniTaskCompletionSource m_Uninitialize = new UniTaskCompletionSource();

            public int LoadCount { get; private set; }

            public DelayedUninitializeProvider(BundleInfo info) : base(info)
            {
                Status = ResourceStatus.Succeeded;
            }

            public override UniTask<OperationHandle<BundleHandle>> InitializeProviderAsync()
            {
                return UniTask.FromResult<OperationHandle<BundleHandle>>(
                    new CompletedBundleOperation(BundleHandle.Success(Info, null)));
            }

            public override async UniTask<OperationHandle> UninitializeProviderAsync()
            {
                await m_Uninitialize.Task;
                return new CompletedOperation();
            }

            public void CompleteUninitialize()
            {
                m_Uninitialize.TrySetResult();
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

        private sealed class CompletedOperation : OperationHandle
        {
            public CompletedOperation()
            {
                SetResult();
            }

            public override void Execute(params object[] args)
            {
            }
        }

        private sealed class CompletedBundleOperation : OperationHandle<BundleHandle>
        {
            public CompletedBundleOperation(BundleHandle bundle)
            {
                SetResult(bundle);
            }

            public override void Execute(params object[] args)
            {
            }
        }
    }
}
