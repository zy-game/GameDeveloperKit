using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 命令事务类，表示一个事务性的命令集合，所有命令作为一个原子操作执行。
    /// </summary>
    public sealed class CommandTransaction : IUndoableCommand, IAsyncUndoableCommand
    {
        private readonly CompositeCommand _compositeCommand;

        /// <summary>
        /// 初始化命令事务的新实例。
        /// </summary>
        /// <param name="name">事务名称。</param>
        /// <param name="commands">命令步骤数组。</param>
        /// <exception cref="ArgumentException">当名称为空时抛出。</exception>
        public CommandTransaction(string name, params CommandStep[] commands)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Transaction name can not be empty.", nameof(name));
            }

            Name = name;
            _compositeCommand = new CompositeCommand(commands);
        }

        /// <summary>
        /// 获取事务名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 获取事务中的命令步骤数。
        /// </summary>
        public int StepCount => _compositeCommand.StepCount;

        /// <summary>
        /// 执行事务中的所有命令。
        /// </summary>
        public void Execute()
        {
            _compositeCommand.Execute();
        }

        /// <summary>
        /// 撤销事务中的所有命令。
        /// </summary>
        public void Undo()
        {
            _compositeCommand.Undo();
        }

        /// <summary>
        /// 异步执行事务中的所有命令。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public UniTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            return _compositeCommand.ExecuteAsync(cancellationToken);
        }

        /// <summary>
        /// 异步撤销事务中的所有命令。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public UniTask UndoAsync(CancellationToken cancellationToken = default)
        {
            return _compositeCommand.UndoAsync(cancellationToken);
        }

        /// <summary>
        /// 从同步命令数组创建命令事务。
        /// </summary>
        /// <param name="name">事务名称。</param>
        /// <param name="commands">同步命令数组。</param>
        /// <returns>命令事务。</returns>
        public static CommandTransaction Create(string name, params IUndoableCommand[] commands)
        {
            if (commands == null || commands.Length == 0)
            {
                return new CommandTransaction(name);
            }

            var steps = new CommandStep[commands.Length];
            for (var i = 0; i < commands.Length; i++)
            {
                steps[i] = new CommandStep(commands[i]);
            }

            return new CommandTransaction(name, steps);
        }

        /// <summary>
        /// 从异步命令数组创建命令事务。
        /// </summary>
        /// <param name="name">事务名称。</param>
        /// <param name="commands">异步命令数组。</param>
        /// <returns>命令事务。</returns>
        public static CommandTransaction Create(string name, params IAsyncUndoableCommand[] commands)
        {
            if (commands == null || commands.Length == 0)
            {
                return new CommandTransaction(name);
            }

            var steps = new CommandStep[commands.Length];
            for (var i = 0; i < commands.Length; i++)
            {
                steps[i] = new CommandStep(commands[i]);
            }

            return new CommandTransaction(name, steps);
        }
    }
}
