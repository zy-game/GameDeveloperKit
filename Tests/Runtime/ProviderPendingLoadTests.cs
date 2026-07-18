using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using GameDeveloperKit.Resource;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class ProviderPendingLoadTests
    {
        [UnityTest]
        public IEnumerator LoadAssetAsync_WhenFirstLoadIsConcurrent_CoalescesAndRetainsPerWaiter()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var provider = CreateProvider();

                var firstTask = provider.LoadAssetAsync("asset");
                var secondTask = provider.LoadAssetAsync("asset");

                Assert.AreEqual(1, provider.AssetLoadCount);
                Assert.IsTrue(provider.HasLoadedAssets);

                provider.CompleteAssetSuccess();
                var first = await firstTask;
                var second = await secondTask;

                Assert.AreSame(first, second);
                Assert.AreEqual(2, first.ReferenceCount);

                await provider.UnloadAsset(first);
                await provider.UnloadAsset(second);
                await provider.UnloadUnusedAssetAsync();

                Assert.AreEqual(ResourceStatus.Released, first.Status);
                Assert.IsFalse(provider.HasLoadedAssets);
                provider.Release();
            });
        }

        [UnityTest]
        public IEnumerator LoadRawAssetAsync_WhenFirstLoadIsConcurrent_CoalescesAndRetainsPerWaiter()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var provider = CreateProvider();

                var firstTask = provider.LoadRawAssetAsync("raw");
                var secondTask = provider.LoadRawAssetAsync("raw");
                provider.CompleteRawSuccess();
                var first = await firstTask;
                var second = await secondTask;

                Assert.AreEqual(1, provider.RawLoadCount);
                Assert.AreSame(first, second);
                Assert.AreEqual(2, first.ReferenceCount);

                await provider.UnloadRawAsset(first);
                await provider.UnloadRawAsset(second);
                await provider.UnloadUnusedAssetAsync();

                Assert.AreEqual(ResourceStatus.Released, first.Status);
                provider.Release();
            });
        }

        [UnityTest]
        public IEnumerator LoadSceneAssetAsync_WhenFirstLoadIsConcurrent_CoalescesAndRetainsPerWaiter()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var provider = CreateProvider();

                var firstTask = provider.LoadSceneAssetAsync("scene");
                var secondTask = provider.LoadSceneAssetAsync("scene");
                provider.CompleteSceneSuccess();
                var first = await firstTask;
                var second = await secondTask;

                Assert.AreEqual(1, provider.SceneLoadCount);
                Assert.AreSame(first, second);
                Assert.AreEqual(2, first.ReferenceCount);

                await provider.UnloadSceneAsset(first);
                await provider.UnloadSceneAsset(second);

                Assert.AreEqual(ResourceStatus.Released, first.Status);
                provider.Release();
            });
        }

        [UnityTest]
        public IEnumerator LoadAssetAsync_WhenSharedLoadFails_ReturnsIndependentFailuresAndAllowsRetry()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var provider = CreateProvider();
                var exception = new InvalidOperationException("load failed");

                var firstTask = provider.LoadAssetAsync("asset");
                var secondTask = provider.LoadAssetAsync("asset");
                provider.CompleteAssetFailure(exception);
                var first = await firstTask;
                var second = await secondTask;

                Assert.AreEqual(1, provider.AssetLoadCount);
                Assert.AreNotSame(first, second);
                Assert.AreSame(exception, first.Error);
                Assert.AreSame(exception, second.Error);

                provider.ResetAssetLoad();
                var retryTask = provider.LoadAssetAsync("asset");
                Assert.AreEqual(2, provider.AssetLoadCount);
                provider.CompleteAssetSuccess();
                var retry = await retryTask;

                Assert.AreEqual(ResourceStatus.Succeeded, retry.Status);
                first.Release();
                second.Release();
                await provider.UnloadAsset(retry);
                await provider.UnloadUnusedAssetAsync();
                provider.Release();
            });
        }

        [UnityTest]
        public IEnumerator UninitializeProviderAsync_WhenLoadIsPending_DrainsAndRejectsNewLoads()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var provider = CreateProvider();
                var activeLoad = provider.LoadAssetAsync("asset");

                var teardown = provider.UninitializeProviderAsync();
                var rejected = await provider.LoadRawAssetAsync("raw");

                Assert.AreEqual(UniTaskStatus.Pending, teardown.Status);
                Assert.AreEqual(ResourceStatus.Failed, rejected.Status);
                StringAssert.Contains("shutting down", rejected.Error.Message);

                provider.CompleteAssetSuccess();
                var active = await activeLoad;
                var blockedOperation = await teardown;

                Assert.AreEqual(OperationStatus.Failed, blockedOperation.Status);
                StringAssert.Contains("loaded handles", blockedOperation.Error.Message);
                try
                {
                    await blockedOperation.WaitCompletionAsync();
                }
                catch
                {
                }

                rejected.Release();
                await provider.UnloadAsset(active);
                await provider.UnloadUnusedAssetAsync();
                var operation = await provider.UninitializeProviderAsync();
                Assert.AreEqual(OperationStatus.Succeeded, operation.Status);
                provider.Release();
                Assert.AreEqual(ResourceStatus.Released, active.Status);
            });
        }

        [UnityTest]
        public IEnumerator Release_WhenLoadIsPending_ThrowsUntilLoadCompletes()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var provider = CreateProvider();
                var load = provider.LoadAssetAsync("asset");

                Assert.Throws<GameException>(() => provider.Release());

                provider.CompleteAssetSuccess();
                var handle = await load;
                Assert.DoesNotThrow(() => provider.Release());
                Assert.AreEqual(ResourceStatus.Released, handle.Status);
            });
        }

        private static ControlledProvider CreateProvider()
        {
            return new ControlledProvider(new BundleInfo
            {
                Name = "pending-test",
                Assets = new List<AssetInfo>
                {
                    new AssetInfo { Location = "asset" },
                    new AssetInfo { Location = "raw" },
                    new AssetInfo { Location = "scene" },
                }
            });
        }

        private sealed class ControlledProvider : ProviderBase
        {
            private UniTaskCompletionSource<AssetHandle> m_AssetLoad = new UniTaskCompletionSource<AssetHandle>();
            private readonly UniTaskCompletionSource<RawAssetHandle> m_RawLoad = new UniTaskCompletionSource<RawAssetHandle>();
            private readonly UniTaskCompletionSource<SceneAssetHandle> m_SceneLoad = new UniTaskCompletionSource<SceneAssetHandle>();

            public int AssetLoadCount { get; private set; }
            public int RawLoadCount { get; private set; }
            public int SceneLoadCount { get; private set; }

            public ControlledProvider(BundleInfo info) : base(info)
            {
            }

            public override UniTask<OperationHandle<BundleHandle>> InitializeProviderAsync()
            {
                return UniTask.FromResult<OperationHandle<BundleHandle>>(
                    new CompletedBundleOperation(BundleHandle.Success(Info, null)));
            }

            public override async UniTask<OperationHandle> UninitializeProviderAsync()
            {
                try
                {
                    await PrepareForUninitializeAsync();
                    return new CompletedOperation();
                }
                catch (Exception exception)
                {
                    return new FailedOperation(exception);
                }
            }

            public void CompleteAssetSuccess()
            {
                m_AssetLoad.TrySetResult(AssetHandle.Success(FindAsset("asset"), null));
            }

            public void CompleteAssetFailure(Exception exception)
            {
                m_AssetLoad.TrySetResult(AssetHandle.Failure(exception));
            }

            public void ResetAssetLoad()
            {
                m_AssetLoad = new UniTaskCompletionSource<AssetHandle>();
            }

            public void CompleteRawSuccess()
            {
                m_RawLoad.TrySetResult(RawAssetHandle.Success(FindAsset("raw"), new byte[] { 1, 2, 3 }));
            }

            public void CompleteSceneSuccess()
            {
                m_SceneLoad.TrySetResult(SceneAssetHandle.Success(FindAsset("scene"), default));
            }

            protected override UniTask<AssetHandle> LoadAssetInternalAsync(AssetInfo asset)
            {
                AssetLoadCount++;
                return m_AssetLoad.Task;
            }

            protected override UniTask<RawAssetHandle> LoadRawAssetInternalAsync(AssetInfo asset)
            {
                RawLoadCount++;
                return m_RawLoad.Task;
            }

            protected override UniTask<SceneAssetHandle> LoadSceneAssetInternalAsync(AssetInfo asset)
            {
                SceneLoadCount++;
                return m_SceneLoad.Task;
            }

            private AssetInfo FindAsset(string location)
            {
                return Info.Assets.Find(asset => asset.Location == location);
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

        private sealed class FailedOperation : OperationHandle
        {
            public FailedOperation(Exception exception)
            {
                SetException(exception);
            }

            public override void Execute(params object[] args)
            {
            }
        }
    }
}
