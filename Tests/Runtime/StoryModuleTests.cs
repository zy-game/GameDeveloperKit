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
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Playback;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryModuleTests : RuntimeTestBase
    {
        private const string SampleVideoSource = MediaCommandNames.VideoSourceStreamingAssets;
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
            var source = FindParameter(playVideo, MediaCommandNames.VideoSourceArgument);
            Assert.IsNotNull(source);
            Assert.AreEqual(ParameterValueType.Option, source.ValueType);
            Assert.IsTrue(source.Required);
            CollectionAssert.AreEqual(
                new[]
                {
                    MediaCommandNames.VideoSourceStreamingAssets,
                    MediaCommandNames.VideoSourcePersistentDataPath,
                    MediaCommandNames.VideoSourceNetworkStream
                },
                source.Options);
            Assert.IsTrue(NodeSchemaRegistry.IsDefaultAuthoringNode(NodeKind.PlayVideo));
            Assert.IsTrue(NodeSchemaRegistry.IsDefaultAuthoringNode(NodeKind.Choice));
            Assert.IsTrue(NodeSchemaRegistry.IsDefaultAuthoringNode(NodeKind.Parallel));
            Assert.IsTrue(NodeSchemaRegistry.IsDefaultAuthoringNode(NodeKind.Merge));
            Assert.AreEqual(Enum.GetValues(typeof(NodeKind)).Length, NodeSchemaRegistry.Schemas.Count);
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
            var program = new Program(
                "story_missing_chapter",
                "1",
                "chapter_02",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "start",
                        new[] { new Step("start", StepKind.Start) }),
                });

            var exception = Assert.Throws<GameException>(() => module.Register(program));

            StringAssert.Contains("entry chapter", exception.Message);
            Assert.IsFalse(module.HasProgram("story_missing_chapter"));
        }

        [Test]
        public void StoryProgram_WhenChoiceTargetMissing_RegistrationFails()
        {
            var module = CreateStartedModule();
            var program = new Program(
                "story_missing_choice_target",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "choice",
                        new[]
                        {
                            new Step(
                                "choice",
                                StepKind.Choice,
                                new StepData(
                                    choices: new[]
                                    {
                                        new Choice("missing", "缺失", null, Target.Step("chapter_01", "missing_step")),
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
            var program = new Program(
                "story_missing_command_target",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "cmd",
                        new[]
                        {
                            new Step(
                                "cmd",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "cmd",
                                        "mini_game",
                                        null,
                                        true,
                                        new[] { "success" },
                                        new Dictionary<string, Target>(StringComparer.Ordinal)
                                        {
                                            ["success"] = Target.Step("chapter_01", "missing_step"),
                                        }))),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition("mini_game", "小游戏", true, Array.Empty<string>(), new[] { "success" }),
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
            var program = new Program(
                "story_undeclared_command_outcome",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "qte",
                        new[]
                        {
                            new Step(
                                "qte",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "qte",
                                        InteractionCommandNames.Qte,
                                        CreateQteArguments(),
                                        true,
                                        new[] { InteractionCommandNames.SuccessOutcome, "timeout" },
                                        new Dictionary<string, Target>(StringComparer.Ordinal)
                                        {
                                            [InteractionCommandNames.SuccessOutcome] = Target.Step("chapter_01", "success_line"),
                                            ["timeout"] = Target.Step("chapter_01", "fail_line"),
                                        }))),
                            new Step(
                                "success_line",
                                StepKind.Line,
                                new StepData(textKey: "interaction.success")),
                            new Step(
                                "fail_line",
                                StepKind.Line,
                                new StepData(textKey: "interaction.fail")),
                        }),
                },
                commandSchema: new CommandSchema(new[]
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
            AssertFrameTracks(frame, FrameTrackKind.Text);
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
            module.Register(CreateProgramDefinition(yesCondition: Expression.FromFunction("can_select_yes")));

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
                yesCondition: Expression.FromFunction("can_select_yes"),
                noCondition: Expression.FromFunction("can_select_no")));

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
            module.Register(CreateProgramDefinition(yesCondition: Expression.FromFunction("can_select_yes")));

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
            var asset = ScriptableObject.CreateInstance<ProgramAsset>();
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
            var presenter = new Presenter(module, framePresenter);
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
            var presenter = new Presenter(module);
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
            var presenter = new Presenter(module);

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
            var presenter = new Presenter(module, framePresenter);
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
            var presenter = new Presenter(module);
            presenter.AddCommandHandler(commandHandler);
            presenter.Start(CreateParallelInlineChoiceProgram());

            var videoHandle = commandHandler.LastHandle;
            var frame = presenter.Select("choice_continue");

            Assert.IsTrue(videoHandle.IsStopped);
            AssertTrackFrame(frame, FrameTrackKind.Text, "chapter_01", "after_choice");
            Assert.AreEqual(0, presenter.ActiveCommandHandles.Count);
        }

        [Test]
        public void StoryPresenter_WhenParallelWaitChoiceAppears_KeepsVideoHandleUntilChoiceSelected()
        {
            var module = CreateStartedModule();
            var commandHandler = new RecordingCommandHandler("play_video");
            var presenter = new Presenter(module);
            presenter.AddCommandHandler(commandHandler);
            presenter.Start(CreateParallelWaitChoiceVideoProgram());

            var videoHandle = commandHandler.LastHandle;
            var choiceFrame = presenter.Evaluate(1.5d);

            Assert.IsFalse(videoHandle.IsStopped);
            Assert.AreEqual(1, commandHandler.Executions.Count);
            Assert.AreEqual(1, presenter.ActiveCommandHandles.Count);
            AssertFrame(choiceFrame, "chapter_01", "parallel");
            AssertFrameTracks(choiceFrame, FrameTrackKind.Command);
            Assert.AreEqual(1, choiceFrame.Choices.Count);
            Assert.IsTrue(choiceFrame.WaitsForChoice);
            Assert.IsTrue(choiceFrame.WaitsForCommand);

            var selectedFrame = presenter.Select("choice_continue");

            Assert.IsTrue(videoHandle.IsStopped);
            AssertTrackFrame(selectedFrame, FrameTrackKind.Text, "chapter_01", "after_choice");
            Assert.AreEqual(0, presenter.ActiveCommandHandles.Count);
        }

        [Test]
        public void StoryPresenter_WhenLoopAudioLeavesFrame_StopsAudioHandle()
        {
            var module = CreateStartedModule();
            var commandHandler = new RecordingCommandHandler("play_audio");
            var presenter = new Presenter(module);
            presenter.AddCommandHandler(commandHandler);

            presenter.Start(CreateLoopAudioContinueProgram());
            var audioHandle = commandHandler.LastHandle;
            var frame = presenter.Continue();

            Assert.IsTrue(audioHandle.IsStopped);
            AssertTrackFrame(frame, FrameTrackKind.Text, "chapter_01", "line");
            Assert.AreEqual(0, presenter.ActiveCommandHandles.Count);
        }

        [Test]
        public void StoryPresenter_WhenParallelChoiceLeavesAudioFrame_StopsAudioHandle()
        {
            var module = CreateStartedModule();
            var commandHandler = new RecordingCommandHandler("play_audio");
            var presenter = new Presenter(module);
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
            var presenter = new Presenter(module);
            presenter.AddCommandHandler(commandHandler);
            presenter.Start(CreateParallelChoiceImageProgram());

            var imageHandle = commandHandler.LastHandle;
            var frame = presenter.Select("choice_a");

            Assert.IsTrue(imageHandle.IsStopped);
            Assert.AreEqual("selected_line", frame.AnchorStep.StepId);
            Assert.AreEqual(0, presenter.ActiveCommandHandles.Count);
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

            AssertTrackFrame(frame, FrameTrackKind.Text, "chapter_01", "success_line");
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

            AssertTrackFrame(frame, FrameTrackKind.Text, "chapter_01", "line_after_wait");
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-0.1d)]
        public void Register_WhenWaitDurationIsInvalid_RejectsProgram(double waitSeconds)
        {
            var module = CreateStartedModule();

            var exception = Assert.Throws<GameException>(() => module.Register(CreateWaitProgram(waitSeconds)));

            StringAssert.Contains("Story wait seconds must be finite and non-negative", exception.Message);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(0d)]
        [TestCase(-0.1d)]
        public void Register_WhenQteDurationIsInvalid_RejectsProgram(double durationSeconds)
        {
            var module = CreateStartedModule();

            var exception = Assert.Throws<GameException>(() =>
                module.Register(CreateParallelWaitQteVideoProgram(durationSeconds)));

            StringAssert.Contains("Story QTE duration must be finite and greater than zero", exception.Message);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-0.1d)]
        public void StorySnapshot_WhenTimeIsInvalid_RejectsSnapshot(double time)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Snapshot(
                "story",
                "1",
                "chapter",
                "step",
                time,
                null,
                null,
                false));
        }

        [Test]
        public void StoryProgram_WhenWaitReceivesPartialDeltas_RemainsWaitingUntilAccumulatedDuration()
        {
            var module = CreateStartedModule();
            module.Register(CreateWaitProgram());
            module.StartProgram("story_wait");

            var frame = module.Evaluate(1d);

            AssertTrackFrame(frame, FrameTrackKind.Wait, "chapter_01", "wait");
            frame = module.Evaluate(0.4d);
            AssertTrackFrame(frame, FrameTrackKind.Wait, "chapter_01", "wait");
            frame = module.Evaluate(0.1d);
            AssertTrackFrame(frame, FrameTrackKind.Text, "chapter_01", "line_after_wait");
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

            AssertTrackFrame(frame, FrameTrackKind.Wait, "chapter_01", "wait");
            frame = module.Evaluate(0.1d);
            AssertTrackFrame(frame, FrameTrackKind.Text, "chapter_01", "line_after_wait");
        }

        [Test]
        public void StoryProgram_WhenJumpTargetsChapter_EntersTargetChapter()
        {
            var module = CreateStartedModule();
            module.Register(CreateChapterJumpProgram());

            var frame = module.StartProgram("story_chapter_jump").CurrentFrame;

            AssertTrackFrame(frame, FrameTrackKind.Text, "chapter_02", "target_line");
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
            AssertFrameTracks(frame, FrameTrackKind.Command, FrameTrackKind.Text);
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
            AssertFrameTracks(frame, FrameTrackKind.Command);
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
            AssertTrackFrame(afterChoice, FrameTrackKind.Text, "chapter_01", "after_merge");
        }

        [Test]
        public void StoryProgram_WhenParallelCommandCompletesFirst_WaitsForTextBranch()
        {
            var module = CreateStartedModule();
            module.Register(CreateParallelContractProgram());
            module.StartProgram("story_parallel_contract");

            var frame = module.CompleteCommand("video", "completed");

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, FrameTrackKind.Text);
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
            AssertFrameTracks(restored, FrameTrackKind.Text);
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
            AssertFrameTracks(frame, FrameTrackKind.Wait);
            Assert.AreEqual("branch_wait", frame.Tracks[0].BranchId);

            frame = module.Evaluate(0.5d);
            AssertTrackFrame(frame, FrameTrackKind.Text, "chapter_01", "after_merge");
        }

        [Test]
        public void StoryProgram_WhenParallelWaitChoiceTriggers_KeepsVideoTrackAndShowsChoice()
        {
            var module = CreateStartedModule();
            module.Register(CreateParallelWaitChoiceVideoProgram());

            var frame = module.StartProgram("story_parallel_wait_choice").CurrentFrame;

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, FrameTrackKind.Command, FrameTrackKind.Wait);
            Assert.AreEqual("video", frame.Tracks[0].Command.CommandId);
            Assert.AreEqual("branch_video", frame.Tracks[0].BranchId);
            Assert.AreEqual("wait_choice", frame.Tracks[1].Step.StepId);
            Assert.AreEqual("branch_interaction", frame.Tracks[1].BranchId);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsTrue(frame.WaitsForTime);

            frame = module.Evaluate(1.5d);

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, FrameTrackKind.Command);
            Assert.AreEqual("video", frame.Tracks[0].Command.CommandId);
            Assert.AreEqual("branch_video", frame.Tracks[0].BranchId);
            Assert.AreEqual(1, frame.Choices.Count);
            Assert.AreEqual("choice_continue", frame.Choices[0].ChoiceId);
            Assert.AreEqual("branch_interaction", frame.Choices[0].BranchId);
            Assert.IsTrue(frame.WaitsForChoice);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsFalse(frame.WaitsForTime);

            frame = module.Select("choice_continue");

            AssertTrackFrame(frame, FrameTrackKind.Text, "chapter_01", "after_choice");
        }

        [Test]
        public void StoryProgram_WhenParallelWaitCommandTriggers_KeepsVideoTrackAndCompletesInteractionOutcome()
        {
            var module = CreateStartedModule();
            module.Register(CreateParallelWaitCommandVideoProgram());

            var frame = module.StartProgram("story_parallel_wait_command").CurrentFrame;

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, FrameTrackKind.Command, FrameTrackKind.Wait);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsTrue(frame.WaitsForTime);

            frame = module.Evaluate(1.5d);

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, FrameTrackKind.Command, FrameTrackKind.Command);
            Assert.AreEqual("video", frame.Tracks[0].Command.CommandId);
            Assert.AreEqual("branch_video", frame.Tracks[0].BranchId);
            Assert.AreEqual("custom_interaction", frame.Tracks[1].Command.CommandId);
            Assert.AreEqual("branch_interaction", frame.Tracks[1].BranchId);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsFalse(frame.WaitsForTime);

            frame = module.CompleteCommand("custom_interaction", "success");

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, FrameTrackKind.Command, FrameTrackKind.Text);
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
            AssertFrameTracks(frame, FrameTrackKind.Command, FrameTrackKind.Wait);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsTrue(frame.WaitsForTime);

            frame = module.Evaluate(1.5d);

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, FrameTrackKind.Command, FrameTrackKind.Command);
            Assert.AreEqual("video", frame.Tracks[0].Command.CommandId);
            Assert.AreEqual(MediaCommandNames.PlayVideo, frame.Tracks[0].Command.Name);
            Assert.AreEqual("branch_video", frame.Tracks[0].BranchId);
            Assert.AreEqual("qte", frame.Tracks[1].Command.CommandId);
            Assert.AreEqual(InteractionCommandNames.Qte, frame.Tracks[1].Command.Name);
            Assert.AreEqual("branch_interaction", frame.Tracks[1].BranchId);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsFalse(frame.WaitsForTime);

            frame = module.CompleteCommand("qte", InteractionCommandNames.SuccessOutcome);

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, FrameTrackKind.Command, FrameTrackKind.Text);
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

            var frame = module.CompleteCommand("qte", InteractionCommandNames.FailOutcome);

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, FrameTrackKind.Command, FrameTrackKind.Text);
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
            AssertFrameTracks(frame, FrameTrackKind.Command, FrameTrackKind.Wait);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsTrue(frame.WaitsForTime);

            frame = module.Evaluate(1.5d);

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, FrameTrackKind.Command, FrameTrackKind.Command);
            Assert.AreEqual("video", frame.Tracks[0].Command.CommandId);
            Assert.AreEqual(MediaCommandNames.PlayVideo, frame.Tracks[0].Command.Name);
            Assert.AreEqual("branch_video", frame.Tracks[0].BranchId);
            Assert.AreEqual("unlock", frame.Tracks[1].Command.CommandId);
            Assert.AreEqual(InteractionCommandNames.Unlock, frame.Tracks[1].Command.Name);
            Assert.AreEqual("chapter_01.door", frame.Tracks[1].Command.Arguments.GetString(InteractionCommandNames.UnlockIdArgument));
            Assert.AreEqual(InteractionCommandNames.PuzzleTypeNodeUnlock, frame.Tracks[1].Command.Arguments.GetString(InteractionCommandNames.PuzzleTypeArgument));
            Assert.AreEqual("unlock.door", frame.Tracks[1].Command.Arguments.GetString(InteractionCommandNames.PromptTextKeyArgument));
            Assert.AreEqual("branch_interaction", frame.Tracks[1].BranchId);
            Assert.IsTrue(frame.WaitsForCommand);
            Assert.IsFalse(frame.WaitsForTime);
            Assert.IsFalse(frame.WaitsForChoice);

            frame = module.CompleteCommand("unlock", InteractionCommandNames.SuccessOutcome);

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, FrameTrackKind.Command, FrameTrackKind.Text);
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

            var frame = module.CompleteCommand("unlock", InteractionCommandNames.FailOutcome);

            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, FrameTrackKind.Command, FrameTrackKind.Text);
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
            AssertFrameTracks(frame, FrameTrackKind.Command, FrameTrackKind.Text);

            frame = module.Continue();
            AssertFrame(frame, "chapter_01", "parallel");
            AssertFrameTracks(frame, FrameTrackKind.Command);

            var targetFrame = module.CompleteCommand("video", "completed");
            AssertTrackFrame(targetFrame, FrameTrackKind.Text, "chapter_02", "target_line");
        }

        [Test]
        public void StoryProgram_WhenParallelHasSingleBranch_RegistrationFails()
        {
            var module = CreateStartedModule();
            var program = CreateParallelContractProgram(new[]
            {
                new ParallelBranch("branch_video", "视频轨", Target.Step("chapter_01", "video")),
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
            var program = new Program(
                "story_missing_command",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "cmd",
                        new[]
                        {
                            new Step(
                                "cmd",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command("cmd", "unknown_cmd"))),
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
                new Dictionary<string, Value>(StringComparer.Ordinal),
                new CommandArgumentDefinition(
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
                new Dictionary<string, Value>(StringComparer.Ordinal)
                {
                    ["duration"] = Value.FromString("fast")
                },
                new CommandArgumentDefinition(
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
                new Dictionary<string, Value>(StringComparer.Ordinal)
                {
                    [MediaCommandNames.VideoSourceArgument] = Value.FromString("asset_bundle")
                },
                new CommandArgumentDefinition(
                    MediaCommandNames.VideoSourceArgument,
                    "来源",
                    ParameterValueType.Option,
                    true,
                    options: new[]
                    {
                        MediaCommandNames.VideoSourceStreamingAssets,
                        MediaCommandNames.VideoSourcePersistentDataPath,
                        MediaCommandNames.VideoSourceNetworkStream
                    }));

            var exception = Assert.Throws<GameException>(() => module.Register(program));

            StringAssert.Contains("story:story_command_arguments", exception.Message);
            StringAssert.Contains("command:play_video", exception.Message);
            StringAssert.Contains("argument:source", exception.Message);
        }

        [Test]
        public void StoryCommandDefinition_WhenCreatedFromArgumentNames_KeepsArgumentDefinitions()
        {
            var definition = new CommandDefinition("play_video", "播放视频", true, new[] { "clip" }, new[] { "completed" });

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

        private static global::GameDeveloperKit.Story.Model.Command CreateMediaCommand(string commandId, string commandName, string argumentKey, string path)
        {
            return new global::GameDeveloperKit.Story.Model.Command(
                commandId,
                commandName,
                new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                {
                    [argumentKey] = Value.FromString(path),
                }));
        }

        private static Program CreateProgramDefinition(
            Expression yesCondition = null,
            Expression noCondition = null)
        {
            return new Program(
                "story_program",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "start",
                        new[]
                        {
                            new Step("start", StepKind.Start),
                            new Step(
                                "line_1",
                                StepKind.Line,
                                new StepData(
                                    textKey: "line.key",
                                    speaker: "npc",
                                    tags: new[] { "story" })),
                            new Step(
                                "choice_1",
                                StepKind.Choice,
                                new StepData(
                                    choices: new[]
                                    {
                                        new Choice("choice_yes", "choice.yes", yesCondition, Target.Step("chapter_01", "cmd_1")),
                                        new Choice("choice_no", "choice.no", noCondition, Target.StoryEnd()),
                                    })),
                            new Step(
                                "cmd_1",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "play_video",
                                        "play_video",
                                        new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                                        {
                                            [MediaCommandNames.VideoSourceArgument] = Value.FromString(SampleVideoSource),
                                            ["clip"] = Value.FromString(SampleVideoPath)
                                        }),
                                        true,
                                        new[] { "completed" },
                                        new Dictionary<string, Target>(StringComparer.Ordinal)
                                        {
                                            ["completed"] = Target.Step("chapter_01", "end")
                                        }))),
                            new Step("end", StepKind.End),
                        }),
                },
                new VariableSchema(new[]
                {
                    new VariableDefinition("flag", VariableType.Boolean, Value.FromBoolean(false)),
                }),
                new CommandSchema(new[]
                {
                    new CommandDefinition("play_video", "播放视频", true, CreatePlayVideoArgumentDefinitions(), new[] { "completed" }),
                }));
        }

        private static Program CreateCommandArgumentProgram(
            IReadOnlyDictionary<string, Value> arguments,
            CommandArgumentDefinition argumentDefinition)
        {
            return new Program(
                "story_command_arguments",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "cmd",
                        new[]
                        {
                            new Step(
                                "cmd",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "cmd",
                                        "play_video",
                                        new ArgumentBag(arguments)))),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition(
                        "play_video",
                        "播放视频",
                        false,
                        new[] { argumentDefinition },
                        Array.Empty<string>()),
                }));
        }

        private static Program CreateLineOnlyProgram()
        {
            return new Program(
                "story_line_only",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "start",
                        new[]
                        {
                            new Step("start", StepKind.Start),
                            new Step(
                                "line_1",
                                StepKind.Line,
                                new StepData(textKey: "line.key")),
                            new Step("end", StepKind.End),
                        }),
                });
        }

        private static Program CreateVideoChoiceProgram()
        {
            return new Program(
                "story_video_choice",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "video",
                        new[]
                        {
                            new Step(
                                "video",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "video",
                                        "play_video",
                                        new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                                        {
                                            [MediaCommandNames.VideoSourceArgument] = Value.FromString(SampleVideoSource),
                                            ["clip"] = Value.FromString(SampleVideoPath)
                                        }),
                                        true))),
                            new Step(
                                "choice",
                                StepKind.Choice,
                                new StepData(
                                    choices: new[]
                                    {
                                        new Choice("choice_continue", "继续", null, Target.Step("chapter_01", "end")),
                                    })),
                            new Step("end", StepKind.End),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition("play_video", "播放视频", true, CreatePlayVideoArgumentDefinitions(), Array.Empty<string>()),
                }));
        }

        private static Program CreateParallelInlineChoiceProgram()
        {
            return new Program(
                "story_parallel_inline_choice",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new Step(
                                "parallel",
                                StepKind.Parallel,
                                new StepData(
                                    branches: new[]
                                    {
                                        new ParallelBranch("branch_video", "视频轨", Target.Step("chapter_01", "video")),
                                        new ParallelBranch("branch_dialogue", "对白轨", Target.Step("chapter_01", "line")),
                                    })),
                            new Step(
                                "video",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "video",
                                        "play_video",
                                        new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                                        {
                                            [MediaCommandNames.VideoSourceArgument] = Value.FromString(SampleVideoSource),
                                            ["clip"] = Value.FromString(SampleVideoPath)
                                        }),
                                        true))),
                            new Step(
                                "line",
                                StepKind.Line,
                                new StepData(
                                    textKey: "parallel.dialogue",
                                    target: Target.Step("chapter_01", "choice"))),
                            new Step(
                                "choice",
                                StepKind.Choice,
                                new StepData(
                                    choices: new[]
                                    {
                                        new Choice("choice_continue", "继续", null, Target.Step("chapter_01", "after_choice")),
                                    })),
                            new Step(
                                "after_choice",
                                StepKind.Line,
                                new StepData(textKey: "after.choice")),
                            new Step("end", StepKind.End),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition("play_video", "播放视频", true, CreatePlayVideoArgumentDefinitions(), Array.Empty<string>()),
                }));
        }

        private static Program CreateMediaNarrationChoiceProgram()
        {
            return new Program(
                "story_media_choice",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "image",
                        new[]
                        {
                            new Step(
                                "image",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "image",
                                        "show_image",
                                        new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                                        {
                                            ["image"] = Value.FromString(SampleImagePath)
                                        })))),
                            new Step(
                                "audio",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "audio",
                                        "play_audio",
                                        new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                                        {
                                            ["clip"] = Value.FromString(SampleAudioPath)
                                        })))),
                            new Step(
                                "narration",
                                StepKind.Line,
                                new StepData(textKey: "narration.key")),
                            new Step(
                                "choice",
                                StepKind.Choice,
                                new StepData(
                                    choices: new[]
                                    {
                                        new Choice("choice_continue", "继续", null, Target.Step("chapter_01", "end")),
                                    })),
                            new Step("end", StepKind.End),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition("show_image", "显示图片", false, new[] { "image" }, Array.Empty<string>()),
                    new CommandDefinition("play_audio", "播放音频", false, new[] { "clip" }, Array.Empty<string>()),
                }));
        }

        private static Program CreateLoopAudioContinueProgram()
        {
            return new Program(
                "story_loop_audio_continue",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "audio",
                        new[]
                        {
                            new Step(
                                "audio",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "audio",
                                        "play_audio",
                                        new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                                        {
                                            ["clip"] = Value.FromString(SampleAudioPath),
                                            ["loop"] = Value.FromBoolean(true)
                                        })))),
                            new Step(
                                "line",
                                StepKind.Line,
                                new StepData(textKey: "after.audio")),
                            new Step("end", StepKind.End),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition(
                        "play_audio",
                        "播放音频",
                        false,
                        new[]
                        {
                            new CommandArgumentDefinition("clip", "音频", ParameterValueType.String, true),
                            new CommandArgumentDefinition("loop", "循环播放", ParameterValueType.Boolean)
                        },
                        Array.Empty<string>()),
                }));
        }

        private static Program CreateParallelChoiceAudioProgram()
        {
            return new Program(
                "story_parallel_choice_audio",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new Step(
                                "parallel",
                                StepKind.Parallel,
                                new StepData(
                                    branches: new[]
                                    {
                                        new ParallelBranch("branch_audio", "音频轨", Target.Step("chapter_01", "audio")),
                                        new ParallelBranch("branch_dialogue", "对白轨", Target.Step("chapter_01", "line")),
                                    })),
                            new Step(
                                "audio",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "audio",
                                        "play_audio",
                                        new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                                        {
                                            ["clip"] = Value.FromString(SampleAudioPath)
                                        })))),
                            new Step(
                                "line",
                                StepKind.Line,
                                new StepData(
                                    textKey: "parallel.line",
                                    target: Target.Step("chapter_01", "choice"))),
                            new Step(
                                "choice",
                                StepKind.Choice,
                                new StepData(
                                    choices: new[]
                                    {
                                        new Choice("choice_a", "选项 A", null, Target.Step("chapter_01", "selected_line"), null, "branch_dialogue"),
                                    })),
                            new Step(
                                "selected_line",
                                StepKind.Line,
                                new StepData(textKey: "selected.line")),
                            new Step("end", StepKind.End),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition("play_audio", "播放音频", false, new[] { "clip" }, Array.Empty<string>()),
                }));
        }

        private static Program CreateParallelChoiceImageProgram()
        {
            return new Program(
                "story_parallel_choice_image",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new Step(
                                "parallel",
                                StepKind.Parallel,
                                new StepData(
                                    branches: new[]
                                    {
                                        new ParallelBranch("branch_image", "图片轨", Target.Step("chapter_01", "image")),
                                        new ParallelBranch("branch_dialogue", "对白轨", Target.Step("chapter_01", "line")),
                                    })),
                            new Step(
                                "image",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "image",
                                        "show_image",
                                        new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                                        {
                                            ["image"] = Value.FromString(SampleImagePath)
                                        }),
                                        false))),
                            new Step(
                                "line",
                                StepKind.Line,
                                new StepData(
                                    textKey: "parallel.line",
                                    target: Target.Step("chapter_01", "choice_a"))),
                            new Step(
                                "choice_a",
                                StepKind.Choice,
                                new StepData(
                                    choices: new[]
                                    {
                                        new Choice("choice_a", "选项 A", null, Target.Step("chapter_01", "selected_line"), null, "branch_dialogue"),
                                    })),
                            new Step(
                                "selected_line",
                                StepKind.Line,
                                new StepData(textKey: "selected.line")),
                            new Step("end", StepKind.End),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition("show_image", "显示图片", false, new[] { "image" }, Array.Empty<string>()),
                }));
        }

        private static Program CreateCommandOutcomeProgram()
        {
            return new Program(
                "story_command_outcome",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "cmd",
                        new[]
                        {
                            new Step(
                                "cmd",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "mini_game",
                                        "mini_game",
                                        new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                                        {
                                            ["miniGameId"] = Value.FromString("lock")
                                        }),
                                        true,
                                        new[] { "success", "fail" },
                                        new Dictionary<string, Target>(StringComparer.Ordinal)
                                        {
                                            ["success"] = Target.Step("chapter_01", "success_line"),
                                            ["fail"] = Target.Step("chapter_01", "fail_line"),
                                        }))),
                            new Step(
                                "success_line",
                                StepKind.Line,
                                new StepData(textKey: "success.key")),
                            new Step("success_end", StepKind.End),
                            new Step(
                                "fail_line",
                                StepKind.Line,
                                new StepData(textKey: "fail.key")),
                            new Step("fail_end", StepKind.End),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition("mini_game", "小游戏", true, new[] { "miniGameId" }, new[] { "success", "fail" }),
                }));
        }

        private static Program CreateBlockingCommandWithoutOutcomeProgram()
        {
            return new Program(
                "story_command_without_outcome",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "cmd",
                        new[]
                        {
                            new Step(
                                "cmd",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "external",
                                        "external_action",
                                        null,
                                        true))),
                            new Step("end", StepKind.End),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition("external_action", "外部动作", true, Array.Empty<string>(), Array.Empty<string>()),
                }));
        }

        private static Program CreateWaitProgram(double waitSeconds = 1.5d)
        {
            return new Program(
                "story_wait",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "wait",
                        new[]
                        {
                            new Step(
                                "wait",
                                StepKind.Wait,
                                new StepData(waitSeconds: waitSeconds)),
                            new Step(
                                "line_after_wait",
                                StepKind.Line,
                                new StepData(textKey: "after.wait")),
                            new Step("end", StepKind.End),
                        }),
                });
        }

        private static Program CreateChapterJumpProgram()
        {
            return new Program(
                "story_chapter_jump",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "jump",
                        new[]
                        {
                            new Step(
                                "jump",
                                StepKind.Jump,
                                new StepData(target: Target.Chapter("chapter_02"))),
                        }),
                    new Chapter(
                        "chapter_02",
                        "第二章",
                        "target_line",
                        new[]
                        {
                            new Step(
                                "target_line",
                                StepKind.Line,
                                new StepData(textKey: "target.line")),
                            new Step("target_end", StepKind.End),
                        }),
                });
        }

        private static Program CreateParallelContractProgram(
            IReadOnlyList<ParallelBranch> branches = null,
            string mergeParallelStepId = "parallel")
        {
            return new Program(
                "story_parallel_contract",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new Step(
                                "parallel",
                                StepKind.Parallel,
                                new StepData(
                                    branches: branches ?? new[]
                                    {
                                        new ParallelBranch("branch_video", "视频轨", Target.Step("chapter_01", "video")),
                                        new ParallelBranch("branch_dialogue", "对白轨", Target.Step("chapter_01", "line")),
                                    })),
                            new Step(
                                "video",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "video",
                                        "play_video",
                                        null,
                                        true,
                                        new[] { "completed" },
                                        new Dictionary<string, Target>(StringComparer.Ordinal)
                                        {
                                            ["completed"] = Target.Step("chapter_01", "merge")
                                        }))),
                            new Step(
                                "line",
                                StepKind.Line,
                                new StepData(textKey: "parallel.line", target: Target.Step("chapter_01", "merge"))),
                            new Step(
                                "merge",
                                StepKind.Merge,
                                new StepData(
                                    target: Target.Step("chapter_01", "merge_choices"),
                                    parallelStepId: mergeParallelStepId)),
                            new Step(
                                "merge_choices",
                                StepKind.Choice,
                                new StepData(
                                    choices: new[]
                                    {
                                        new Choice("choice_continue", "继续", null, Target.Step("chapter_01", "after_merge")),
                                    })),
                            new Step(
                                "after_merge",
                                StepKind.Line,
                                new StepData(textKey: "after.merge")),
                            new Step("end", StepKind.End),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition("play_video", "播放视频", true, Array.Empty<string>(), new[] { "completed" }),
                }));
        }

        private static Program CreateParallelWaitProgram()
        {
            return new Program(
                "story_parallel_wait",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new Step(
                                "parallel",
                                StepKind.Parallel,
                                new StepData(
                                    branches: new[]
                                    {
                                        new ParallelBranch("branch_wait", "等待轨", Target.Step("chapter_01", "wait")),
                                        new ParallelBranch("branch_line", "文本轨", Target.Step("chapter_01", "line")),
                                    })),
                            new Step(
                                "wait",
                                StepKind.Wait,
                                new StepData(waitSeconds: 1.5d, target: Target.Step("chapter_01", "merge"))),
                            new Step(
                                "line",
                                StepKind.Line,
                                new StepData(textKey: "parallel.wait.line", target: Target.Step("chapter_01", "merge"))),
                            new Step(
                                "merge",
                                StepKind.Merge,
                                new StepData(
                                    target: Target.Step("chapter_01", "after_merge"),
                                    parallelStepId: "parallel")),
                            new Step(
                                "after_merge",
                                StepKind.Line,
                                new StepData(textKey: "after.parallel.wait")),
                            new Step("end", StepKind.End),
                        }),
                });
        }

        private static Program CreateParallelWaitChoiceVideoProgram()
        {
            return new Program(
                "story_parallel_wait_choice",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new Step(
                                "parallel",
                                StepKind.Parallel,
                                new StepData(
                                    branches: new[]
                                    {
                                        new ParallelBranch("branch_video", "视频轨", Target.Step("chapter_01", "video")),
                                        new ParallelBranch("branch_interaction", "交互轨", Target.Step("chapter_01", "wait_choice")),
                                    })),
                            new Step(
                                "video",
                                StepKind.Command,
                                new StepData(command: CreateWaitVideoCommand())),
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
                                new StepData(textKey: "after.choice")),
                            new Step("end", StepKind.End),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition("play_video", "播放视频", true, CreatePlayVideoArgumentDefinitions(), Array.Empty<string>()),
                }));
        }

        private static Program CreateParallelWaitCommandVideoProgram()
        {
            return new Program(
                "story_parallel_wait_command",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new Step(
                                "parallel",
                                StepKind.Parallel,
                                new StepData(
                                    branches: new[]
                                    {
                                        new ParallelBranch("branch_video", "视频轨", Target.Step("chapter_01", "video")),
                                        new ParallelBranch("branch_interaction", "交互轨", Target.Step("chapter_01", "wait_command")),
                                    })),
                            new Step(
                                "video",
                                StepKind.Command,
                                new StepData(command: CreateWaitVideoCommand())),
                            new Step(
                                "wait_command",
                                StepKind.Wait,
                                new StepData(waitSeconds: 1.5d, target: Target.Step("chapter_01", "custom_interaction"))),
                            new Step(
                                "custom_interaction",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "custom_interaction",
                                        "custom_interaction",
                                        waitForCompletion: true,
                                        outcomePorts: new[] { "success", "fail" },
                                        outcomeTargets: new Dictionary<string, Target>(StringComparer.Ordinal)
                                        {
                                            ["success"] = Target.Step("chapter_01", "success_line"),
                                            ["fail"] = Target.Step("chapter_01", "fail_line"),
                                        }))),
                            new Step(
                                "success_line",
                                StepKind.Line,
                                new StepData(textKey: "interaction.success")),
                            new Step(
                                "fail_line",
                                StepKind.Line,
                                new StepData(textKey: "interaction.fail")),
                            new Step("end", StepKind.End),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition("play_video", "播放视频", true, CreatePlayVideoArgumentDefinitions(), Array.Empty<string>()),
                    new CommandDefinition("custom_interaction", "自定义互动", true, Array.Empty<CommandArgumentDefinition>(), new[] { "success", "fail" }),
                }));
        }

        private static Program CreateParallelWaitQteVideoProgram(double qteDurationSeconds = 3d)
        {
            return new Program(
                "story_parallel_wait_qte",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new Step(
                                "parallel",
                                StepKind.Parallel,
                                new StepData(
                                    branches: new[]
                                    {
                                        new ParallelBranch("branch_video", "视频轨", Target.Step("chapter_01", "video")),
                                        new ParallelBranch("branch_interaction", "交互轨", Target.Step("chapter_01", "wait_qte")),
                                    })),
                            new Step(
                                "video",
                                StepKind.Command,
                                new StepData(command: CreateWaitVideoCommand())),
                            new Step(
                                "wait_qte",
                                StepKind.Wait,
                                new StepData(waitSeconds: 1.5d, target: Target.Step("chapter_01", "qte"))),
                            new Step(
                                "qte",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "qte",
                                        InteractionCommandNames.Qte,
                                        CreateQteArguments(qteDurationSeconds),
                                        true,
                                        new[]
                                        {
                                            InteractionCommandNames.SuccessOutcome,
                                            InteractionCommandNames.FailOutcome
                                        },
                                        new Dictionary<string, Target>(StringComparer.Ordinal)
                                        {
                                            [InteractionCommandNames.SuccessOutcome] = Target.Step("chapter_01", "success_line"),
                                            [InteractionCommandNames.FailOutcome] = Target.Step("chapter_01", "fail_line"),
                                        }))),
                            new Step(
                                "success_line",
                                StepKind.Line,
                                new StepData(textKey: "interaction.success")),
                            new Step(
                                "fail_line",
                                StepKind.Line,
                                new StepData(textKey: "interaction.fail")),
                            new Step("end", StepKind.End),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition(MediaCommandNames.PlayVideo, "播放视频", true, CreatePlayVideoArgumentDefinitions(), Array.Empty<string>()),
                    CreateQteCommandDefinition(),
                }));
        }

        private static Program CreateParallelWaitUnlockVideoProgram()
        {
            return new Program(
                "story_parallel_wait_unlock",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new Step(
                                "parallel",
                                StepKind.Parallel,
                                new StepData(
                                    branches: new[]
                                    {
                                        new ParallelBranch("branch_video", "视频轨", Target.Step("chapter_01", "video")),
                                        new ParallelBranch("branch_interaction", "交互轨", Target.Step("chapter_01", "wait_unlock")),
                                    })),
                            new Step(
                                "video",
                                StepKind.Command,
                                new StepData(command: CreateWaitVideoCommand())),
                            new Step(
                                "wait_unlock",
                                StepKind.Wait,
                                new StepData(waitSeconds: 1.5d, target: Target.Step("chapter_01", "unlock"))),
                            new Step(
                                "unlock",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "unlock",
                                        InteractionCommandNames.Unlock,
                                        CreateUnlockArguments(),
                                        true,
                                        new[]
                                        {
                                            InteractionCommandNames.SuccessOutcome,
                                            InteractionCommandNames.FailOutcome
                                        },
                                        new Dictionary<string, Target>(StringComparer.Ordinal)
                                        {
                                            [InteractionCommandNames.SuccessOutcome] = Target.Step("chapter_01", "success_line"),
                                            [InteractionCommandNames.FailOutcome] = Target.Step("chapter_01", "fail_line"),
                                        }))),
                            new Step(
                                "success_line",
                                StepKind.Line,
                                new StepData(textKey: "interaction.success")),
                            new Step(
                                "fail_line",
                                StepKind.Line,
                                new StepData(textKey: "interaction.fail")),
                            new Step("end", StepKind.End),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition(MediaCommandNames.PlayVideo, "播放视频", true, CreatePlayVideoArgumentDefinitions(), Array.Empty<string>()),
                    CreateUnlockCommandDefinition(),
                }));
        }

        private static global::GameDeveloperKit.Story.Model.Command CreateWaitVideoCommand()
        {
            return new global::GameDeveloperKit.Story.Model.Command(
                "video",
                MediaCommandNames.PlayVideo,
                new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                {
                    [MediaCommandNames.VideoSourceArgument] = Value.FromString(SampleVideoSource),
                    [MediaCommandNames.ClipArgument] = Value.FromString(SampleVideoPath)
                }),
                true);
        }

        private static ArgumentBag CreateUnlockArguments()
        {
            return new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
            {
                [InteractionCommandNames.UnlockIdArgument] = Value.FromString("chapter_01.door"),
                [InteractionCommandNames.PuzzleTypeArgument] = Value.FromString(InteractionCommandNames.PuzzleTypeNodeUnlock),
                [InteractionCommandNames.PromptTextKeyArgument] = Value.FromString("unlock.door"),
            });
        }

        private static ArgumentBag CreateQteArguments(double durationSeconds = 3d)
        {
            return new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
            {
                [InteractionCommandNames.InputActionIdArgument] = Value.FromString("space"),
                [InteractionCommandNames.DurationSecondsArgument] = Value.FromNumber(durationSeconds),
                [InteractionCommandNames.RequiredCountArgument] = Value.FromNumber(5d),
                [InteractionCommandNames.PromptTextKeyArgument] = Value.FromString("qte.break_free"),
            });
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
                        "输入动作 ID",
                        ParameterValueType.String,
                        true),
                    new CommandArgumentDefinition(
                        InteractionCommandNames.DurationSecondsArgument,
                        "时长",
                        ParameterValueType.Number,
                        true),
                    new CommandArgumentDefinition(
                        InteractionCommandNames.RequiredCountArgument,
                        "需要次数",
                        ParameterValueType.Number),
                    new CommandArgumentDefinition(
                        InteractionCommandNames.PromptTextKeyArgument,
                        "提示文本",
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
                "解锁",
                true,
                new[]
                {
                    new CommandArgumentDefinition(
                        InteractionCommandNames.UnlockIdArgument,
                        "解锁 ID",
                        ParameterValueType.String,
                        true),
                    new CommandArgumentDefinition(
                        InteractionCommandNames.PuzzleTypeArgument,
                        "玩法类型",
                        ParameterValueType.Option,
                        true,
                        options: new[]
                        {
                            InteractionCommandNames.PuzzleTypeLineConnect,
                            InteractionCommandNames.PuzzleTypeNodeUnlock,
                            InteractionCommandNames.PuzzleTypeCustom
                        }),
                    new CommandArgumentDefinition(
                        InteractionCommandNames.PromptTextKeyArgument,
                        "提示文本",
                        ParameterValueType.String,
                        true),
                },
                new[]
                {
                    InteractionCommandNames.SuccessOutcome,
                    InteractionCommandNames.FailOutcome,
                });
        }

        private static Program CreateParallelJumpChapterProgram()
        {
            return new Program(
                "story_parallel_jump_chapter",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "第一章",
                        "parallel",
                        new[]
                        {
                            new Step(
                                "parallel",
                                StepKind.Parallel,
                                new StepData(
                                    branches: new[]
                                    {
                                        new ParallelBranch("branch_video", "视频轨", Target.Step("chapter_01", "video")),
                                        new ParallelBranch("branch_line", "文本轨", Target.Step("chapter_01", "line")),
                                    })),
                            new Step(
                                "video",
                                StepKind.Command,
                                new StepData(
                                    command: new global::GameDeveloperKit.Story.Model.Command(
                                        "video",
                                        "play_video",
                                        null,
                                        true,
                                        new[] { "completed" },
                                        new Dictionary<string, Target>(StringComparer.Ordinal)
                                        {
                                            ["completed"] = Target.Chapter("chapter_02")
                                        }))),
                            new Step(
                                "line",
                                StepKind.Line,
                                new StepData(textKey: "parallel.jump.line", target: Target.StoryEnd())),
                            new Step("chapter_01_end", StepKind.End),
                        }),
                    new Chapter(
                        "chapter_02",
                        "第二章",
                        "target_line",
                        new[]
                        {
                            new Step(
                                "target_line",
                                StepKind.Line,
                                new StepData(textKey: "target.line")),
                            new Step("target_end", StepKind.End),
                        }),
                },
                commandSchema: new CommandSchema(new[]
                {
                    new CommandDefinition("play_video", "播放视频", true, Array.Empty<string>(), new[] { "completed" }),
                }));
        }

        private static bool HasPort(NodeSchema schema, string portId)
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

        private static NodeParameterDefinition FindParameter(NodeSchema schema, string key)
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

        private static CommandArgumentDefinition[] CreatePlayVideoArgumentDefinitions()
        {
            return new[]
            {
                new CommandArgumentDefinition(
                    MediaCommandNames.VideoSourceArgument,
                    "来源",
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
                    "视频",
                    ParameterValueType.AssetReference,
                    true,
                    "video")
            };
        }

        private static FrameTrack AssertTextFrame(Frame frame, string chapterId, string stepId)
        {
            var track = AssertTrackFrame(frame, FrameTrackKind.Text, chapterId, stepId);
            Assert.IsFalse(frame.WaitsForChoice);
            Assert.IsFalse(frame.WaitsForCommand);
            Assert.IsFalse(frame.WaitsForTime);
            return track;
        }

        private static global::GameDeveloperKit.Story.Model.Command AssertCommandFrame(Frame frame, string chapterId, string stepId)
        {
            var track = AssertTrackFrame(frame, FrameTrackKind.Command, chapterId, stepId);
            Assert.IsNotNull(track.Command);
            return track.Command;
        }

        private static void AssertChoiceFrame(Frame frame, string chapterId, string stepId, int choiceCount)
        {
            AssertFrame(frame, chapterId, stepId);
            Assert.AreEqual(choiceCount, frame.Choices.Count);
            Assert.IsTrue(frame.WaitsForChoice);
            Assert.IsFalse(frame.WaitsForCommand);
            Assert.IsFalse(frame.WaitsForTime);
            Assert.IsFalse(frame.IsCompleted);
        }

        private static void AssertCompletedFrame(Frame frame, string chapterId, string stepId)
        {
            AssertFrame(frame, chapterId, stepId);
            Assert.AreEqual(0, frame.Tracks.Count);
            Assert.AreEqual(0, frame.Choices.Count);
            Assert.IsTrue(frame.IsCompleted);
        }

        private static FrameTrack AssertTrackFrame(Frame frame, FrameTrackKind kind, string chapterId, string stepId)
        {
            AssertFrame(frame, chapterId, stepId);
            AssertFrameTracks(frame, kind);
            Assert.AreEqual(0, frame.Choices.Count);
            Assert.IsFalse(frame.IsCompleted);
            return frame.Tracks[0];
        }

        private static void AssertFrameTracks(Frame frame, params FrameTrackKind[] kinds)
        {
            Assert.IsNotNull(frame);
            Assert.AreEqual(kinds.Length, frame.Tracks.Count);
            for (var i = 0; i < kinds.Length; i++)
            {
                Assert.AreEqual(kinds[i], frame.Tracks[i].Kind);
            }
        }

        private static void AssertFrame(Frame frame, string chapterId, string stepId)
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

        private sealed class FixedFunctionResolver : IFunctionResolver
        {
            private readonly bool m_Result;

            public FixedFunctionResolver(bool result = true)
            {
                m_Result = result;
            }

            public Value Evaluate(string functionName, IReadOnlyList<Value> arguments, RuntimeContext context)
            {
                return Value.FromBoolean(m_Result);
            }
        }

        private readonly struct RecordedCommandExecution
        {
            public RecordedCommandExecution(global::GameDeveloperKit.Story.Model.Command command, RuntimeContext context)
            {
                Command = command;
                Context = context;
            }

            public global::GameDeveloperKit.Story.Model.Command Command { get; }

            public RuntimeContext Context { get; }
        }

        private sealed class RecordingCommandHandler : ICommandHandler
        {
            private readonly string m_CommandName;
            private readonly List<RecordedCommandExecution> m_Executions = new List<RecordedCommandExecution>();

            public RecordingCommandHandler(string commandName)
            {
                m_CommandName = commandName;
            }

            public IReadOnlyList<RecordedCommandExecution> Executions => m_Executions;

            public CommandHandle LastHandle { get; private set; }

            public bool CanHandle(global::GameDeveloperKit.Story.Model.Command command)
            {
                return command != null && string.Equals(command.Name, m_CommandName, StringComparison.Ordinal);
            }

            public ICommandHandle Execute(global::GameDeveloperKit.Story.Model.Command command, RuntimeContext context)
            {
                LastHandle = new CommandHandle(command);
                m_Executions.Add(new RecordedCommandExecution(command, context));
                return LastHandle;
            }
        }

        private sealed class RecordingFramePresenter : IFramePresenter
        {
            public Frame PresentedFrame { get; private set; }

            public Frame ClearedFrame { get; private set; }

            public void Present(Frame frame, Presenter presenter)
            {
                PresentedFrame = frame;
            }

            public void Clear(Frame frame)
            {
                ClearedFrame = frame;
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
