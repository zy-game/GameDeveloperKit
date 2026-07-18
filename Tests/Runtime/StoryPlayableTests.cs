using System;
using System.Collections.Generic;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story;
using NUnit.Framework;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Playback;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryPlayableTests : RuntimeTestBase
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
        public void CanHandle_WhenMediaCommand_ReturnsTrue()
        {
            using var playable = new MediaCommandHandler(App.Playable, null, null);

            Assert.IsTrue(playable.CanHandle(CreateCommand("video", MediaCommandNames.PlayVideo, MediaCommandNames.ClipArgument, "clip")));
            Assert.IsTrue(playable.CanHandle(CreateCommand("image", MediaCommandNames.ShowImage, MediaCommandNames.ImageArgument, "image")));
            Assert.IsTrue(playable.CanHandle(CreateCommand("audio", MediaCommandNames.PlayAudio, MediaCommandNames.ClipArgument, "audio")));
            Assert.IsFalse(playable.CanHandle(new global::GameDeveloperKit.Story.Model.Command("event", "emit_event")));
        }

        [Test]
        public void Execute_WhenVideoPathIsInvalid_FailsStoryHandle()
        {
            using var playable = new MediaCommandHandler(App.Playable, null, null);
            var command = CreateCommand(
                "video",
                MediaCommandNames.PlayVideo,
                MediaCommandNames.ClipArgument,
                "relative.mp4",
                MediaCommandNames.VideoSourceNetworkStream);

            var handle = playable.Execute(command, default);

            Assert.AreSame(command, handle.Command);
            Assert.IsNotNull(handle.Error);
            StringAssert.Contains("path is invalid", handle.Error.Message);
        }

        [Test]
        public void StoryPlayerView_TypeBelongsToRuntimeAssembly()
        {
            Assert.AreEqual("GameDeveloperKit.Runtime", typeof(PlayerView).Assembly.GetName().Name);
        }

        private static global::GameDeveloperKit.Story.Model.Command CreateCommand(
            string id,
            string name,
            string argument,
            string value,
            string videoSource = null)
        {
            var values = new Dictionary<string, Value>(StringComparer.Ordinal)
            {
                [argument] = Value.FromString(value)
            };
            if (videoSource != null)
            {
                values[MediaCommandNames.VideoSourceArgument] = Value.FromString(videoSource);
            }

            return new global::GameDeveloperKit.Story.Model.Command(id, name, new ArgumentBag(values));
        }
    }
}
