using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 流程状态接口，定义流程状态的基本行为
    /// </summary>
    public interface IProcedureState
    {
        /// <summary>
        /// 获取状态名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 状态进入时的异步处理
        /// </summary>
        /// <param name="userData">用户数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        UniTask OnEnterAsync(object userData = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 状态退出时的异步处理
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        UniTask OnExitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 状态更新时的处理
        /// </summary>
        /// <param name="deltaTime">增量时间</param>
        void OnUpdate(float deltaTime);
    }
}
