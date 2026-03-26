using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 复合命令类，组合多个命令作为一个原子操作执行。
    /// </summary>
    public sealed class CompositeCommand : IUndoableCommand, IAsyncUndoableCommand
    {
        private readonly IReadOnlyList<CommandStep> _commands;

        /// <summary>
        /// 初始化复合命令的新实例。
        /// </summary>
        /// <param name="commands">命令步骤数组。</param>
        public CompositeCommand(params CommandStep[] commands)
        {
            _commands = commands ?? Array.Empty<CommandStep>();
        }

        /// <summary>
        /// 获取命令步骤数。
        /// </summary>
        public int StepCount => _commands.Count;

        /// <summary>
        /// 执行所有命令，如果失败则回滚已执行的命令。
        /// </summary>
        public void Execute()
        {
            var executedCount = 0;
            try
            {
                for (var i = 0; i < _commands.Count; i++)
                {
                    _commands[i].Execute();
                    executedCount++;
                }
            }
            catch
            {
                Rollback(executedCount);
                throw;
            }
        }

        /// <summary>
        /// 从同步命令数组创建复合命令。
        /// </summary>
        /// <param name="commands">同步命令数组。</param>
        /// <returns>复合命令。</returns>
        public static CompositeCommand Create(params IUndoableCommand[] commands)
        {
            if (commands == null || commands.Length == 0)
            {
                return new CompositeCommand();
            }

            var steps = new CommandStep[commands.Length];
            for (var i = 0; i < commands.Length; i++)
            {
                steps[i] = new CommandStep(commands[i]);
            }

            return new CompositeCommand(steps);
        }

        /// <summary>
        /// 从异步命令数组创建复合命令。
        /// </summary>
        /// <param name="commands">异步命令数组。</param>
        /// <returns>复合命令。</returns>
        public static CompositeCommand Create(params IAsyncUndoableCommand[] commands)
        {
            if (commands == null || commands.Length == 0)
            {
                return new CompositeCommand();
            }

            var steps = new CommandStep[commands.Length];
            for (var i = 0; i < commands.Length; i++)
            {
                steps[i] = new CommandStep(commands[i]);
            }

            return new CompositeCommand(steps);
        }

        /// <summary>
        /// 撤销所有命令。
        /// </summary>
        public void Undo()
        {
            Rollback(_commands.Count);
        }

        /// <summary>
        /// 异步执行所有命令，如果失败则回滚已执行的命令。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public async UniTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var executedCount = 0;
            try
            {
                for (var i = 0; i < _commands.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await _commands[i].ExecuteAsync(cancellationToken);
                    executedCount++;
                }
            }
            catch
            {
                await RollbackAsync(executedCount, cancellationToken);
                throw;
            }
        }

        /// <summary>
        /// 异步撤销所有命令。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public UniTask UndoAsync(CancellationToken cancellationToken = default)
        {
            return RollbackAsync(_commands.Count, cancellationToken);
        }

        /// <summary>
        /// 回滚指定数量的命令。
        /// </summary>
        /// <param name="count">要回滚的命令数。</param>
        private void Rollback(int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                _commands[i].Undo();
            }
        }

        /// <summary>
        /// 异步回滚指定数量的命令。
        /// </summary>
        /// <param name="count">要回滚的命令数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async UniTask RollbackAsync(int count, CancellationToken cancellationToken)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _commands[i].UndoAsync(cancellationToken);
            }
        }
    }
}
