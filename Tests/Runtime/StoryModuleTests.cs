using System;
using System.Collections.Generic;
using GameDeveloperKit.Config;
using GameDeveloperKit.Data;
using GameDeveloperKit.Localization;
using GameDeveloperKit.Procedure;
using GameDeveloperKit.Resource;
using GameDeveloperKit.Story;
using GameDeveloperKit.Timer;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryModuleTests : RuntimeTestBase
    {
        private const string SampleVideoSource = StoryMediaCommandNames.VideoSourceStreamingAssets;
        private const string SampleVideoPath = "Assets/StreamingAssets/videos/0.mp4";
        private const string SampleImagePath = "Assets/Bundles/Story/UI/test.jpg";
        private const string SampleAudioPath = "Assets/Bundles/Story/Sounds/bgm.mp3";

        [TearDown]
        public void TearDown()
        {
            TryUnregister<StoryModule>();
            TryUnregister<ConfigModule>();
            TryUnregister<DataModule>();
            TryUnregister<ProcedureModule>();
            TryUnregister<ResourceModule>();
            TryUnregister<LocalizationModule>();
            TryUnregister<TimerModule>();
        }

        [SetUp]
        public void SetUp()
        {
            App.Shutdown().GetAwaiter().GetResult();
        }

        [Test]
        public void TimelineBase_WhenEvaluated_ClampsTimeAndInvokesEvaluate()
        {
            var timeline = new TestTimeline(10f);

            timeline.Evaluate(12f);

            Assert.AreEqual(10f, timeline.CurrentTime);
            Assert.AreEqual(10f, timeline.LastEvaluatedTime);
        }

        [Test]
        public void AppStory_WhenAccessed_ReturnsStartedModuleWithoutRuntimeDependencies()
        {
            var module = App.Story;

            Assert.IsNotNull(module);
            Assert.IsTrue(App.TryGetRegistered<StoryModule>(out _));
            Assert.IsFalse(App.TryGetRegistered<ConfigModule>(out _));
            Assert.IsFalse(App.TryGetRegistered<DataModule>(out _));
            Assert.IsFalse(App.TryGetRegistered<ProcedureModule>(out _));
            Assert.IsFalse(App.TryGetRegistered<ResourceModule>(out _));
            Assert.IsFalse(App.TryGetRegistered<LocalizationModule>(out _));
            Assert.IsFalse(App.TryGetRegistered<TimerModule>(out _));
        }

        [Test]
        public void SessionUnlockStateProvider_WhenStateWritten_ReadsUnlockState()
        {
            var provider = new SessionUnlockStateProvider();

            Assert.IsFalse(provider.TryGetUnlockState("chapter_01.door", out var unlocked));
            Assert.IsFalse(unlocked);

            Assert.IsTrue(provider.TrySetUnlockState("chapter_01.door", true, out var errorMessage));
            Assert.IsNull(errorMessage);
            Assert.IsTrue(provider.TryGetUnlockState("chapter_01.door", out unlocked));
            Assert.IsTrue(unlocked);

            Assert.IsTrue(provider.TrySetUnlockState("chapter_01.door", false, out errorMessage));
            Assert.IsNull(errorMessage);
            Assert.IsTrue(provider.TryGetUnlockState("chapter_01.door", out unlocked));
            Assert.IsFalse(unlocked);

            Assert.IsFalse(provider.TrySetUnlockState(" ", true, out errorMessage));
            StringAssert.Contains("Unlock id", errorMessage);
        }

        [Test]
        public void NodeSchemaRegistry_WhenQueried_ReturnsCoreSemanticSchemas()
        {
            var playVideo = NodeSchemaRegistry.Get(NodeKind.PlayVideo);
            var choice = NodeSchemaRegistry.Get(NodeKind.Choice);

            Assert.AreEqual(NodeCategory.Action, playVideo.Category);
            Assert.AreEqual(NodeCategory.Interaction, choice.Category);
            Assert.IsTrue(HasPort(playVideo, "completed"));
            Assert.IsTrue(HasPort(choice, "selected"));
            var source = FindParameter(playVideo, StoryMediaCommandNames.VideoSourceArgument);
            Assert.IsNotNull(source);
            Assert.AreEqual(ParameterValueType.Option, source.ValueType);
            Assert.IsTrue(source.Required);
            CollectionAssert.AreEqual(
                new[]
                {
                    StoryMediaCommandNames.VideoSourceStreamingAssets,
                    StoryMediaCommandNames.VideoSourcePersistentDataPath,
                    StoryMediaCommandNames.VideoSourceNetworkStream
                },
                source.Options);
            Assert.IsTrue(NodeSchemaRegistry.IsDefaultAuthoringNode(NodeKind.PlayVideo));
            Assert.IsTrue(NodeSchemaRegistry.IsDefaultAuthoringNode(NodeKind.Choice));
            Assert.IsTrue(NodeSchemaRegistry.IsDefaultAuthoringNode(NodeKind.Parallel));
            Assert.IsTrue(NodeSchemaRegistry.IsDefaultAuthoringNode(NodeKind.Merge));
            Assert.AreEqual(15, NodeSchemaRegistry.Schemas.Count);
            foreach (var schema in NodeSchemaRegistry.Schemas)
            {
                Assert.IsTrue(NodeSchemaRegistry.IsDefaultAuthoringNode(schema.Kind), schema.Kind.ToString());
            }
        }

        [Test]
        public void StoryProgram_WhenRegistered_CanStartAndReportPresence()
        {
            var module = CreateStartedModule();
            var program = CreateProgramDefinition();

            module.Register(program);
            var runner = module.StartProgram("story_program");

            Assert.IsTrue(module.HasProgram("story_program"));
            Assert.IsTrue(module.TryGetProgram("story_program", out var registered));
            Assert.AreSame(program, registered);
            Assert.AreEqual("story_program", runner.StoryId);
            Assert.AreEqual("chapter_01", runner.CurrentChapterId);
            AssertFrame(runner.CurrentFrame, "chapter_01", "line_1");
        }

        [Test]
        public void StoryProgram_WhenEntryChapterMissing_RegistrationFails()
        {
            var module = CreateStartedModule();
            var program = new StoryProgram(
                "story_missing_chapter",
                "1",
                "chapter_02",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "start",
                        new[] { new StoryStep("start", StoryStepKind.Start) }),
                });

            var exception = Assert.Throws<GameException>(() => module.Register(program));

            StringAssert.Contains("entry chapter", exception.Message);
            Assert.IsFalse(module.HasProgram("story_missing_chapter"));
        }

        [Test]
        public void StoryProgram_WhenChoiceTargetMissing_RegistrationFails()
        {
            var module = CreateStartedModule();
            var program = new StoryProgram(
                "story_missing_choice_target",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "choice",
                        new[]
                        {
                            new StoryStep(
                                "choice",
                                StoryStepKind.Choice,
                                new StoryStepData(
                                    choices: new[]
                                    {
                                        new StoryChoice("missing", "缺失", null, StoryTarget.Step("chapter_01", "missing_step")),
                                    })),
                        }),
                });

            var exception = Assert.Throws<GameException>(() => module.Register(program));

            StringAssert.Contains("target step does not exist", exception.Message);
            StringAssert.Contains("choice:missing", exception.Message);
            Assert.IsFalse(module.HasProgram("story_missing_choice_target"));
        }

        [Test]
        public void StoryProgram_WhenCommandOutcomeTargetMissing_RegistrationFails()
        {
            var module = CreateStartedModule();
            var program = new StoryProgram(
                "story_missing_command_target",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "cmd",
                        new[]
                        {
                            new StoryStep(
                                "cmd",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "cmd",
                                        "mini_game",
                                        null,
                                        true,
                                        new[] { "success" },
                                        new Dictionary<string, StoryTarget>(StringComparer.Ordinal)
                                        {
                                            ["success"] = StoryTarget.Step("chapter_01", "missing_step"),
                                        }))),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition("mini_game", "小游戏", true, Array.Empty<string>(), new[] { "success" }),
                }));

            var exception = Assert.Throws<GameException>(() => module.Register(program));

            StringAssert.Contains("target step does not exist", exception.Message);
            StringAssert.Contains("command outcome:success", exception.Message);
            Assert.IsFalse(module.HasProgram("story_missing_command_target"));
        }

        [Test]
        public void StoryProgram_WhenCommandOutcomeIsNotDeclared_RegistrationFails()
        {
            var module = CreateStartedModule();
            var program = new StoryProgram(
                "story_undeclared_command_outcome",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "qte",
                        new[]
                        {
                            new StoryStep(
                                "qte",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "qte",
                                        StoryInteractionCommandNames.Qte,
                                        CreateQteArguments(),
                                        true,
                                        new[] { StoryInteractionCommandNames.SuccessOutcome, "timeout" },
                                        new Dictionary<string, StoryTarget>(StringComparer.Ordinal)
                                        {
                                            [StoryInteractionCommandNames.SuccessOutcome] = StoryTarget.Step("chapter_01", "success_line"),
                                            ["timeout"] = StoryTarget.Step("chapter_01", "fail_line"),
                                        }))),
                            new StoryStep(
                                "success_line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "interaction.success")),
                            new StoryStep(
                                "fail_line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "interaction.fail")),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    CreateQteCommandDefinition(),
                }));

            var exception = Assert.Throws<GameException>(() => module.Register(program));

            StringAssert.Contains("Story command outcome is not declared in schema.", exception.Message);
            StringAssert.Contains("outcome:timeout", exception.Message);
            Assert.IsFalse(module.HasProgram("story_undeclared_command_outcome"));
        }

        [Test]
        public void StoryProgram_WhenStarted_ContinuingAndCompletingProducesExpectedOutputs()
        {
            var module = CreateStartedModule();
            module.SetFunctionResolver(new FixedFunctionResolver());
            module.Register(CreateProgramDefinition());

            var runner = module.StartProgram("story_program");

            var frame = runner.CurrentFrame;
            AssertFrame(frame, "chapter_01", "line_1");
            AssertFrameTracks(frame, StoryFrameTrackKind.Text);
            Assert.AreEqual("line.key", frame.Tracks[0].TextKey);
            Assert.AreEqual(0, frame.Choices.Count);
            Assert.IsFalse(frame.WaitsForChoice);

            var afterLine = module.Continue();
            AssertChoiceFrame(afterLine, "chapter_01", "choice_1", 2);

            var afterChoice = module.Select("choice_yes");
            var command = AssertCommandFrame(afterChoice, "chapter_01", "cmd_1");
            Assert.AreEqual("play_video", command.Name);
            Assert.IsTrue(command.WaitForCompletion);
            Assert.AreEqual("choice_yes", runner.History[0].PortId);

            var afterCommand = module.CompleteCommand("play_video", "completed");
            AssertCompletedFrame(afterCommand, "chapter_01", "end");

            var snapshot = module.CreateSnapshot();
            Assert.AreEqual("story_program", snapshot.StoryId);
            Assert.IsTrue(snapshot.Completed);
            Assert.AreEqual("chapter_01", snapshot.ChapterId);
            Assert.AreEqual("end", snapshot.StepId);

            var restored = module.Restore(snapshot);
            AssertCompletedFrame(restored.CurrentFrame, "chapter_01", "end");
        }

        [Test]
        public void StoryProgram_WhenSelectingMissingChoice_ThrowsLocatedError()
        {
            var module = CreateStartedModule();
            module.Register(CreateProgramDefinition());
            module.StartProgram("story_program");
            module.Continue();

            var exception = Assert.Throws<GameException>(() => module.Select("missing_choice"));

            StringAssert.Contains("story:story_program", exception.Message);
            StringAssert.Contains("chapter:chapter_01", exception.Message);
            StringAssert.Contains("step:choice_1", exception.Message);
            StringAssert.Contains("choice:missing_choice", exception.Message);
        }

        [Test]
        public void StoryProgram_WhenSelectingBeforeChoiceFrame_ThrowsLocatedError()
        {
            var module = CreateStartedModule();
            module.Register(CreateLineOnlyProgram());
            module.StartProgram("story_line_only");

            var exception = Assert.Throws<GameException>(() => module.Select("choice_yes"));

            StringAssert.Contains("Story choice is not active.", exception.Message);
            StringAssert.Contains("story:story_line_only", exception.Message);
            StringAssert.Contains("chapter:chapter_01", exception.Message);
            StringAssert.Contains("step:line_1", exception.Message);
        }

        [Test]
        public void StoryProgram_WhenChoiceConditionIsFalse_FiltersChoice()
        {
            var module = CreateStartedModule();
            module.SetFunctionResolver(new FixedFunctionResolver(false));
            module.Register(CreateProgramDefinition(yesCondition: StoryExpression.FromFunction("can_select_yes")));

            module.StartProgram("story_program");
            var output = module.Continue();

            AssertChoiceFrame(output, "chapter_01", "choice_1", 1);
            Assert.AreEqual("choice_no", output.Choices[0].ChoiceId);
        }

        [Test]
        public void StoryProgram_WhenAllChoiceConditionsAreFalse_ThrowsLocatedError()
        {
            var module = CreateStartedModule();
            module.SetFunctionResolver(new FixedFunctionResolver(false));
            module.Register(CreateProgramDefinition(
                yesCondition: StoryExpression.FromFunction("can_select_yes"),
                noCondition: StoryExpression.FromFunction("can_select_no")));

            module.StartProgram("story_program");
            var exception = Assert.Throws<GameException>(() => module.Continue());

            StringAssert.Contains("Story choice has no available options.", exception.Message);
            StringAssert.Contains("story:story_program", exception.Message);
            StringAssert.Contains("chapter:chapter_01", exception.Message);
            StringAssert.Contains("step:choice_1", exception.Message);
        }

        [Test]
        public void StoryProgram_WhenFunctionResolverIsMissing_ThrowsLocatedError()
        {
            var module = CreateStartedModule();
            module.Register(CreateProgramDefinition(yesCondition: StoryExpression.FromFunction("can_select_yes")));

            module.StartProgram("story_program");
            var exception = Assert.Throws<GameException>(() => module.Continue());

            StringAssert.Contains("Story function resolver is missing.", exception.Message);
            StringAssert.Contains("function:can_select_yes", exception.Message);
        }

        [Test]
        public void StoryProgram_WhenVideoIsFollowedByChoice_ProducesSequentialCommandThenChoiceFrames()
        {
            var module = CreateStartedModule();
            module.Register(CreateVideoChoiceProgram());

            var frame = module.StartProgram("story_video_choice").CurrentFrame;

            var command = AssertCommandFrame(frame, "chapter_01", "video");
            Assert.AreEqual("play_video", command.Name);
            Assert.IsTrue(command.WaitForCompletion);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsFalse(frame.WaitsForChoice);

            var choiceFrame = module.CompleteCommand("video", null);
            AssertChoiceFrame(choiceFrame, "chapter_01", "choice", 1);
            Assert.AreEqual("choice_continue", choiceFrame.Choices[0].ChoiceId);
        }

        [Test]
        public void StoryProgramAsset_WhenChoiceHasNoCondition_RestoresChoiceAsAvailable()
        {
            var module = CreateStartedModule();
            var asset = ScriptableObject.CreateInstance<StoryProgramAsset>();
            try
            {
                asset.SetProgram(CreateVideoChoiceProgram());
                module.Register(asset.ToProgram());

                var frame = module.StartProgram("story_video_choice").CurrentFrame;
                AssertCommandFrame(frame, "chapter_01", "video");

                var choiceFrame = module.CompleteCommand("video", null);
                AssertChoiceFrame(choiceFrame, "chapter_01", "choice", 1);
                Assert.IsNull(choiceFrame.Choices[0].Condition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void StoryProgram_WhenImageAudioNarrationAreFollowedByChoice_ProducesSequentialFrames()
        {
            var module = CreateStartedModule();
            module.Register(CreateMediaNarrationChoiceProgram());

            var frame = module.StartProgram("story_media_choice").CurrentFrame;

            var image = AssertCommandFrame(frame, "chapter_01", "image");
            Assert.AreEqual("show_image", image.Name);
            Assert.IsFalse(frame.WaitsForCommand);
            Assert.IsFalse(frame.WaitsForChoice);

            frame = module.Continue();
            var audio = AssertCommandFrame(frame, "chapter_01", "audio");
            Assert.AreEqual("play_audio", audio.Name);
            Assert.IsFalse(frame.WaitsForCommand);
            Assert.IsFalse(frame.WaitsForChoice);

            frame = module.Continue();
            var narration = AssertTextFrame(frame, "chapter_01", "narration");
            Assert.AreEqual("narration.key", narration.TextKey);

            frame = module.Continue();
            AssertChoiceFrame(frame, "chapter_01", "choice", 1);
            Assert.AreEqual("choice_continue", frame.Choices[0].ChoiceId);
        }

        [Test]
        public void StoryPresenter_WhenStarted_PresentsFrameAndDispatchesCommand()
        {
            var module = CreateStartedModule();
            var framePresenter = new RecordingFramePresenter();
            var commandHandler = new RecordingCommandHandler("play_video");
            var presenter = new StoryPresenter(module, framePresenter);
            presenter.AddCommandHandler(commandHandler);

            var frame = presenter.Start(CreateVideoChoiceProgram());

            var command = AssertCommandFrame(frame, "chapter_01", "video");
            Assert.AreEqual("play_video", command.Name);
            Assert.AreSame(frame, framePresenter.PresentedFrame);
            Assert.AreEqual(1, commandHandler.Executions.Count);
            Assert.AreSame(command, commandHandler.Executions[0].Command);
            Assert.AreEqual("video", commandHandler.Executions[0].Context.Step.StepId);
            Assert.AreEqual(1, presenter.ActiveCommandHandles.Count);
        }

        [Test]
        public void StoryPresenter_WhenBlockingCommandHandleCompletes_AdvancesStory()
        {
            var module = CreateStartedModule();
            var commandHandler = new RecordingCommandHandler("play_video");
            var presenter = new StoryPresenter(module);
            presenter.AddCommandHandler(commandHandler);
            presenter.Start(CreateVideoChoiceProgram());

            commandHandler.LastHandle.Complete();

            AssertChoiceFrame(presenter.CurrentFrame, "chapter_01", "choice", 1);
            Assert.AreEqual(0, presenter.ActiveCommandHandles.Count);
            Assert.IsNull(presenter.LastError);
        }

        [Test]
        public void StoryPresenter_WhenNoCommandHandlerRegistered_AllowsManualCompletion()
        {
            var module = CreateStartedModule();
            var presenter = new StoryPresenter(module);

            presenter.Start(CreateVideoChoiceProgram());
            var frame = presenter.CompleteCommand("video", null);

            AssertChoiceFrame(frame, "chapter_01", "choice", 1);
            Assert.AreEqual(0, presenter.ActiveCommandHandles.Count);
        }

        [Test]
        public void StoryPresenter_WhenStopped_StopsActiveCommandHandlesAndClearsFrame()
        {
            var module = CreateStartedModule();
            var framePresenter = new RecordingFramePresenter();
            var commandHandler = new RecordingCommandHandler("play_video");
            var presenter = new StoryPresenter(module, framePresenter);
            presenter.AddCommandHandler(commandHandler);
            presenter.Start(CreateVideoChoiceProgram());

            presenter.Stop();

            Assert.IsTrue(commandHandler.LastHandle.IsStopped);
            Assert.AreEqual(0, presenter.ActiveCommandHandles.Count);
            Assert.IsNotNull(framePresenter.ClearedFrame);
        }

        [Test]
        public void StoryPresenter_WhenParallelChoiceSelected_StopsSiblingCommandHandles()
        {
            var module = CreateStartedModule();
            var commandHandler = new RecordingCommandHandler("play_video");
            var presenter = new StoryPresenter(module);
            presenter.AddCommandHandler(commandHandler);
            presenter.Start(CreateParallelInlineChoiceProgram());

            var videoHandle = commandHandler.LastHandle;
            var frame = presenter.Select("choice_continue");

            Assert.IsTrue(videoHandle.IsStopped);
            AssertTrackFrame(frame, StoryFrameTrackKind.Text, "chapter_01", "after_choice");
            Assert.AreEqual(0, presenter.ActiveCommandHandles.Count);
        }

        [Test]
        public void StoryPresenter_WhenParallelWaitChoiceAppears_KeepsVideoHandleUntilChoiceSelected()
        {
            var module = CreateStartedModule();
            var commandHandler = new RecordingCommandHandler("play_video");
            var presenter = new StoryPresenter(module);
            presenter.AddCommandHandler(commandHandler);
            presenter.Start(CreateParallelWaitChoiceVideoProgram());

            var videoHandle = commandHandler.LastHandle;
            var choiceFrame = presenter.Evaluate(1.5d);

            Assert.IsFalse(videoHandle.IsStopped);
            Assert.AreEqual(1, commandHandler.Executions.Count);
            Assert.AreEqual(1, presenter.ActiveCommandHandles.Count);
            AssertFrame(choiceFrame, "chapter_01", "parallel");
            AssertFrameTracks(choiceFrame, StoryFrameTrackKind.Command);
            Assert.AreEqual(1, choiceFrame.Choices.Count);
            Assert.IsTrue(choiceFrame.WaitsForChoice);
            Assert.IsTrue(choiceFrame.WaitsForCommand);

            var selectedFrame = presenter.Select("choice_continue");

            Assert.IsTrue(videoHandle.IsStopped);
            AssertTrackFrame(selectedFrame, StoryFrameTrackKind.Text, "chapter_01", "after_choice");
            Assert.AreEqual(0, presenter.ActiveCommandHandles.Count);
        }

        [Test]
        public void StoryPresenter_WhenLoopAudioLeavesFrame_StopsAudioHandle()
        {
            var module = CreateStartedModule();
            var commandHandler = new RecordingCommandHandler("play_audio");
            var presenter = new StoryPresenter(module);
            presenter.AddCommandHandler(commandHandler);

            presenter.Start(CreateLoopAudioContinueProgram());
            var audioHandle = commandHandler.LastHandle;
            var frame = presenter.Continue();

            Assert.IsTrue(audioHandle.IsStopped);
            AssertTrackFrame(frame, StoryFrameTrackKind.Text, "chapter_01", "line");
            Assert.AreEqual(0, presenter.ActiveCommandHandles.Count);
        }

        [Test]
        public void StoryPresenter_WhenParallelChoiceLeavesAudioFrame_StopsAudioHandle()
        {
            var module = CreateStartedModule();
            var commandHandler = new RecordingCommandHandler("play_audio");
            var presenter = new StoryPresenter(module);
            presenter.AddCommandHandler(commandHandler);
            presenter.Start(CreateParallelChoiceAudioProgram());

            var audioHandle = commandHandler.LastHandle;
            var frame = presenter.Select("choice_a");

            Assert.IsTrue(audioHandle.IsStopped);
            Assert.AreEqual("selected_line", frame.AnchorStep.StepId);
            Assert.AreEqual(0, presenter.ActiveCommandHandles.Count);
        }

        [Test]
        public void StoryPresenter_WhenParallelChoiceLeavesImageFrame_StopsImageHandle()
        {
            var module = CreateStartedModule();
            var commandHandler = new RecordingCommandHandler("show_image");
            var presenter = new StoryPresenter(module);
            presenter.AddCommandHandler(commandHandler);
            presenter.Start(CreateParallelChoiceImageProgram());

            var imageHandle = commandHandler.LastHandle;
            var frame = presenter.Select("choice_a");

            Assert.IsTrue(imageHandle.IsStopped);
            Assert.AreEqual("selected_line", frame.AnchorStep.StepId);
            Assert.AreEqual(0, presenter.ActiveCommandHandles.Count);
        }

        [Test]
        public void StoryMediaCommandHandler_WhenPlayerRegistered_HandlesMatchingCommand()
        {
            var videoPlayer = new RecordingMediaCommandPlayer();
            var handler = new StoryMediaCommandHandler(videoPlayer: videoPlayer);

            Assert.IsTrue(handler.CanHandle(CreateMediaCommand("video", StoryMediaCommandNames.PlayVideo, StoryMediaCommandNames.ClipArgument, "Assets/video.mp4")));
            Assert.IsFalse(handler.CanHandle(CreateMediaCommand("image", StoryMediaCommandNames.ShowImage, StoryMediaCommandNames.ImageArgument, "Assets/image.png")));
            Assert.IsFalse(handler.CanHandle(new StoryCommand("event", "emit_event")));
        }

        [Test]
        public void StoryMediaCommandHandler_WhenExecuted_ForwardsResourcePath()
        {
            var mediaPlayer = new RecordingMediaCommandPlayer();
            var handler = new StoryMediaCommandHandler(mediaPlayer, mediaPlayer, mediaPlayer);
            var video = CreateMediaCommand("video", StoryMediaCommandNames.PlayVideo, StoryMediaCommandNames.ClipArgument, "Assets/video.mp4");
            var image = CreateMediaCommand("image", StoryMediaCommandNames.ShowImage, StoryMediaCommandNames.ImageArgument, "Assets/image.png");
            var audio = CreateMediaCommand("audio", StoryMediaCommandNames.PlayAudio, StoryMediaCommandNames.ClipArgument, "Assets/audio.wav");

            var context = default(StoryRuntimeContext);
            var videoHandle = handler.Execute(video, context);
            handler.Execute(image, context);
            handler.Execute(audio, context);

            Assert.AreSame(video, videoHandle.Command);
            CollectionAssert.AreEqual(
                new[] { "video:Assets/video.mp4", "image:Assets/image.png", "audio:Assets/audio.wav" },
                mediaPlayer.Executions);
        }

        [Test]
        public void StoryMediaCommandHandler_WhenRequiredPathMissing_Throws()
        {
            var mediaPlayer = new RecordingMediaCommandPlayer();
            var handler = new StoryMediaCommandHandler(videoPlayer: mediaPlayer);
            var command = new StoryCommand("video", StoryMediaCommandNames.PlayVideo);

            var exception = Assert.Throws<GameException>(() => handler.Execute(command, default(StoryRuntimeContext)));

            StringAssert.Contains("argument:clip", exception.Message);
            StringAssert.Contains("command:video", exception.Message);
        }

        [Test]
        public void StoryProgram_WhenCommandOutcomeIsInvalid_ThrowsLocatedError()
        {
            var module = CreateStartedModule();
            module.Register(CreateCommandOutcomeProgram());
            module.StartProgram("story_command_outcome");

            var exception = Assert.Throws<GameException>(() => module.CompleteCommand("mini_game", "missing"));

            StringAssert.Contains("Story command outcome is not declared.", exception.Message);
            StringAssert.Contains("story:story_command_outcome", exception.Message);
            StringAssert.Contains("chapter:chapter_01", exception.Message);
            StringAssert.Contains("step:cmd", exception.Message);
            StringAssert.Contains("command:mini_game", exception.Message);
            StringAssert.Contains("outcome:missing", exception.Message);
        }

        [Test]
        public void StoryProgram_WhenCommandOutcomeIsValid_JumpsToOutcomeTarget()
        {
            var module = CreateStartedModule();
            module.Register(CreateCommandOutcomeProgram());
            module.StartProgram("story_command_outcome");

            var frame = module.CompleteCommand("mini_game", "success");

            AssertTrackFrame(frame, StoryFrameTrackKind.Text, "chapter_01", "success_line");
        }

        [Test]
        public void StoryProgram_WhenCompletingCommandWithoutOutcome_RejectsUnexpectedOutcome()
        {
            var module = CreateStartedModule();
            module.Register(CreateBlockingCommandWithoutOutcomeProgram());
            module.StartProgram("story_command_without_outcome");

            var exception = Assert.Throws<GameException>(() => module.CompleteCommand("external", "success"));

            StringAssert.Contains("Story command outcome is not declared.", exception.Message);
            StringAssert.Contains("command:external", exception.Message);
            StringAssert.Contains("outcome:success", exception.Message);
        }

        [Test]
        public void StoryProgram_WhenEvaluateWithoutWait_ThrowsLocatedError()
        {
            var module = CreateStartedModule();
            module.Register(CreateLineOnlyProgram());
            module.StartProgram("story_line_only");

            var exception = Assert.Throws<GameException>(() => module.Evaluate(1d));

            StringAssert.Contains("Story wait is not active.", exception.Message);
            StringAssert.Contains("story:story_line_only", exception.Message);
            StringAssert.Contains("chapter:chapter_01", exception.Message);
            StringAssert.Contains("step:line_1", exception.Message);
        }

        [Test]
        public void StoryProgram_WhenWaitCompletes_AdvancesToNextFrame()
        {
            var module = CreateStartedModule();
            module.Register(CreateWaitProgram());
            module.StartProgram("story_wait");

            var frame = module.Evaluate(2d);

            AssertTrackFrame(frame, StoryFrameTrackKind.Text, "chapter_01", "line_after_wait");
        }

        [Test]
        public void StoryProgram_WhenWaitReceivesPartialDeltas_RemainsWaitingUntilAccumulatedDuration()
        {
            var module = CreateStartedModule();
            module.Register(CreateWaitProgram());
            module.StartProgram("story_wait");

            var frame = module.Evaluate(1d);

            AssertTrackFrame(frame, StoryFrameTrackKind.Wait, "chapter_01", "wait");
            frame = module.Evaluate(0.4d);
            AssertTrackFrame(frame, StoryFrameTrackKind.Wait, "chapter_01", "wait");
            frame = module.Evaluate(0.1d);
            AssertTrackFrame(frame, StoryFrameTrackKind.Text, "chapter_01", "line_after_wait");
        }

        [Test]
        public void StoryProgram_WhenWaitSnapshotRestored_KeepsElapsedTime()
        {
            var module = CreateStartedModule();
            module.Register(CreateWaitProgram());
            module.StartProgram("story_wait");
            module.Evaluate(1d);

            var snapshot = module.CreateSnapshot();
            module.Restore(snapshot);
            var frame = module.Evaluate(0.4d);

            AssertTrackFrame(frame, StoryFrameTrackKind.Wait, "chapter_01", "wait");
            frame = module.Evaluate(0.1d);
            AssertTrackFrame(frame, StoryFrameTrackKind.Text, "chapter_01", "line_after_wait");
        }

        [Test]
        public void StoryProgram_WhenJumpTargetsChapter_EntersTargetChapter()
        {
            var module = CreateStartedModule();
            module.Register(CreateChapterJumpProgram());

            var frame = module.StartProgram("story_chapter_jump").CurrentFrame;

            AssertTrackFrame(frame, StoryFrameTrackKind.Text, "chapter_02", "target_line");
        }

        [Test]
        public void StoryProgram_WhenParallelContractIsValid_RegistersProgram()
        {
            var module = CreateStartedModule();
            var program = CreateParallelContractProgram();

            module.Register(program);

            Assert.IsTrue(module.HasProgram("story_parallel_contract"));
        }

        [Test]
        public void StoryProgram_WhenStartedAtParallel_BuildsCombinedBranchFrame()
        {
            var module = CreateStartedModule();
            module.Register(CreateParallelContractProgram());

            var frame = module.StartProgram("story_parallel_contract").CurrentFrame;

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Command, StoryFrameTrackKind.Text);
            Assert.AreEqual("video", frame.Tracks[0].Command.CommandId);
            Assert.AreEqual("branch_video", frame.Tracks[0].BranchId);
            Assert.AreEqual("视频轨", frame.Tracks[0].BranchLabel);
            Assert.AreEqual("line", frame.Tracks[1].Step.StepId);
            Assert.AreEqual("branch_dialogue", frame.Tracks[1].BranchId);
            Assert.AreEqual(0, frame.Choices.Count);
            Assert.IsFalse(frame.WaitsForChoice);
            Assert.IsTrue(frame.WaitsForCommand);
        }

        [Test]
        public void StoryProgram_WhenParallelTextContinues_OnlyAdvancesTextBranch()
        {
            var module = CreateStartedModule();
            module.Register(CreateParallelContractProgram());
            module.StartProgram("story_parallel_contract");

            var frame = module.Continue();

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Command);
            Assert.AreEqual("video", frame.Tracks[0].Command.CommandId);
            Assert.AreEqual("branch_video", frame.Tracks[0].BranchId);
            Assert.AreEqual(0, frame.Choices.Count);
            Assert.IsFalse(frame.WaitsForChoice);
            Assert.IsTrue(frame.WaitsForCommand);
        }

        [Test]
        public void StoryProgram_WhenParallelBranchesReachMerge_ContinuesAfterMerge()
        {
            var module = CreateStartedModule();
            module.Register(CreateParallelContractProgram());
            module.StartProgram("story_parallel_contract");

            var afterLine = module.Continue();
            Assert.IsTrue(afterLine.WaitsForCommand);

            var afterVideo = module.CompleteCommand("video", "completed");

            AssertChoiceFrame(afterVideo, "chapter_01", "merge_choices", 1);
            Assert.AreEqual("choice_continue", afterVideo.Choices[0].ChoiceId);
            Assert.IsNull(afterVideo.Choices[0].BranchId);

            var afterChoice = module.Select("choice_continue");
            AssertTrackFrame(afterChoice, StoryFrameTrackKind.Text, "chapter_01", "after_merge");
        }

        [Test]
        public void StoryProgram_WhenParallelCommandCompletesFirst_WaitsForTextBranch()
        {
            var module = CreateStartedModule();
            module.Register(CreateParallelContractProgram());
            module.StartProgram("story_parallel_contract");

            var frame = module.CompleteCommand("video", "completed");

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Text);
            Assert.AreEqual("line", frame.Tracks[0].Step.StepId);
            Assert.AreEqual("branch_dialogue", frame.Tracks[0].BranchId);
            Assert.AreEqual(0, frame.Choices.Count);
            Assert.IsFalse(frame.WaitsForChoice);
            Assert.IsFalse(frame.WaitsForCommand);
        }

        [Test]
        public void StoryProgram_WhenParallelSnapshotRestored_DoesNotReplayCompletedBranch()
        {
            var module = CreateStartedModule();
            module.Register(CreateParallelContractProgram());
            module.StartProgram("story_parallel_contract");
            module.CompleteCommand("video", "completed");

            var snapshot = module.CreateSnapshot();
            var restored = module.Restore(snapshot).CurrentFrame;

            AssertFrame(restored, "chapter_01", "parallel");
            AssertFrameTracks(restored, StoryFrameTrackKind.Text);
            Assert.AreEqual("line", restored.Tracks[0].Step.StepId);
            Assert.AreEqual("branch_dialogue", restored.Tracks[0].BranchId);

            var afterLine = module.Continue();
            AssertChoiceFrame(afterLine, "chapter_01", "merge_choices", 1);
        }

        [Test]
        public void StoryProgram_WhenParallelWaitReceivesPartialDeltas_RemainsWaitingUntilAccumulatedDuration()
        {
            var module = CreateStartedModule();
            module.Register(CreateParallelWaitProgram());
            module.StartProgram("story_parallel_wait");
            module.Continue();

            var frame = module.Evaluate(1d);

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Wait);
            Assert.AreEqual("branch_wait", frame.Tracks[0].BranchId);

            frame = module.Evaluate(0.5d);
            AssertTrackFrame(frame, StoryFrameTrackKind.Text, "chapter_01", "after_merge");
        }

        [Test]
        public void StoryProgram_WhenParallelWaitChoiceTriggers_KeepsVideoTrackAndShowsChoice()
        {
            var module = CreateStartedModule();
            module.Register(CreateParallelWaitChoiceVideoProgram());

            var frame = module.StartProgram("story_parallel_wait_choice").CurrentFrame;

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Command, StoryFrameTrackKind.Wait);
            Assert.AreEqual("video", frame.Tracks[0].Command.CommandId);
            Assert.AreEqual("branch_video", frame.Tracks[0].BranchId);
            Assert.AreEqual("wait_choice", frame.Tracks[1].Step.StepId);
            Assert.AreEqual("branch_interaction", frame.Tracks[1].BranchId);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsTrue(frame.WaitsForTime);

            frame = module.Evaluate(1.5d);

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Command);
            Assert.AreEqual("video", frame.Tracks[0].Command.CommandId);
            Assert.AreEqual("branch_video", frame.Tracks[0].BranchId);
            Assert.AreEqual(1, frame.Choices.Count);
            Assert.AreEqual("choice_continue", frame.Choices[0].ChoiceId);
            Assert.AreEqual("branch_interaction", frame.Choices[0].BranchId);
            Assert.IsTrue(frame.WaitsForChoice);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsFalse(frame.WaitsForTime);

            frame = module.Select("choice_continue");

            AssertTrackFrame(frame, StoryFrameTrackKind.Text, "chapter_01", "after_choice");
        }

        [Test]
        public void StoryProgram_WhenParallelWaitCommandTriggers_KeepsVideoTrackAndCompletesInteractionOutcome()
        {
            var module = CreateStartedModule();
            module.Register(CreateParallelWaitCommandVideoProgram());

            var frame = module.StartProgram("story_parallel_wait_command").CurrentFrame;

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Command, StoryFrameTrackKind.Wait);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsTrue(frame.WaitsForTime);

            frame = module.Evaluate(1.5d);

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Command, StoryFrameTrackKind.Command);
            Assert.AreEqual("video", frame.Tracks[0].Command.CommandId);
            Assert.AreEqual("branch_video", frame.Tracks[0].BranchId);
            Assert.AreEqual("custom_interaction", frame.Tracks[1].Command.CommandId);
            Assert.AreEqual("branch_interaction", frame.Tracks[1].BranchId);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsFalse(frame.WaitsForTime);

            frame = module.CompleteCommand("custom_interaction", "success");

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Command, StoryFrameTrackKind.Text);
            Assert.AreEqual("video", frame.Tracks[0].Command.CommandId);
            Assert.AreEqual("success_line", frame.Tracks[1].Step.StepId);
            Assert.AreEqual("branch_interaction", frame.Tracks[1].BranchId);
            Assert.IsTrue(frame.WaitsForCommand);
        }

        [Test]
        public void StoryProgram_WhenParallelWaitQteTriggers_KeepsVideoTrackAndCompletesQteOutcome()
        {
            var module = CreateStartedModule();
            module.Register(CreateParallelWaitQteVideoProgram());

            var frame = module.StartProgram("story_parallel_wait_qte").CurrentFrame;

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Command, StoryFrameTrackKind.Wait);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsTrue(frame.WaitsForTime);

            frame = module.Evaluate(1.5d);

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Command, StoryFrameTrackKind.Command);
            Assert.AreEqual("video", frame.Tracks[0].Command.CommandId);
            Assert.AreEqual(StoryMediaCommandNames.PlayVideo, frame.Tracks[0].Command.Name);
            Assert.AreEqual("branch_video", frame.Tracks[0].BranchId);
            Assert.AreEqual("qte", frame.Tracks[1].Command.CommandId);
            Assert.AreEqual(StoryInteractionCommandNames.Qte, frame.Tracks[1].Command.Name);
            Assert.AreEqual("branch_interaction", frame.Tracks[1].BranchId);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsFalse(frame.WaitsForTime);

            frame = module.CompleteCommand("qte", StoryInteractionCommandNames.SuccessOutcome);

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Command, StoryFrameTrackKind.Text);
            Assert.AreEqual("video", frame.Tracks[0].Command.CommandId);
            Assert.AreEqual("success_line", frame.Tracks[1].Step.StepId);
            Assert.AreEqual("branch_interaction", frame.Tracks[1].BranchId);
            Assert.IsTrue(frame.WaitsForCommand);
        }

        [Test]
        public void StoryProgram_WhenParallelWaitQteFails_AdvancesFailBranch()
        {
            var module = CreateStartedModule();
            module.Register(CreateParallelWaitQteVideoProgram());

            module.StartProgram("story_parallel_wait_qte");
            module.Evaluate(1.5d);

            var frame = module.CompleteCommand("qte", StoryInteractionCommandNames.FailOutcome);

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Command, StoryFrameTrackKind.Text);
            Assert.AreEqual("video", frame.Tracks[0].Command.CommandId);
            Assert.AreEqual("fail_line", frame.Tracks[1].Step.StepId);
            Assert.AreEqual("branch_interaction", frame.Tracks[1].BranchId);
            Assert.IsTrue(frame.WaitsForCommand);
        }

        [Test]
        public void StoryProgram_WhenParallelWaitUnlockTriggers_KeepsVideoTrackAndCompletesSuccessOutcome()
        {
            var module = CreateStartedModule();
            module.Register(CreateParallelWaitUnlockVideoProgram());

            var frame = module.StartProgram("story_parallel_wait_unlock").CurrentFrame;

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Command, StoryFrameTrackKind.Wait);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsTrue(frame.WaitsForTime);

            frame = module.Evaluate(1.5d);

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Command, StoryFrameTrackKind.Command);
            Assert.AreEqual("video", frame.Tracks[0].Command.CommandId);
            Assert.AreEqual(StoryMediaCommandNames.PlayVideo, frame.Tracks[0].Command.Name);
            Assert.AreEqual("branch_video", frame.Tracks[0].BranchId);
            Assert.AreEqual("unlock", frame.Tracks[1].Command.CommandId);
            Assert.AreEqual(StoryInteractionCommandNames.Unlock, frame.Tracks[1].Command.Name);
            Assert.AreEqual("chapter_01.door", frame.Tracks[1].Command.Arguments.GetString(StoryInteractionCommandNames.UnlockIdArgument));
            Assert.AreEqual(StoryInteractionCommandNames.PuzzleTypeNodeUnlock, frame.Tracks[1].Command.Arguments.GetString(StoryInteractionCommandNames.PuzzleTypeArgument));
            Assert.AreEqual("unlock.door", frame.Tracks[1].Command.Arguments.GetString(StoryInteractionCommandNames.PromptTextKeyArgument));
            Assert.AreEqual("branch_interaction", frame.Tracks[1].BranchId);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsFalse(frame.WaitsForTime);
            Assert.IsFalse(frame.WaitsForChoice);

            frame = module.CompleteCommand("unlock", StoryInteractionCommandNames.SuccessOutcome);

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Command, StoryFrameTrackKind.Text);
            Assert.AreEqual("video", frame.Tracks[0].Command.CommandId);
            Assert.AreEqual("success_line", frame.Tracks[1].Step.StepId);
            Assert.AreEqual("branch_interaction", frame.Tracks[1].BranchId);
            Assert.IsTrue(frame.WaitsForCommand);
        }

        [Test]
        public void StoryProgram_WhenParallelWaitUnlockFails_AdvancesFailBranch()
        {
            var module = CreateStartedModule();
            module.Register(CreateParallelWaitUnlockVideoProgram());

            module.StartProgram("story_parallel_wait_unlock");
            module.Evaluate(1.5d);

            var frame = module.CompleteCommand("unlock", StoryInteractionCommandNames.FailOutcome);

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Command, StoryFrameTrackKind.Text);
            Assert.AreEqual("video", frame.Tracks[0].Command.CommandId);
            Assert.AreEqual("fail_line", frame.Tracks[1].Step.StepId);
            Assert.AreEqual("branch_interaction", frame.Tracks[1].BranchId);
            Assert.IsTrue(frame.WaitsForCommand);
        }

        [Test]
        public void StoryProgram_WhenParallelCommandTargetsChapter_TransitionsOutOfParallel()
        {
            var module = CreateStartedModule();
            module.Register(CreateParallelJumpChapterProgram());

            var frame = module.StartProgram("story_parallel_jump_chapter").CurrentFrame;

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Command, StoryFrameTrackKind.Text);

            frame = module.Continue();
            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, StoryFrameTrackKind.Command);

            var targetFrame = module.CompleteCommand("video", "completed");
            AssertTrackFrame(targetFrame, StoryFrameTrackKind.Text, "chapter_02", "target_line");
        }

        [Test]
        public void StoryProgram_WhenParallelHasSingleBranch_RegistrationFails()
        {
            var module = CreateStartedModule();
            var program = CreateParallelContractProgram(new[]
            {
                new StoryParallelBranch("branch_video", "视频轨", StoryTarget.Step("chapter_01", "video")),
            });

            var exception = Assert.Throws<GameException>(() => module.Register(program));

            StringAssert.Contains("parallel step must have at least two branches", exception.Message);
            StringAssert.Contains("story:story_parallel_contract", exception.Message);
            StringAssert.Contains("chapter:chapter_01", exception.Message);
            StringAssert.Contains("step:parallel", exception.Message);
        }

        [Test]
        public void StoryProgram_WhenMergeReferencesMissingParallel_RegistrationFails()
        {
            var module = CreateStartedModule();
            var program = CreateParallelContractProgram(mergeParallelStepId: "missing_parallel");

            var exception = Assert.Throws<GameException>(() => module.Register(program));

            StringAssert.Contains("merge parallel step does not exist", exception.Message);
            StringAssert.Contains("story:story_parallel_contract", exception.Message);
            StringAssert.Contains("chapter:chapter_01", exception.Message);
            StringAssert.Contains("step:merge", exception.Message);
            StringAssert.Contains("parallel:missing_parallel", exception.Message);
        }

        [Test]
        public void StoryProgram_WhenCommandSchemaMissing_RegistrationFails()
        {
            var module = CreateStartedModule();
            var program = new StoryProgram(
                "story_missing_command",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "cmd",
                        new[]
                        {
                            new StoryStep(
                                "cmd",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand("cmd", "unknown_cmd"))),
                        }),
                });

            var exception = Assert.Throws<GameException>(() => module.Register(program));

            StringAssert.Contains("command:unknown_cmd", exception.Message);
        }

        [Test]
        public void StoryProgram_WhenRequiredCommandArgumentMissing_RegistrationFails()
        {
            var module = CreateStartedModule();
            var program = CreateCommandArgumentProgram(
                new Dictionary<string, StoryValue>(StringComparer.Ordinal),
                new StoryCommandArgumentDefinition(
                    "clip",
                    "视频片段",
                    ParameterValueType.AssetReference,
                    true,
                    "video"));

            var exception = Assert.Throws<GameException>(() => module.Register(program));

            StringAssert.Contains("story:story_command_arguments", exception.Message);
            StringAssert.Contains("chapter:chapter_01", exception.Message);
            StringAssert.Contains("step:cmd", exception.Message);
            StringAssert.Contains("command:play_video", exception.Message);
            StringAssert.Contains("argument:clip", exception.Message);
        }

        [Test]
        public void StoryProgram_WhenCommandArgumentTypeMismatches_RegistrationFails()
        {
            var module = CreateStartedModule();
            var program = CreateCommandArgumentProgram(
                new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                {
                    ["duration"] = StoryValue.FromString("fast")
                },
                new StoryCommandArgumentDefinition(
                    "duration",
                    "时长",
                    ParameterValueType.Number,
                    true));

            var exception = Assert.Throws<GameException>(() => module.Register(program));

            StringAssert.Contains("story:story_command_arguments", exception.Message);
            StringAssert.Contains("chapter:chapter_01", exception.Message);
            StringAssert.Contains("step:cmd", exception.Message);
            StringAssert.Contains("command:play_video", exception.Message);
            StringAssert.Contains("argument:duration", exception.Message);
        }

        [Test]
        public void StoryProgram_WhenCommandArgumentOptionIsInvalid_RegistrationFails()
        {
            var module = CreateStartedModule();
            var program = CreateCommandArgumentProgram(
                new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                {
                    [StoryMediaCommandNames.VideoSourceArgument] = StoryValue.FromString("asset_bundle")
                },
                new StoryCommandArgumentDefinition(
                    StoryMediaCommandNames.VideoSourceArgument,
                    "来源",
                    ParameterValueType.Option,
                    true,
                    options: new[]
                    {
                        StoryMediaCommandNames.VideoSourceStreamingAssets,
                        StoryMediaCommandNames.VideoSourcePersistentDataPath,
                        StoryMediaCommandNames.VideoSourceNetworkStream
                    }));

            var exception = Assert.Throws<GameException>(() => module.Register(program));

            StringAssert.Contains("story:story_command_arguments", exception.Message);
            StringAssert.Contains("command:play_video", exception.Message);
            StringAssert.Contains("argument:source", exception.Message);
        }

        [Test]
        public void StoryCommandDefinition_WhenCreatedFromArgumentNames_KeepsArgumentDefinitions()
        {
            var definition = new StoryCommandDefinition("play_video", "播放视频", true, new[] { "clip" }, new[] { "completed" });

            CollectionAssert.Contains(definition.ArgumentNames, "clip");
            Assert.AreEqual(1, definition.ArgumentDefinitions.Count);
            Assert.AreEqual("clip", definition.ArgumentDefinitions[0].Key);
            Assert.AreEqual(ParameterValueType.String, definition.ArgumentDefinitions[0].ValueType);
        }

        private static StoryModule CreateStartedModule()
        {
            var module = new StoryModule();
            module.Startup();
            return module;
        }

        private static StoryCommand CreateMediaCommand(string commandId, string commandName, string argumentKey, string path)
        {
            return new StoryCommand(
                commandId,
                commandName,
                new StoryArgumentBag(new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                {
                    [argumentKey] = StoryValue.FromString(path),
                }));
        }

        private static StoryProgram CreateProgramDefinition(
            StoryExpression yesCondition = null,
            StoryExpression noCondition = null)
        {
            return new StoryProgram(
                "story_program",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "start",
                        new[]
                        {
                            new StoryStep("start", StoryStepKind.Start),
                            new StoryStep(
                                "line_1",
                                StoryStepKind.Line,
                                new StoryStepData(
                                    textKey: "line.key",
                                    speaker: "npc",
                                    tags: new[] { "story" })),
                            new StoryStep(
                                "choice_1",
                                StoryStepKind.Choice,
                                new StoryStepData(
                                    choices: new[]
                                    {
                                        new StoryChoice("choice_yes", "choice.yes", yesCondition, StoryTarget.Step("chapter_01", "cmd_1")),
                                        new StoryChoice("choice_no", "choice.no", noCondition, StoryTarget.StoryEnd()),
                                    })),
                            new StoryStep(
                                "cmd_1",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "play_video",
                                        "play_video",
                                        new StoryArgumentBag(new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                                        {
                                            [StoryMediaCommandNames.VideoSourceArgument] = StoryValue.FromString(SampleVideoSource),
                                            ["clip"] = StoryValue.FromString(SampleVideoPath)
                                        }),
                                        true,
                                        new[] { "completed" },
                                        new Dictionary<string, StoryTarget>(StringComparer.Ordinal)
                                        {
                                            ["completed"] = StoryTarget.Step("chapter_01", "end")
                                        }))),
                            new StoryStep("end", StoryStepKind.End),
                        }),
                },
                new StoryVariableSchema(new[]
                {
                    new StoryVariableDefinition("flag", StoryVariableType.Boolean, StoryValue.FromBoolean(false)),
                }),
                new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition("play_video", "播放视频", true, CreatePlayVideoArgumentDefinitions(), new[] { "completed" }),
                }));
        }

        private static StoryProgram CreateCommandArgumentProgram(
            IReadOnlyDictionary<string, StoryValue> arguments,
            StoryCommandArgumentDefinition argumentDefinition)
        {
            return new StoryProgram(
                "story_command_arguments",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "cmd",
                        new[]
                        {
                            new StoryStep(
                                "cmd",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "cmd",
                                        "play_video",
                                        new StoryArgumentBag(arguments)))),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition(
                        "play_video",
                        "播放视频",
                        false,
                        new[] { argumentDefinition },
                        Array.Empty<string>()),
                }));
        }

        private static StoryProgram CreateLineOnlyProgram()
        {
            return new StoryProgram(
                "story_line_only",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "start",
                        new[]
                        {
                            new StoryStep("start", StoryStepKind.Start),
                            new StoryStep(
                                "line_1",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "line.key")),
                            new StoryStep("end", StoryStepKind.End),
                        }),
                });
        }

        private static StoryProgram CreateVideoChoiceProgram()
        {
            return new StoryProgram(
                "story_video_choice",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "video",
                        new[]
                        {
                            new StoryStep(
                                "video",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "video",
                                        "play_video",
                                        new StoryArgumentBag(new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                                        {
                                            [StoryMediaCommandNames.VideoSourceArgument] = StoryValue.FromString(SampleVideoSource),
                                            ["clip"] = StoryValue.FromString(SampleVideoPath)
                                        }),
                                        true))),
                            new StoryStep(
                                "choice",
                                StoryStepKind.Choice,
                                new StoryStepData(
                                    choices: new[]
                                    {
                                        new StoryChoice("choice_continue", "继续", null, StoryTarget.Step("chapter_01", "end")),
                                    })),
                            new StoryStep("end", StoryStepKind.End),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition("play_video", "播放视频", true, CreatePlayVideoArgumentDefinitions(), Array.Empty<string>()),
                }));
        }

        private static StoryProgram CreateParallelInlineChoiceProgram()
        {
            return new StoryProgram(
                "story_parallel_inline_choice",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new StoryStep(
                                "parallel",
                                StoryStepKind.Parallel,
                                new StoryStepData(
                                    branches: new[]
                                    {
                                        new StoryParallelBranch("branch_video", "视频轨", StoryTarget.Step("chapter_01", "video")),
                                        new StoryParallelBranch("branch_dialogue", "对白轨", StoryTarget.Step("chapter_01", "line")),
                                    })),
                            new StoryStep(
                                "video",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "video",
                                        "play_video",
                                        new StoryArgumentBag(new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                                        {
                                            [StoryMediaCommandNames.VideoSourceArgument] = StoryValue.FromString(SampleVideoSource),
                                            ["clip"] = StoryValue.FromString(SampleVideoPath)
                                        }),
                                        true))),
                            new StoryStep(
                                "line",
                                StoryStepKind.Line,
                                new StoryStepData(
                                    textKey: "parallel.dialogue",
                                    target: StoryTarget.Step("chapter_01", "choice"))),
                            new StoryStep(
                                "choice",
                                StoryStepKind.Choice,
                                new StoryStepData(
                                    choices: new[]
                                    {
                                        new StoryChoice("choice_continue", "继续", null, StoryTarget.Step("chapter_01", "after_choice")),
                                    })),
                            new StoryStep(
                                "after_choice",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "after.choice")),
                            new StoryStep("end", StoryStepKind.End),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition("play_video", "播放视频", true, CreatePlayVideoArgumentDefinitions(), Array.Empty<string>()),
                }));
        }

        private static StoryProgram CreateMediaNarrationChoiceProgram()
        {
            return new StoryProgram(
                "story_media_choice",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "image",
                        new[]
                        {
                            new StoryStep(
                                "image",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "image",
                                        "show_image",
                                        new StoryArgumentBag(new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                                        {
                                            ["image"] = StoryValue.FromString(SampleImagePath)
                                        })))),
                            new StoryStep(
                                "audio",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "audio",
                                        "play_audio",
                                        new StoryArgumentBag(new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                                        {
                                            ["clip"] = StoryValue.FromString(SampleAudioPath)
                                        })))),
                            new StoryStep(
                                "narration",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "narration.key")),
                            new StoryStep(
                                "choice",
                                StoryStepKind.Choice,
                                new StoryStepData(
                                    choices: new[]
                                    {
                                        new StoryChoice("choice_continue", "继续", null, StoryTarget.Step("chapter_01", "end")),
                                    })),
                            new StoryStep("end", StoryStepKind.End),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition("show_image", "显示图片", false, new[] { "image" }, Array.Empty<string>()),
                    new StoryCommandDefinition("play_audio", "播放音频", false, new[] { "clip" }, Array.Empty<string>()),
                }));
        }

        private static StoryProgram CreateLoopAudioContinueProgram()
        {
            return new StoryProgram(
                "story_loop_audio_continue",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "audio",
                        new[]
                        {
                            new StoryStep(
                                "audio",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "audio",
                                        "play_audio",
                                        new StoryArgumentBag(new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                                        {
                                            ["clip"] = StoryValue.FromString(SampleAudioPath),
                                            ["loop"] = StoryValue.FromBoolean(true)
                                        })))),
                            new StoryStep(
                                "line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "after.audio")),
                            new StoryStep("end", StoryStepKind.End),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition(
                        "play_audio",
                        "播放音频",
                        false,
                        new[]
                        {
                            new StoryCommandArgumentDefinition("clip", "音频", ParameterValueType.String, true),
                            new StoryCommandArgumentDefinition("loop", "循环播放", ParameterValueType.Boolean)
                        },
                        Array.Empty<string>()),
                }));
        }

        private static StoryProgram CreateParallelChoiceAudioProgram()
        {
            return new StoryProgram(
                "story_parallel_choice_audio",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new StoryStep(
                                "parallel",
                                StoryStepKind.Parallel,
                                new StoryStepData(
                                    branches: new[]
                                    {
                                        new StoryParallelBranch("branch_audio", "音频轨", StoryTarget.Step("chapter_01", "audio")),
                                        new StoryParallelBranch("branch_dialogue", "对白轨", StoryTarget.Step("chapter_01", "line")),
                                    })),
                            new StoryStep(
                                "audio",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "audio",
                                        "play_audio",
                                        new StoryArgumentBag(new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                                        {
                                            ["clip"] = StoryValue.FromString(SampleAudioPath)
                                        })))),
                            new StoryStep(
                                "line",
                                StoryStepKind.Line,
                                new StoryStepData(
                                    textKey: "parallel.line",
                                    target: StoryTarget.Step("chapter_01", "choice"))),
                            new StoryStep(
                                "choice",
                                StoryStepKind.Choice,
                                new StoryStepData(
                                    choices: new[]
                                    {
                                        new StoryChoice("choice_a", "选项 A", null, StoryTarget.Step("chapter_01", "selected_line"), null, "branch_dialogue"),
                                    })),
                            new StoryStep(
                                "selected_line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "selected.line")),
                            new StoryStep("end", StoryStepKind.End),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition("play_audio", "播放音频", false, new[] { "clip" }, Array.Empty<string>()),
                }));
        }

        private static StoryProgram CreateParallelChoiceImageProgram()
        {
            return new StoryProgram(
                "story_parallel_choice_image",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new StoryStep(
                                "parallel",
                                StoryStepKind.Parallel,
                                new StoryStepData(
                                    branches: new[]
                                    {
                                        new StoryParallelBranch("branch_image", "图片轨", StoryTarget.Step("chapter_01", "image")),
                                        new StoryParallelBranch("branch_dialogue", "对白轨", StoryTarget.Step("chapter_01", "line")),
                                    })),
                            new StoryStep(
                                "image",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "image",
                                        "show_image",
                                        new StoryArgumentBag(new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                                        {
                                            ["image"] = StoryValue.FromString(SampleImagePath)
                                        }),
                                        false))),
                            new StoryStep(
                                "line",
                                StoryStepKind.Line,
                                new StoryStepData(
                                    textKey: "parallel.line",
                                    target: StoryTarget.Step("chapter_01", "choice_a"))),
                            new StoryStep(
                                "choice_a",
                                StoryStepKind.Choice,
                                new StoryStepData(
                                    choices: new[]
                                    {
                                        new StoryChoice("choice_a", "选项 A", null, StoryTarget.Step("chapter_01", "selected_line"), null, "branch_dialogue"),
                                    })),
                            new StoryStep(
                                "selected_line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "selected.line")),
                            new StoryStep("end", StoryStepKind.End),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition("show_image", "显示图片", false, new[] { "image" }, Array.Empty<string>()),
                }));
        }

        private static StoryProgram CreateCommandOutcomeProgram()
        {
            return new StoryProgram(
                "story_command_outcome",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "cmd",
                        new[]
                        {
                            new StoryStep(
                                "cmd",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "mini_game",
                                        "mini_game",
                                        new StoryArgumentBag(new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                                        {
                                            ["miniGameId"] = StoryValue.FromString("lock")
                                        }),
                                        true,
                                        new[] { "success", "fail" },
                                        new Dictionary<string, StoryTarget>(StringComparer.Ordinal)
                                        {
                                            ["success"] = StoryTarget.Step("chapter_01", "success_line"),
                                            ["fail"] = StoryTarget.Step("chapter_01", "fail_line"),
                                        }))),
                            new StoryStep(
                                "success_line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "success.key")),
                            new StoryStep("success_end", StoryStepKind.End),
                            new StoryStep(
                                "fail_line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "fail.key")),
                            new StoryStep("fail_end", StoryStepKind.End),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition("mini_game", "小游戏", true, new[] { "miniGameId" }, new[] { "success", "fail" }),
                }));
        }

        private static StoryProgram CreateBlockingCommandWithoutOutcomeProgram()
        {
            return new StoryProgram(
                "story_command_without_outcome",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "cmd",
                        new[]
                        {
                            new StoryStep(
                                "cmd",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "external",
                                        "external_action",
                                        null,
                                        true))),
                            new StoryStep("end", StoryStepKind.End),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition("external_action", "外部动作", true, Array.Empty<string>(), Array.Empty<string>()),
                }));
        }

        private static StoryProgram CreateWaitProgram()
        {
            return new StoryProgram(
                "story_wait",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "wait",
                        new[]
                        {
                            new StoryStep(
                                "wait",
                                StoryStepKind.Wait,
                                new StoryStepData(waitSeconds: 1.5d)),
                            new StoryStep(
                                "line_after_wait",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "after.wait")),
                            new StoryStep("end", StoryStepKind.End),
                        }),
                });
        }

        private static StoryProgram CreateChapterJumpProgram()
        {
            return new StoryProgram(
                "story_chapter_jump",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "jump",
                        new[]
                        {
                            new StoryStep(
                                "jump",
                                StoryStepKind.Jump,
                                new StoryStepData(target: StoryTarget.Chapter("chapter_02"))),
                        }),
                    new StoryChapter(
                        "chapter_02",
                        "第二章",
                        "target_line",
                        new[]
                        {
                            new StoryStep(
                                "target_line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "target.line")),
                            new StoryStep("target_end", StoryStepKind.End),
                        }),
                });
        }

        private static StoryProgram CreateParallelContractProgram(
            IReadOnlyList<StoryParallelBranch> branches = null,
            string mergeParallelStepId = "parallel")
        {
            return new StoryProgram(
                "story_parallel_contract",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new StoryStep(
                                "parallel",
                                StoryStepKind.Parallel,
                                new StoryStepData(
                                    branches: branches ?? new[]
                                    {
                                        new StoryParallelBranch("branch_video", "视频轨", StoryTarget.Step("chapter_01", "video")),
                                        new StoryParallelBranch("branch_dialogue", "对白轨", StoryTarget.Step("chapter_01", "line")),
                                    })),
                            new StoryStep(
                                "video",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "video",
                                        "play_video",
                                        null,
                                        true,
                                        new[] { "completed" },
                                        new Dictionary<string, StoryTarget>(StringComparer.Ordinal)
                                        {
                                            ["completed"] = StoryTarget.Step("chapter_01", "merge")
                                        }))),
                            new StoryStep(
                                "line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "parallel.line", target: StoryTarget.Step("chapter_01", "merge"))),
                            new StoryStep(
                                "merge",
                                StoryStepKind.Merge,
                                new StoryStepData(
                                    target: StoryTarget.Step("chapter_01", "merge_choices"),
                                    parallelStepId: mergeParallelStepId)),
                            new StoryStep(
                                "merge_choices",
                                StoryStepKind.Choice,
                                new StoryStepData(
                                    choices: new[]
                                    {
                                        new StoryChoice("choice_continue", "继续", null, StoryTarget.Step("chapter_01", "after_merge")),
                                    })),
                            new StoryStep(
                                "after_merge",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "after.merge")),
                            new StoryStep("end", StoryStepKind.End),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition("play_video", "播放视频", true, Array.Empty<string>(), new[] { "completed" }),
                }));
        }

        private static StoryProgram CreateParallelWaitProgram()
        {
            return new StoryProgram(
                "story_parallel_wait",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new StoryStep(
                                "parallel",
                                StoryStepKind.Parallel,
                                new StoryStepData(
                                    branches: new[]
                                    {
                                        new StoryParallelBranch("branch_wait", "等待轨", StoryTarget.Step("chapter_01", "wait")),
                                        new StoryParallelBranch("branch_line", "文本轨", StoryTarget.Step("chapter_01", "line")),
                                    })),
                            new StoryStep(
                                "wait",
                                StoryStepKind.Wait,
                                new StoryStepData(waitSeconds: 1.5d, target: StoryTarget.Step("chapter_01", "merge"))),
                            new StoryStep(
                                "line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "parallel.wait.line", target: StoryTarget.Step("chapter_01", "merge"))),
                            new StoryStep(
                                "merge",
                                StoryStepKind.Merge,
                                new StoryStepData(
                                    target: StoryTarget.Step("chapter_01", "after_merge"),
                                    parallelStepId: "parallel")),
                            new StoryStep(
                                "after_merge",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "after.parallel.wait")),
                            new StoryStep("end", StoryStepKind.End),
                        }),
                });
        }

        private static StoryProgram CreateParallelWaitChoiceVideoProgram()
        {
            return new StoryProgram(
                "story_parallel_wait_choice",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new StoryStep(
                                "parallel",
                                StoryStepKind.Parallel,
                                new StoryStepData(
                                    branches: new[]
                                    {
                                        new StoryParallelBranch("branch_video", "视频轨", StoryTarget.Step("chapter_01", "video")),
                                        new StoryParallelBranch("branch_interaction", "交互轨", StoryTarget.Step("chapter_01", "wait_choice")),
                                    })),
                            new StoryStep(
                                "video",
                                StoryStepKind.Command,
                                new StoryStepData(command: CreateWaitVideoCommand())),
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
                                new StoryStepData(textKey: "after.choice")),
                            new StoryStep("end", StoryStepKind.End),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition("play_video", "播放视频", true, CreatePlayVideoArgumentDefinitions(), Array.Empty<string>()),
                }));
        }

        private static StoryProgram CreateParallelWaitCommandVideoProgram()
        {
            return new StoryProgram(
                "story_parallel_wait_command",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new StoryStep(
                                "parallel",
                                StoryStepKind.Parallel,
                                new StoryStepData(
                                    branches: new[]
                                    {
                                        new StoryParallelBranch("branch_video", "视频轨", StoryTarget.Step("chapter_01", "video")),
                                        new StoryParallelBranch("branch_interaction", "交互轨", StoryTarget.Step("chapter_01", "wait_command")),
                                    })),
                            new StoryStep(
                                "video",
                                StoryStepKind.Command,
                                new StoryStepData(command: CreateWaitVideoCommand())),
                            new StoryStep(
                                "wait_command",
                                StoryStepKind.Wait,
                                new StoryStepData(waitSeconds: 1.5d, target: StoryTarget.Step("chapter_01", "custom_interaction"))),
                            new StoryStep(
                                "custom_interaction",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "custom_interaction",
                                        "custom_interaction",
                                        waitForCompletion: true,
                                        outcomePorts: new[] { "success", "fail" },
                                        outcomeTargets: new Dictionary<string, StoryTarget>(StringComparer.Ordinal)
                                        {
                                            ["success"] = StoryTarget.Step("chapter_01", "success_line"),
                                            ["fail"] = StoryTarget.Step("chapter_01", "fail_line"),
                                        }))),
                            new StoryStep(
                                "success_line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "interaction.success")),
                            new StoryStep(
                                "fail_line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "interaction.fail")),
                            new StoryStep("end", StoryStepKind.End),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition("play_video", "播放视频", true, CreatePlayVideoArgumentDefinitions(), Array.Empty<string>()),
                    new StoryCommandDefinition("custom_interaction", "自定义互动", true, Array.Empty<StoryCommandArgumentDefinition>(), new[] { "success", "fail" }),
                }));
        }

        private static StoryProgram CreateParallelWaitQteVideoProgram()
        {
            return new StoryProgram(
                "story_parallel_wait_qte",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new StoryStep(
                                "parallel",
                                StoryStepKind.Parallel,
                                new StoryStepData(
                                    branches: new[]
                                    {
                                        new StoryParallelBranch("branch_video", "视频轨", StoryTarget.Step("chapter_01", "video")),
                                        new StoryParallelBranch("branch_interaction", "交互轨", StoryTarget.Step("chapter_01", "wait_qte")),
                                    })),
                            new StoryStep(
                                "video",
                                StoryStepKind.Command,
                                new StoryStepData(command: CreateWaitVideoCommand())),
                            new StoryStep(
                                "wait_qte",
                                StoryStepKind.Wait,
                                new StoryStepData(waitSeconds: 1.5d, target: StoryTarget.Step("chapter_01", "qte"))),
                            new StoryStep(
                                "qte",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "qte",
                                        StoryInteractionCommandNames.Qte,
                                        CreateQteArguments(),
                                        true,
                                        new[]
                                        {
                                            StoryInteractionCommandNames.SuccessOutcome,
                                            StoryInteractionCommandNames.FailOutcome
                                        },
                                        new Dictionary<string, StoryTarget>(StringComparer.Ordinal)
                                        {
                                            [StoryInteractionCommandNames.SuccessOutcome] = StoryTarget.Step("chapter_01", "success_line"),
                                            [StoryInteractionCommandNames.FailOutcome] = StoryTarget.Step("chapter_01", "fail_line"),
                                        }))),
                            new StoryStep(
                                "success_line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "interaction.success")),
                            new StoryStep(
                                "fail_line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "interaction.fail")),
                            new StoryStep("end", StoryStepKind.End),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition(StoryMediaCommandNames.PlayVideo, "播放视频", true, CreatePlayVideoArgumentDefinitions(), Array.Empty<string>()),
                    CreateQteCommandDefinition(),
                }));
        }

        private static StoryProgram CreateParallelWaitUnlockVideoProgram()
        {
            return new StoryProgram(
                "story_parallel_wait_unlock",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new StoryStep(
                                "parallel",
                                StoryStepKind.Parallel,
                                new StoryStepData(
                                    branches: new[]
                                    {
                                        new StoryParallelBranch("branch_video", "视频轨", StoryTarget.Step("chapter_01", "video")),
                                        new StoryParallelBranch("branch_interaction", "交互轨", StoryTarget.Step("chapter_01", "wait_unlock")),
                                    })),
                            new StoryStep(
                                "video",
                                StoryStepKind.Command,
                                new StoryStepData(command: CreateWaitVideoCommand())),
                            new StoryStep(
                                "wait_unlock",
                                StoryStepKind.Wait,
                                new StoryStepData(waitSeconds: 1.5d, target: StoryTarget.Step("chapter_01", "unlock"))),
                            new StoryStep(
                                "unlock",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "unlock",
                                        StoryInteractionCommandNames.Unlock,
                                        CreateUnlockArguments(),
                                        true,
                                        new[]
                                        {
                                            StoryInteractionCommandNames.SuccessOutcome,
                                            StoryInteractionCommandNames.FailOutcome
                                        },
                                        new Dictionary<string, StoryTarget>(StringComparer.Ordinal)
                                        {
                                            [StoryInteractionCommandNames.SuccessOutcome] = StoryTarget.Step("chapter_01", "success_line"),
                                            [StoryInteractionCommandNames.FailOutcome] = StoryTarget.Step("chapter_01", "fail_line"),
                                        }))),
                            new StoryStep(
                                "success_line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "interaction.success")),
                            new StoryStep(
                                "fail_line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "interaction.fail")),
                            new StoryStep("end", StoryStepKind.End),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition(StoryMediaCommandNames.PlayVideo, "播放视频", true, CreatePlayVideoArgumentDefinitions(), Array.Empty<string>()),
                    CreateUnlockCommandDefinition(),
                }));
        }

        private static StoryCommand CreateWaitVideoCommand()
        {
            return new StoryCommand(
                "video",
                StoryMediaCommandNames.PlayVideo,
                new StoryArgumentBag(new Dictionary<string, StoryValue>(StringComparer.Ordinal)
                {
                    [StoryMediaCommandNames.VideoSourceArgument] = StoryValue.FromString(SampleVideoSource),
                    [StoryMediaCommandNames.ClipArgument] = StoryValue.FromString(SampleVideoPath)
                }),
                true);
        }

        private static StoryArgumentBag CreateUnlockArguments()
        {
            return new StoryArgumentBag(new Dictionary<string, StoryValue>(StringComparer.Ordinal)
            {
                [StoryInteractionCommandNames.UnlockIdArgument] = StoryValue.FromString("chapter_01.door"),
                [StoryInteractionCommandNames.PuzzleTypeArgument] = StoryValue.FromString(StoryInteractionCommandNames.PuzzleTypeNodeUnlock),
                [StoryInteractionCommandNames.PromptTextKeyArgument] = StoryValue.FromString("unlock.door"),
            });
        }

        private static StoryArgumentBag CreateQteArguments()
        {
            return new StoryArgumentBag(new Dictionary<string, StoryValue>(StringComparer.Ordinal)
            {
                [StoryInteractionCommandNames.InputActionIdArgument] = StoryValue.FromString("space"),
                [StoryInteractionCommandNames.DurationSecondsArgument] = StoryValue.FromNumber(3d),
                [StoryInteractionCommandNames.RequiredCountArgument] = StoryValue.FromNumber(5d),
                [StoryInteractionCommandNames.PromptTextKeyArgument] = StoryValue.FromString("qte.break_free"),
            });
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
                        "输入动作 ID",
                        ParameterValueType.String,
                        true),
                    new StoryCommandArgumentDefinition(
                        StoryInteractionCommandNames.DurationSecondsArgument,
                        "时长",
                        ParameterValueType.Number,
                        true),
                    new StoryCommandArgumentDefinition(
                        StoryInteractionCommandNames.RequiredCountArgument,
                        "需要次数",
                        ParameterValueType.Number),
                    new StoryCommandArgumentDefinition(
                        StoryInteractionCommandNames.PromptTextKeyArgument,
                        "提示文本",
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
                "解锁",
                true,
                new[]
                {
                    new StoryCommandArgumentDefinition(
                        StoryInteractionCommandNames.UnlockIdArgument,
                        "解锁 ID",
                        ParameterValueType.String,
                        true),
                    new StoryCommandArgumentDefinition(
                        StoryInteractionCommandNames.PuzzleTypeArgument,
                        "玩法类型",
                        ParameterValueType.Option,
                        true,
                        options: new[]
                        {
                            StoryInteractionCommandNames.PuzzleTypeLineConnect,
                            StoryInteractionCommandNames.PuzzleTypeNodeUnlock,
                            StoryInteractionCommandNames.PuzzleTypeCustom
                        }),
                    new StoryCommandArgumentDefinition(
                        StoryInteractionCommandNames.PromptTextKeyArgument,
                        "提示文本",
                        ParameterValueType.String,
                        true),
                },
                new[]
                {
                    StoryInteractionCommandNames.SuccessOutcome,
                    StoryInteractionCommandNames.FailOutcome,
                });
        }

        private static StoryProgram CreateParallelJumpChapterProgram()
        {
            return new StoryProgram(
                "story_parallel_jump_chapter",
                "1",
                "chapter_01",
                new[]
                {
                    new StoryChapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new StoryStep(
                                "parallel",
                                StoryStepKind.Parallel,
                                new StoryStepData(
                                    branches: new[]
                                    {
                                        new StoryParallelBranch("branch_video", "视频轨", StoryTarget.Step("chapter_01", "video")),
                                        new StoryParallelBranch("branch_line", "文本轨", StoryTarget.Step("chapter_01", "line")),
                                    })),
                            new StoryStep(
                                "video",
                                StoryStepKind.Command,
                                new StoryStepData(
                                    command: new StoryCommand(
                                        "video",
                                        "play_video",
                                        null,
                                        true,
                                        new[] { "completed" },
                                        new Dictionary<string, StoryTarget>(StringComparer.Ordinal)
                                        {
                                            ["completed"] = StoryTarget.Chapter("chapter_02")
                                        }))),
                            new StoryStep(
                                "line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "parallel.jump.line", target: StoryTarget.StoryEnd())),
                            new StoryStep("chapter_01_end", StoryStepKind.End),
                        }),
                    new StoryChapter(
                        "chapter_02",
                        "第二章",
                        "target_line",
                        new[]
                        {
                            new StoryStep(
                                "target_line",
                                StoryStepKind.Line,
                                new StoryStepData(textKey: "target.line")),
                            new StoryStep("target_end", StoryStepKind.End),
                        }),
                },
                commandSchema: new StoryCommandSchema(new[]
                {
                    new StoryCommandDefinition("play_video", "播放视频", true, Array.Empty<string>(), new[] { "completed" }),
                }));
        }

        private static bool HasPort(NodeParameterSchema schema, string portId)
        {
            for (var i = 0; i < schema.Ports.Count; i++)
            {
                if (schema.Ports[i].PortId == portId)
                {
                    return true;
                }
            }

            return false;
        }

        private static NodeParameterDefinition FindParameter(NodeParameterSchema schema, string key)
        {
            for (var i = 0; i < schema.Parameters.Count; i++)
            {
                if (schema.Parameters[i].Key == key)
                {
                    return schema.Parameters[i];
                }
            }

            return default;
        }

        private static StoryCommandArgumentDefinition[] CreatePlayVideoArgumentDefinitions()
        {
            return new[]
            {
                new StoryCommandArgumentDefinition(
                    StoryMediaCommandNames.VideoSourceArgument,
                    "来源",
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
                    "视频",
                    ParameterValueType.AssetReference,
                    true,
                    "video")
            };
        }

        private static StoryFrameTrack AssertTextFrame(StoryFrame frame, string chapterId, string stepId)
        {
            var track = AssertTrackFrame(frame, StoryFrameTrackKind.Text, chapterId, stepId);
            Assert.IsFalse(frame.WaitsForChoice);
            Assert.IsFalse(frame.WaitsForCommand);
            Assert.IsFalse(frame.WaitsForTime);
            return track;
        }

        private static StoryCommand AssertCommandFrame(StoryFrame frame, string chapterId, string stepId)
        {
            var track = AssertTrackFrame(frame, StoryFrameTrackKind.Command, chapterId, stepId);
            Assert.IsNotNull(track.Command);
            return track.Command;
        }

        private static void AssertChoiceFrame(StoryFrame frame, string chapterId, string stepId, int choiceCount)
        {
            AssertFrame(frame, chapterId, stepId);
            Assert.AreEqual(choiceCount, frame.Choices.Count);
            Assert.IsTrue(frame.WaitsForChoice);
            Assert.IsFalse(frame.WaitsForCommand);
            Assert.IsFalse(frame.WaitsForTime);
            Assert.IsFalse(frame.IsCompleted);
        }

        private static void AssertCompletedFrame(StoryFrame frame, string chapterId, string stepId)
        {
            AssertFrame(frame, chapterId, stepId);
            Assert.AreEqual(0, frame.Tracks.Count);
            Assert.AreEqual(0, frame.Choices.Count);
            Assert.IsTrue(frame.IsCompleted);
        }

        private static StoryFrameTrack AssertTrackFrame(StoryFrame frame, StoryFrameTrackKind kind, string chapterId, string stepId)
        {
            AssertFrame(frame, chapterId, stepId);
            AssertFrameTracks(frame, kind);
            Assert.AreEqual(0, frame.Choices.Count);
            Assert.IsFalse(frame.IsCompleted);
            return frame.Tracks[0];
        }

        private static void AssertFrameTracks(StoryFrame frame, params StoryFrameTrackKind[] kinds)
        {
            Assert.IsNotNull(frame);
            Assert.AreEqual(kinds.Length, frame.Tracks.Count);
            for (var i = 0; i < kinds.Length; i++)
            {
                Assert.AreEqual(kinds[i], frame.Tracks[i].Kind);
            }
        }

        private static void AssertFrame(StoryFrame frame, string chapterId, string stepId)
        {
            Assert.IsNotNull(frame);
            Assert.AreEqual(chapterId, frame.Chapter.ChapterId);
            Assert.AreEqual(stepId, frame.AnchorStep.StepId);
        }

        private static void TryUnregister<T>() where T : class, IGameModule
        {
            try
            {
                App.Unregister<T>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }
        }

        private sealed class FixedFunctionResolver : IStoryFunctionResolver
        {
            private readonly bool m_Result;

            public FixedFunctionResolver(bool result = true)
            {
                m_Result = result;
            }

            public StoryValue Evaluate(string functionName, IReadOnlyList<StoryValue> arguments, StoryRuntimeContext context)
            {
                return StoryValue.FromBoolean(m_Result);
            }
        }

        private readonly struct RecordedCommandExecution
        {
            public RecordedCommandExecution(StoryCommand command, StoryRuntimeContext context)
            {
                Command = command;
                Context = context;
            }

            public StoryCommand Command { get; }

            public StoryRuntimeContext Context { get; }
        }

        private sealed class RecordingCommandHandler : IStoryCommandHandler
        {
            private readonly string m_CommandName;
            private readonly List<RecordedCommandExecution> m_Executions = new List<RecordedCommandExecution>();

            public RecordingCommandHandler(string commandName)
            {
                m_CommandName = commandName;
            }

            public IReadOnlyList<RecordedCommandExecution> Executions => m_Executions;

            public StoryCommandHandle LastHandle { get; private set; }

            public bool CanHandle(StoryCommand command)
            {
                return command != null && string.Equals(command.Name, m_CommandName, StringComparison.Ordinal);
            }

            public IStoryCommandHandle Execute(StoryCommand command, StoryRuntimeContext context)
            {
                LastHandle = new StoryCommandHandle(command);
                m_Executions.Add(new RecordedCommandExecution(command, context));
                return LastHandle;
            }
        }

        private sealed class RecordingFramePresenter : IStoryFramePresenter
        {
            public StoryFrame PresentedFrame { get; private set; }

            public StoryFrame ClearedFrame { get; private set; }

            public void Present(StoryFrame frame, StoryPresenter presenter)
            {
                PresentedFrame = frame;
            }

            public void Clear(StoryFrame frame)
            {
                ClearedFrame = frame;
            }
        }

        private sealed class RecordingMediaCommandPlayer :
            IStoryVideoCommandPlayer,
            IStoryImageCommandPlayer,
            IStoryAudioCommandPlayer
        {
            private readonly List<string> m_Executions = new List<string>();

            public IReadOnlyList<string> Executions => m_Executions;

            public IStoryCommandHandle PlayVideo(StoryCommand command, StoryRuntimeContext context, string clipPath)
            {
                m_Executions.Add("video:" + clipPath);
                return new StoryCommandHandle(command);
            }

            public IStoryCommandHandle ShowImage(StoryCommand command, StoryRuntimeContext context, string imagePath)
            {
                m_Executions.Add("image:" + imagePath);
                return new StoryCommandHandle(command);
            }

            public IStoryCommandHandle PlayAudio(StoryCommand command, StoryRuntimeContext context, string clipPath)
            {
                m_Executions.Add("audio:" + clipPath);
                return new StoryCommandHandle(command);
            }
        }

        private sealed class TestTimeline : TimelineBase
        {
            public TestTimeline(float duration)
            {
                Duration = duration;
            }

            public float LastEvaluatedTime { get; private set; }

            protected override void OnEvaluate(float time)
            {
                LastEvaluatedTime = time;
            }
        }
    }
}
