using System;
using System.Collections.Generic;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story;
using NUnit.Framework;

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
            using var playable = new StoryPlayable(App.Playable, null, null);

            Assert.IsTrue(playable.CanHandle(CreateCommand("video", StoryMediaCommandNames.PlayVideo, StoryMediaCommandNames.ClipArgument, "clip")));
            Assert.IsTrue(playable.CanHandle(CreateCommand("image", StoryMediaCommandNames.ShowImage, StoryMediaCommandNames.ImageArgument, "image")));
            Assert.IsTrue(playable.CanHandle(CreateCommand("audio", StoryMediaCommandNames.PlayAudio, StoryMediaCommandNames.ClipArgument, "audio")));
            Assert.IsFalse(playable.CanHandle(new StoryCommand("event", "emit_event")));
        }

        [Test]
        public void Execute_WhenVideoPathIsInvalid_FailsStoryHandle()
        {
            using var playable = new StoryPlayable(App.Playable, null, null);
            var command = CreateCommand(
                "video",
                StoryMediaCommandNames.PlayVideo,
                StoryMediaCommandNames.ClipArgument,
                "relative.mp4",
                StoryMediaCommandNames.VideoSourceNetworkStream);

            var handle = playable.Execute(command, default);

            Assert.AreSame(command, handle.Command);
            Assert.IsNotNull(handle.Error);
            StringAssert.Contains("path is invalid", handle.Error.Message);
        }

        [Test]
        public void StoryPlayerView_TypeBelongsToRuntimeAssembly()
        {
            Assert.AreEqual("GameDeveloperKit.Runtime", typeof(StoryPlayerView).Assembly.GetName().Name);
        }

        private static StoryCommand CreateCommand(
            string id,
            string name,
            string argument,
            string value,
            string videoSource = null)
        {
            var values = new Dictionary<string, StoryValue>(StringComparer.Ordinal)
            {
                [argument] = StoryValue.FromString(value)
            };
            if (videoSource != null)
            {
                values[StoryMediaCommandNames.VideoSourceArgument] = StoryValue.FromString(videoSource);
            }

            return new StoryCommand(id, name, new StoryArgumentBag(values));
        }
    }
}
