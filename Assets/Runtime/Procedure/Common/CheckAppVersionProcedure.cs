using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Procedure
{
    /// <summary>
    /// 版本检查流程
    /// </summary>
    [Procedure]
    public class CheckAppVersionProcedure : StateBase
    {
        public override async UniTask<ProcedureResult> OnExecuteAsync(IStateManager procedureManager, CancellationToken ct, params object[] args)
        {
            Game.Debug.Info("VersionCheckProcedure: 开始版本检查...");
            await UniTask.Yield(ct);
            Game.Debug.Info("VersionCheckProcedure: 版本检查完成");
            return ProcedureResult.Next<PreLoadingCustomPackageProcedure>(args);
        }
    }
}
