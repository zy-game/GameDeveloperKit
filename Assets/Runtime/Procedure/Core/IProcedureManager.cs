using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Procedure
{
    public interface IProcedureManager : IModule
    {
        /// <summary>
        /// 获取当前流程
        /// </summary>
        StateBase CurrentProcedure { get; }

        /// <summary>
        /// 启动流程链（指定入口流程）
        /// </summary>
        UniTask StartAsync<T>(CancellationToken cancellationToken = default, params object[] args) where T : StateBase;

        /// <summary>
        /// 等待到达特定流程
        /// </summary>
        UniTask WaitForAsync<T>() where T : StateBase;
    }
}