using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Events;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Playback;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.UI;
using NUnit.Framework;

namespace GameDeveloperKit.Tests.Runtime
{
    public sealed class StoryPlayerWindowContractTests
    {
        [Test]
        public void PlayerWindows_DirectlyUseUIWindowAndExposeBusinessHooks()
        {
            Assert.IsTrue(typeof(UIWindow).IsAssignableFrom(typeof(VideoPlayerWindow)));
            Assert.IsTrue(typeof(UIWindow).IsAssignableFrom(typeof(ImagePlayerWindow)));
            Assert.IsTrue(typeof(VideoPlayerWindow).IsAssignableFrom(typeof(StoryPlaybackWindow)));
            Assert.IsFalse(typeof(VideoPlayerWindow).IsAbstract);
            Assert.IsFalse(typeof(ImagePlayerWindow).IsAbstract);
            Assert.IsNotNull(typeof(VideoPlayerWindow).GetCustomAttribute<UIOption>());
            Assert.IsNotNull(typeof(ImagePlayerWindow).GetCustomAttribute<UIOption>());
            var storyOption = typeof(StoryPlaybackWindow).GetCustomAttribute<UIOption>();
            Assert.IsNotNull(storyOption);
            Assert.AreEqual("Assets/Bundles/Playback/StoryPlaybackWindow.prefab", storyOption.Path);
            Assert.IsTrue(typeof(VideoPlayerWindow).GetMethod(
                nameof(VideoPlayerWindow.PlayAsync),
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(bool),
                    typeof(VideoPlayableOptions),
                    typeof(CancellationToken)
                }).IsVirtual);
            Assert.IsTrue(typeof(ImagePlayerWindow).GetMethod(
                nameof(ImagePlayerWindow.PlayAsync),
                new[] { typeof(string), typeof(CancellationToken) }).IsVirtual);
            Assert.IsNotNull(typeof(ImagePlayerWindow).GetEvent(nameof(ImagePlayerWindow.ImageClicked)));
            Assert.IsTrue(typeof(VideoPlayerWindow).GetMethod(nameof(VideoPlayerWindow.ToggleControls)).IsVirtual);
            Assert.IsTrue(typeof(ImagePlayerWindow).GetMethod(nameof(ImagePlayerWindow.ClickCurrentImage)).IsVirtual);
        }

        [Test]
        public void UnlockNode_IsRegisteredAsAnEventActionAndLogicIsNotAnAuthoringNode()
        {
            Assert.IsTrue(NodeSchemaRegistry.TryGet(NodeKind.Unlock, out var unlock));
            Assert.AreEqual(NodeCategory.Action, unlock.Category);
            Assert.IsTrue(unlock.Parameters.Any(item => item.Key == StoryCommandNames.UnlockIdArgument));
            CollectionAssert.DoesNotContain(Enum.GetNames(typeof(NodeKind)), "Logic");
        }

        [Test]
        public void StoryUnlockEvent_PreservesPlaybackContext()
        {
            var args = new StoryUnlockEvent("story", "volume", "episode", "step", "chapter-2");
            Assert.AreEqual("story", args.StoryId);
            Assert.AreEqual("volume", args.VolumeId);
            Assert.AreEqual("episode", args.EpisodeId);
            Assert.AreEqual("step", args.StepId);
            Assert.AreEqual("chapter-2", args.UnlockId);
            Assert.IsFalse(args.HasUse());
        }

        [Test]
        public void StoryPlaybackWindow_WhenUnlockIsEntered_FiresOnceAndContinues()
        {
            App.Shutdown().GetAwaiter().GetResult();
            var module = new StoryModule();
            module.Startup();
            var unlockCommand = new global::GameDeveloperKit.Story.Model.Command(
                "unlock_chapter_2",
                StoryCommandNames.Unlock,
                new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                {
                    [StoryCommandNames.UnlockIdArgument] = Value.FromString("chapter-2")
                }),
                true,
                new[] { MediaCommandNames.CompletedOutcome });
            var episode = new Episode(
                "episode",
                "Episode",
                "start",
                new[] { new EpisodeExit("done") },
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("unlock"))),
                    new Step(
                        "unlock",
                        StepKind.Command,
                        new StepData(command: unlockCommand, target: Target.Step("end"))),
                    new Step("end", StepKind.End, new StepData(exitId: "done"))
                });
            var volume = new Volume(
                "volume",
                "Volume",
                new[] { episode },
                new Route(new[] { RouteEdge.FromRoot("root", episode.EpisodeId) }));
            var program = new Program(
                "story",
                "1",
                new[] { volume },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition(
                        StoryCommandNames.Unlock,
                        "Unlock",
                        true,
                        new[] { StoryCommandNames.UnlockIdArgument },
                        new[] { MediaCommandNames.CompletedOutcome })
                }));
            var window = new StoryPlaybackWindow();
            var fired = 0;
            var subscription = App.Event.Subscribe<StoryUnlockEvent>(evt =>
            {
                fired++;
                Assert.AreEqual("chapter-2", evt.UnlockId);
            });

            try
            {
                module.Register(program);
                window.ConfigureModules(module);

                window.PlayRegisteredAsync(
                    program.StoryId,
                    volume.VolumeId,
                    episode.EpisodeId).GetAwaiter().GetResult();

                Assert.AreEqual(1, fired);
                Assert.IsTrue(window.CurrentFrame.IsCompleted);
                Assert.AreEqual(1, module.CurrentRunner.History.Count);
                Assert.AreEqual("unlock_chapter_2", module.CurrentRunner.History[0].ActionId);
                Assert.IsNull(window.LastError);
            }
            finally
            {
                subscription.Cancel();
                window.Release();
                module.Shutdown();
                App.Shutdown().GetAwaiter().GetResult();
            }
        }

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
    }
}
