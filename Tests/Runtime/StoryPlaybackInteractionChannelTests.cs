using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Story;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Playback;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryPlaybackInteractionChannelTests : RuntimeTestBase
    {
        private const string SampleImagePath = "Assets/Bundles/Story/UI/test.jpg";

        private readonly List<GameObject> m_GameObjects = new List<GameObject>();
        private readonly List<StoryModule> m_Modules = new List<StoryModule>();

        [SetUp]
        public void SetUp()
        {
            App.Shutdown().GetAwaiter().GetResult();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                for (var i = 0; i < m_GameObjects.Count; i++)
                {
                    var gameObject = m_GameObjects[i];
                    if (gameObject != null)
                    {
                        UnityEngine.Object.Destroy(gameObject);
                    }
                }

                m_GameObjects.Clear();
                await UniTask.Yield();

                for (var i = 0; i < m_Modules.Count; i++)
                {
                    m_Modules[i]?.Shutdown();
                }

                m_Modules.Clear();
                await App.Shutdown();
            });
        }

        [Test]
        public void StoryModuleInteractions_WhenRegistered_ReturnsChannel()
        {
            var module = CreateStartedModule();
            var channel = new RecordingInteractionChannel(null);

            module.SetInteractions(channel);

            Assert.AreSame(channel, module.GetInteractions());

            module.SetInteractions(null);

            Assert.IsNull(module.GetInteractions());
        }

        [Test]
        public void PlaybackSurfaceView_WhenVideoSeekSurfaceMissing_KeepsOptionalSurfaceNull()
        {
            var surface = new PlaybackSurfaceView();

            Assert.IsNull(surface.VideoSeek);
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenDefaultSurfaceCreated_ProvidesHiddenVideoSeekSurface()
        {
            var module = CreateStartedModule();
            var view = CreatePlayerView(module);

            var surface = view.CreateDefaultSurfaceView();

            Assert.IsNotNull(surface.VideoSeek);
            Assert.IsNotNull(surface.VideoSeek.Slider);
            Assert.IsNotNull(surface.VideoSeek.Root);
            Assert.IsNotNull(surface.VideoSeek.PauseButton);
            Assert.IsFalse(surface.VideoSeek.Root.gameObject.activeSelf);
            yield break;
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WithRegisteredChannel_UsesLifecycleAndInputSurfaces()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryInteractionSurface", 2);
                var channel = new RecordingInteractionChannel(_ => surface, true);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);

                await view.PlayAsync(CreateLineChoiceProgram("story_interaction_lifecycle"));

                AssertFrame(view.CurrentFrame, "chapter_01", "line");
                Assert.IsNull(view.LastError);
                Assert.IsTrue(channel.AwakeTokenCanBeCanceled);
                AssertEventOrder(channel.Events, "awake:start", "awake:end");
                AssertEventOrder(channel.Events, "awake:end", "started");
                AssertEventOrder(channel.Events, "started", "chapter:chapter_01");
                AssertEventOrder(channel.Events, "chapter:chapter_01", "surface:Text:chapter_01:line");
                AssertEventOrder(channel.Events, "frame:chapter_01:line", "surface:Text:chapter_01:line");
                Assert.AreEqual("speaker.one", surface.SpeakerText.text);
                Assert.AreEqual("line.one", surface.BodyText.text);
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Choice));
                Assert.IsFalse(surface.ContinueButton.gameObject.activeSelf);
                Assert.AreEqual("choice.yes", GetButtonText(surface.ChoiceButtons[0]));
                Assert.AreEqual("choice.no", GetButtonText(surface.ChoiceButtons[1]));

                view.StopPlayback();

                Assert.IsTrue(channel.Events.Contains("stopped"));
                Assert.IsNull(view.CurrentFrame);
                surface.ChoiceButtons[0].onClick.Invoke();
                Assert.IsNull(view.LastError);
                Assert.IsNull(view.CurrentFrame);
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenChoiceMovesToNextChapter_NotifiesBeforeSurfaceQuery()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryChapterSurface", 2);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);

                await view.PlayAsync(CreateChoiceToChapterProgram("story_chapter_switch"));
                surface.ChoiceButtons[1].onClick.Invoke();

                AssertFrame(view.CurrentFrame, "chapter_02", "line_no");
                Assert.IsNull(view.LastError);
                AssertEventOrder(channel.Events, "chapter:chapter_02", "surface:Text:chapter_02:line_no");
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenCommandFramePresented_RequestsVideoAndImageSurfaces()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryMediaSurface", 0);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);
                var frame = CreateMediaFrame();

                view.Present(frame, null);
                await UniTask.Yield();

                Assert.IsNull(view.LastError);
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Video));
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Image));
                AssertEventOrder(channel.Events, "frame:chapter_media:video", "surface:Video:chapter_media:video");
                AssertEventOrder(channel.Events, "frame:chapter_media:video", "surface:Image:chapter_media:video");
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenQteFramePresented_RequestsCustomSurfaceAndKeepsVideoSurface()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryQteSurface", 0);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);
                var frame = CreateVideoQteFrame();

                view.Present(frame, null);
                await UniTask.Yield();

                Assert.IsNull(view.LastError);
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Video));
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Custom));
                Assert.AreEqual(0, channel.GetRequestCount(InteractionRequestKind.Continue));
                AssertEventOrder(channel.Events, "frame:chapter_qte:parallel", "surface:Video:chapter_qte:parallel");
                AssertEventOrder(channel.Events, "frame:chapter_qte:parallel", "surface:Custom:chapter_qte:parallel");
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenUnlockFramePresented_RequestsCustomSurfaceAndKeepsVideoSurface()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryUnlockSurface", 0);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);
                var frame = CreateVideoUnlockFrame();

                view.Present(frame, null);
                await UniTask.Yield();

                Assert.IsNull(view.LastError);
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Video));
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Custom));
                Assert.AreEqual(0, channel.GetRequestCount(InteractionRequestKind.Continue));
                AssertEventOrder(channel.Events, "frame:chapter_unlock:parallel", "surface:Video:chapter_unlock:parallel");
                AssertEventOrder(channel.Events, "frame:chapter_unlock:parallel", "surface:Custom:chapter_unlock:parallel");
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenQteCustomRootMissing_ThrowsConfigurationError()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = new PlaybackSurfaceView(videoOutput: CreateRawImage(CreateRoot("StoryMissingQteRootSurface"), "VideoOutput"));
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);

                Exception exception = null;
                try
                {
                    view.Present(CreateVideoQteFrame(), null);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                await UniTask.Yield();

                Assert.IsNotNull(exception);
                StringAssert.Contains("custom root surface is missing", exception.Message);
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Video));
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Custom));
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenUnlockCustomRootMissing_ThrowsConfigurationError()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = new PlaybackSurfaceView(videoOutput: CreateRawImage(CreateRoot("StoryMissingUnlockRootSurface"), "VideoOutput"));
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);

                Exception exception = null;
                try
                {
                    view.Present(CreateVideoUnlockFrame(), null);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                await UniTask.Yield();

                Assert.IsNotNull(exception);
                StringAssert.Contains("custom root surface is missing", exception.Message);
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Video));
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Custom));
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenVideoPrewarmFails_DoesNotNotifyStartedOrQuerySurfaces()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryPrewarmFailureSurface", 0);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);

                await view.PlayAsync(CreateVideoProgram("story_video_prewarm_failure"));

                Assert.IsNotNull(view.LastError);
                StringAssert.Contains("video path is invalid", view.LastError.Message);
                Assert.IsTrue(channel.Events.Contains("awake:start"));
                Assert.IsTrue(channel.Events.Contains("awake:end"));
                Assert.IsFalse(channel.Events.Contains("started"), string.Join(", ", channel.Events));
                Assert.IsFalse(channel.Events.Exists(value => value.StartsWith("surface:", StringComparison.Ordinal)));
                Assert.AreEqual(0, channel.GetRequestCount(InteractionRequestKind.Video));
                Assert.IsNull(view.CurrentFrame);
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenRequiredVideoSurfaceMissing_ThrowsConfigurationError()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryMissingVideoSurface", 0, false);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);

                Exception exception = null;
                try
                {
                    view.Present(CreateMediaFrame(), null);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                await UniTask.Yield();

                Assert.IsNotNull(exception);
                StringAssert.Contains("video output surface is missing", exception.Message);
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Video));
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenChoiceButtonCountMismatches_ReportsConfigurationError()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryChoiceMismatchSurface", 1);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);

                await view.PlayAsync(CreateChoiceToChapterProgram("story_choice_mismatch"));

                Assert.IsNotNull(view.LastError);
                StringAssert.Contains("choice button count does not match", view.LastError.Message);
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Choice));
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenDefaultQteButtonClickedRequiredTimes_AdvancesSuccess()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryQteSuccessSurface", 0);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);

                await view.PlayAsync(CreateQteProgram("story_qte_success", 5d, 2));

                AssertFrame(view.CurrentFrame, "chapter_qte", "qte");
                Assert.IsNull(view.LastError);
                var inputButton = FindChildButton(surface.CustomRoot, "InputButton");
                Assert.IsNotNull(inputButton);

                inputButton.onClick.Invoke();
                AssertFrame(view.CurrentFrame, "chapter_qte", "qte");

                inputButton.onClick.Invoke();

                AssertFrame(view.CurrentFrame, "chapter_qte", "success_line");
                Assert.IsNull(view.LastError);
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenDefaultQteTimesOut_AdvancesFail()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryQteFailSurface", 0);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);

                await view.PlayAsync(CreateQteProgram("story_qte_fail", 0.01d, 2));
                await UniTask.Delay(TimeSpan.FromMilliseconds(50d));
                for (var i = 0; i < 8 && string.Equals(view.CurrentFrame?.AnchorStep.StepId, "qte", StringComparison.Ordinal); i++)
                {
                    await UniTask.Yield();
                }

                AssertFrame(view.CurrentFrame, "chapter_qte", "fail_line");
                Assert.IsNull(view.LastError);
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenDefaultUnlockAlreadyUnlocked_AdvancesSuccessWithoutOverlay()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryUnlockAlreadyUnlockedSurface", 0);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);
                channel.TrySetUnlockState("chapter_unlock.door", true, out _);

                await view.PlayAsync(CreateUnlockProgram("story_unlock_already_unlocked"));

                AssertFrame(view.CurrentFrame, "chapter_unlock", "success_line");
                Assert.IsNull(view.LastError);
                Assert.IsNull(surface.CustomRoot.Find("StoryUnlockOverlay"));
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenDefaultUnlockButtonClicked_AdvancesSuccessAndWritesState()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryUnlockSuccessSurface", 0);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);

                await view.PlayAsync(CreateUnlockProgram("story_unlock_success"));

                AssertFrame(view.CurrentFrame, "chapter_unlock", "unlock");
                Assert.IsNull(view.LastError);
                var unlockButton = FindChildButton(surface.CustomRoot, "UnlockButton");
                Assert.IsNotNull(unlockButton);

                unlockButton.onClick.Invoke();
                await UniTask.Yield();

                AssertFrame(view.CurrentFrame, "chapter_unlock", "success_line");
                Assert.IsNull(view.LastError);
                Assert.IsTrue(channel.TryGetUnlockState("chapter_unlock.door", out var unlocked));
                Assert.IsTrue(unlocked);
                Assert.IsNull(surface.CustomRoot.Find("StoryUnlockOverlay"));
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenDefaultUnlockFailClicked_AdvancesFailWithoutWritingState()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryUnlockFailSurface", 0);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);

                await view.PlayAsync(CreateUnlockProgram("story_unlock_fail"));

                var failButton = FindChildButton(surface.CustomRoot, "FailButton");
                Assert.IsNotNull(failButton);

                failButton.onClick.Invoke();
                await UniTask.Yield();

                AssertFrame(view.CurrentFrame, "chapter_unlock", "fail_line");
                Assert.IsNull(view.LastError);
                Assert.IsFalse(channel.TryGetUnlockState("chapter_unlock.door", out var unlocked));
                Assert.IsFalse(unlocked);
                Assert.IsNull(surface.CustomRoot.Find("StoryUnlockOverlay"));
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenDefaultUnlockCancelClicked_AdvancesFail()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryUnlockCancelSurface", 0);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);

                await view.PlayAsync(CreateUnlockProgram("story_unlock_cancel"));

                var cancelButton = FindChildButton(surface.CustomRoot, "CancelButton");
                Assert.IsNotNull(cancelButton);

                cancelButton.onClick.Invoke();
                await UniTask.Yield();

                AssertFrame(view.CurrentFrame, "chapter_unlock", "fail_line");
                Assert.IsNull(view.LastError);
                Assert.IsNull(surface.CustomRoot.Find("StoryUnlockOverlay"));
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenDefaultUnlockWriteRejected_AdvancesFailWithoutWritingState()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryUnlockRejectedSurface", 0);
                var channel = new RecordingInteractionChannel(_ => surface)
                {
                    RejectUnlockWrites = true,
                };
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);

                await view.PlayAsync(CreateUnlockProgram("story_unlock_rejected"));

                var unlockButton = FindChildButton(surface.CustomRoot, "UnlockButton");
                Assert.IsNotNull(unlockButton);

                unlockButton.onClick.Invoke();
                await UniTask.Yield();

                AssertFrame(view.CurrentFrame, "chapter_unlock", "fail_line");
                Assert.IsNull(view.LastError);
                Assert.IsTrue(channel.UnlockWriteAttempted);
                Assert.IsNull(surface.CustomRoot.Find("StoryUnlockOverlay"));
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenDefaultUnlockStopped_CleansOverlayWithoutCompletingOutcome()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryUnlockStopSurface", 0);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);

                await view.PlayAsync(CreateUnlockProgram("story_unlock_stop"));

                AssertFrame(view.CurrentFrame, "chapter_unlock", "unlock");
                Assert.IsNotNull(surface.CustomRoot.Find("StoryUnlockOverlay"));

                view.StopPlayback();
                await UniTask.Yield();

                Assert.IsNull(view.CurrentFrame);
                Assert.IsNull(surface.CustomRoot.Find("StoryUnlockOverlay"));
            });
        }

        [UnityTest]
        public IEnumerator StoryQteCommandHandler_WhenHandleStopped_CleansOverlayWithoutCompletingOutcome()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var root = CreateRoot("StoryQteStopRoot");
                var command = CreateQteCommand(5d, 2, false);
                var handler = new QteCommandHandler(() => root);
                var handle = handler.Execute(command, new RuntimeContext(null, null, null, 0d, null, null));
                var completed = false;
                handle.Completed += _ => completed = true;

                Assert.IsNotNull(root.Find("StoryQteOverlay"));

                handle.Stop();
                await UniTask.Yield();

                Assert.IsTrue(handle.IsStopped);
                Assert.IsFalse(completed);
                Assert.IsNull(root.Find("StoryQteOverlay"));
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenParallelWaitChoicePresented_RequestsVideoAndChoiceSurfaces()
        {
            return UniTask.ToCoroutine(() =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryParallelWaitChoiceSurface", 1);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlayerView(module);
                var program = CreateParallelWaitChoiceVideoProgram("story_playback_parallel_wait_choice");

                module.Register(program);
                var runner = module.StartProgram(program.StoryId);
                view.Present(runner.CurrentFrame, null);

                AssertFrame(view.CurrentFrame, "chapter_01", "line_intro");
                Assert.IsTrue(surface.ContinueButton.gameObject.activeSelf);

                var initialParallelFrame = module.Continue();
                view.Present(initialParallelFrame, null);

                AssertFrame(view.CurrentFrame, "chapter_01", "parallel");
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Video));
                Assert.IsFalse(surface.ContinueButton.gameObject.activeSelf);

                var choiceFrame = module.Evaluate(1.5d);
                view.Present(choiceFrame, null);

                AssertFrame(view.CurrentFrame, "chapter_01", "parallel");
                Assert.IsNull(view.LastError);
                Assert.AreEqual(2, channel.GetRequestCount(InteractionRequestKind.Video));
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Choice));
                Assert.AreEqual("choice.continue", GetButtonText(surface.ChoiceButtons[0]));
                Assert.IsTrue(surface.ChoiceButtons[0].gameObject.activeSelf);
                Assert.IsFalse(surface.ContinueButton.gameObject.activeSelf);
                AssertEventOrder(channel.Events, "frame:chapter_01:parallel", "surface:Choice:chapter_01:parallel");
                AssertEventOrder(channel.Events, "frame:chapter_01:parallel", "surface:Video:chapter_01:parallel");
                return UniTask.CompletedTask;
            });
        }

        private StoryModule CreateStartedModule()
        {
            var module = new StoryModule();
            module.Startup();
            m_Modules.Add(module);
            return module;
        }

        private PlayerView CreatePlayerView(StoryModule module)
        {
            var gameObject = new GameObject("StoryInteractionPlayerView");
            m_GameObjects.Add(gameObject);
            var view = gameObject.AddComponent<PlayerView>();
            view.ConfigureModules(module);
            return view;
        }

        private PlaybackSurfaceView CreateSurface(string name, int choiceButtonCount, bool includeVideo = true)
        {
            var root = CreateRoot(name);
            var choiceButtons = new List<Button>();
            for (var i = 0; i < choiceButtonCount; i++)
            {
                var choiceButton = CreateButton(root, "ChoiceButton" + i);
                choiceButton.gameObject.SetActive(false);
                choiceButtons.Add(choiceButton);
            }

            var continueButton = CreateButton(root, "ContinueButton");
            continueButton.gameObject.SetActive(false);

            return new PlaybackSurfaceView(
                includeVideo ? CreateRawImage(root, "VideoOutput") : null,
                CreateRawImage(root, "ImageOutput"),
                CreateText(root, "SpeakerText"),
                CreateText(root, "BodyText"),
                continueButton,
                choiceButtons,
                root);
        }

        private RectTransform CreateRoot(string name)
        {
            var rootObject = new GameObject(name, typeof(RectTransform));
            m_GameObjects.Add(rootObject);
            return (RectTransform)rootObject.transform;
        }

        private static RawImage CreateRawImage(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.AddComponent<RawImage>();
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.AddComponent<TextMeshProUGUI>();
            var font = Resources.Load<TMP_FontAsset>("SIMSUN SDF");
            if (font != null)
            {
                text.font = font;
            }

            return text;
        }

        private static Button CreateButton(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.AddComponent<Image>();
            var button = gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            CreateText(gameObject.transform, "Label");
            return button;
        }

        private static Program CreateLineChoiceProgram(string storyId)
        {
            return new Program(
                storyId,
                "1",
                "chapter_01",
                CreateChoiceChapters("line"));
        }

        private static Program CreateChoiceToChapterProgram(string storyId)
        {
            return new Program(
                storyId,
                "1",
                "chapter_01",
                CreateChoiceChapters("choice"));
        }

        private static IReadOnlyList<Chapter> CreateChoiceChapters(string chapterOneEntry)
        {
            return new[]
            {
                new Chapter(
                    "chapter_01",
                    "Chapter 01",
                    chapterOneEntry,
                    new[]
                    {
                        new Step(
                            "line",
                            StepKind.Line,
                            new StepData(
                                "line.one",
                                "speaker.one",
                                target: Target.Step("chapter_01", "choice"))),
                        new Step(
                            "choice",
                            StepKind.Choice,
                            new StepData(
                                choices: new[]
                                {
                                    new Choice("choice_yes", "choice.yes", null, Target.Step("chapter_02", "line_yes")),
                                    new Choice("choice_no", "choice.no", null, Target.Step("chapter_02", "line_no")),
                                })),
                    }),
                new Chapter(
                    "chapter_02",
                    "Chapter 02",
                    "line_yes",
                    new[]
                    {
                        new Step(
                            "line_yes",
                            StepKind.Line,
                            new StepData("line.yes", target: Target.Step("chapter_02", "end"))),
                        new Step(
                            "line_no",
                            StepKind.Line,
                            new StepData("line.no", target: Target.Step("chapter_02", "end"))),
                        new Step("end", StepKind.End),
                    }),
            };
        }

        private static Program CreateVideoProgram(string storyId)
        {
            return new Program(
                storyId,
                "1",
                "chapter_video",
                new[]
                {
                    new Chapter(
                        "chapter_video",
                        "Chapter Video",
                        "video",
                        new[]
                        {
                            new Step(
                                "video",
                                StepKind.Command,
                                new StepData(command: CreateVideoCommand("video"))),
                        }),
                });
        }

        private static Program CreateParallelWaitChoiceVideoProgram(string storyId)
        {
            return new Program(
                storyId,
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "Chapter 01",
                        "line_intro",
                        new[]
                        {
                            new Step(
                                "line_intro",
                                StepKind.Line,
                                new StepData(
                                    "line.intro",
                                    "speaker.intro",
                                    target: Target.Step("chapter_01", "parallel"))),
                            new Step(
                                "parallel",
                                StepKind.Parallel,
                                new StepData(
                                    branches: new[]
                                    {
                                        new ParallelBranch("branch_video", "Video", Target.Step("chapter_01", "video")),
                                        new ParallelBranch("branch_interaction", "Interaction", Target.Step("chapter_01", "wait_choice")),
                                    })),
                            new Step(
                                "video",
                                StepKind.Command,
                                new StepData(command: CreateVideoCommand("video"))),
                            new Step(
                                "wait_choice",
                                StepKind.Wait,
                                new StepData(waitSeconds: 1.5d, target: Target.Step("chapter_01", "choice"))),
                            new Step(
                                "choice",
                                StepKind.Choice,
                                new StepData(
                                    choices: new[]
                                    {
                                        new Choice("choice_continue", "choice.continue", null, Target.Step("chapter_01", "after_choice")),
                                    })),
                            new Step(
                                "after_choice",
                                StepKind.Line,
                                new StepData("after.choice")),
                            new Step("end", StepKind.End),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition(
                        MediaCommandNames.PlayVideo,
                        "Play Video",
                        true,
                        CreatePlayVideoArgumentDefinitions(),
                        new[] { MediaCommandNames.CompletedOutcome }),
                }));
        }

        private static Program CreateQteProgram(string storyId, double durationSeconds, int requiredCount)
        {
            return new Program(
                storyId,
                "1",
                "chapter_qte",
                new[]
                {
                    new Chapter(
                        "chapter_qte",
                        "Chapter QTE",
                        "qte",
                        new[]
                        {
                            new Step(
                                "qte",
                                StepKind.Command,
                                new StepData(
                                    command: CreateQteCommand(durationSeconds, requiredCount, true))),
                            new Step(
                                "success_line",
                                StepKind.Line,
                                new StepData("qte.success")),
                            new Step(
                                "fail_line",
                                StepKind.Line,
                                new StepData("qte.fail")),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    CreateQteCommandDefinition(),
                }));
        }

        private static Program CreateUnlockProgram(string storyId)
        {
            return new Program(
                storyId,
                "1",
                "chapter_unlock",
                new[]
                {
                    new Chapter(
                        "chapter_unlock",
                        "Chapter Unlock",
                        "unlock",
                        new[]
                        {
                            new Step(
                                "unlock",
                                StepKind.Command,
                                new StepData(command: CreateUnlockCommand(true))),
                            new Step(
                                "success_line",
                                StepKind.Line,
                                new StepData("unlock.success")),
                            new Step(
                                "fail_line",
                                StepKind.Line,
                                new StepData("unlock.fail")),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    CreateUnlockCommandDefinition(),
                }));
        }

        private static CommandArgumentDefinition[] CreatePlayVideoArgumentDefinitions()
        {
            return new[]
            {
                new CommandArgumentDefinition(
                    MediaCommandNames.VideoSourceArgument,
                    "Source",
                    ParameterValueType.Option,
                    true,
                    options: new[]
                    {
                        MediaCommandNames.VideoSourceStreamingAssets,
                        MediaCommandNames.VideoSourcePersistentDataPath,
                        MediaCommandNames.VideoSourceNetworkStream
                    }),
                new CommandArgumentDefinition(
                    MediaCommandNames.ClipArgument,
                    "Clip",
                    ParameterValueType.AssetReference,
                    true,
                    "video")
            };
        }

        private static Frame CreateMediaFrame()
        {
            var videoStep = new Step(
                "video",
                StepKind.Command,
                new StepData(command: CreateVideoCommand("video")));
            var imageStep = new Step(
                "image",
                StepKind.Command,
                new StepData(command: CreateImageCommand("image")));
            var chapter = new Chapter(
                "chapter_media",
                "Chapter Media",
                "video",
                new[] { videoStep, imageStep });
            var program = new Program(
                "story_media_frame",
                "1",
                "chapter_media",
                new[] { chapter });

            return new Frame(
                program,
                chapter,
                videoStep,
                new[]
                {
                    FrameTrack.CreateCommand(videoStep),
                    FrameTrack.CreateCommand(imageStep),
                });
        }

        private static Frame CreateVideoQteFrame()
        {
            var videoStep = new Step(
                "video",
                StepKind.Command,
                new StepData(command: CreateVideoCommand("video")));
            var qteStep = new Step(
                "qte",
                StepKind.Command,
                new StepData(command: CreateQteCommand(3d, 5, false)));
            var parallelStep = new Step(
                "parallel",
                StepKind.Parallel,
                new StepData(
                    branches: new[]
                    {
                        new ParallelBranch("branch_video", "Video", Target.Step("chapter_qte", "video")),
                        new ParallelBranch("branch_interaction", "Interaction", Target.Step("chapter_qte", "qte")),
                    }));
            var chapter = new Chapter(
                "chapter_qte",
                "Chapter QTE",
                "parallel",
                new[] { parallelStep, videoStep, qteStep });
            var program = new Program(
                "story_qte_frame",
                "1",
                "chapter_qte",
                new[] { chapter });

            return new Frame(
                program,
                chapter,
                parallelStep,
                new[]
                {
                    FrameTrack.CreateCommand(videoStep, "branch_video", "Video"),
                    FrameTrack.CreateCommand(qteStep, "branch_interaction", "Interaction"),
                },
                null,
                false,
                true);
        }

        private static Frame CreateVideoUnlockFrame()
        {
            var videoStep = new Step(
                "video",
                StepKind.Command,
                new StepData(command: CreateVideoCommand("video")));
            var unlockStep = new Step(
                "unlock",
                StepKind.Command,
                new StepData(command: CreateUnlockCommand(false)));
            var parallelStep = new Step(
                "parallel",
                StepKind.Parallel,
                new StepData(
                    branches: new[]
                    {
                        new ParallelBranch("branch_video", "Video", Target.Step("chapter_unlock", "video")),
                        new ParallelBranch("branch_interaction", "Interaction", Target.Step("chapter_unlock", "unlock")),
                    }));
            var chapter = new Chapter(
                "chapter_unlock",
                "Chapter Unlock",
                "parallel",
                new[] { parallelStep, videoStep, unlockStep });
            var program = new Program(
                "story_unlock_frame",
                "1",
                "chapter_unlock",
                new[] { chapter });

            return new Frame(
                program,
                chapter,
                parallelStep,
                new[]
                {
                    FrameTrack.CreateCommand(videoStep, "branch_video", "Video"),
                    FrameTrack.CreateCommand(unlockStep, "branch_interaction", "Interaction"),
                },
                null,
                false,
                true);
        }

        private static global::GameDeveloperKit.Story.Model.Command CreateVideoCommand(string commandId)
        {
            return new global::GameDeveloperKit.Story.Model.Command(
                commandId,
                MediaCommandNames.PlayVideo,
                new ArgumentBag(
                    new Dictionary<string, Value>(StringComparer.Ordinal)
                    {
                        [MediaCommandNames.VideoSourceArgument] = Value.FromString(MediaCommandNames.VideoSourceNetworkStream),
                        [MediaCommandNames.ClipArgument] = Value.FromString("invalid"),
                    }),
                true,
                new[] { MediaCommandNames.CompletedOutcome });
        }

        private static global::GameDeveloperKit.Story.Model.Command CreateQteCommand(double durationSeconds, int requiredCount, bool includeOutcomeTargets)
        {
            var outcomeTargets = includeOutcomeTargets
                ? new Dictionary<string, Target>(StringComparer.Ordinal)
                {
                    [InteractionCommandNames.SuccessOutcome] = Target.Step("chapter_qte", "success_line"),
                    [InteractionCommandNames.FailOutcome] = Target.Step("chapter_qte", "fail_line"),
                }
                : null;
            return new global::GameDeveloperKit.Story.Model.Command(
                "qte",
                InteractionCommandNames.Qte,
                new ArgumentBag(
                    new Dictionary<string, Value>(StringComparer.Ordinal)
                    {
                        [InteractionCommandNames.InputActionIdArgument] = Value.FromString("space"),
                        [InteractionCommandNames.DurationSecondsArgument] = Value.FromNumber(durationSeconds),
                        [InteractionCommandNames.RequiredCountArgument] = Value.FromNumber(requiredCount),
                        [InteractionCommandNames.PromptTextKeyArgument] = Value.FromString("qte.break_free"),
                    }),
                true,
                new[]
                {
                    InteractionCommandNames.SuccessOutcome,
                    InteractionCommandNames.FailOutcome,
                },
                outcomeTargets);
        }

        private static global::GameDeveloperKit.Story.Model.Command CreateUnlockCommand(bool includeOutcomeTargets)
        {
            var outcomeTargets = includeOutcomeTargets
                ? new Dictionary<string, Target>(StringComparer.Ordinal)
                {
                    [InteractionCommandNames.SuccessOutcome] = Target.Step("chapter_unlock", "success_line"),
                    [InteractionCommandNames.FailOutcome] = Target.Step("chapter_unlock", "fail_line"),
                }
                : null;
            return new global::GameDeveloperKit.Story.Model.Command(
                "unlock",
                InteractionCommandNames.Unlock,
                new ArgumentBag(
                    new Dictionary<string, Value>(StringComparer.Ordinal)
                    {
                        [InteractionCommandNames.UnlockIdArgument] = Value.FromString("chapter_unlock.door"),
                        [InteractionCommandNames.PuzzleTypeArgument] = Value.FromString(InteractionCommandNames.PuzzleTypeNodeUnlock),
                        [InteractionCommandNames.PromptTextKeyArgument] = Value.FromString("unlock.door"),
                    }),
                true,
                new[]
                {
                    InteractionCommandNames.SuccessOutcome,
                    InteractionCommandNames.FailOutcome,
                },
                outcomeTargets);
        }

        private static CommandDefinition CreateQteCommandDefinition()
        {
            return new CommandDefinition(
                InteractionCommandNames.Qte,
                "QTE",
                true,
                new[]
                {
                    new CommandArgumentDefinition(
                        InteractionCommandNames.InputActionIdArgument,
                        "Input",
                        ParameterValueType.String,
                        true),
                    new CommandArgumentDefinition(
                        InteractionCommandNames.DurationSecondsArgument,
                        "Duration",
                        ParameterValueType.Number,
                        true),
                    new CommandArgumentDefinition(
                        InteractionCommandNames.RequiredCountArgument,
                        "Required Count",
                        ParameterValueType.Number),
                    new CommandArgumentDefinition(
                        InteractionCommandNames.PromptTextKeyArgument,
                        "Prompt",
                        ParameterValueType.String,
                        true),
                },
                new[]
                {
                    InteractionCommandNames.SuccessOutcome,
                    InteractionCommandNames.FailOutcome,
                });
        }

        private static CommandDefinition CreateUnlockCommandDefinition()
        {
            return new CommandDefinition(
                InteractionCommandNames.Unlock,
                "Unlock",
                true,
                new[]
                {
                    new CommandArgumentDefinition(
                        InteractionCommandNames.UnlockIdArgument,
                        "Unlock Id",
                        ParameterValueType.String,
                        true),
                    new CommandArgumentDefinition(
                        InteractionCommandNames.PuzzleTypeArgument,
                        "Puzzle Type",
                        ParameterValueType.Option,
                        true,
                        options: new[]
                        {
                            InteractionCommandNames.PuzzleTypeLineConnect,
                            InteractionCommandNames.PuzzleTypeNodeUnlock,
                            InteractionCommandNames.PuzzleTypeCustom,
                        }),
                    new CommandArgumentDefinition(
                        InteractionCommandNames.PromptTextKeyArgument,
                        "Prompt",
                        ParameterValueType.String,
                        true),
                },
                new[]
                {
                    InteractionCommandNames.SuccessOutcome,
                    InteractionCommandNames.FailOutcome,
                });
        }

        private static global::GameDeveloperKit.Story.Model.Command CreateImageCommand(string commandId)
        {
            return new global::GameDeveloperKit.Story.Model.Command(
                commandId,
                MediaCommandNames.ShowImage,
                new ArgumentBag(
                    new Dictionary<string, Value>(StringComparer.Ordinal)
                    {
                        [MediaCommandNames.ImageArgument] = Value.FromString(SampleImagePath),
                    }));
        }

        private static string GetButtonText(Button button)
        {
            var text = button.GetComponentInChildren<TMP_Text>(true);
            return text != null ? text.text : null;
        }

        private static Button FindChildButton(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            var buttons = root.GetComponentsInChildren<Button>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && string.Equals(buttons[i].name, name, StringComparison.Ordinal))
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private static void AssertFrame(Frame frame, string chapterId, string stepId)
        {
            Assert.IsNotNull(frame);
            Assert.AreEqual(chapterId, frame.Chapter.ChapterId);
            Assert.AreEqual(stepId, frame.AnchorStep.StepId);
        }

        private static void AssertEventOrder(IReadOnlyList<string> events, string earlier, string later)
        {
            var earlierIndex = IndexOf(events, earlier);
            var laterIndex = IndexOf(events, later);
            Assert.IsTrue(earlierIndex >= 0, $"Missing event: {earlier}");
            Assert.IsTrue(laterIndex >= 0, $"Missing event: {later}");
            Assert.Less(earlierIndex, laterIndex, $"{earlier} should happen before {later}");
        }

        private static int IndexOf(IReadOnlyList<string> events, string value)
        {
            for (var i = 0; i < events.Count; i++)
            {
                if (string.Equals(events[i], value, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private sealed class RecordingInteractionChannel : IInteractionChannel, IUnlockStateProvider
        {
            private readonly Func<InteractionRequest, PlaybackSurfaceView> m_SurfaceFactory;
            private readonly Dictionary<InteractionRequestKind, int> m_RequestCounts =
                new Dictionary<InteractionRequestKind, int>();
            private readonly Dictionary<string, bool> m_UnlockStates = new Dictionary<string, bool>(StringComparer.Ordinal);
            private readonly bool m_YieldAwake;

            public RecordingInteractionChannel(
                Func<InteractionRequest, PlaybackSurfaceView> surfaceFactory,
                bool yieldAwake = false)
            {
                m_SurfaceFactory = surfaceFactory;
                m_YieldAwake = yieldAwake;
            }

            public List<string> Events { get; } = new List<string>();

            public bool AwakeTokenCanBeCanceled { get; private set; }

            public bool RejectUnlockWrites { get; set; }

            public bool UnlockWriteAttempted { get; private set; }

            public async UniTask OnAwake(InteractionContext context, CancellationToken cancellationToken)
            {
                Events.Add("awake:start");
                AwakeTokenCanBeCanceled = cancellationToken.CanBeCanceled;
                if (m_YieldAwake)
                {
                    await UniTask.Yield();
                }

                Events.Add("awake:end");
            }

            public void OnStoryStarted(InteractionContext context)
            {
                Events.Add("started");
            }

            public void OnChapterChanged(ChapterInteractionContext context)
            {
                Events.Add("chapter:" + context.Chapter.ChapterId);
            }

            public void OnFrameChanged(Frame frame)
            {
                Events.Add("frame:" + GetFrameKey(frame));
            }

            public PlaybackSurfaceView GetPlaybackSurfaceView(InteractionRequest request)
            {
                if (m_RequestCounts.ContainsKey(request.Kind) is false)
                {
                    m_RequestCounts[request.Kind] = 0;
                }

                m_RequestCounts[request.Kind]++;
                Events.Add("surface:" + request.Kind + ":" + GetFrameKey(request.Frame));
                return m_SurfaceFactory?.Invoke(request);
            }

            public int GetRequestCount(InteractionRequestKind kind)
            {
                return m_RequestCounts.TryGetValue(kind, out var count) ? count : 0;
            }

            public void Tick(float deltaTime)
            {
            }

            public void OnStoryStopped()
            {
                Events.Add("stopped");
            }

            public bool TryGetUnlockState(string unlockId, out bool unlocked)
            {
                if (string.IsNullOrWhiteSpace(unlockId))
                {
                    unlocked = false;
                    return false;
                }

                return m_UnlockStates.TryGetValue(unlockId, out unlocked);
            }

            public bool TrySetUnlockState(string unlockId, bool unlocked, out string errorMessage)
            {
                UnlockWriteAttempted = true;
                if (RejectUnlockWrites)
                {
                    errorMessage = "rejected";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(unlockId))
                {
                    errorMessage = "Unlock id cannot be empty.";
                    return false;
                }

                m_UnlockStates[unlockId] = unlocked;
                errorMessage = null;
                return true;
            }

            public void Dispose()
            {
            }

            private static string GetFrameKey(Frame frame)
            {
                if (frame == null)
                {
                    return "null";
                }

                return frame.Chapter.ChapterId + ":" + frame.AnchorStep.StepId;
            }
        }
    }
}
