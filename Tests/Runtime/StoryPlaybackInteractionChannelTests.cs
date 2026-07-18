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
                var handler = new StoryQteCommandHandler(() => root);
                var handle = handler.Execute(command, new StoryRuntimeContext(null, null, null, 0d, null, null));
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

        private StoryPlayerView CreatePlayerView(StoryModule module)
        {
            var gameObject = new GameObject("StoryInteractionPlayerView");
            m_GameObjects.Add(gameObject);
            var view = gameObject.AddComponent<StoryPlayerView>();
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

        private static StoryProgram CreateLineChoiceProgram(string storyId)
        {
            return new StoryProgram(
                storyId,
                "1",
                "chapter_01",
                CreateChoiceChapters("line"));
        }

        private static StoryProgram CreateChoiceToChapterProgram(string storyId)
        {
            return new StoryProgram(
                storyId,
                "1",
                "chapter_01",
                CreateChoiceChapters("choice"));
        }

        private static IReadOnlyList<StoryChapter> CreateChoiceChapters(string chapterOneEntry)
        {
            return new[]
            {
                new StoryChapter(
                    "chapter_01",
                    "Chapter 01",
                    chapterOneEntry,
                    new[]
                    {
                        new StoryStep(
                            "line",
                            StoryStepKind.Line,
                            new StoryStepData(
                                "line.one",
                                "speaker.one",
                                target: StoryTarget.Step("chapter_01", "choice"))),
                        new StoryStep(
                            "choice",
                            StoryStepKind.Choice,
                            new StoryStepData(
                                choices: new[]
                                {
                                    new StoryChoice("choice_yes", "choice.yes", null, StoryTarget.Step("chapter_02", "line_yes")),
                                    new StoryChoice("choice_no", "choice.no", null, StoryTarget.Step("chapter_02", "line_no")),
                                })),
                    }),
                new StoryChapter(
                    "chapter_02",
                    "Chapter 02",
                    "line_yes",
                    new[]
                    {
                        new StoryStep(
                            "line_yes",
                            StoryStepKind.Line,
                            new StoryStepData("line.yes", target: StoryTarget.Step("chapter_02", "end"))),
                        new StoryStep(
                            "line_no",
                            StoryStepKind.Line,
                            new StoryStepData("line.no", target: StoryTarget.Step("chapter_02", "end"))),
                        new StoryStep("end", StoryStepKind.End),
                    }),
            };
        }

        private static StoryProgram CreateVideoProgram(string storyId)
        {
            return new StoryProgram(
                storyId,
                "1",
                "chapter_video",
                new[]
                {
                    new StoryChapter(
                        "chapter_video",
                        "Chapter Video",
                        "video",
                        new[]
                        {
                            new StoryStep(
                                "video",
                                StoryStepKind.Command,
                                new StoryStepData(command: CreateVideoCommand("video"))),
                        }),
                });
        }

        private static StoryProgram CreateParallelWaitChoiceVideoProgram(string storyId)
        {
            return new StoryProgram(
                storyId,
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "Chapter 01",
                        "line_intro",
                        new[]
                        {
                            new StoryStep(
                                "line_intro",
                                StoryStepKind.Line,
                                new StoryStepData(
                                    "line.intro",
                                    "speaker.intro",
                                    target: StoryTarget.Step("chapter_01", "parallel"))),
                            new StoryStep(
                                "parallel",
                                StoryStepKind.Parallel,
                                new StoryStepData(
                                    branches: new[]
                                    {
                                        new StoryParallelBranch("branch_video", "Video", StoryTarget.Step("chapter_01", "video")),
                                        new StoryParallelBranch("branch_interaction", "Interaction", StoryTarget.Step("chapter_01", "wait_choice")),
                                    })),
                            new StoryStep(
                                "video",
                                StoryStepKind.Command,
                                new StoryStepData(command: CreateVideoCommand("video"))),
                            new StoryStep(
                                "wait_choice",
                                StoryStepKind.Wait,
                                new StoryStepData(waitSeconds: 1.5d, target: StoryTarget.Step("chapter_01", "choice"))),
                            new StoryStep(
                                "choice",
                                StoryStepKind.Choice,
                                new StoryStepData(
                                    choices: new[]
                                    {
                                        new StoryChoice("choice_continue", "choice.continue", null, StoryTarget.Step("chapter_01", "after_choice")),
                                    })),
                            new StoryStep(
                                "after_choice",
                                StoryStepKind.Line,
                                new StoryStepData("after.choice")),
                            new StoryStep("end", StoryStepKind.End),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition(
                        StoryMediaCommandNames.PlayVideo,
                        "Play Video",
                        true,
                        CreatePlayVideoArgumentDefinitions(),
                        new[] { StoryMediaCommandNames.CompletedOutcome }),
                }));
        }

        private static StoryProgram CreateQteProgram(string storyId, double durationSeconds, int requiredCount)
        {
            return new StoryProgram(
                storyId,
                "1",
                "chapter_qte",
                new[]
                {
                    new StoryChapter(
                        "chapter_qte",
                        "Chapter QTE",
                        "qte",
                        new[]
                        {
                            new StoryStep(
                                "qte",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: CreateQteCommand(durationSeconds, requiredCount, true))),
                            new StoryStep(
                                "success_line",
                                StoryStepKind.Line,
                                new StoryStepData("qte.success")),
                            new StoryStep(
                                "fail_line",
                                StoryStepKind.Line,
                                new StoryStepData("qte.fail")),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    CreateQteCommandDefinition(),
                }));
        }

        private static StoryProgram CreateUnlockProgram(string storyId)
        {
            return new StoryProgram(
                storyId,
                "1",
                "chapter_unlock",
                new[]
                {
                    new StoryChapter(
                        "chapter_unlock",
                        "Chapter Unlock",
                        "unlock",
                        new[]
                        {
                            new StoryStep(
                                "unlock",
                                StoryStepKind.Command,
                                new StoryStepData(command: CreateUnlockCommand(true))),
                            new StoryStep(
                                "success_line",
                                StoryStepKind.Line,
                                new StoryStepData("unlock.success")),
                            new StoryStep(
                                "fail_line",
                                StoryStepKind.Line,
                                new StoryStepData("unlock.fail")),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    CreateUnlockCommandDefinition(),
                }));
        }

        private static StoryCommandArgumentDefinition[] CreatePlayVideoArgumentDefinitions()
        {
            return new[]
            {
                new StoryCommandArgumentDefinition(
                    StoryMediaCommandNames.VideoSourceArgument,
                    "Source",
                    ParameterValueType.Option,
                    true,
                    options: new[]
                    {
                        StoryMediaCommandNames.VideoSourceStreamingAssets,
                        StoryMediaCommandNames.VideoSourcePersistentDataPath,
                        StoryMediaCommandNames.VideoSourceNetworkStream
                    }),
                new StoryCommandArgumentDefinition(
                    StoryMediaCommandNames.ClipArgument,
                    "Clip",
                    ParameterValueType.AssetReference,
                    true,
                    "video")
            };
        }

        private static StoryFrame CreateMediaFrame()
        {
            var videoStep = new StoryStep(
                "video",
                StoryStepKind.Command,
                new StoryStepData(command: CreateVideoCommand("video")));
            var imageStep = new StoryStep(
                "image",
                StoryStepKind.Command,
                new StoryStepData(command: CreateImageCommand("image")));
            var chapter = new StoryChapter(
                "chapter_media",
                "Chapter Media",
                "video",
                new[] { videoStep, imageStep });
            var program = new StoryProgram(
                "story_media_frame",
                "1",
                "chapter_media",
                new[] { chapter });

            return new StoryFrame(
                program,
                chapter,
                videoStep,
                new[]
                {
                    StoryFrameTrack.CreateCommand(videoStep),
                    StoryFrameTrack.CreateCommand(imageStep),
                });
        }

        private static StoryFrame CreateVideoQteFrame()
        {
            var videoStep = new StoryStep(
                "video",
                StoryStepKind.Command,
                new StoryStepData(command: CreateVideoCommand("video")));
            var qteStep = new StoryStep(
                "qte",
                StoryStepKind.Command,
                new StoryStepData(command: CreateQteCommand(3d, 5, false)));
            var parallelStep = new StoryStep(
                "parallel",
                StoryStepKind.Parallel,
                new StoryStepData(
                    branches: new[]
                    {
                        new StoryParallelBranch("branch_video", "Video", StoryTarget.Step("chapter_qte", "video")),
                        new StoryParallelBranch("branch_interaction", "Interaction", StoryTarget.Step("chapter_qte", "qte")),
                    }));
            var chapter = new StoryChapter(
                "chapter_qte",
                "Chapter QTE",
                "parallel",
                new[] { parallelStep, videoStep, qteStep });
            var program = new StoryProgram(
                "story_qte_frame",
                "1",
                "chapter_qte",
                new[] { chapter });

            return new StoryFrame(
                program,
                chapter,
                parallelStep,
                new[]
                {
                    StoryFrameTrack.CreateCommand(videoStep, "branch_video", "Video"),
                    StoryFrameTrack.CreateCommand(qteStep, "branch_interaction", "Interaction"),
                },
                null,
                false,
                true);
        }

        private static StoryFrame CreateVideoUnlockFrame()
        {
            var videoStep = new StoryStep(
                "video",
                StoryStepKind.Command,
                new StoryStepData(command: CreateVideoCommand("video")));
            var unlockStep = new StoryStep(
                "unlock",
                StoryStepKind.Command,
                new StoryStepData(command: CreateUnlockCommand(false)));
            var parallelStep = new StoryStep(
                "parallel",
                StoryStepKind.Parallel,
                new StoryStepData(
                    branches: new[]
                    {
                        new StoryParallelBranch("branch_video", "Video", StoryTarget.Step("chapter_unlock", "video")),
                        new StoryParallelBranch("branch_interaction", "Interaction", StoryTarget.Step("chapter_unlock", "unlock")),
                    }));
            var chapter = new StoryChapter(
                "chapter_unlock",
                "Chapter Unlock",
                "parallel",
                new[] { parallelStep, videoStep, unlockStep });
            var program = new StoryProgram(
                "story_unlock_frame",
                "1",
                "chapter_unlock",
                new[] { chapter });

            return new StoryFrame(
                program,
                chapter,
                parallelStep,
                new[]
                {
                    StoryFrameTrack.CreateCommand(videoStep, "branch_video", "Video"),
                    StoryFrameTrack.CreateCommand(unlockStep, "branch_interaction", "Interaction"),
                },
                null,
                false,
                true);
        }

        private static StoryCommand CreateVideoCommand(string commandId)
        {
            return new StoryCommand(
                commandId,
                StoryMediaCommandNames.PlayVideo,
                new StoryArgumentBag(
                    new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                    {
                        [StoryMediaCommandNames.VideoSourceArgument] = StoryValue.FromString(StoryMediaCommandNames.VideoSourceNetworkStream),
                        [StoryMediaCommandNames.ClipArgument] = StoryValue.FromString("invalid"),
                    }),
                true,
                new[] { StoryMediaCommandNames.CompletedOutcome });
        }

        private static StoryCommand CreateQteCommand(double durationSeconds, int requiredCount, bool includeOutcomeTargets)
        {
            var outcomeTargets = includeOutcomeTargets
                ? new Dictionary<string, StoryTarget>(StringComparer.Ordinal)
                {
                    [StoryInteractionCommandNames.SuccessOutcome] = StoryTarget.Step("chapter_qte", "success_line"),
                    [StoryInteractionCommandNames.FailOutcome] = StoryTarget.Step("chapter_qte", "fail_line"),
                }
                : null;
            return new StoryCommand(
                "qte",
                StoryInteractionCommandNames.Qte,
                new StoryArgumentBag(
                    new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                    {
                        [StoryInteractionCommandNames.InputActionIdArgument] = StoryValue.FromString("space"),
                        [StoryInteractionCommandNames.DurationSecondsArgument] = StoryValue.FromNumber(durationSeconds),
                        [StoryInteractionCommandNames.RequiredCountArgument] = StoryValue.FromNumber(requiredCount),
                        [StoryInteractionCommandNames.PromptTextKeyArgument] = StoryValue.FromString("qte.break_free"),
                    }),
                true,
                new[]
                {
                    StoryInteractionCommandNames.SuccessOutcome,
                    StoryInteractionCommandNames.FailOutcome,
                },
                outcomeTargets);
        }

        private static StoryCommand CreateUnlockCommand(bool includeOutcomeTargets)
        {
            var outcomeTargets = includeOutcomeTargets
                ? new Dictionary<string, StoryTarget>(StringComparer.Ordinal)
                {
                    [StoryInteractionCommandNames.SuccessOutcome] = StoryTarget.Step("chapter_unlock", "success_line"),
                    [StoryInteractionCommandNames.FailOutcome] = StoryTarget.Step("chapter_unlock", "fail_line"),
                }
                : null;
            return new StoryCommand(
                "unlock",
                StoryInteractionCommandNames.Unlock,
                new StoryArgumentBag(
                    new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                    {
                        [StoryInteractionCommandNames.UnlockIdArgument] = StoryValue.FromString("chapter_unlock.door"),
                        [StoryInteractionCommandNames.PuzzleTypeArgument] = StoryValue.FromString(StoryInteractionCommandNames.PuzzleTypeNodeUnlock),
                        [StoryInteractionCommandNames.PromptTextKeyArgument] = StoryValue.FromString("unlock.door"),
                    }),
                true,
                new[]
                {
                    StoryInteractionCommandNames.SuccessOutcome,
                    StoryInteractionCommandNames.FailOutcome,
                },
                outcomeTargets);
        }

        private static StoryCommandDefinition CreateQteCommandDefinition()
        {
            return new StoryCommandDefinition(
                StoryInteractionCommandNames.Qte,
                "QTE",
                true,
                new[]
                {
                    new StoryCommandArgumentDefinition(
                        StoryInteractionCommandNames.InputActionIdArgument,
                        "Input",
                        ParameterValueType.String,
                        true),
                    new StoryCommandArgumentDefinition(
                        StoryInteractionCommandNames.DurationSecondsArgument,
                        "Duration",
                        ParameterValueType.Number,
                        true),
                    new StoryCommandArgumentDefinition(
                        StoryInteractionCommandNames.RequiredCountArgument,
                        "Required Count",
                        ParameterValueType.Number),
                    new StoryCommandArgumentDefinition(
                        StoryInteractionCommandNames.PromptTextKeyArgument,
                        "Prompt",
                        ParameterValueType.String,
                        true),
                },
                new[]
                {
                    StoryInteractionCommandNames.SuccessOutcome,
                    StoryInteractionCommandNames.FailOutcome,
                });
        }

        private static StoryCommandDefinition CreateUnlockCommandDefinition()
        {
            return new StoryCommandDefinition(
                StoryInteractionCommandNames.Unlock,
                "Unlock",
                true,
                new[]
                {
                    new StoryCommandArgumentDefinition(
                        StoryInteractionCommandNames.UnlockIdArgument,
                        "Unlock Id",
                        ParameterValueType.String,
                        true),
                    new StoryCommandArgumentDefinition(
                        StoryInteractionCommandNames.PuzzleTypeArgument,
                        "Puzzle Type",
                        ParameterValueType.Option,
                        true,
                        options: new[]
                        {
                            StoryInteractionCommandNames.PuzzleTypeLineConnect,
                            StoryInteractionCommandNames.PuzzleTypeNodeUnlock,
                            StoryInteractionCommandNames.PuzzleTypeCustom,
                        }),
                    new StoryCommandArgumentDefinition(
                        StoryInteractionCommandNames.PromptTextKeyArgument,
                        "Prompt",
                        ParameterValueType.String,
                        true),
                },
                new[]
                {
                    StoryInteractionCommandNames.SuccessOutcome,
                    StoryInteractionCommandNames.FailOutcome,
                });
        }

        private static StoryCommand CreateImageCommand(string commandId)
        {
            return new StoryCommand(
                commandId,
                StoryMediaCommandNames.ShowImage,
                new StoryArgumentBag(
                    new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                    {
                        [StoryMediaCommandNames.ImageArgument] = StoryValue.FromString(SampleImagePath),
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

        private static void AssertFrame(StoryFrame frame, string chapterId, string stepId)
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

            public void OnFrameChanged(StoryFrame frame)
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

            private static string GetFrameKey(StoryFrame frame)
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
