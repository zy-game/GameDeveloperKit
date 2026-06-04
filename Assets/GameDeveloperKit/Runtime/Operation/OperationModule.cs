using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Operation
{
    /// <summary>
    /// 操作模块，负责注册、执行和清理运行中的操作句柄。
    /// </summary>
    public class OperationModule : GameModuleBase
    {
        private readonly Dictionary<OperationKey, OperationHandle> m_Operations = new Dictionary<OperationKey, OperationHandle>();

        /// <summary>
        /// 启动操作模块。
        /// </summary>
        /// <returns>模块启动任务。</returns>
        public override UniTask Startup()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 关闭操作模块，并取消所有尚未完成的操作。
        /// </summary>
        /// <returns>模块关闭任务。</returns>
        public override UniTask Shutdown()
        {
            foreach (var operation in new List<OperationHandle>(m_Operations.Values))
            {
                if (!operation.IsDone)
                {
                    operation.SetCancel();
                }
            }

            m_Operations.Clear();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 以操作句柄类型作为操作键执行操作。
        /// </summary>
        /// <typeparam name="T">操作句柄类型。</typeparam>
        /// <param name="args">操作参数。</param>
        /// <returns>已创建并开始执行的操作句柄。</returns>
        public T Execute<T>(params object[] args) where T : OperationHandle
        {
            return ExecuteWithKey<T>(typeof(T), args);
        }

        /// <summary>
        /// 以指定操作键执行操作。
        /// </summary>
        /// <typeparam name="T">操作句柄类型。</typeparam>
        /// <param name="key">操作键。</param>
        /// <param name="args">操作参数。</param>
        /// <returns>已创建并开始执行的操作句柄。</returns>
        public T ExecuteWithKey<T>(object key, params object[] args) where T : OperationHandle
        {
            var operation = (T)Activator.CreateInstance(typeof(T), true);
            Execute(key, operation, args);
            return operation;
        }

        /// <summary>
        /// 执行操作
        /// </summary>
        /// <param name="key">操作键。</param>
        /// <param name="operation">操作句柄。</param>
        public void Execute(object key, OperationHandle operation)
        {
            Execute(key, operation, Array.Empty<object>());
        }

        /// <summary>
        /// 执行操作
        /// </summary>
        /// <param name="key">操作键。</param>
        /// <param name="operation">操作句柄。</param>
        /// <param name="args">操作参数。</param>
        /// <exception cref="ArgumentNullException">操作键或操作句柄为空时抛出。</exception>
        public void Execute(object key, OperationHandle operation, params object[] args)
        {
            var operationKey = RegisterOperation(key, operation);

            operation.SetRunning();
            var runVersion = operation.RunVersion;
            try
            {
                operation.Execute(args ?? Array.Empty<object>());
            }
            catch (Exception exception)
            {
                operation.SetException(exception);
            }

            if (operation.IsDone)
            {
                operation.ObserveCompletion();
                RemoveOperation(operationKey, operation, runVersion);
                return;
            }

            CleanupWhenCompletedAsync(operationKey, operation, runVersion).Forget();
        }

        /// <summary>
        /// 以操作句柄类型作为操作键执行操作并等待完成。
        /// </summary>
        /// <typeparam name="T">操作句柄类型。</typeparam>
        /// <param name="args">操作参数。</param>
        /// <returns>已完成的操作句柄。</returns>
        public UniTask<T> WaitCompletionAsync<T>(params object[] args) where T : OperationHandle
        {
            return WaitCompletionWithKeyAsync<T>(typeof(T), args);
        }

        /// <summary>
        /// 以指定操作键执行操作并等待完成。
        /// </summary>
        /// <typeparam name="T">操作句柄类型。</typeparam>
        /// <param name="key">操作键。</param>
        /// <param name="args">操作参数。</param>
        /// <returns>已完成的操作句柄。</returns>
        public async UniTask<T> WaitCompletionWithKeyAsync<T>(object key, params object[] args) where T : OperationHandle
        {
            var operation = ExecuteWithKey<T>(key, args);
            try
            {
                await operation.WaitCompletionAsync();
            }
            catch
            {
            }
            finally
            {
                RemoveOperation(new OperationKey(key, operation.GetType()), operation);
            }

            return operation;
        }

        /// <summary>
        /// 设置结果
        /// </summary>
        /// <param name="key">操作键。</param>
        /// <param name="_value">操作结果。</param>
        public void SetResult(object key, object _value)
        {
            var operation = GetSingleOperation(key);
            operation.SetResultObject(_value);
            RemoveOperation(new OperationKey(key, operation.GetType()), operation);
        }

        /// <summary>
        /// 以操作句柄类型作为操作键设置结果。
        /// </summary>
        /// <typeparam name="T">操作句柄类型。</typeparam>
        public void SetResult<T>() where T : OperationHandle
        {
            SetResult<T>(null);
        }

        /// <summary>
        /// 以操作句柄类型作为操作键设置结果。
        /// </summary>
        /// <typeparam name="T">操作句柄类型。</typeparam>
        /// <param name="_value">操作结果。</param>
        public void SetResult<T>(object _value) where T : OperationHandle
        {
            var operationKey = CreateTypeOperationKey<T>();
            var operation = GetOperation(operationKey);
            operation.SetResultObject(_value);
            RemoveOperation(operationKey, operation);
        }

        /// <summary>
        /// 设置错误信息
        /// </summary>
        /// <param name="key">操作键。</param>
        /// <param name="ex">错误信息。</param>
        /// <exception cref="ArgumentNullException">错误信息为空时抛出。</exception>
        public void SetException(object key, Exception ex)
        {
            if (ex == null)
            {
                throw new ArgumentNullException(nameof(ex));
            }

            var operation = GetSingleOperation(key);
            operation.SetException(ex);
            RemoveOperation(new OperationKey(key, operation.GetType()), operation);
        }

        /// <summary>
        /// 以操作句柄类型作为操作键设置错误信息。
        /// </summary>
        /// <typeparam name="T">操作句柄类型。</typeparam>
        /// <param name="ex">错误信息。</param>
        /// <exception cref="ArgumentNullException">错误信息为空时抛出。</exception>
        public void SetException<T>(Exception ex) where T : OperationHandle
        {
            if (ex == null)
            {
                throw new ArgumentNullException(nameof(ex));
            }

            var operationKey = CreateTypeOperationKey<T>();
            var operation = GetOperation(operationKey);
            operation.SetException(ex);
            RemoveOperation(operationKey, operation);
        }

        /// <summary>
        /// 设置操作取消
        /// </summary>
        /// <param name="key">操作键。</param>
        public void SetCanceled(object key)
        {
            var operation = GetSingleOperation(key);
            operation.SetCancel();
            RemoveOperation(new OperationKey(key, operation.GetType()), operation);
        }

        /// <summary>
        /// 以操作句柄类型作为操作键设置操作取消。
        /// </summary>
        /// <typeparam name="T">操作句柄类型。</typeparam>
        public void SetCanceled<T>() where T : OperationHandle
        {
            var operationKey = CreateTypeOperationKey<T>();
            var operation = GetOperation(operationKey);
            operation.SetCancel();
            RemoveOperation(operationKey, operation);
        }

        /// <summary>
        /// 注册运行中的操作。
        /// </summary>
        /// <param name="key">操作键。</param>
        /// <param name="operation">操作句柄。</param>
        /// <returns>运行中操作的复合键。</returns>
        /// <exception cref="ArgumentNullException">操作键或操作句柄为空时抛出。</exception>
        /// <exception cref="GameException">操作已经运行、已经完成或同键同类型操作已经存在时抛出。</exception>
        private OperationKey RegisterOperation(object key, OperationHandle operation)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (operation.Status is OperationStatus.Running)
            {
                throw new GameException($"Operation '{operation.GetType().Name}' is already running.");
            }

            if (operation.IsDone)
            {
                throw new GameException($"Operation '{operation.GetType().Name}' is already completed.");
            }

            var operationKey = new OperationKey(key, operation.GetType());
            if (m_Operations.ContainsKey(operationKey))
            {
                if (m_Operations.TryGetValue(operationKey, out var running) &&
                    ReferenceEquals(running, operation) &&
                    operation.Status is OperationStatus.None or OperationStatus.Pending or OperationStatus.Paused)
                {
                    m_Operations.Remove(operationKey);
                }
                else
                {
                    throw new GameException($"Operation '{operation.GetType().Name}' is already running for the specified key.");
                }
            }

            if (m_Operations.ContainsKey(operationKey))
            {
                throw new GameException($"Operation '{operation.GetType().Name}' is already running for the specified key.");
            }

            m_Operations.Add(operationKey, operation);
            return operationKey;
        }

        /// <summary>
        /// 根据操作键获取唯一运行中的操作。
        /// </summary>
        /// <param name="key">操作键。</param>
        /// <returns>运行中的操作句柄。</returns>
        /// <exception cref="ArgumentNullException">操作键为空时抛出。</exception>
        /// <exception cref="GameException">未找到操作或找到多个同键操作时抛出。</exception>
        private OperationHandle GetSingleOperation(object key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            OperationHandle target = null;
            foreach (var operation in m_Operations)
            {
                if (!Equals(operation.Key.Key, key))
                {
                    continue;
                }

                if (target != null)
                {
                    throw new GameException("Multiple running operations were found for the specified key.");
                }

                target = operation.Value;
            }

            if (target == null)
            {
                throw new GameException("No running operation was found for the specified key.");
            }

            return target;
        }

        /// <summary>
        /// 根据复合键获取运行中的操作。
        /// </summary>
        /// <param name="key">运行中操作的复合键。</param>
        /// <returns>运行中的操作句柄。</returns>
        /// <exception cref="GameException">未找到操作时抛出。</exception>
        private OperationHandle GetOperation(OperationKey key)
        {
            if (!m_Operations.TryGetValue(key, out var operation))
            {
                throw new GameException("No running operation was found for the specified key.");
            }

            return operation;
        }

        /// <summary>
        /// 创建以操作句柄类型为业务键的复合键。
        /// </summary>
        /// <typeparam name="T">操作句柄类型。</typeparam>
        /// <returns>运行中操作的复合键。</returns>
        private static OperationKey CreateTypeOperationKey<T>() where T : OperationHandle
        {
            var operationType = typeof(T);
            return new OperationKey(operationType, operationType);
        }

        /// <summary>
        /// 从运行中操作表移除指定操作。
        /// </summary>
        /// <param name="key">运行中操作的复合键。</param>
        /// <param name="operation">操作句柄。</param>
        private void RemoveOperation(OperationKey key, OperationHandle operation)
        {
            RemoveOperation(key, operation, operation.RunVersion);
        }

        /// <summary>
        /// 从运行中操作表移除指定运行版本的操作。
        /// </summary>
        /// <param name="key">运行中操作的复合键。</param>
        /// <param name="operation">操作句柄。</param>
        /// <param name="runVersion">操作开始执行时的运行版本。</param>
        private void RemoveOperation(OperationKey key, OperationHandle operation, int runVersion)
        {
            if (m_Operations.TryGetValue(key, out var running) &&
                ReferenceEquals(running, operation) &&
                operation.RunVersion == runVersion)
            {
                m_Operations.Remove(key);
            }
        }

        /// <summary>
        /// 等待操作完成后自动清理运行中操作表。
        /// </summary>
        /// <param name="key">运行中操作的复合键。</param>
        /// <param name="operation">操作句柄。</param>
        /// <param name="runVersion">操作开始执行时的运行版本。</param>
        /// <returns>异步清理任务。</returns>
        private async UniTaskVoid CleanupWhenCompletedAsync(OperationKey key, OperationHandle operation, int runVersion)
        {
            try
            {
                await operation.WaitCompletionAsync();
            }
            catch
            {
            }
            finally
            {
                RemoveOperation(key, operation, runVersion);
            }
        }

        /// <summary>
        /// 运行中操作的复合键，由业务键和操作类型共同决定唯一性。
        /// </summary>
        private readonly struct OperationKey : IEquatable<OperationKey>
        {
            /// <summary>
            /// 业务操作键。
            /// </summary>
            public readonly object Key;

            private readonly Type m_OperationType;

            /// <summary>
            /// 初始化运行中操作复合键。
            /// </summary>
            /// <param name="key">业务操作键。</param>
            /// <param name="operationType">操作句柄类型。</param>
            public OperationKey(object key, Type operationType)
            {
                Key = key;
                m_OperationType = operationType;
            }

            /// <summary>
            /// 判断两个操作键是否相等。
            /// </summary>
            /// <param name="other">另一个操作键。</param>
            /// <returns>如果业务键和操作类型都相等，则返回true；否则返回false。</returns>
            public bool Equals(OperationKey other)
            {
                return Equals(Key, other.Key) && m_OperationType == other.m_OperationType;
            }

            /// <summary>
            /// 判断指定对象是否与当前操作键相等。
            /// </summary>
            /// <param name="obj">待比较对象。</param>
            /// <returns>如果对象是相等的操作键，则返回true；否则返回false。</returns>
            public override bool Equals(object obj)
            {
                return obj is OperationKey other && Equals(other);
            }

            /// <summary>
            /// 获取操作键哈希码。
            /// </summary>
            /// <returns>操作键哈希码。</returns>
            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Key != null ? Key.GetHashCode() : 0) * 397) ^ (m_OperationType != null ? m_OperationType.GetHashCode() : 0);
                }
            }
        }
    }
}
