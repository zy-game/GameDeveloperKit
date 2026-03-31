using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 框架初始化流程入口，仅负责资源准备。
    /// </summary>
    public sealed class GameFrameworkInitializeProcedure : ProcedureStateBase
    {
        public const string StateName = "GameFrameworkInitializeProcedure";

        public GameFrameworkInitializeProcedure()
            : base(StateName)
        {
        }

        public override async UniTask OnEnterAsync(object userData = null, CancellationToken cancellationToken = default)
        {
            if (!Game.HasModule<ResourceModule>())
            {
                return;
            }

            await Game.Resource.UpdateService.PrepareAllPackagesAsync(cancellationToken);
        }
    }
}
