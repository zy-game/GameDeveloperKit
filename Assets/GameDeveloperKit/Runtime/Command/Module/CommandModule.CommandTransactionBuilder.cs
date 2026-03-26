using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 命令模块的事务构建相关实现
    /// </summary>
    public sealed partial class CommandModule
    {
        /// <summary>
        /// 命令事务构建器，用于收集命令步骤并生成命令事务
        /// </summary>
        public sealed class CommandTransactionBuilder : IDisposable, IReferencePoolable
        {
            private CommandModule _module;
            private readonly List<CommandStep> _steps = new();
            private bool _released;

            /// <summary>
            /// 初始化命令事务构建器的新实例
            /// </summary>
            public CommandTransactionBuilder()
            {
            }

            /// <summary>
            /// 初始化事务构建器
            /// </summary>
            /// <param name="module">所属命令模块</param>
            /// <param name="transactionName">事务名称</param>
            /// <exception cref="ArgumentNullException">命令模块为空</exception>
            /// <exception cref="ArgumentException">事务名称为空</exception>
            internal void Initialize(CommandModule module, string transactionName)
            {
                _module = module ?? throw new ArgumentNullException(nameof(module));
                if (string.IsNullOrWhiteSpace(transactionName))
                {
                    throw new ArgumentException("Transaction name can not be empty.", nameof(transactionName));
                }

                TransactionName = transactionName;
                _released = false;
            }

            /// <summary>
            /// 获取事务名称
            /// </summary>
            public string TransactionName { get; private set; }

            /// <summary>
            /// 获取当前已添加的步骤数量
            /// </summary>
            public int StepCount => _steps.Count;

            /// <summary>
            /// 添加可撤销命令步骤
            /// </summary>
            /// <param name="command">可撤销命令</param>
            /// <returns>当前事务构建器</returns>
            /// <exception cref="InvalidOperationException">构建器已释放</exception>
            public CommandTransactionBuilder Add(IUndoableCommand command)
            {
                ThrowIfReleased();
                _steps.Add(new CommandStep(command));
                return this;
            }

            /// <summary>
            /// 添加异步可撤销命令步骤
            /// </summary>
            /// <param name="command">异步可撤销命令</param>
            /// <returns>当前事务构建器</returns>
            /// <exception cref="InvalidOperationException">构建器已释放</exception>
            public CommandTransactionBuilder Add(IAsyncUndoableCommand command)
            {
                ThrowIfReleased();
                _steps.Add(new CommandStep(command));
                return this;
            }

            /// <summary>
            /// 添加命令步骤
            /// </summary>
            /// <param name="step">命令步骤</param>
            /// <returns>当前事务构建器</returns>
            /// <exception cref="InvalidOperationException">构建器已释放</exception>
            public CommandTransactionBuilder Add(CommandStep step)
            {
                ThrowIfReleased();
                _steps.Add(step);
                return this;
            }

            /// <summary>
            /// 构建命令事务
            /// </summary>
            /// <returns>构建完成的命令事务</returns>
            /// <exception cref="InvalidOperationException">构建器已释放</exception>
            public CommandTransaction Build()
            {
                ThrowIfReleased();
                return new CommandTransaction(TransactionName, _steps.ToArray());
            }

            /// <summary>
            /// 构建并执行命令事务
            /// </summary>
            /// <param name="historyName">命令历史名称</param>
            /// <exception cref="InvalidOperationException">构建器已释放</exception>
            public void Execute(string historyName = DefaultHistoryName)
            {
                ThrowIfReleased();
                try
                {
                    _module.ExecuteTransaction(Build(), historyName);
                }
                finally
                {
                    Dispose();
                }
            }

            /// <summary>
            /// 异步构建并执行命令事务
            /// </summary>
            /// <param name="historyName">命令历史名称</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>异步任务</returns>
            /// <exception cref="InvalidOperationException">构建器已释放</exception>
            public async UniTask ExecuteAsync(string historyName = DefaultHistoryName, CancellationToken cancellationToken = default)
            {
                ThrowIfReleased();
                try
                {
                    await _module.ExecuteAsync(Build(), historyName, cancellationToken);
                }
                finally
                {
                    Dispose();
                }
            }

            /// <summary>
            /// 释放事务构建器并归还到对象池
            /// </summary>
            public void Dispose()
            {
                if (_released)
                {
                    return;
                }

                _released = true;
                _module?.ReleaseTransactionBuilder(this);
            }

            /// <summary>
            /// 重置对象池中的事务构建器状态
            /// </summary>
            public void ResetForPool()
            {
                _module = null;
                TransactionName = string.Empty;
                _steps.Clear();
                _released = false;
            }

            private void ThrowIfReleased()
            {
                if (_released)
                {
                    throw new InvalidOperationException("CommandTransactionBuilder has already been released.");
                }
            }
        }
    }
}
