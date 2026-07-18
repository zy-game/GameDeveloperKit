using System.Collections;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GameDeveloperKit.Tests
{
    public sealed class AssetUtilityTests : RuntimeTestBase
    {
        [UnityTest]
        public IEnumerator BoundObjectDestroy_ReleasesHandleOwnerWithoutResolvingResourceModule()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Shutdown();
                var gameObject = new GameObject("AssetUtilityTests", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                var texture = new Texture2D(1, 1);
                var handle = AssetHandle.Success(
                    new AssetInfo { Location = "asset-utility-test" },
                    texture);
                var owner = new RecordingOwner();
                handle.AttachOwner(owner);
                try
                {
                    handle.SetTexture(gameObject.GetComponent<RawImage>());
                    Assert.IsFalse(App.TryGetRegistered<ResourceModule>(out _));

                    Object.DestroyImmediate(gameObject);

                    Assert.AreEqual(1, owner.ReleaseCount);
                    Assert.AreSame(handle, owner.ReleasedHandle);
                    Assert.IsFalse(App.TryGetRegistered<ResourceModule>(out _));
                }
                finally
                {
                    if (gameObject != null)
                    {
                        Object.DestroyImmediate(gameObject);
                    }

                    Object.DestroyImmediate(texture);
                }
            });
        }

        private sealed class RecordingOwner : IResourceHandleOwner
        {
            public int ReleaseCount { get; private set; }

            public ResourceHandle ReleasedHandle { get; private set; }

            public bool ReleaseHandle<TInfo>(ResourceHandle<TInfo> handle)
                where TInfo : class
            {
                ReleaseCount++;
                ReleasedHandle = handle as ResourceHandle;
                return true;
            }
        }
    }
}
