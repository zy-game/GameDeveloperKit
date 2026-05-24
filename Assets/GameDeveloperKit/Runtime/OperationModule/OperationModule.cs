using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Operation
{
    public class OperationModule : GameModuleBase
    {
        public override UniTask Startup()
        {
            return UniTask.CompletedTask;
        }

        public override UniTask Shutdown()
        {
            return UniTask.CompletedTask;
        }

        public T Execute<T>(object key, params object[] args) where T : OperationHandle
        {
            var operation = Activator.CreateInstance<T>();
            Execute(key, operation, args);
            return operation;
        }

        public void Execute(object key, OperationHandle operation)
        {
            Execute(key, operation, Array.Empty<object>());
        }

        public void Execute(object key, OperationHandle operation, params object[] args)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            try
            {
                operation.Execute(args);
            }
            catch (Exception exception)
            {
                operation.SetException(exception);
            }
        }

        public async UniTask<T> WaitCompletionAsync<T>(object key, params object[] args) where T : OperationHandle
        {
            var operation = Execute<T>(key, args);
            try
            {
                await operation.WaitCompletionAsync();
            }
            catch
            {
            }

            return operation;
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
