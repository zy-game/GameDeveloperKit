using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Logic;

namespace GameDeveloperKit.StoryEditor.Model
{
    [LogicNode("sample.story.completed", "剧情结束通知", "Sample")]
    [System.ComponentModel.Description("通知样例业务剧情已经结束。")]
    [OutputPort("completed", "完成")]
    internal sealed class SampleStoryCompletedLogic : ILogicNode
    {
        public SampleStoryCompletedLogic()
        {
        }

        public UniTask<LogicResult> ExecuteAsync(LogicContext context, CancellationToken cancellationToken)
        {
            return UniTask.FromResult(LogicResult.To("completed"));
        }
    }

    [LogicNode("sample.minigame.lockpick", "撬锁小游戏", "Sample")]
    [OutputPort("success", "成功")]
    [OutputPort("fail", "失败")]
    [OutputPort("cancel", "取消")]
    internal sealed class SampleLockpickLogic : ILogicNode
    {
        public SampleLockpickLogic()
        {
        }

        public UniTask<LogicResult> ExecuteAsync(LogicContext context, CancellationToken cancellationToken)
        {
            return UniTask.FromResult(LogicResult.To("success"));
        }
    }

    [LogicNode("sample.qte", "QTE", "Sample")]
    [LogicParameter("inputActionId", "输入动作 ID", Required = true)]
    [LogicParameter("durationSeconds", "时长", ParameterValueType.Number, Required = true)]
    [LogicParameter("requiredCount", "需要次数", ParameterValueType.Number)]
    [LogicParameter("promptTextKey", "提示文本", Required = true, FieldRendererKey = "story.text-reference")]
    [OutputPort("success", "成功")]
    [OutputPort("fail", "失败")]
    internal sealed class SampleQteLogic : ILogicNode
    {
        public SampleQteLogic()
        {
        }

        public UniTask<LogicResult> ExecuteAsync(LogicContext context, CancellationToken cancellationToken)
        {
            return UniTask.FromResult(LogicResult.To("success"));
        }
    }

    [LogicNode("sample.unlock", "解锁", "Sample")]
    [LogicParameter("unlockId", "解锁 ID", Required = true)]
    [LogicParameter(
        "puzzleType",
        "玩法类型",
        ParameterValueType.Option,
        Required = true,
        Options = new[] { "line_connect", "node_unlock", "custom" })]
    [LogicParameter("promptTextKey", "提示文本", Required = true, FieldRendererKey = "story.text-reference")]
    [OutputPort("success", "成功")]
    [OutputPort("fail", "失败")]
    internal sealed class SampleUnlockLogic : ILogicNode
    {
        public SampleUnlockLogic()
        {
        }

        public UniTask<LogicResult> ExecuteAsync(LogicContext context, CancellationToken cancellationToken)
        {
            return UniTask.FromResult(LogicResult.To("success"));
        }
    }

    [LogicNode("sample.final-settlement", "最终结算", "Sample")]
    [LogicParameter("settlementId", "结算 ID", Required = true)]
    [OutputPort("completed", "完成")]
    [OutputPort("failed", "失败")]
    internal sealed class SampleFinalSettlementLogic : ILogicNode
    {
        public SampleFinalSettlementLogic()
        {
        }

        public UniTask<LogicResult> ExecuteAsync(LogicContext context, CancellationToken cancellationToken)
        {
            return UniTask.FromResult(LogicResult.To("completed"));
        }
    }
}
