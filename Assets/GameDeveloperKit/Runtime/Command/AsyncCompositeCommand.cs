using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    public sealed class AsyncCompositeCommand : IAsyncUndoableCommand
    {
        private readonly CompositeCommand _compositeCommand;

        public AsyncCompositeCommand(params IAsyncUndoableCommand[] commands)
        {
            _compositeCommand = new CompositeCommand(ToSteps(commands));
        }

        public UniTask ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return _compositeCommand.ExecuteAsync(cancellationToken);
        }

        public UniTask UndoAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return _compositeCommand.UndoAsync(cancellationToken);
        }

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
