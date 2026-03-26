using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 异步复合命令类，组合多个异步命令作为一个原子操作执行。
    /// </summary>
    public sealed class AsyncCompositeCommand : IAsyncUndoableCommand
    {
        private readonly CompositeCommand _compositeCommand;

        /// <summary>
        /// 初始化异步复合命令的新实例。
        /// </summary>
        /// <param name="commands">异步命令数组。</param>
        public AsyncCompositeCommand(params IAsyncUndoableCommand[] commands)
        {
            _compositeCommand = new CompositeCommand(ToSteps(commands));
        }

        /// <summary>
        /// 异步执行所有命令。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public UniTask ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return _compositeCommand.ExecuteAsync(cancellationToken);
        }

        /// <summary>
        /// 异步撤销所有命令。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public UniTask UndoAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return _compositeCommand.UndoAsync(cancellationToken);
        }

        /// <summary>
        /// 将异步命令数组转换为命令步骤数组。
        /// </summary>
        /// <param name="commands">异步命令数组。</param>
        /// <returns>命令步骤数组。</returns>
        private static CommandStep[] ToSteps(IAsyncUndoableCommand[] commands)
        {
            if (commands == null || commands.Length == 0)
            {
                return System.Array.Empty<CommandStep>();
            }

            var steps = new CommandStep[commands.Length];
            for (var i = 0; i < commands.Length; i++)
            {
                steps[i] = new CommandStep(commands[i]);
            }

            return steps;
        }
    }
}
