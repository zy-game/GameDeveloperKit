using System;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Playback;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryPlaybackWindowTests
    {
        [Test]
        public void StoryPlaybackWindow_WhenStarted_UsesStoryModuleFrameDirectly()
        {
            var module = new StoryModule();
            module.Startup();
            var episode = new Episode(
                "episode",
                "Episode",
                "start",
                new[] { new EpisodeExit("done") },
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("end"))),
                    new Step("end", StepKind.End, new StepData(exitId: "done"))
                });
            var volume = new Volume(
                "volume",
                "Volume",
                new[] { episode },
                new Route(new[] { RouteEdge.FromRoot("root", episode.EpisodeId) }));
            var program = new Program("story", "1", new[] { volume });
            var window = new StoryPlaybackWindow();
            try
            {
                module.Register(program);
                window.ConfigureModules(module);

                window.PlayRegisteredAsync(
                    program.StoryId,
                    volume.VolumeId,
                    episode.EpisodeId).GetAwaiter().GetResult();

                Assert.AreSame(module.CurrentFrame, window.CurrentFrame);
                Assert.IsTrue(window.CurrentFrame.IsCompleted);
                Assert.IsNull(window.LastError);
            }
            finally
            {
                window.Release();
                module.Shutdown();
            }
        }

        [Test]
        public void RuntimeAssembly_AfterStoryWindowRebuild_HasNoPlaybackMicroFrameworkTypes()
        {
            var assembly = typeof(StoryPlaybackWindow).Assembly;
            Assert.IsTrue(typeof(VideoPlayerWindow).IsAssignableFrom(typeof(StoryPlaybackWindow)));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Story.Playback.PlaybackView"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Story.Playback.Presenter"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Story.Playback.ICommandHandler"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Story.Playback.ICommandHandle"));
        }

    }
}
