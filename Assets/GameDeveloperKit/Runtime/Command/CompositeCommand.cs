using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    public sealed class CompositeCommand : IUndoableCommand, IAsyncUndoableCommand
    {
        private readonly IReadOnlyList<CommandStep> _commands;

        public CompositeCommand(params CommandStep[] commands)
        {
            _commands = commands ?? Array.Empty<CommandStep>();
        }

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

        public void Undo()
        {
            Rollback(_commands.Count);
        }

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

        public UniTask UndoAsync(CancellationToken cancellationToken = default)
        {
            return RollbackAsync(_commands.Count, cancellationToken);
        }

        private void Rollback(int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                _commands[i].Undo();
            }
        }

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
