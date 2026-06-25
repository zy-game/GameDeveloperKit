using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Story;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryVideoPreloadTests
    {
        private readonly List<GameObject> m_GameObjects = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var gameObject in m_GameObjects)
            {
                if (gameObject != null)
                {
                    UnityEngine.Object.Destroy(gameObject);
                }
            }

            m_GameObjects.Clear();
            yield return null;
        }

        [Test]
        public void PreloadStatus_WhenRead_ContainsExpectedStates()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "Pending",
                    "ReadyToPlay",
                    "FirstFrameReady",
                    "Failed",
                    "Canceled"
                },
                Enum.GetNames(typeof(StoryAvProVideoPreloadStatus)));
        }

        [UnityTest]
        public IEnumerator PreloadVideoAsync_WhenPathInvalid_ReturnsFailedHandle()
        {
            return UniTask.ToCoroutine(async () =>
            {
                using (var player = new StoryAvProVideoCommandPlayer(null, false))
                {
                    var command = CreateVideoCommand("video", StoryMediaCommandNames.VideoSourceNetworkStream, "videos/0.mp4");

                    var handle = await player.PreloadVideoAsync(command);

                    Assert.AreSame(command, handle.Command);
                    Assert.AreEqual(StoryMediaCommandNames.VideoSourceNetworkStream, handle.Source);
                    Assert.AreEqual("videos/0.mp4", handle.ClipPath);
                    Assert.AreEqual(StoryAvProVideoPreloadStatus.Failed, handle.Status);
                    Assert.IsNotNull(handle.Error);
                    Assert.IsTrue(handle.IsTerminal);
                    Assert.IsFalse(handle.CanAcquire);
                    Assert.IsNull(player.PreloadQueue);
                }
            });
        }

        [Test]
        public void PreloadQueue_WhenCapacityIsInvalid_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StoryAvProVideoPreloadQueue(null, false, 0));
        }

        [UnityTest]
        public IEnumerator PreloadQueue_WhenCommandIsNull_ThrowsBeforeResolvingPath()
        {
            return UniTask.ToCoroutine(async () =>
            {
                using (var queue = new StoryAvProVideoPreloadQueue(null, false))
                {
                    ArgumentNullException exception = null;
                    try
                    {
                        await queue.PreloadAsync(
                            null,
                            StoryMediaCommandNames.VideoSourceStreamingAssets,
                            "videos/0.mp4");
                    }
                    catch (ArgumentNullException ex)
                    {
                        exception = ex;
                    }

                    Assert.IsNotNull(exception);
                    Assert.AreEqual("command", exception.ParamName);
                    Assert.AreEqual(0, queue.Count);
                }
            });
        }

        [UnityTest]
        public IEnumerator PreloadQueue_WhenPathInvalid_ReturnsFailedHandleWithoutQueueEntry()
        {
            return UniTask.ToCoroutine(async () =>
            {
                using (var queue = new StoryAvProVideoPreloadQueue(null, false))
                {
                    var command = CreateVideoCommand("video", StoryMediaCommandNames.VideoSourceNetworkStream, "videos/0.mp4");

                    var handle = await queue.PreloadAsync(
                        command,
                        StoryMediaCommandNames.VideoSourceNetworkStream,
                        "videos/0.mp4");

                    Assert.AreSame(command, handle.Command);
                    Assert.AreEqual(StoryAvProVideoPreloadStatus.Failed, handle.Status);
                    Assert.IsTrue(handle.IsTerminal);
                    Assert.IsFalse(handle.CanAcquire);
                    Assert.IsNotNull(handle.Error);
                    Assert.AreEqual(0, queue.Count);
                }
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenPresenterCreated_ConfiguresVideoPreloadQueue()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var gameObject = new GameObject("StoryPlayerView");
                m_GameObjects.Add(gameObject);
                var view = gameObject.AddComponent<StoryPlayerView>();
                view.ConfigureModules(CreateStartedStoryModule());

                view.Play(CreateVideoProgram());
                await UniTask.Yield();

                var presenter = view.Presenter;
                Assert.IsNotNull(presenter);
                var commandPlayer = FindVideoCommandPlayer(presenter);
                Assert.IsNotNull(commandPlayer);
                Assert.IsNotNull(commandPlayer.PreloadQueue);
                Assert.AreEqual(2, commandPlayer.PreloadQueue.Capacity);
                Assert.AreEqual(1, commandPlayer.PreloadLookAheadCount);
            });
        }

        private static StoryAvProVideoCommandPlayer FindVideoCommandPlayer(StoryPresenter presenter)
        {
            var field = typeof(StoryPresenter).GetField(
                "m_CommandHandlers",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var handlers = (IEnumerable<IStoryCommandHandler>)field.GetValue(presenter);
            foreach (var handler in handlers)
            {
                var mediaHandler = handler as StoryMediaCommandHandler;
                if (mediaHandler == null)
                {
                    continue;
                }

                var videoField = typeof(StoryMediaCommandHandler).GetField(
                    "m_VideoCommandPlayer",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                return videoField.GetValue(mediaHandler) as StoryAvProVideoCommandPlayer;
            }

            return null;
        }

        private static StoryModule CreateStartedStoryModule()
        {
            var module = new StoryModule();
            module.Startup();
            return module;
        }

        private static StoryProgram CreateVideoProgram()
        {
            return new StoryProgram(
                "story_video_preload_test",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "Chapter 01",
                        "video",
                        new[]
                        {
                            new StoryStep(
                                "video",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: CreateVideoCommand(
                                        "video",
                                        StoryMediaCommandNames.VideoSourceNetworkStream,
                                        "invalid"))),
                        }),
                });
        }

        private static StoryCommand CreateVideoCommand(string commandId, string source, string clip)
        {
            return new StoryCommand(
                commandId,
                StoryMediaCommandNames.PlayVideo,
                new StoryArgumentBag(
                    new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                    {
                        [StoryMediaCommandNames.VideoSourceArgument] = StoryValue.FromString(source),
                        [StoryMediaCommandNames.ClipArgument] = StoryValue.FromString(clip),
                    }),
                true,
                new[] { StoryMediaCommandNames.CompletedOutcome });
        }
    }
}
