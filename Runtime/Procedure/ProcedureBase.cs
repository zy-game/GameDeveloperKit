using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Procedure
{
    /// <summary>
    /// 顶层流程基类。
    /// </summary>
    public abstract class ProcedureBase : IReference
    {
        /// <summary>
        /// 初始化流程实例。
        /// </summary>
        /// <returns>初始化任务。</returns>
        public virtual UniTask OnInitializeAsync()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 进入当前流程。
        /// </summary>
        /// <param name="previous">上一个流程。</param>
        /// <param name="userData">切换参数。</param>
        /// <returns>进入任务。</returns>
        public virtual UniTask OnEnterAsync(ProcedureBase previous, object userData)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 离开当前流程。
        /// </summary>
        /// <param name="next">下一个流程。</param>
        /// <param name="userData">切换参数。</param>
        /// <returns>离开任务。</returns>
        public virtual UniTask OnLeaveAsync(ProcedureBase next, object userData)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 更新当前流程。
        /// </summary>
        /// <param name="deltaTime">当前帧间隔。</param>
        /// <param name="unscaledDeltaTime">未缩放帧间隔。</param>
        public virtual void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
        }

        /// <summary>
        /// 释放流程实例。
        /// </summary>
        public virtual void Release()
        {
        }
    }
}
