using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    public sealed class CommandHistory
    {
        private readonly Stack<HistoryEntry> _undoEntries = new();
        private readonly Stack<HistoryEntry> _redoEntries = new();
        private bool _isRunning;

        public CommandHistory(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("History name can not be empty.", nameof(name));
            }

            Name = name;
        }

        public string Name { get; }

        public bool IsRunning => _isRunning;

        public bool CanUndo => _undoEntries.Count > 0;

        public bool CanRedo => _redoEntries.Count > 0;

        public int UndoCount => _undoEntries.Count;

        public int RedoCount => _redoEntries.Count;

        public void Execute(ICommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            EnterRunningScope();
            try
            {
                command.Execute();

                if (command is IUndoableCommand undoableCommand)
                {
                    _undoEntries.Push(HistoryEntry.Create(undoableCommand));
                    _redoEntries.Clear();
                }
            }
            finally
            {
                ExitRunningScope();
            }
        }

        public async UniTask ExecuteAsync(IAsyncCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            EnterRunningScope();
            try
            {
                await command.ExecuteAsync(cancellationToken);

                if (command is IAsyncUndoableCommand undoableCommand)
                {
                    _undoEntries.Push(HistoryEntry.Create(undoableCommand));
                    _redoEntries.Clear();
                }
            }
            finally
            {
                ExitRunningScope();
            }
        }

        public void Undo()
        {
            EnterRunningScope();
            try
            {
                if (_undoEntries.Count == 0)
                {
                    throw new InvalidOperationException($"History '{Name}' has no commands to undo.");
                }

                var entry = _undoEntries.Peek();
                if (entry.IsAsync)
                {
                    throw new InvalidOperationException($"History '{Name}' can only undo the next command asynchronously.");
                }

                _undoEntries.Pop();
                entry.Undo();
                _redoEntries.Push(entry);
            }
            finally
            {
                ExitRunningScope();
            }
        }

        public bool TryUndo()
        {
            if (!CanUndo)
            {
                return false;
            }

            var entry = _undoEntries.Peek();
            if (entry.IsAsync)
            {
                return false;
            }

            Undo();
            return true;
        }

        public async UniTask UndoAsync(CancellationToken cancellationToken = default)
        {
            EnterRunningScope();
            try
            {
                if (_undoEntries.Count == 0)
                {
                    throw new InvalidOperationException($"History '{Name}' has no commands to undo.");
                }

                var entry = _undoEntries.Pop();
                await entry.UndoAsync(cancellationToken);
                _redoEntries.Push(entry);
            }
            finally
            {
                ExitRunningScope();
            }
        }

        public async UniTask<bool> TryUndoAsync(CancellationToken cancellationToken = default)
        {
            if (!CanUndo)
            {
                return false;
            }

            await UndoAsync(cancellationToken);
            return true;
        }

        public void Redo()
        {
            EnterRunningScope();
            try
            {
                if (_redoEntries.Count == 0)
                {
                    throw new InvalidOperationException($"History '{Name}' has no commands to redo.");
                }

                var entry = _redoEntries.Peek();
                if (entry.IsAsync)
                {
                    throw new InvalidOperationException($"History '{Name}' can only redo the next command asynchronously.");
                }

                _redoEntries.Pop();
                entry.Execute();
                _undoEntries.Push(entry);
            }
            finally
            {
                ExitRunningScope();
            }
        }

        public bool TryRedo()
        {
            if (!CanRedo)
            {
                return false;
            }

            var entry = _redoEntries.Peek();
            if (entry.IsAsync)
            {
                return false;
            }

            Redo();
            return true;
        }

        public async UniTask RedoAsync(CancellationToken cancellationToken = default)
        {
            EnterRunningScope();
            try
            {
                if (_redoEntries.Count == 0)
                {
                    throw new InvalidOperationException($"History '{Name}' has no commands to redo.");
                }

                var entry = _redoEntries.Pop();
                await entry.ExecuteAsync(cancellationToken);
                _undoEntries.Push(entry);
            }
            finally
            {
                ExitRunningScope();
            }
        }

        public async UniTask<bool> TryRedoAsync(CancellationToken cancellationToken = default)
        {
            if (!CanRedo)
            {
                return false;
            }

            await RedoAsync(cancellationToken);
            return true;
        }

        public void Clear()
        {
            _undoEntries.Clear();
            _redoEntries.Clear();
        }

        private void EnterRunningScope()
        {
            if (_isRunning)
            {
                throw new InvalidOperationException($"History '{Name}' is already running a command.");
            }

            _isRunning = true;
        }

        private void ExitRunningScope()
        {
            _isRunning = false;
        }

        private readonly struct HistoryEntry
        {
            private HistoryEntry(IUndoableCommand syncCommand)
            {
                SyncCommand = syncCommand;
                AsyncCommand = null;
            }

            private HistoryEntry(IAsyncUndoableCommand asyncCommand)
            {
                SyncCommand = null;
                AsyncCommand = asyncCommand;
            }

            public IUndoableCommand SyncCommand { get; }
            public IAsyncUndoableCommand AsyncCommand { get; }
            public bool IsAsync => AsyncCommand != null;

            public static HistoryEntry Create(IUndoableCommand command)
            {
                return new HistoryEntry(command);
            }

            public static HistoryEntry Create(IAsyncUndoableCommand command)
            {
                return new HistoryEntry(command);
            }

            public void Execute()
            {
                if (SyncCommand == null)
                {
                    throw new InvalidOperationException("The next command can only be redone asynchronously.");
                }

                SyncCommand.Execute();
            }

            public async UniTask ExecuteAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (AsyncCommand != null)
                {
                    await AsyncCommand.ExecuteAsync(cancellationToken);
                    return;
                }

                SyncCommand.Execute();
            }

            public void Undo()
            {
                if (SyncCommand == null)
                {
                    throw new InvalidOperationException("The next command can only be undone asynchronously.");
                }

                SyncCommand.Undo();
            }

            public async UniTask UndoAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (AsyncCommand != null)
                {
                    await AsyncCommand.UndoAsync(cancellationToken);
                    return;
                }

                SyncCommand.Undo();
            }
        }
    }
}
