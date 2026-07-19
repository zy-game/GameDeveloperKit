using System.Collections.Generic;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Settlement;

namespace GameDeveloperKit.StoryEditor.Model
{
    internal sealed class SampleSettlementDefinitionProvider : ISettlementDefinitionProvider
    {
        public IReadOnlyList<SettlementDefinition> GetDefinitions()
        {
            return new[]
            {
                new SettlementDefinition(
                    "sample.reward",
                    "样例奖励",
                    "Sample",
                    new[]
                    {
                        new SettlementArgumentDefinition("itemId", "道具 ID", required: true),
                        new SettlementArgumentDefinition("amount", "数量", ParameterValueType.Number, true)
                    }),
                new SettlementDefinition(
                    "sample.flag",
                    "样例标记",
                    "Sample",
                    new[]
                    {
                        new SettlementArgumentDefinition("value", "值", ParameterValueType.Boolean, true)
                    }),
                new SettlementDefinition("sample.operation", "样例操作", "Sample")
            };
        }
    }
}
