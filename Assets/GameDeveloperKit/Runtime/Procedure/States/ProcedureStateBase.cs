using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 流程状态基类，提供流程状态的基本实现和生命周期管理功能
    /// </summary>
    public abstract class ProcedureStateBase : IProcedureState
    {
        /// <summary>
        /// 初始化流程状态基类的新实例
        /// </summary>
        /// <param name="name">流程状态名称</param>
        /// <exception cref="ArgumentException">流程状态名称为空</exception>
        protected ProcedureStateBase(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Procedure state name can not be empty.", nameof(name));
            }

            Name = name;
        }

        /// <summary>
        /// 获取流程状态名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 异步进入流程状态
        /// </summary>
        /// <param name="userData">用户数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public virtual UniTask OnEnterAsync(object userData = null, CancellationToken cancellationToken = default)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 异步退出流程状态
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public virtual UniTask OnExitAsync(CancellationToken cancellationToken = default)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 更新流程状态
        /// </summary>
        /// <param name="deltaTime">时间增量</param>
        public virtual void OnUpdate(float deltaTime)
        {
        }
    }
}
