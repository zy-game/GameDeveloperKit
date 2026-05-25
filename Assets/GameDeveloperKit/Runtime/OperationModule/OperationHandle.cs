using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Operation
{
    public enum OperationStatus : byte
    {
        None = 0,
        Pending = 1,
        Running = 2,
        Cancelled = 3,
        Succeeded = 4,
        Failed = 5,
    }

    public abstract class OperationHandle : IReference
    {
        private float _progress;
        private Exception _error;
        private OperationStatus _status;
        private Action<float> _progressHandle;
        private UniTaskCompletionSource _cts;

        /// <summary>
        /// 手柄状态
        /// </summary>
        public OperationStatus Status => _status;

        /// <summary>
        /// 错误信息
        /// </summary>
        public Exception Error => _error;

        public OperationHandle()
        {
            _status = OperationStatus.None;
            _cts = new UniTaskCompletionSource();
        }

        /// <summary>
        /// 运行句柄
        /// </summary>
        /// <param name="args"></param>
        public abstract void Execute(params object[] args);

        /// <summary>
        /// 设置进度回调
        /// </summary>
        /// <param name="progressHandle"></param>
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
        /// <param name="progressHandle"></param>
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
        /// <param name="progress"></param>
        public void SetProgress(float progress)
        {
            this._progress = progress;
        }

        /// <summary>
        /// 设置结果
        /// </summary>
        public void SetResult()
        {
            this._status = OperationStatus.Succeeded;
            this._cts?.TrySetResult();
        }

        /// <summary>
        /// 设置错误信息
        /// </summary>
        /// <param name="ex"></param>
        public void SetException(Exception ex)
        {
            this._error = ex;
            this._status = OperationStatus.Failed;
            this._cts?.TrySetException(ex);
        }

        /// <summary>
        /// 设置操作取消
        /// </summary>
        public void SetCanceled()
        {
            this._status = OperationStatus.Cancelled;
            this._cts?.TrySetCanceled();
        }

        /// <summary>
        /// 等待异步完成
        /// </summary>
        /// <returns></returns>
        public UniTask WaitCompletionAsync()
        {
            return _cts.Task;
        }

        /// <summary>
        /// 释放句柄
        /// </summary>
        public virtual void Release()
        {
            this._status = OperationStatus.None;
            this._cts = null;
        }
    }

    /// <summary>
    /// 操作手柄
    /// </summary>
    /// <typeparam name="T">返回数据类型</typeparam>
    public abstract class OperationHandle<T> : OperationHandle
    {
        private T _value;

        /// <summary>
        /// 返回结果
        /// </summary>
        public T Value => _value;


        /// <summary>
        /// 设置结果
        /// </summary>
        /// <param name="_value"></param>
        public void SetResult(T _value)
        {
            this._value = _value;
            base.SetResult();
        }

        public override void Release()
        {
            base.Release();
            this._value = default;
        }
    }
}
