using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Operation
{
    public class OperationModule : GameModuleBase
    {
        public override async UniTask Startup()
        {
            throw new System.NotImplementedException();
        }

        public override async UniTask Shutdown()
        {
            throw new System.NotImplementedException();
        }

        public T Execute<T>(object key, params object[] args) where T : OperationHandle
        {
            throw new System.NotImplementedException();
        }

        public void Execute(object key, OperationHandle operation)
        {
        }

        public UniTask<T> WaitCompletionAsync<T>(object key, params object[] args) where T : OperationHandle
        {
            return default;
        }

        /// <summary>
        /// 设置结果
        /// </summary>
        /// <param name="_value"></param>
        public void SetResult(object key, object _value)
        {
        }

        /// <summary>
        /// 设置错误信息
        /// </summary>
        /// <param name="ex"></param>
        public void SetException(object key, Exception ex)
        {
        }

        /// <summary>
        /// 设置操作取消
        /// </summary>
        public void SetCanceled(object key)
        {
        }
    }
}