using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Resource;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class VisualPlayableTests : RuntimeTestBase
    {
        [SetUp]
        public void SetUp()
        {
            if (App.TryGetRegistered<PlayableModule>(out _))
            {
                App.Unregister<PlayableModule>().GetAwaiter().GetResult();
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (App.TryGetRegistered<PlayableModule>(out _))
            {
                App.Unregister<PlayableModule>().GetAwaiter().GetResult();
            }
        }

        [Test]
        public void PlayableModule_WhenResolved_RegistersVisualPlayables()
        {
            var module = App.Playable;

            Assert.IsNotNull(module.Text);
            Assert.IsNotNull(module.Image);
            Assert.IsNotNull(module.Video);
        }

        [Test]
        public void PlayTextAsync_WhenStopped_ClearsOutput()
        {
            var output = string.Empty;

            var handle = App.Playable.PlayTextAsync("line", value => output = value).GetAwaiter().GetResult();

            Assert.AreEqual("line", output);
            Assert.AreEqual(PlayableStatus.Playing, handle.Status);
            handle.Stop();
            Assert.AreEqual(string.Empty, output);
            Assert.AreEqual(PlayableStatus.Stopped, handle.Status);
        }

        [Test]
        public void VisualRequests_WhenSourceIsEmpty_RejectImmediately()
        {
            Assert.Throws<ArgumentException>(() => new ImagePlayableRequest("", _ => { }));
            Assert.Throws<ArgumentException>(() => new VideoPlayableRequest(""));
            Assert.Throws<ArgumentNullException>(() => new TextPlayableRequest(null, _ => { }));
        }

        [Test]
        public void PlayTextAsync_WhenCanceled_DoesNotWriteOutput()
        {
            using var source = new CancellationTokenSource();
            source.Cancel();
            var called = false;

            Assert.Throws<OperationCanceledException>(() =>
                App.Playable.PlayTextAsync("line", _ => called = true, source.Token).GetAwaiter().GetResult());
            Assert.IsFalse(called);
        }

        [Test]
        public void StartLoadedImage_WhenOutputThrows_ReleasesLoadedAsset()
        {
            var texture = new Texture2D(1, 1);
            var asset = AssetHandle.Success(
                new AssetInfo { Location = "image-output-failure", TypeName = nameof(Texture2D) },
                texture);
            var request = new ImagePlayableRequest(
                "image-output-failure",
                _ => throw new InvalidOperationException("output failed"));

            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    ImagePlayable.StartLoadedImage(request, texture, asset));

                Assert.AreEqual("output failed", exception.Message);
                Assert.AreEqual(ResourceStatus.Released, asset.Status);
                Assert.AreEqual(0, asset.ReferenceCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void StartHandle_WhenVideoStartThrows_RollsBackActiveHandle()
        {
            var playable = new VideoPlayable();
            var handle = new VideoPlayableHandle(
                "video-start-failure",
                new VideoPlayableOptions { DontDestroyOnLoad = false },
                false);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                playable.StartHandle(handle, _ => throw new InvalidOperationException("open failed")));

            Assert.AreEqual("open failed", exception.Message);
            Assert.IsEmpty(playable.ActiveHandles);
            Assert.AreEqual(PlayableStatus.Stopped, handle.Status);
            playable.Dispose();
        }
    }
}
