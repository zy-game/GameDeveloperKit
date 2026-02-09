using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Procedure
{
    /// <summary>
    /// 框架初始化流程
    /// </summary>
    [Procedure]
    public class InitializeFrameworkProcedure : StateBase
    {
        public override async UniTask<ProcedureResult> OnExecuteAsync(IStateManager procedureManager, CancellationToken ct, params object[] args)
        {
            Game.Debug.Info("InitializeProcedure: 开始框架初始化...");
            Startup startup = args.OfType<Startup>().FirstOrDefault();
            Game.Resource.SetMode(startup.ResourceMode);
            (await Game.UI.OpenFormAsync<CommonLoadingForm>()).SetProgress(0f);
            Game.Debug.Info($"InitializeProcedure: 资源模式设置为 {Game.Resource.GetMode()}");
            await Game.Resource.SetResourceServerUrl(startup.ResourceUpdateUrl);
            await UniTask.Yield(ct);
            Game.Debug.Info("InitializeProcedure: 框架初始化完成");
            return ProcedureResult.Next<CheckAppVersionProcedure>(args);
        }
    }
}
