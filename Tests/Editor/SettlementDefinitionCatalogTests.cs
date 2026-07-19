using System.Collections.Generic;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Settlement;
using GameDeveloperKit.StoryEditor.Settlement;
using NUnit.Framework;

namespace GameDeveloperKit.Tests.Editor
{
    public sealed class SettlementDefinitionCatalogTests
    {
        [Test]
        public void Catalog_WhenPlanMatchesBusinessDefinition_AcceptsTypedArguments()
        {
            var catalog = SettlementDefinitionCatalog.Create(new[] { new Provider() });
            var plan = Plan(new Dictionary<string, Value>
            {
                ["itemId"] = Value.FromString("badge"),
                ["amount"] = Value.FromNumber(2)
            });

            Assert.IsTrue(catalog.TryValidate(plan, out var error), error);
        }

        [Test]
        public void Catalog_WhenKindOrArgumentsAreInvalid_RejectsPlan()
        {
            var catalog = SettlementDefinitionCatalog.Create(new[] { new Provider() });
            var unknown = new SettlementPlan(
                "finish",
                1,
                new[] { new SettlementOperation("operation", "unknown") });
            var missing = Plan(new Dictionary<string, Value>
            {
                ["itemId"] = Value.FromString("badge")
            });
            var wrongType = Plan(new Dictionary<string, Value>
            {
                ["itemId"] = Value.FromString("badge"),
                ["amount"] = Value.FromString("two")
            });

            Assert.IsFalse(catalog.TryValidate(unknown, out var unknownError));
            StringAssert.Contains("not registered", unknownError);
            Assert.IsFalse(catalog.TryValidate(missing, out var missingError));
            StringAssert.Contains("required", missingError);
            Assert.IsFalse(catalog.TryValidate(wrongType, out var typeError));
            StringAssert.Contains("invalid type", typeError);
        }

        [Test]
        public void Catalog_WhenProvidersDuplicateKind_ReportsConfigurationError()
        {
            var catalog = SettlementDefinitionCatalog.Create(new ISettlementDefinitionProvider[]
            {
                new Provider(),
                new Provider()
            });

            Assert.AreEqual(1, catalog.Definitions.Count);
            Assert.AreEqual(1, catalog.Errors.Count);
            StringAssert.Contains("duplicated", catalog.Errors[0]);
        }

        private static SettlementPlan Plan(IReadOnlyDictionary<string, Value> arguments)
        {
            return new SettlementPlan(
                "finish",
                1,
                new[]
                {
                    new SettlementOperation(
                        "reward",
                        "test.reward",
                        new ArgumentBag(arguments))
                });
        }

        private sealed class Provider : ISettlementDefinitionProvider
        {
            public IReadOnlyList<SettlementDefinition> GetDefinitions()
            {
                return new[]
                {
                    new SettlementDefinition(
                        "test.reward",
                        "Reward",
                        "Test",
                        new[]
                        {
                            new SettlementArgumentDefinition("itemId", "Item", required: true),
                            new SettlementArgumentDefinition("amount", "Amount", ParameterValueType.Number, true)
                        })
                };
            }
        }
    }
}
