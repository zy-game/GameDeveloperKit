using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Procedure
{
    /// <summary>
    /// 流程管理器接口
    /// </summary>
    public interface IStateManager
    {
        /// <summary>
        /// 获取当前流程
        /// </summary>
        StateBase CurrentProcedure { get; }

        /// <summary>
        /// 启动流程链（指定入口流程）
        /// </summary>
        /// <typeparam name="T">入口流程类型</typeparam>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="args">参数</param>
        UniTask StartAsync<T>(CancellationToken cancellationToken = default, params object[] args) where T : StateBase;

        /// <summary>
        /// 等待到达特定流程
        /// </summary>
        /// <typeparam name="T">目标流程类型</typeparam>
        /// <param name="cancellationToken">取消令牌</param>
        UniTask WaitForAsync<T>(CancellationToken cancellationToken = default) where T : StateBase;

        /// <summary>
        /// 是否存在流程
        /// </summary>
        /// <typeparam name="T">要检查的流程类型</typeparam>
        /// <returns>是否存在</returns>
        bool Contains<T>() where T : StateBase;

        /// <summary>
        /// 关闭流程
        /// </summary>
        void Shutdown();
    }
}