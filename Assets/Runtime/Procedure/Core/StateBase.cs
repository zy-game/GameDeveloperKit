using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Procedure
{
    /// <summary>
    /// 流程基类
    /// </summary>
    public abstract class StateBase
    {
        /// <summary>
        /// 异步执行流程
        /// </summary>
        /// <param name="procedureManager">流程管理器</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="args">参数</param>
        /// <returns>流程执行结果</returns>
        public virtual UniTask<ProcedureResult> OnExecuteAsync(IStateManager procedureManager, CancellationToken cancellationToken, params object[] args)
        {
            return UniTask.FromResult(ProcedureResult.End);
        }

        /// <summary>
        /// 销毁流程
        /// </summary>
        public virtual void OnDestroy(IStateManager procedureManager)
        {
        }
    }
}