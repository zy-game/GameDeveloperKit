using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Operation
{
    /// <summary>
    /// 操作句柄基类，用于承载异步操作状态、进度、错误信息和完成等待。
    /// </summary>
    public abstract class OperationHandle
    {
        private float _progress;
        private Exception _error;
        private OperationStatus _status;
        private Action<float> _progressHandle;
        private readonly UniTaskCompletionSource _cts;

        /// <summary>
        /// 手柄状态
        /// </summary>
        public OperationStatus Status => _status;

        /// <summary>
        /// 错误信息
        /// </summary>
        public Exception Error => _error;

        /// <summary>
        /// 操作是否已经进入终止状态。
        /// </summary>
        internal bool IsDone => _status is OperationStatus.Cancelled or OperationStatus.Succeeded or OperationStatus.Failed;

        /// <summary>
        /// 初始化操作句柄。
        /// </summary>
        public OperationHandle()
        {
            _status = OperationStatus.None;
            _cts = new UniTaskCompletionSource();
        }

        /// <summary>
        /// 运行句柄
        /// </summary>
        public abstract void Execute(params object[] args);

        /// <summary>
        /// 将操作设置为运行状态。
        /// </summary>
        internal void SetRunning()
        {
            if (_status != OperationStatus.None)
            {
                throw new GameException($"Operation '{GetType().Name}' can only be executed once.");
            }

            _status = OperationStatus.Running;
        }

        /// <summary>
        /// 通过对象形式设置操作结果。
        /// </summary>
        /// <param name="value">操作结果。</param>
        /// <exception cref="GameException">非泛型操作句柄收到结果值时抛出。</exception>
        internal virtual void SetResultObject(object value)
        {
            if (value != null)
            {
                throw new GameException($"{GetType().Name} does not accept a result value.");
            }

            SetResult();
        }

        /// <summary>
        /// 设置进度回调
        /// </summary>
        /// <param name="progressHandle">progress Handle 参数。</param>
        public void SetProgressHandle(Action<float> progressHandle)
        {
            if (progressHandle == null)
            {
                return;
            }

            _progressHandle = progressHandle;
            progressHandle.Invoke(this._progress);
        }

        /// <summary>
        /// 设置进度回调
        /// </summary>
        /// <param name="progressHandle">progress Handle 参数。</param>
        public void SetProgressHandle(IProgress<float> progressHandle)
        {
            if (progressHandle == null)
            {
                return;
            }

            _progressHandle += progress => progressHandle.Report(progress);
            progressHandle.Report(this._progress);
        }

        /// <summary>
        /// 设置进度
        /// </summary>
        public void SetProgress(float progress)
        {
            this._progress = progress;
            this._progressHandle?.Invoke(this._progress);
        }

        /// <summary>
        /// 将运行中的领域操作标记为暂停。
        /// </summary>
        /// <returns>状态是否发生变化。</returns>
        protected bool PauseExecution()
        {
            if (_status != OperationStatus.Running)
            {
                return false;
            }

            _status = OperationStatus.Paused;
            return true;
        }

        /// <summary>
        /// 将暂停中的领域操作恢复为运行状态。
        /// </summary>
        /// <returns>状态是否发生变化。</returns>
        protected bool ResumeExecution()
        {
            if (_status != OperationStatus.Paused)
            {
                return false;
            }

            _status = OperationStatus.Running;
            return true;
        }

        /// <summary>
        /// 设置结果
        /// </summary>
        public void SetResult()
        {
            if (IsDone)
            {
                return;
            }

            this._status = OperationStatus.Succeeded;
            this._cts.TrySetResult();
        }

        /// <summary>
        /// 设置错误信息
        /// </summary>
        public void SetException(Exception ex)
        {
            if (IsDone)
            {
                return;
            }

            if (ex == null)
            {
                throw new ArgumentNullException(nameof(ex));
            }

            this._error = ex;
            this._status = OperationStatus.Failed;
            this._cts.TrySetException(ex);
        }

        /// <summary>
        /// 设置操作取消
        /// </summary>
        public virtual void SetCancel()
        {
            if (IsDone)
            {
                return;
            }

            this._status = OperationStatus.Cancelled;
            this._cts.TrySetCanceled();
        }

        /// <summary>
        /// 等待异步完成
        /// </summary>
        public UniTask WaitCompletionAsync()
        {
            return _cts.Task;
        }

        /// <summary>
        /// 观察完成源，避免调用方只检查状态时产生未观察异常。
        /// </summary>
        internal void ObserveCompletion()
        {
            _cts.Task.Forget(_ => { });
        }
    }

}
