using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story;
using GameDeveloperKit.UI;
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
        private readonly List<PlaybackView> m_Views = new List<PlaybackView>();

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
                for (var i = 0; i < m_Views.Count; i++)
                {
                    m_Views[i]?.Release();
                }

                m_Views.Clear();
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
        public void PlaybackView_WhenBusinessDerived_CanOverrideWindowLifecycle()
        {
            var view = new BusinessPlaybackView();

            view.OnOpenAsync().GetAwaiter().GetResult();

            Assert.IsFalse(typeof(PlaybackView).IsSealed);
            Assert.IsTrue(view.Opened);
            Assert.IsTrue(typeof(UIWindow).IsAssignableFrom(typeof(BusinessPlaybackView)));
            var option = (UIOption)Attribute.GetCustomAttribute(
                typeof(BusinessPlaybackView),
                typeof(UIOption),
                false);
            Assert.IsNotNull(option);
            Assert.AreEqual("Assets/Bundles/Playback/PlaybackView.prefab", option.Path);
            AssertProtectedVirtual(BusinessPlaybackView.EpisodeCompletedMethod);
            AssertProtectedVirtual(BusinessPlaybackView.EpisodeChangedMethod);
            AssertProtectedVirtual(BusinessPlaybackView.VideoPlaybackStartedMethod);
            AssertProtectedVirtual(OperationPlaybackView.OnPlaybackAwakeMethod);
            AssertProtectedVirtual(OperationPlaybackView.ShowOperationMethod);
            AssertProtectedVirtual(OperationPlaybackView.ClearOperationsMethod);
        }

        [UnityTest]
        public IEnumerator PlaybackView_WhenOperationsOverridden_UsesBusinessDisplayInsteadOfDefaultPrefab()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var view = CreateUnconfiguredPlaybackView<OperationPlaybackView>();
                var surface = CreateSurface("BusinessPlaybackSurface", 0);
                view.Surface = surface;
                view.SetTextResolver(new PassthroughTextResolver());
                var frame = CreateCompositeOperationFrame();
                using (var presenter = new Presenter(module))
                {
                    await view.AwakeForTestAsync(new InteractionContext(
                        module,
                        presenter,
                        frame.Program.StoryId,
                        frame.Program));
                }

                SetPrivateField(view, "m_CurrentEpisode", frame.Episode);
                view.Present(frame, null);

                Assert.AreEqual(1, view.AwakeCount);
                Assert.AreEqual(1, view.OperationFrames.Count);
                var operations = view.OperationFrames[0];
                var dialogue = FindOperation(operations, PlaybackOperationKind.Dialogue);
                var choices = FindOperation(operations, PlaybackOperationKind.Choices);
                Assert.IsNotNull(dialogue);
                Assert.AreEqual("dialogue.speaker", dialogue.Speaker);
                Assert.AreEqual("dialogue.text", dialogue.Text);
                Assert.IsNotNull(choices);
                Assert.AreEqual(2, choices.Choices.Count);
                Assert.AreEqual("choice.a", choices.Choices[0].Text);
                Assert.AreEqual("choice.b", choices.Choices[1].Text);

                var dialogueRoot = view.GameObject.transform.Find("DialoguePanel");
                Assert.IsNotNull(dialogueRoot);
                Assert.IsFalse(dialogueRoot.gameObject.activeSelf);
                Assert.AreSame(surface.VideoOutput, GetPrivateField<RawImage>(view, "m_CurrentVideoOutput"));

                view.Clear(frame);
                view.Present(
                    Frame.CreateText(frame.Program, frame.Volume, frame.Episode, frame.AnchorStep),
                    null);

                Assert.AreSame(surface.VideoOutput, GetPrivateField<RawImage>(view, "m_CurrentVideoOutput"));
                Assert.AreEqual(1, view.ClearCount);
                view.StopPlayback();
                Assert.IsNull(GetPrivateField<PlaybackSurfaceView>(view, "m_CustomPlaybackSurface"));
            });
        }

        [Test]
        public void PlaybackView_WhenCompositeFramePresented_DispatchesEveryOperationKindTogether()
        {
            var view = CreateUnconfiguredPlaybackView<OperationPlaybackView>();
            view.SetTextResolver(new PassthroughTextResolver());
            var frame = CreateCompositeOperationFrame();
            SetPrivateField(view, "m_CurrentEpisode", frame.Episode);

            view.Present(frame, null);

            Assert.AreEqual(1, view.OperationFrames.Count);
            var operations = view.OperationFrames[0];
            AssertOperationKinds(
                operations,
                PlaybackOperationKind.Dialogue,
                PlaybackOperationKind.Narration,
                PlaybackOperationKind.Video,
                PlaybackOperationKind.Image,
                PlaybackOperationKind.Audio,
                PlaybackOperationKind.Command,
                PlaybackOperationKind.Wait,
                PlaybackOperationKind.Choices);
            Assert.AreEqual(1.25d, FindOperation(operations, PlaybackOperationKind.Wait).WaitSeconds);

            view.Present(
                Frame.CreateText(frame.Program, frame.Volume, frame.Episode, frame.AnchorStep),
                null);
            Assert.IsNotNull(FindOperation(view.OperationFrames[1], PlaybackOperationKind.Continue));

            view.Present(
                Frame.CreateCompleted(frame.Program, frame.Volume, frame.Episode, frame.AnchorStep, "completed"),
                null);
            Assert.IsNotNull(FindOperation(view.OperationFrames[2], PlaybackOperationKind.Completed));
        }

        [UnityTest]
        public IEnumerator PlaybackView_WhenBusinessDerived_ReceivesEpisodeLifecycleWithoutChannelRegistration()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var view = CreatePlaybackView<BusinessPlaybackView>(module);

                await view.PlayAsync(CreateChoiceToEpisodeProgram("story_business_playback_lifecycle"));
                view.Select("choice_no");

                Assert.IsNull(view.LastError);
                Assert.AreEqual(1, view.Completions.Count);
                Assert.AreEqual("episode_01", view.Completions[0].EpisodeId);
                Assert.AreEqual("choice_no", view.Completions[0].ExitId);
                Assert.AreEqual(2, view.EpisodeChanges.Count);
                Assert.IsNull(view.EpisodeChanges[0].PreviousEpisode);
                Assert.AreEqual("episode_01", view.EpisodeChanges[0].Episode.EpisodeId);
                Assert.AreEqual("episode_01", view.EpisodeChanges[1].PreviousEpisode.EpisodeId);
                Assert.AreEqual("episode_02", view.EpisodeChanges[1].Episode.EpisodeId);
            });
        }

        [Test]
        public void PlaybackSurfaceView_WhenVideoSeekSurfaceMissing_KeepsOptionalSurfaceNull()
        {
            var surface = new PlaybackSurfaceView();

            Assert.IsNull(surface.VideoSeek);
        }

        [UnityTest]
        public IEnumerator PlaybackView_WhenDefaultSurfaceCreated_ProvidesPrefabMediaControls()
        {
            var module = CreateStartedModule();
            var view = CreatePlaybackView(module);

            var surface = view.CreateDefaultSurfaceView();

            Assert.IsNotNull(surface.VideoSeek);
            Assert.IsNotNull(surface.VideoSeek.Slider);
            Assert.IsNotNull(surface.VideoSeek.Root);
            Assert.IsNotNull(surface.VideoSeek.PauseButton);
            Assert.IsFalse(surface.VideoSeek.Root.gameObject.activeSelf);
            Assert.IsNotNull(surface.VideoQuality);
            Assert.IsNotNull(surface.VideoQuality.Button);
            Assert.IsNotNull(surface.VideoQuality.Label);
            Assert.IsNotNull(surface.VideoQuality.MenuRoot);
            Assert.IsNotNull(surface.VideoQuality.OptionsRoot);
            Assert.IsNotNull(surface.VideoQuality.OptionTemplate);
            Assert.IsFalse(surface.VideoQuality.Root.gameObject.activeSelf);
            yield break;
        }

        [Test]
        public void VideoQualityBinder_WhenMenuProvided_ShowsEveryQualityOption()
        {
            var root = CreateRoot("QualityMenuSurface");
            var button = CreateButton(root, "QualityButton");
            var label = button.GetComponentInChildren<TMP_Text>(true);
            var menuRoot = CreateRoot("QualityMenu");
            menuRoot.SetParent(root, false);
            var optionsRoot = CreateRoot("Options");
            optionsRoot.SetParent(menuRoot, false);
            var template = CreateButton(optionsRoot, "OptionTemplate");
            template.gameObject.SetActive(false);
            menuRoot.gameObject.SetActive(false);
            var playback = new VideoPlayableHandle(
                "https://cdn.example.com/auto.m3u8",
                new VideoPlayableOptions
                {
                    SupportsAutoQuality = true,
                    QualityOptions = new[]
                    {
                        new VideoQualityOption("HD", 1280, 720, 3000000, "https://cdn.example.com/720.m3u8"),
                        new VideoQualityOption("FHD", 1920, 1080, 6000000, "https://cdn.example.com/1080.m3u8")
                    }
                },
                false);
            var binder = new PlaybackView.VideoQualityBinder();

            try
            {
                binder.Bind(new VideoQualitySurface(
                    root,
                    button,
                    label,
                    menuRoot,
                    optionsRoot,
                    template), playback);

                button.onClick.Invoke();

                Assert.IsTrue(menuRoot.gameObject.activeSelf);
                var options = optionsRoot.GetComponentsInChildren<Button>(false);
                Assert.AreEqual(3, options.Length);
                CollectionAssert.AreEquivalent(
                    new[] { "自动", "720P", "1080P" },
                    new[]
                    {
                        GetButtonText(options[0]),
                        GetButtonText(options[1]),
                        GetButtonText(options[2])
                    });
            }
            finally
            {
                binder.Unbind();
                playback.Dispose();
            }
        }

        [Test]
        public void VideoQualityBinder_WhenSingleQualityProvided_ShowsDisabledQuality()
        {
            var root = CreateRoot("SingleQualitySurface");
            var button = CreateButton(root, "QualityButton");
            var label = button.GetComponentInChildren<TMP_Text>(true);
            var playback = new VideoPlayableHandle(
                "https://cdn.example.com/720.m3u8",
                new VideoPlayableOptions
                {
                    QualityOptions = new[]
                    {
                        new VideoQualityOption("HD", 1280, 720, 3000000, "https://cdn.example.com/720.m3u8")
                    }
                },
                false);
            var binder = new PlaybackView.VideoQualityBinder();
            root.gameObject.SetActive(false);

            try
            {
                binder.Bind(new VideoQualitySurface(root, button, label), playback);

                Assert.IsTrue(root.gameObject.activeSelf);
                Assert.IsFalse(button.interactable);
                Assert.AreEqual("720P", label.text);
            }
            finally
            {
                binder.Unbind();
                playback.Dispose();
            }
        }

        [Test]
        public void VideoQualityBinder_WhenSwitchIsPending_RefreshKeepsButtonDisabled()
        {
            var root = CreateRoot("SwitchingQualitySurface");
            var button = CreateButton(root, "QualityButton");
            var label = button.GetComponentInChildren<TMP_Text>(true);
            var playback = new VideoPlayableHandle(
                "https://cdn.example.com/auto.m3u8",
                new VideoPlayableOptions
                {
                    SupportsAutoQuality = true,
                    QualityOptions = new[]
                    {
                        new VideoQualityOption("HD", 1280, 720, 3000000, "https://cdn.example.com/720.m3u8"),
                        new VideoQualityOption("FHD", 1920, 1080, 6000000, "https://cdn.example.com/1080.m3u8")
                    }
                },
                false);
            var binder = new PlaybackView.VideoQualityBinder();
            var surface = new VideoQualitySurface(root, button, label);

            try
            {
                binder.Bind(surface, playback);
                var switchingField = typeof(PlaybackView.VideoQualityBinder).GetField(
                    "m_IsSwitching",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.IsNotNull(switchingField);
                switchingField.SetValue(binder, true);
                button.interactable = false;

                binder.Bind(surface, playback);

                Assert.IsFalse(button.interactable);
            }
            finally
            {
                binder.Unbind();
                playback.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator PlaybackView_WithRegisteredChannel_UsesLifecycleAndInputSurfaces()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryInteractionSurface", 2);
                var channel = new RecordingInteractionChannel(_ => surface, true);
                module.SetInteractions(channel);
                var view = CreatePlaybackView(module);

                await view.PlayAsync(CreateLineChoiceProgram("story_interaction_lifecycle"));

                AssertFrame(view.CurrentFrame, "episode_01", "line");
                Assert.IsNull(view.LastError);
                Assert.IsTrue(channel.AwakeTokenCanBeCanceled);
                AssertEventOrder(channel.Events, "awake:start", "awake:end");
                AssertEventOrder(channel.Events, "awake:end", "started");
                AssertEventOrder(channel.Events, "started", "episode:episode_01");
                AssertEventOrder(channel.Events, "episode:episode_01", "surface:Text:episode_01:line");
                AssertEventOrder(channel.Events, "frame:episode_01:line", "surface:Text:episode_01:line");
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
        public IEnumerator PlaybackView_WhenChoiceMovesToNextEpisode_NotifiesBeforeSurfaceQuery()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryEpisodeSurface", 2);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlaybackView(module);

                await view.PlayAsync(CreateChoiceToEpisodeProgram("story_episode_switch"));
                surface.ChoiceButtons[1].onClick.Invoke();

                AssertFrame(view.CurrentFrame, "episode_02", "line_no");
                Assert.IsNull(view.LastError);
                AssertEventOrder(channel.Events, "episode:episode_02", "surface:Text:episode_02:line_no");
            });
        }

        [UnityTest]
        public IEnumerator PlaybackView_WhenCommandFramePresented_RequestsVideoAndImageSurfaces()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryMediaSurface", 0);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlaybackView(module);
                var frame = CreateMediaFrame();

                view.Present(frame, null);
                await UniTask.Yield();

                Assert.IsNull(view.LastError);
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Video));
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Image));
                AssertEventOrder(channel.Events, "frame:episode_media:video", "surface:Video:episode_media:video");
                AssertEventOrder(channel.Events, "frame:episode_media:video", "surface:Image:episode_media:video");
            });
        }

        [UnityTest]
        public IEnumerator PlaybackView_WhenInitialVideoIsPending_ShowsBlackVideoOutput()
        {
            return UniTask.ToCoroutine(() =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryInitialVideoSurface", 0);
                module.SetInteractions(new RecordingInteractionChannel(_ => surface));
                var view = CreatePlaybackView(module);

                view.Present(CreateMediaFrame(), null);

                Assert.AreSame(Texture2D.blackTexture, surface.VideoOutput.texture);
                Assert.IsTrue(surface.VideoOutput.gameObject.activeSelf);
                return UniTask.CompletedTask;
            });
        }

        [UnityTest]
        public IEnumerator PlaybackView_WhenPendingVideoOutputIsClearedAndHidden_RestoresBlackVideoOutput()
        {
            return UniTask.ToCoroutine(() =>
            {
                var module = CreateStartedModule();
                var view = CreatePlaybackView(module);
                var output = view.CreateDefaultSurfaceView().VideoOutput;

                output.texture = null;
                output.gameObject.SetActive(false);
                InvokePrivate(view, "UpdateVideoOutput");

                Assert.AreSame(Texture2D.blackTexture, output.texture);
                Assert.IsTrue(output.gameObject.activeSelf);
                return UniTask.CompletedTask;
            });
        }

        [UnityTest]
        public IEnumerator PlaybackView_WhenVideoFrameFollowsVideoFrame_RetainsPreviousTextureWhileNextVideoIsPending()
        {
            return UniTask.ToCoroutine(() =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryVideoTransitionSurface", 0);
                module.SetInteractions(new RecordingInteractionChannel(_ => surface));
                var view = CreatePlaybackView(module);
                var frame = CreateMediaFrame();

                view.Present(frame, null);
                surface.VideoOutput.texture = Texture2D.whiteTexture;
                surface.VideoOutput.gameObject.SetActive(true);

                view.Clear(frame);
                view.Present(frame, null);
                InvokePrivate(view, "UpdateVideoOutput");

                Assert.AreSame(Texture2D.whiteTexture, surface.VideoOutput.texture);
                Assert.IsTrue(surface.VideoOutput.gameObject.activeSelf);
                return UniTask.CompletedTask;
            });
        }

        [UnityTest]
        public IEnumerator PlaybackView_WhenNonVideoFrameFollowsVideoFrame_ClearsPreviousTexture()
        {
            return UniTask.ToCoroutine(() =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryNonVideoTransitionSurface", 0);
                module.SetInteractions(new RecordingInteractionChannel(_ => surface));
                var view = CreatePlaybackView(module);
                var frame = CreateMediaFrame();

                view.Present(frame, null);
                surface.VideoOutput.texture = Texture2D.whiteTexture;
                surface.VideoOutput.gameObject.SetActive(true);

                view.Clear(frame);
                view.Present(
                    Frame.CreateText(frame.Program, frame.Volume, frame.Episode, frame.AnchorStep),
                    null);

                Assert.IsNull(surface.VideoOutput.texture);
                Assert.IsFalse(surface.VideoOutput.gameObject.activeSelf);
                return UniTask.CompletedTask;
            });
        }

        [UnityTest]
        public IEnumerator PlaybackView_WhenStopped_ClearsRetainedCustomVideoOutput()
        {
            return UniTask.ToCoroutine(() =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryStoppedVideoSurface", 0);
                module.SetInteractions(new RecordingInteractionChannel(_ => surface));
                var view = CreatePlaybackView(module);
                var frame = CreateMediaFrame();

                view.Present(frame, null);
                surface.VideoOutput.texture = Texture2D.whiteTexture;
                surface.VideoOutput.gameObject.SetActive(true);

                view.StopPlayback();

                Assert.IsNull(surface.VideoOutput.texture);
                Assert.IsFalse(surface.VideoOutput.gameObject.activeSelf);
                return UniTask.CompletedTask;
            });
        }


        [UnityTest]
        public IEnumerator PlaybackView_WhenVideoPrewarmFails_DoesNotNotifyStartedOrQuerySurfaces()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryPrewarmFailureSurface", 0);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlaybackView(module);

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
        public IEnumerator PlaybackView_WhenInitialVideoPrewarmStarts_ShowsBlackBeforeOpeningMedia()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var view = CreatePlaybackView(module);
                var output = view.CreateDefaultSurfaceView().VideoOutput;

                var playback = view.PlayAsync(CreateVideoProgram("story_video_initial_placeholder"));

                Assert.AreSame(Texture2D.blackTexture, output.texture);
                Assert.IsTrue(output.gameObject.activeSelf);

                await playback;
            });
        }

        [UnityTest]
        public IEnumerator PlaybackView_WhenRequiredVideoSurfaceMissing_ThrowsConfigurationError()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryMissingVideoSurface", 0, false);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlaybackView(module);

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
        public IEnumerator PlaybackView_WhenChoiceButtonCountMismatches_ReportsConfigurationError()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryChoiceMismatchSurface", 1);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlaybackView(module);

                await view.PlayAsync(CreateChoiceToEpisodeProgram("story_choice_mismatch"));

                Assert.IsNotNull(view.LastError);
                StringAssert.Contains("choice button count does not match", view.LastError.Message);
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Choice));
            });
        }


        [UnityTest]
        public IEnumerator PlaybackView_WhenParallelWaitChoicePresented_RequestsVideoAndChoiceSurfaces()
        {
            return UniTask.ToCoroutine(() =>
            {
                var module = CreateStartedModule();
                var surface = CreateSurface("StoryParallelWaitChoiceSurface", 1);
                var channel = new RecordingInteractionChannel(_ => surface);
                module.SetInteractions(channel);
                var view = CreatePlaybackView(module);
                var program = CreateParallelWaitChoiceVideoProgram("story_playback_parallel_wait_choice");

                module.Register(program);
                var runner = module.StartProgram(program.StoryId);
                view.Present(runner.CurrentFrame, null);

                AssertFrame(view.CurrentFrame, "episode_01", "line_intro");
                Assert.IsTrue(surface.ContinueButton.gameObject.activeSelf);

                var initialParallelFrame = module.Continue();
                view.Present(initialParallelFrame, null);

                AssertFrame(view.CurrentFrame, "episode_01", "parallel");
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Video));
                Assert.IsFalse(surface.ContinueButton.gameObject.activeSelf);

                var choiceFrame = module.Evaluate(1.5d);
                view.Present(choiceFrame, null);

                AssertFrame(view.CurrentFrame, "episode_01", "parallel");
                Assert.IsNull(view.LastError);
                Assert.AreEqual(2, channel.GetRequestCount(InteractionRequestKind.Video));
                Assert.AreEqual(1, channel.GetRequestCount(InteractionRequestKind.Choice));
                Assert.AreEqual("choice.continue", GetButtonText(surface.ChoiceButtons[0]));
                Assert.IsTrue(surface.ChoiceButtons[0].gameObject.activeSelf);
                Assert.IsFalse(surface.ContinueButton.gameObject.activeSelf);
                AssertEventOrder(channel.Events, "frame:episode_01:parallel", "surface:Choice:episode_01:parallel");
                AssertEventOrder(channel.Events, "frame:episode_01:parallel", "surface:Video:episode_01:parallel");
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

        private PlaybackView CreatePlaybackView(StoryModule module)
        {
            var view = CreatePlaybackViewInstance();
            m_GameObjects.Add(view.GameObject);
            m_Views.Add(view);
            view.ConfigureModules(module);
            return view;
        }

        private T CreatePlaybackView<T>(StoryModule module)
            where T : PlaybackView, new()
        {
            var view = CreatePlaybackViewInstance<T>();
            m_GameObjects.Add(view.GameObject);
            m_Views.Add(view);
            view.ConfigureModules(module);
            return view;
        }

        private T CreateUnconfiguredPlaybackView<T>()
            where T : PlaybackView, new()
        {
            var view = CreatePlaybackViewInstance<T>();
            m_GameObjects.Add(view.GameObject);
            m_Views.Add(view);
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
                choiceButtons);
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
            return StoryProgramTestFactory.Program(
                storyId,
                "1",
                "episode_01",
                CreateChoiceEpisodes("line"));
        }

        private static Program CreateChoiceToEpisodeProgram(string storyId)
        {
            return StoryProgramTestFactory.Program(
                storyId,
                "1",
                "episode_01",
                CreateChoiceEpisodes("choice"));
        }

        private static IReadOnlyList<Episode> CreateChoiceEpisodes(string episodeOneEntry)
        {
            return new[]
            {
                StoryProgramTestFactory.Episode(
                    "episode_01",
                    "Episode 01",
                    episodeOneEntry,
                    new[]
                    {
                        new Step(
                            "line",
                            StepKind.Line,
                            new StepData(
                                "line.one",
                                "speaker.one",
                                target: Target.Step("choice"))),
                        new Step(
                            "choice",
                            StepKind.Choice,
                            new StepData(
                                choices: new[]
                                {
                                    new Choice("choice_yes", "choice_yes", "choice.yes"),
                                    new Choice("choice_no", "choice_no", "choice.no"),
                                })),
                    }),
                StoryProgramTestFactory.Episode(
                    "episode_02",
                    "Episode 02",
                    "line_yes",
                    new[]
                    {
                        new Step(
                            "line_yes",
                            StepKind.Line,
                            new StepData("line.yes", target: Target.Step("end"))),
                        new Step(
                            "line_no",
                            StepKind.Line,
                            new StepData("line.no", target: Target.Step("end"))),
                        new Step("end", StepKind.End),
                    }),
            };
        }

        private static Program CreateVideoProgram(string storyId)
        {
            return StoryProgramTestFactory.Program(
                storyId,
                "1",
                "episode_video",
                new[]
                {
                    StoryProgramTestFactory.Episode(
                        "episode_video",
                        "Episode Video",
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

        private static Frame CreateCompositeOperationFrame()
        {
            var dialogue = new Step("dialogue", StepKind.Line, new StepData("dialogue.text", "dialogue.speaker"));
            var narration = new Step("narration", StepKind.Line, new StepData("narration.text"));
            var video = new Step("video", StepKind.Command, new StepData(command: CreateVideoCommand("video")));
            var image = new Step("image", StepKind.Command, new StepData(command: CreateImageCommand("image")));
            var audioCommand = new global::GameDeveloperKit.Story.Model.Command(
                "audio",
                MediaCommandNames.PlayAudio);
            var audio = new Step("audio", StepKind.Command, new StepData(command: audioCommand));
            var businessCommand = new global::GameDeveloperKit.Story.Model.Command(
                "business",
                "business_command");
            var business = new Step("business", StepKind.Command, new StepData(command: businessCommand));
            var wait = new Step("wait", StepKind.Wait, new StepData(waitSeconds: 1.25d));
            var choices = new[]
            {
                new Choice("choice_a", "choice_a", "choice.a"),
                new Choice("choice_b", "choice_b", "choice.b"),
            };
            var episode = StoryProgramTestFactory.Episode(
                "episode_operations",
                "Episode Operations",
                dialogue.StepId,
                new[] { dialogue, narration, video, image, audio, business, wait });
            var program = StoryProgramTestFactory.Program(
                "story_composite_operations",
                "1",
                episode.EpisodeId,
                new[] { episode });

            return new Frame(
                program,
                program.Volumes[0],
                episode,
                dialogue,
                new[]
                {
                    FrameTrack.CreateText(dialogue),
                    FrameTrack.CreateText(narration),
                    FrameTrack.CreateCommand(video),
                    FrameTrack.CreateCommand(image),
                    FrameTrack.CreateCommand(audio),
                    FrameTrack.CreateCommand(business),
                    FrameTrack.CreateWait(wait, 1.25d),
                },
                choices,
                waitsForChoice: true,
                waitsForTime: true);
        }

        private static Program CreateParallelWaitChoiceVideoProgram(string storyId)
        {
            return StoryProgramTestFactory.Program(
                storyId,
                "1",
                "episode_01",
                new[]
                {
                    StoryProgramTestFactory.Episode(
                        "episode_01",
                        "Episode 01",
                        "line_intro",
                        new[]
                        {
                            new Step(
                                "line_intro",
                                StepKind.Line,
                                new StepData(
                                    "line.intro",
                                    "speaker.intro",
                                    target: Target.Step("parallel"))),
                            new Step(
                                "parallel",
                                StepKind.Parallel,
                                new StepData(
                                    branches: new[]
                                    {
                                        new ParallelBranch("branch_video", "Video", Target.Step("video")),
                                        new ParallelBranch("branch_interaction", "Interaction", Target.Step("wait_choice")),
                                    })),
                            new Step(
                                "video",
                                StepKind.Command,
                                new StepData(command: CreateVideoCommand("video"))),
                            new Step(
                                "wait_choice",
                                StepKind.Wait,
                                new StepData(waitSeconds: 1.5d, target: Target.Step("choice"))),
                            new Step(
                                "choice",
                                StepKind.Choice,
                                new StepData(
                                    choices: new[]
                                    {
                                        new Choice("choice_continue", "choice_continue", "choice.continue"),
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
            var episode = StoryProgramTestFactory.Episode(
                "episode_media",
                "Episode Media",
                "video",
                new[] { videoStep, imageStep });
            var program = StoryProgramTestFactory.Program(
                "story_media_frame",
                "1",
                "episode_media",
                new[] { episode });

            return new Frame(
                program,
                program.Volumes[0],
                episode,
                videoStep,
                new[]
                {
                    FrameTrack.CreateCommand(videoStep),
                    FrameTrack.CreateCommand(imageStep),
                });
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

        private static void AssertFrame(Frame frame, string episodeId, string stepId)
        {
            Assert.IsNotNull(frame);
            Assert.AreEqual(episodeId, frame.Episode.EpisodeId);
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

        private static void AssertProtectedVirtual(string methodName)
        {
            var method = typeof(PlaybackView).GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            Assert.IsTrue(method.IsFamily, methodName);
            Assert.IsTrue(method.IsVirtual, methodName);
        }

        private static PlaybackOperation FindOperation(
            IReadOnlyList<PlaybackOperation> operations,
            PlaybackOperationKind kind)
        {
            for (var i = 0; i < operations.Count; i++)
            {
                if (operations[i].Kind == kind)
                {
                    return operations[i];
                }
            }

            return null;
        }

        private static void AssertOperationKinds(
            IReadOnlyList<PlaybackOperation> operations,
            params PlaybackOperationKind[] kinds)
        {
            for (var i = 0; i < kinds.Length; i++)
            {
                Assert.IsNotNull(FindOperation(operations, kinds[i]), kinds[i].ToString());
            }
        }

        private static T GetPrivateField<T>(PlaybackView view, string fieldName)
            where T : class
        {
            var field = typeof(PlaybackView).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            return field.GetValue(view) as T;
        }

        private static void SetPrivateField<T>(PlaybackView view, string fieldName, T value)
        {
            var field = typeof(PlaybackView).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(view, value);
        }

        private static void InvokePrivate(PlaybackView view, string methodName)
        {
            var method = typeof(PlaybackView).GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(view, null);
        }

        private sealed class RecordingInteractionChannel : IInteractionChannel
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

            public void OnEpisodeChanged(EpisodeInteractionContext context)
            {
                Events.Add("episode:" + context.Episode.EpisodeId);
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

                return frame.Episode.EpisodeId + ":" + frame.AnchorStep.StepId;
            }
        }

        [UIOption("Assets/Bundles/Playback/PlaybackView.prefab", 500, CacheEnabled = false)]
        private sealed class BusinessPlaybackView : PlaybackView
        {
            public const string EpisodeCompletedMethod = "OnEpisodeCompleted";
            public const string EpisodeChangedMethod = "OnEpisodeChanged";
            public const string VideoPlaybackStartedMethod = "OnVideoPlaybackStarted";

            public BusinessPlaybackView()
            {
            }

            public bool Opened { get; private set; }

            public List<EpisodeCompletion> Completions { get; } = new List<EpisodeCompletion>();

            public List<EpisodeInteractionContext> EpisodeChanges { get; } =
                new List<EpisodeInteractionContext>();

            public override UniTask OnOpenAsync()
            {
                Opened = true;
                return UniTask.CompletedTask;
            }

            protected override void OnEpisodeCompleted(EpisodeCompletion completion)
            {
                Completions.Add(completion);
            }

            protected override void OnEpisodeChanged(EpisodeInteractionContext context)
            {
                EpisodeChanges.Add(context);
            }

            protected override void OnVideoPlaybackStarted(VideoPlayableHandle playback)
            {
            }
        }

        [UIOption("Assets/Bundles/Playback/PlaybackView.prefab", 500, CacheEnabled = false)]
        private sealed class OperationPlaybackView : PlaybackView
        {
            public const string OnPlaybackAwakeMethod = "OnPlaybackAwakeAsync";
            public const string ShowOperationMethod = "ShowOperation";
            public const string ClearOperationsMethod = "ClearOperations";

            public PlaybackSurfaceView Surface { get; set; }

            public int AwakeCount { get; private set; }

            public int ClearCount { get; private set; }

            public List<PlaybackOperation[]> OperationFrames { get; } = new List<PlaybackOperation[]>();

            public UniTask AwakeForTestAsync(InteractionContext context)
            {
                return OnPlaybackAwakeAsync(context, CancellationToken.None);
            }

            protected override UniTask OnPlaybackAwakeAsync(
                InteractionContext context,
                CancellationToken cancellationToken)
            {
                AwakeCount++;
                if (Surface != null)
                {
                    SetPlaybackSurface(Surface);
                }

                return UniTask.CompletedTask;
            }

            protected override void ShowOperation(params PlaybackOperation[] operations)
            {
                OperationFrames.Add(operations);
            }

            protected override void ClearOperations()
            {
                ClearCount++;
                base.ClearOperations();
            }
        }

        private sealed class PassthroughTextResolver : GameDeveloperKit.Story.Text.ITextResolver
        {
            public string Resolve(GameDeveloperKit.Story.Text.TextReference reference)
            {
                return reference.Value;
            }
        }
    }
}
