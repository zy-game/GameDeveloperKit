using System.Collections.Generic;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Event;

namespace GameDeveloperKit.StoryEditor.Model
{
    internal sealed class SampleEventDefinitionProvider : IEventDefinitionProvider
    {
        public IReadOnlyList<EventDefinition> GetDefinitions()
        {
            return new[]
            {
                new EventDefinition(
                    "sample.story.completed",
                    "剧情结束通知",
                    "Sample",
                    EventMode.Notify),
                new EventDefinition(
                    "sample.minigame.lockpick",
                    "撬锁小游戏",
                    "Sample",
                    EventMode.Request,
                    outcomes: new[] { "success", "fail", "cancel" }),
                new EventDefinition(
                    "sample.qte",
                    "QTE",
                    "Sample",
                    EventMode.Request,
                    new[]
                    {
                        new EventArgumentDefinition("inputActionId", "输入动作 ID", required: true),
                        new EventArgumentDefinition("durationSeconds", "时长", ParameterValueType.Number, true),
                        new EventArgumentDefinition("requiredCount", "需要次数", ParameterValueType.Number),
                        new EventArgumentDefinition("promptTextKey", "提示文本", required: true, fieldRendererKey: "story.text-reference")
                    },
                    new[] { "success", "fail" }),
                new EventDefinition(
                    "sample.unlock",
                    "解锁",
                    "Sample",
                    EventMode.Request,
                    new[]
                    {
                        new EventArgumentDefinition("unlockId", "解锁 ID", required: true),
                        new EventArgumentDefinition(
                            "puzzleType",
                            "玩法类型",
                            ParameterValueType.Option,
                            true,
                            new[] { "line_connect", "node_unlock", "custom" }),
                        new EventArgumentDefinition("promptTextKey", "提示文本", required: true, fieldRendererKey: "story.text-reference")
                    },
                    new[] { "success", "fail" })
            };
        }
    }
}
