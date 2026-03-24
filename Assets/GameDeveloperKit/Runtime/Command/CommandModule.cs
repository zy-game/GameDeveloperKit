using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    public sealed class CommandModule : IGameFrameworkModule
    {
        public const string DefaultHistoryName = "Default";

        private readonly Dictionary<string, CommandHistory> _histories = new(StringComparer.Ordinal);

        public IReadOnlyCollection<CommandHistory> Histories => _histories.Values;

        public CommandHistory DefaultHistory => GetOrCreateHistory();

        public CommandHistory GetOrCreateHistory(string historyName = DefaultHistoryName)
        {
            if (string.IsNullOrWhiteSpace(historyName))
            {
                throw new ArgumentException("History name can not be empty.", nameof(historyName));
            }

            if (_histories.TryGetValue(historyName, out var history))
            {
                return history;
            }

            history = new CommandHistory(historyName);
            _histories.Add(historyName, history);
            return history;
        }

        public bool HasHistory(string historyName)
        {
            if (string.IsNullOrWhiteSpace(historyName))
            {
                return false;
            }

            return _histories.ContainsKey(historyName);
        }

        public bool TryGetHistory(string historyName, out CommandHistory history)
        {
            if (string.IsNullOrWhiteSpace(historyName))
            {
                history = null;
                return false;
            }

            return _histories.TryGetValue(historyName, out history);
        }

        public bool RemoveHistory(string historyName)
        {
            if (string.IsNullOrWhiteSpace(historyName))
            {
                return false;
            }

            if (!_histories.Remove(historyName, out var history))
            {
                return false;
            }

            history.Clear();
            return true;
        }

        public void Execute(ICommand command, string historyName = DefaultHistoryName)
        {
            GetOrCreateHistory(historyName).Execute(command);
        }

        public UniTask ExecuteAsync(IAsyncCommand command, string historyName = DefaultHistoryName, CancellationToken cancellationToken = default)
        {
            return GetOrCreateHistory(historyName).ExecuteAsync(command, cancellationToken);
        }

        public void Undo(string historyName = DefaultHistoryName)
        {
            GetOrCreateHistory(historyName).Undo();
        }

        public bool TryUndo(string historyName = DefaultHistoryName)
        {
            return GetOrCreateHistory(historyName).TryUndo();
        }

        public UniTask UndoAsync(string historyName = DefaultHistoryName, CancellationToken cancellationToken = default)
        {
            return GetOrCreateHistory(historyName).UndoAsync(cancellationToken);
        }

        public UniTask<bool> TryUndoAsync(string historyName = DefaultHistoryName, CancellationToken cancellationToken = default)
        {
            return GetOrCreateHistory(historyName).TryUndoAsync(cancellationToken);
        }

        public void Redo(string historyName = DefaultHistoryName)
        {
            GetOrCreateHistory(historyName).Redo();
        }

        public bool TryRedo(string historyName = DefaultHistoryName)
        {
            return GetOrCreateHistory(historyName).TryRedo();
        }

        public UniTask RedoAsync(string historyName = DefaultHistoryName, CancellationToken cancellationToken = default)
        {
            return GetOrCreateHistory(historyName).RedoAsync(cancellationToken);
        }

        public UniTask<bool> TryRedoAsync(string historyName = DefaultHistoryName, CancellationToken cancellationToken = default)
        {
            return GetOrCreateHistory(historyName).TryRedoAsync(cancellationToken);
        }

        public bool CanUndo(string historyName = DefaultHistoryName)
        {
            return GetOrCreateHistory(historyName).CanUndo;
        }

        public bool CanRedo(string historyName = DefaultHistoryName)
        {
            return GetOrCreateHistory(historyName).CanRedo;
        }

        public void Clear(string historyName = DefaultHistoryName)
        {
            GetOrCreateHistory(historyName).Clear();
        }

        public void ClearAll()
        {
            foreach (var history in _histories.Values)
            {
                history.Clear();
            }
        }

        public void Dispose()
        {
            ClearAll();
            _histories.Clear();
        }
    }
}
