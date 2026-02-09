using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.UI;

namespace GameDeveloperKit.Procedure
{
    /// <summary>
    /// 预加载流程
    /// </summary>
    [Procedure]
    public class PreLoadingCustomPackageProcedure : StateBase
    {
        public override async UniTask<ProcedureResult> OnExecuteAsync(IStateManager procedureManager, CancellationToken ct, params object[] args)
        {
            var loading = Game.UI.GetForm<ILoading>();
            Game.Debug.Info("PreloadProcedure: 开始预加载...");

            var startup = args?.OfType<Startup>().FirstOrDefault();
            loading?.SetProgress(0.3f);

            var packages = startup?.PreloadBasePackages;
            if (packages != null && packages.Length > 0)
            {
                var step = 0.3f / packages.Length;
                for (int i = 0; i < packages.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var packageName = packages[i];
                    if (string.IsNullOrEmpty(packageName)) continue;

                    Game.Debug.Info($"PreloadProcedure: 加载首包资源包 '{packageName}'...");
                    var package = await Game.Resource.LoadPackageAsync(packageName);
                    if (package == null)
                        Game.Debug.Warning($"PreloadProcedure: 加载资源包 '{packageName}' 失败");
                    loading?.SetProgress(0.3f + step * (i + 1));
                }
            }
            else
            {
                Game.Debug.Info("PreloadProcedure: 未配置首包，跳过资源包加载");
                loading?.SetProgress(0.6f);
            }

            loading?.SetProgress(1f);
            await UniTask.Yield(ct);
            Game.Debug.Info("PreloadProcedure: 预加载完成");

            // 检查是否配置了自定义 Procedure
            var customTypeName = startup?.CustomProcedureTypeName;
            if (!string.IsNullOrEmpty(customTypeName))
            {
                // 尝试从所有已加载的程序集中查找类型
                Type customType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    customType = assembly.GetType(customTypeName);
                    if (customType != null) break;
                }

                if (customType != null && typeof(StateBase).IsAssignableFrom(customType))
                {
                    Game.Debug.Info($"PreloadProcedure: 执行自定义流程 '{customTypeName}'");
                    var customProcedure = Activator.CreateInstance(customType) as StateBase;
                    if (customProcedure != null)
                    {
                        await customProcedure.OnExecuteAsync(procedureManager, ct, args);
                        Game.Debug.Info($"PreloadProcedure: 自定义流程 '{customTypeName}' 执行完毕");
                    }
                }
                else
                {
                    Game.Debug.Warning($"PreloadProcedure: 自定义流程类型 '{customTypeName}' 无效");
                }
            }
            Game.UI.CloseForm<ILoading>();
            return ProcedureResult.End;
        }
    }
}
