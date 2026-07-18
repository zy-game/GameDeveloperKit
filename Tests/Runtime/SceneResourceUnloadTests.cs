using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using GameDeveloperKit.Resource;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class SceneResourceUnloadTests : RuntimeTestBase
    {
        private const string SceneName = "SampleScene";
        private const string SceneLocation = "scenes/sample";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            return UniTask.ToCoroutine(CleanupSceneAsync);
        }

        [UnityTest]
        public IEnumerator UnloadSceneAsset_WhenLastReferenceReleased_WaitsForUnityUnload()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var provider = CreateProvider();
                var handle = await provider.LoadSceneAssetAsync(SceneLocation);

                Assert.IsTrue(handle.Asset.isLoaded);

                await provider.UnloadSceneAsset(handle);

                Assert.IsFalse(SceneManager.GetSceneByName(SceneName).isLoaded);
                Assert.AreEqual(ResourceStatus.Released, handle.Status);
                Assert.IsFalse(provider.HasLoadedAssets);
                provider.Release();
            });
        }

        [UnityTest]
        public IEnumerator ResourceModuleUnloadSceneAsset_WhenLastReferenceReleased_WaitsForUnityUnload()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var provider = CreateProvider();
                var handle = await provider.LoadSceneAssetAsync(SceneLocation);
                var module = CreateReadyModule(provider);

                await module.UnloadSceneAsset(handle);

                Assert.IsFalse(SceneManager.GetSceneByName(SceneName).isLoaded);
                Assert.AreEqual(ResourceStatus.Released, handle.Status);
                Assert.IsFalse(provider.HasLoadedAssets);
                module.Shutdown();
            });
        }

        [UnityTest]
        public IEnumerator UnloadSceneAsset_WhenReferencesRemain_KeepsSceneLoadedUntilLastRelease()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var provider = CreateProvider();
                var firstTask = provider.LoadSceneAssetAsync(SceneLocation);
                var secondTask = provider.LoadSceneAssetAsync(SceneLocation);
                var first = await firstTask;
                var second = await secondTask;

                Assert.AreSame(first, second);
                Assert.AreEqual(2, first.ReferenceCount);

                await provider.UnloadSceneAsset(first);

                Assert.IsTrue(first.Asset.isLoaded);
                Assert.AreEqual(1, first.ReferenceCount);

                await provider.UnloadSceneAsset(second);

                Assert.IsFalse(SceneManager.GetSceneByName(SceneName).isLoaded);
                Assert.AreEqual(ResourceStatus.Released, first.Status);
                provider.Release();
            });
        }

        [UnityTest]
        public IEnumerator UnloadSceneAsset_WhenCalledConcurrently_SharesOneUnloadTerminal()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var provider = CreateProvider();
                var handle = await provider.LoadSceneAssetAsync(SceneLocation);

                var first = provider.UnloadSceneAsset(handle);
                var second = provider.UnloadSceneAsset(handle);
                await UniTask.WhenAll(first, second);

                Assert.AreEqual(ResourceStatus.Released, handle.Status);
                Assert.IsFalse(SceneManager.GetSceneByName(SceneName).isLoaded);
                provider.Release();
            });
        }

        [UnityTest]
        public IEnumerator LoadSceneAssetAsync_WhenSameSceneIsUnloading_WaitsThenCreatesNewHandle()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var provider = CreateProvider();
                var original = await provider.LoadSceneAssetAsync(SceneLocation);

                var unload = provider.UnloadSceneAsset(original);
                var reload = provider.LoadSceneAssetAsync(SceneLocation);
                await unload;
                var reloaded = await reload;

                Assert.AreNotSame(original, reloaded);
                Assert.AreEqual(ResourceStatus.Released, original.Status);
                Assert.AreEqual(ResourceStatus.Succeeded, reloaded.Status);
                Assert.IsTrue(reloaded.Asset.isLoaded);
                Assert.AreEqual(2, provider.LoadCount);

                await provider.UnloadSceneAsset(reloaded);
                provider.Release();
            });
        }

        [UnityTest]
        public IEnumerator UnloadSceneAsset_WhenSceneIsActive_FailsWithoutReleasingAndCanRetry()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var provider = CreateProvider();
                var originalActiveScene = SceneManager.GetActiveScene();
                var handle = await provider.LoadSceneAssetAsync(SceneLocation);
                Assert.IsTrue(SceneManager.SetActiveScene(handle.Asset));

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await provider.UnloadSceneAsset(handle);
                });

                StringAssert.Contains("Cannot unload active scene", exception.Message);
                Assert.IsTrue(handle.Asset.isLoaded);
                Assert.AreEqual(ResourceStatus.Succeeded, handle.Status);
                Assert.IsTrue(provider.HasLoadedAssets);

                Assert.IsTrue(SceneManager.SetActiveScene(originalActiveScene));
                await provider.UnloadSceneAsset(handle);

                Assert.AreEqual(ResourceStatus.Released, handle.Status);
                Assert.IsFalse(SceneManager.GetSceneByName(SceneName).isLoaded);
                provider.Release();
            });
        }

        [UnityTest]
        public IEnumerator UnloadUnusedAssetAsync_WhenSceneHandleReleased_DrainsUnityScene()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var provider = CreateProvider();
                var handle = await provider.LoadSceneAssetAsync(SceneLocation);

                handle.Release();
                Assert.IsTrue(handle.Asset.isLoaded);

                await provider.UnloadUnusedAssetAsync();

                Assert.AreEqual(ResourceStatus.Released, handle.Status);
                Assert.IsFalse(SceneManager.GetSceneByName(SceneName).isLoaded);
                provider.Release();
            });
        }

        [UnityTest]
        public IEnumerator Release_WhenSceneIsLoaded_ThrowsUntilAsyncUnloadCompletes()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var provider = CreateProvider();
                var handle = await provider.LoadSceneAssetAsync(SceneLocation);

                var exception = Assert.Throws<GameException>(() => provider.Release());
                StringAssert.Contains("Await scene unload first", exception.Message);
                Assert.IsTrue(handle.Asset.isLoaded);
                Assert.AreEqual(ResourceStatus.Succeeded, handle.Status);

                await provider.UnloadSceneAsset(handle);
                Assert.DoesNotThrow(() => provider.Release());
            });
        }

        [UnityTest]
        public IEnumerator ResourceModuleUninitializeAsync_WhenProviderOwnsScene_DrainsBeforeRelease()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = new ResourceModule();
                var provider = CreateProvider();
                var handle = await provider.LoadSceneAssetAsync(SceneLocation);
                module.Providers.Add(provider);

                await module.UninitializeAsync();

                Assert.AreEqual(ResourceStatus.Released, handle.Status);
                Assert.AreEqual(0, provider.ReferenceCount);
                Assert.IsFalse(SceneManager.GetSceneByName(SceneName).isLoaded);
                Assert.IsEmpty(module.Providers);
            });
        }

        [UnityTest]
        public IEnumerator ResourceModuleUninitializeAsync_WhenSceneIsActive_PreservesProviderUntilRetry()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = new ResourceModule();
                var provider = CreateProvider();
                var originalActiveScene = SceneManager.GetActiveScene();
                var handle = await provider.LoadSceneAssetAsync(SceneLocation);
                module.Providers.Add(provider);
                Assert.IsTrue(SceneManager.SetActiveScene(handle.Asset));

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await module.UninitializeAsync();
                });

                StringAssert.Contains("Cannot unload active scene", exception.Message);
                Assert.AreEqual(1, module.Providers.Count);
                Assert.AreSame(provider, module.Providers[0]);
                Assert.AreEqual(1, provider.ReferenceCount);
                Assert.IsTrue(handle.Asset.isLoaded);

                var retained = await provider.LoadSceneAssetAsync(SceneLocation);
                Assert.AreSame(handle, retained);
                Assert.AreEqual(2, handle.ReferenceCount);
                await provider.UnloadSceneAsset(retained);

                Assert.IsTrue(SceneManager.SetActiveScene(originalActiveScene));
                await module.UninitializeAsync();

                Assert.IsEmpty(module.Providers);
                Assert.AreEqual(ResourceStatus.Released, handle.Status);
            });
        }

        [UnityTest]
        public IEnumerator ApplyManifestIndexAsync_WhenPriorProviderOwnsScene_DrainsBeforeCommit()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = new ResourceModule();
                var provider = CreateProvider();
                var handle = await provider.LoadSceneAssetAsync(SceneLocation);
                module.Providers.Add(provider);
                var settings = new ResourceSettings { Mode = ResourceMode.Offline };
                var index = ResourceManifestValidator.ValidateAndIndex(
                    new ManifestInfo { Packages = new List<PackageInfo>() },
                    ResourceMode.Offline);

                await InvokeApplyManifestIndexAsync(module, settings, index);

                Assert.AreEqual(ResourceStatus.Released, handle.Status);
                Assert.AreEqual(0, provider.ReferenceCount);
                Assert.IsFalse(SceneManager.GetSceneByName(SceneName).isLoaded);
                Assert.AreSame(settings, module.Settings);
                Assert.AreSame(index, module.ManifestIndexInternal);
                Assert.IsEmpty(module.Providers);
            });
        }

        private static SceneTestProvider CreateProvider()
        {
            return new SceneTestProvider(new BundleInfo
            {
                Name = "scene.bundle",
                ProviderId = ResourceProviderIds.AssetBundle,
                Assets = new List<AssetInfo>
                {
                    new AssetInfo { Location = SceneLocation }
                }
            });
        }

        private static ResourceModule CreateReadyModule(SceneTestProvider provider)
        {
            var manifest = new ManifestInfo
            {
                Packages = new List<PackageInfo>
                {
                    new PackageInfo
                    {
                        Name = "ScenePackage",
                        Bundles = new List<BundleInfo> { provider.Info }
                    }
                }
            };
            var module = new ResourceModule();
            SetPrivateField(
                module,
                "_manifestIndex",
                ResourceManifestValidator.ValidateAndIndex(manifest, ResourceMode.Offline));
            SetPrivateField(module, "_setting", new ResourceSettings { Mode = ResourceMode.Offline });
            SetPrivateField(module, "_initializeState", ResourceInitializeState.LocalInitialized);
            module.Providers.Add(provider);
            return module;
        }

        private static async UniTask CleanupSceneAsync()
        {
            var scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() is false || scene.isLoaded is false)
            {
                return;
            }

            if (scene == SceneManager.GetActiveScene())
            {
                for (var index = 0; index < SceneManager.sceneCount; index++)
                {
                    var candidate = SceneManager.GetSceneAt(index);
                    if (candidate.IsValid() && candidate.isLoaded && candidate != scene)
                    {
                        SceneManager.SetActiveScene(candidate);
                        break;
                    }
                }
            }

            var operation = SceneManager.UnloadSceneAsync(scene);
            if (operation != null)
            {
                await operation;
            }
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

        private static UniTask InvokeApplyManifestIndexAsync(
            ResourceModule module,
            ResourceSettings settings,
            ResourceManifestIndex index)
        {
            var method = typeof(ResourceModule).GetMethod(
                "ApplyManifestIndexAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (UniTask)method.Invoke(module, new object[] { settings, index });
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }

        private sealed class SceneTestProvider : ProviderBase
        {
            public SceneTestProvider(BundleInfo info) : base(info)
            {
            }

            public int LoadCount { get; private set; }

            public override UniTask<OperationHandle<BundleHandle>> InitializeProviderAsync()
            {
                return UniTask.FromResult<OperationHandle<BundleHandle>>(
                    new CompletedBundleOperationHandle(BundleHandle.Success(Info, null)));
            }

            public override UniTask<OperationHandle> UninitializeProviderAsync()
            {
                return UniTask.FromResult<OperationHandle>(new CompletedOperationHandle());
            }

            protected override UniTask<AssetHandle> LoadAssetInternalAsync(AssetInfo asset)
            {
                return UniTask.FromResult(AssetHandle.Failure(new NotSupportedException()));
            }

            protected override UniTask<RawAssetHandle> LoadRawAssetInternalAsync(AssetInfo asset)
            {
                return UniTask.FromResult(RawAssetHandle.Failure(new NotSupportedException()));
            }

            protected override async UniTask<SceneAssetHandle> LoadSceneAssetInternalAsync(AssetInfo asset)
            {
                LoadCount++;
                await UniTask.Yield();
                var scene = SceneManager.CreateScene(SceneName);
                return SceneAssetHandle.Success(asset, scene);
            }

        }

        private sealed class CompletedOperationHandle : OperationHandle
        {
            public CompletedOperationHandle()
            {
                SetResult();
            }

            public override void Execute(params object[] args)
            {
            }
        }

        private sealed class CompletedBundleOperationHandle : OperationHandle<BundleHandle>
        {
            public CompletedBundleOperationHandle(BundleHandle bundle)
            {
                SetResult(bundle);
            }

            public override void Execute(params object[] args)
            {
            }
        }
    }
}
