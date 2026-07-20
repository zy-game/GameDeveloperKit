using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Logic;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Playback;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests.Runtime
{
    public sealed class LogicCommandHandlerTests
    {
        [Test]
        public void Handler_WhenExecutorReturnsDeclaredOutput_CompletesHandle()
        {
            var logicId = RegisterNode((_, __) =>
                UniTask.FromResult(LogicResult.To("has")));
            var command = CreateCommand(logicId);
            var context = CreateContext(command);

            var handle = new LogicCommandHandler().Execute(command, context);

            Assert.IsTrue(handle.IsCompleted);
            Assert.AreEqual("has", handle.OutcomeId);
            Assert.IsNull(handle.Error);
        }

        [Test]
        public void Handler_WhenExecutorReturnsInvalidOutput_KeepsCommandFailedAndLocated()
        {
            var logicId = RegisterNode((_, __) =>
                UniTask.FromResult(LogicResult.To("unknown")));
            var command = CreateCommand(logicId);

            var handle = new LogicCommandHandler().Execute(command, CreateContext(command));

            Assert.IsFalse(handle.IsCompleted);
            Assert.IsInstanceOf<GameException>(handle.Error);
            StringAssert.Contains("story:logic_story", handle.Error.Message);
            StringAssert.Contains("volume:volume_test", handle.Error.Message);
            StringAssert.Contains("episode:episode_01", handle.Error.Message);
            StringAssert.Contains("step:logic", handle.Error.Message);
            StringAssert.Contains("command:logic", handle.Error.Message);
            StringAssert.Contains($"logic:{logicId}", handle.Error.Message);
            StringAssert.Contains("output:unknown", handle.Error.Message);
        }

        [Test]
        public void Handler_WhenExecutorThrows_WrapsFailureAndPreservesInnerException()
        {
            var businessError = new InvalidOperationException("business failure");
            var logicId = RegisterNode((_, __) =>
                UniTask.FromException<LogicResult>(businessError));
            var command = CreateCommand(logicId);

            var handle = new LogicCommandHandler().Execute(command, CreateContext(command));

            Assert.IsInstanceOf<GameException>(handle.Error);
            Assert.AreSame(businessError, handle.Error.InnerException);
            StringAssert.Contains($"logic:{logicId}", handle.Error.Message);
        }

        [Test]
        public void Handler_WhenStopped_CancelsExecutorAndIgnoresLateResult()
        {
            var completion = new UniTaskCompletionSource<LogicResult>();
            var wasCanceled = false;
            var logicId = RegisterNode((_, token) =>
            {
                token.Register(() => wasCanceled = true);
                return completion.Task;
            });
            var command = CreateCommand(logicId);
            var handle = new LogicCommandHandler().Execute(command, CreateContext(command));

            handle.Stop();
            completion.TrySetResult(LogicResult.To("has"));

            Assert.IsTrue(wasCanceled);
            Assert.IsTrue(handle.IsStopped);
            Assert.IsFalse(handle.IsCompleted);
            Assert.IsNull(handle.OutcomeId);
        }

        [Test]
        public void Handler_WhenPendingSnapshotReplayed_KeepsInvocationIdAndLoopChangesIt()
        {
            var invocations = new List<string>();
            var factoryCount = 0;
            var logicId = $"tests.handler.invocation.{Guid.NewGuid():N}";
            LogicNodeRegistry.Register(
                logicId,
                () =>
                {
                    factoryCount++;
                    return new LogicNodeContractTests.TestLogicNode
                    {
                        Execution = (context, _) =>
                        {
                            invocations.Add(context.InvocationId);
                            return UniTask.FromResult(LogicResult.To("has"));
                        }
                    };
                });
            var command = CreateCommand(logicId);
            var handler = new LogicCommandHandler();

            handler.Execute(command, CreateContext(command, Array.Empty<HistoryEntry>()));
            handler.Execute(command, CreateContext(command, new[]
            {
                new HistoryEntry("episode_01", "other", "completed", null, "other", "completed", 0f)
            }));
            handler.Execute(command, CreateContext(command, new[]
            {
                new HistoryEntry("episode_01", "logic", "has", null, "logic", "has", 0f)
            }));

            Assert.AreEqual(3, factoryCount);
            Assert.AreEqual(invocations[0], invocations[1]);
            Assert.AreNotEqual(invocations[0], invocations[2]);
            StringAssert.Contains("history:0", invocations[0]);
            StringAssert.Contains("history:1", invocations[2]);
        }

        [Test]
        public void Handler_WhenRegistryMissing_ThrowsLocatedFailure()
        {
            var logicId = $"tests.handler.missing.{Guid.NewGuid():N}";
            var command = CreateCommand(logicId);

            var exception = Assert.Throws<GameException>(() =>
                new LogicCommandHandler().Execute(command, CreateContext(command)));

            StringAssert.Contains("story:logic_story", exception.Message);
            StringAssert.Contains("episode:episode_01", exception.Message);
            StringAssert.Contains("step:logic", exception.Message);
            StringAssert.Contains("command:logic", exception.Message);
            StringAssert.Contains($"logic:{logicId}", exception.Message);
        }

        [Test]
        public void Presenter_WhenLogicCompletes_AdvancesOnlySelectedOutput()
        {
            var logicId = RegisterNode((_, __) =>
                UniTask.FromResult(LogicResult.To("has")));
            var program = CreateProgram(logicId);
            var module = new StoryModule();
            module.Startup();
            var presenter = new Presenter(module);
            presenter.AddCommandHandler(new LogicCommandHandler());

            presenter.Start(program, StoryProgramTestFactory.VolumeId, "episode_01");

            Assert.AreEqual("owned", presenter.CurrentFrame.AnchorStep.StepId);
            Assert.AreEqual(0, presenter.ActiveCommandHandles.Count);
            Assert.IsNull(presenter.LastError);
        }

        [Test]
        public void Presenter_WhenLogicHandlerMissing_ThrowsLocatedFailure()
        {
            var logicId = RegisterNode((_, __) =>
                UniTask.FromResult(LogicResult.To("has")));
            var program = CreateProgram(logicId);
            var module = new StoryModule();
            module.Startup();
            var presenter = new Presenter(module);

            var exception = Assert.Throws<GameException>(() =>
                presenter.Start(program, StoryProgramTestFactory.VolumeId, "episode_01"));

            StringAssert.Contains("logic command handler is not registered", exception.Message);
            StringAssert.Contains($"logic:{logicId}", exception.Message);
        }

        [Test]
        public void PlayerView_WhenConfigured_AutomaticallyRegistersLogicHandler()
        {
            var gameObject = new GameObject("LogicPlayerView");
            var module = new StoryModule();
            module.Startup();
            try
            {
                var view = gameObject.AddComponent<PlayerView>();
                view.ConfigureModules(module);
                var handlersField = typeof(Presenter).GetField(
                    "m_CommandHandlers",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(handlersField);
                var handlers = (IReadOnlyList<ICommandHandler>)handlersField.GetValue(view.Presenter);

                Assert.IsTrue(handlers.Any(handler => handler is LogicCommandHandler));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static string RegisterNode(
            Func<LogicContext, CancellationToken, UniTask<LogicResult>> execute)
        {
            var logicId = $"tests.handler.{Guid.NewGuid():N}";
            LogicNodeRegistry.Register(
                logicId,
                () => new LogicNodeContractTests.TestLogicNode { Execution = execute });
            return logicId;
        }

        private static global::GameDeveloperKit.Story.Model.Command CreateCommand(string logicId)
        {
            return LogicCommandCodec.Create(
                "logic",
                logicId,
                new ArgumentBag(new Dictionary<string, Value>
                {
                    ["itemId"] = Value.FromString("item.sword")
                }),
                new[] { "has", "missing" },
                new Dictionary<string, Target>
                {
                    ["has"] = Target.Step("owned"),
                    ["missing"] = Target.Step("not_owned")
                });
        }

        private static RuntimeContext CreateContext(
            global::GameDeveloperKit.Story.Model.Command command,
            IReadOnlyList<HistoryEntry> history = null)
        {
            var step = new Step("logic", StepKind.Command, new StepData(command: command));
            var episode = StoryProgramTestFactory.Episode(
                "episode_01",
                "Episode",
                "logic",
                new[] { step });
            var program = StoryProgramTestFactory.Program(
                "logic_story",
                "1",
                "episode_01",
                new[] { episode });
            return new RuntimeContext(
                program,
                program.Volumes[0],
                episode,
                step,
                0d,
                null,
                history);
        }

        private static Program CreateProgram(string logicId)
        {
            var command = CreateCommand(logicId);
            var logic = new Step("logic", StepKind.Command, new StepData(command: command));
            var owned = new Step("owned", StepKind.End, new StepData(exitId: "owned"));
            var notOwned = new Step("not_owned", StepKind.End, new StepData(exitId: "not_owned"));
            var episode = StoryProgramTestFactory.Episode(
                "episode_01",
                "Episode",
                "logic",
                new[] { logic, owned, notOwned });
            var schema = new CommandSchema(new[]
            {
                new CommandDefinition(
                    logicId,
                    "Logic",
                    true,
                    new[]
                    {
                        new CommandArgumentDefinition(
                            LogicCommandCodec.MarkerArgument,
                            "Logic marker",
                            ParameterValueType.Boolean,
                            true),
                        new CommandArgumentDefinition("itemId", "Item ID")
                    },
                    new[] { "has", "missing" })
            });
            return StoryProgramTestFactory.Program(
                "logic_story",
                "1",
                "episode_01",
                new[] { episode },
                commandSchema: schema);
        }
    }
}
