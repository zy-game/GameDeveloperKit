using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Story.Logic;
using GameDeveloperKit.Story.Model;
using NUnit.Framework;

namespace GameDeveloperKit.Tests.Runtime
{
    public sealed class LogicNodeContractTests
    {
        [Test]
        public void Registry_WhenGeneratedTypeRegistered_CreatesLogicNode()
        {
            var logicId = $"test.logic.{Guid.NewGuid():N}";

            LogicNodeRegistry.RegisterGenerated<TestLogicNode>(logicId);
            LogicNodeRegistry.RegisterGenerated<TestLogicNode>(logicId);

            Assert.IsInstanceOf<TestLogicNode>(LogicNodeRegistry.Create(logicId));
        }

        [Test]
        public void Registry_WhenDifferentTypeUsesSameId_RejectsDuplicate()
        {
            var logicId = $"test.logic.{Guid.NewGuid():N}";
            LogicNodeRegistry.RegisterGenerated<TestLogicNode>(logicId);

            var exception = Assert.Throws<GameException>(() =>
                LogicNodeRegistry.RegisterGenerated<OtherLogicNode>(logicId));

            StringAssert.Contains(logicId, exception.Message);
            StringAssert.Contains(typeof(TestLogicNode).FullName, exception.Message);
            StringAssert.Contains(typeof(OtherLogicNode).FullName, exception.Message);
        }

        [Test]
        public void CommandCodec_WhenRoundTripped_PreservesLogicData()
        {
            var arguments = new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
            {
                ["itemId"] = Value.FromString("item.sword")
            });
            var outputs = new[] { "has", "missing" };
            var targets = new Dictionary<string, Target>(StringComparer.Ordinal)
            {
                ["has"] = Target.Step("owned"),
                ["missing"] = Target.Step("not_owned")
            };

            var command = LogicCommandCodec.Create(
                "check_item",
                "inventory.has-item",
                arguments,
                outputs,
                targets);

            Assert.IsTrue(LogicCommandCodec.TryDecode(command, out var logicId, out var decoded, out var error), error);
            Assert.AreEqual("inventory.has-item", logicId);
            Assert.AreEqual("item.sword", decoded.GetString("itemId"));
            Assert.IsFalse(decoded.Values.ContainsKey(LogicCommandCodec.MarkerArgument));
            Assert.AreEqual("owned", command.GetOutcomeTarget("has").StepId);
            Assert.AreEqual("not_owned", command.GetOutcomeTarget("missing").StepId);
        }

        [Test]
        public void CommandCodec_WhenOutputTargetMissing_RejectsCommand()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                LogicCommandCodec.Create(
                    "check_item",
                    "inventory.has-item",
                    new ArgumentBag(),
                    new[] { "has", "missing" },
                    new Dictionary<string, Target>(StringComparer.Ordinal)
                    {
                        ["has"] = Target.Step("owned")
                    }));

            StringAssert.Contains("missing", exception.Message);
        }

        [LogicNode("tests.registry.first", "Registry First")]
        [OutputPort("completed", "Completed")]
        public sealed class TestLogicNode : ILogicNode
        {
            public Func<LogicContext, CancellationToken, UniTask<LogicResult>> Execution { get; set; }

            public UniTask<LogicResult> ExecuteAsync(
                LogicContext context,
                CancellationToken cancellationToken)
            {
                return Execution == null
                    ? UniTask.FromResult(LogicResult.To("completed"))
                    : Execution(context, cancellationToken);
            }
        }

        [LogicNode("tests.registry.second", "Registry Second")]
        [OutputPort("completed", "Completed")]
        public sealed class OtherLogicNode : ILogicNode
        {
            public UniTask<LogicResult> ExecuteAsync(
                LogicContext context,
                CancellationToken cancellationToken)
            {
                return UniTask.FromResult(LogicResult.To("completed"));
            }
        }
    }
}
