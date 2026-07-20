using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Settlement;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class EpisodeSettlementContractTests
    {
        [Test]
        public void PlanCodec_WhenGenericOperationsRoundTrip_PreservesIdentityKindAndValues()
        {
            var plan = new SettlementPlan(
                "episode_finish",
                SettlementPlan.CurrentVersion,
                new[]
                {
                    new SettlementOperation(
                        "reward",
                        "economy.reward",
                        new ArgumentBag(new Dictionary<string, Value>
                        {
                            ["enabled"] = Value.FromBoolean(true),
                            ["amount"] = Value.FromNumber(2.5d),
                            ["itemId"] = Value.FromString("badge"),
                            ["optional"] = Value.Null
                        })),
                    new SettlementOperation("flag", "progress.flag")
                });

            var json = SettlementPlanCodec.Serialize(plan);

            Assert.IsTrue(SettlementPlanCodec.TryDeserialize(json, out var restored, out var error), error);
            Assert.AreEqual("episode_finish", restored.SettlementId);
            Assert.AreEqual(1, restored.Version);
            Assert.AreEqual(2, restored.Operations.Count);
            Assert.AreEqual("reward", restored.Operations[0].OperationId);
            Assert.AreEqual("economy.reward", restored.Operations[0].Kind);
            Assert.IsTrue(restored.Operations[0].Arguments.GetBoolean("enabled"));
            Assert.AreEqual(2.5d, restored.Operations[0].Arguments.GetNumber("amount"));
            Assert.AreEqual("badge", restored.Operations[0].Arguments.GetString("itemId"));
            Assert.AreEqual(ValueKind.Null, restored.Operations[0].Arguments.GetValue("optional").Kind);
        }

        [Test]
        public void Plan_WhenIdentityOrVersionIsInvalid_RejectsBeforeExecution()
        {
            var operation = new SettlementOperation("operation", "business.kind");

            Assert.Throws<ArgumentException>(() => new SettlementOperation(" ", "business.kind"));
            Assert.Throws<ArgumentException>(() => new SettlementOperation("operation", " "));
            Assert.Throws<ArgumentException>(() => new SettlementPlan(" ", 1, new[] { operation }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SettlementPlan("finish", 0, new[] { operation }));
            Assert.Throws<ArgumentException>(() => new SettlementPlan("finish", 1, new[] { operation, operation }));
            Assert.IsFalse(SettlementPlanCodec.TryDeserialize("{\"settlementId\":\"finish\",\"version\":2,\"operations\":[]}", out _, out _));
        }

        [Test]
        public void PlanCodec_WhenNumberIsNotFinite_RejectsValue()
        {
            var plan = new SettlementPlan(
                "finish",
                1,
                new[]
                {
                    new SettlementOperation(
                        "operation",
                        "business.kind",
                        new ArgumentBag(new Dictionary<string, Value>
                        {
                            ["amount"] = Value.FromNumber(double.NaN)
                        }))
                });

            Assert.Throws<ArgumentException>(() => SettlementPlanCodec.Serialize(plan));
        }

        [Test]
        public void Context_WhenCreated_UsesEpisodeIdentityAndPlanVersionForIdempotency()
        {
            var context = new SettlementContext("story", "volume", "episode", "finish", 3);

            Assert.AreEqual("story", context.StoryId);
            Assert.AreEqual("volume", context.VolumeId);
            Assert.AreEqual("episode", context.EpisodeId);
            Assert.AreEqual("finish", context.SettlementId);
            Assert.AreEqual(3, context.PlanVersion);
            Assert.AreEqual("story:episode:finish:v3", context.IdempotencyKey);
            Assert.IsNull(typeof(SettlementContext).GetProperty("EpisodeId"));
        }
    }
}
