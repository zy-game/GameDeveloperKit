using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 命令模块，提供命令执行、撤销/重做和命令事务功能。
    /// 支持多命令历史和异步命令。
    /// </summary>
    public sealed partial class CommandModule : IGameFrameworkModule
    {
        /// <summary>
        /// 默认命令历史名称。
        /// </summary>
        public const string DefaultHistoryName = "Default";

        private readonly Dictionary<string, CommandHistory> _histories = new(StringComparer.Ordinal);
        private bool _diagnosticsRegistered;
        private int _commandExecutedCount;
        private int _undoPerformedCount;
        private int _redoPerformedCount;
        private int _commandFailureCount;
        private int _transactionExecutedCount;
        private string _lastHistoryName;
        private string _lastCommandType;
        private string _lastTransactionName;

        /// <summary>
        /// 获取所有命令历史记录。
        /// </summary>
        public IReadOnlyCollection<CommandHistory> Histories => _histories.Values;

        /// <summary>
        /// 获取默认命令历史记录。
        /// </summary>
        public CommandHistory DefaultHistory => GetOrCreateHistory();

        /// <summary>
        /// 获取或设置默认历史记录的最大撤销条目数。
        /// </summary>
        public int DefaultHistoryCapacity { get; private set; } = 128;

        /// <summary>
        /// 当命令执行时触发。
        /// </summary>
        public event Action<CommandHistory, object> CommandExecuted;

        /// <summary>
        /// 当执行撤销时触发。
        /// </summary>
        public event Action<CommandHistory, object> UndoPerformed;

        /// <summary>
        /// 当执行重做时触发。
        /// </summary>
        public event Action<CommandHistory, object> RedoPerformed;

        /// <summary>
        /// 当命令执行失败时触发。
        /// </summary>
        public event Action<CommandHistory, object, Exception> CommandFailed;

        /// <summary>
        /// 获取或创建命令历史记录。
        /// </summary>
        /// <param name="historyName">历史记录名称。</param>
        /// <returns>命令历史记录。</returns>
        /// <exception cref="ArgumentException">当历史记录名称为空时抛出。</exception>
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

            history = new CommandHistory(historyName, DefaultHistoryCapacity);
            history.CommandExecuted += HandleCommandExecuted;
            history.UndoPerformed += HandleUndoPerformed;
            history.RedoPerformed += HandleRedoPerformed;
            history.CommandFailed += HandleCommandFailed;
            _histories.Add(historyName, history);
            EnsureDiagnosticsSnapshotProviders();
            return history;
        }

        /// <summary>
        /// 检查是否存在指定的历史记录。
        /// </summary>
        /// <param name="historyName">历史记录名称。</param>
        /// <returns>如果存在则返回 true，否则返回 false。</returns>
        public bool HasHistory(string historyName)
        {
            if (string.IsNullOrWhiteSpace(historyName))
            {
                return false;
            }

            return _histories.ContainsKey(historyName);
        }

        /// <summary>
        /// 尝试获取指定的历史记录。
        /// </summary>
        /// <param name="historyName">历史记录名称。</param>
        /// <param name="history">输出历史记录。</param>
        /// <returns>如果获取成功则返回 true，否则返回 false。</returns>
        public bool TryGetHistory(string historyName, out CommandHistory history)
        {
            if (string.IsNullOrWhiteSpace(historyName))
            {
                history = null;
                return false;
            }

            return _histories.TryGetValue(historyName, out history);
        }

        /// <summary>
        /// 移除指定的历史记录。
        /// </summary>
        /// <param name="historyName">历史记录名称。</param>
        /// <returns>如果移除成功则返回 true，否则返回 false。</returns>
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

            UnbindHistory(history);
            history.Clear();
            return true;
        }

        /// <summary>
        /// 设置默认历史记录的最大撤销条目数。
        /// </summary>
        /// <param name="maxUndoEntries">最大撤销条目数。</param>
        /// <param name="applyToExistingHistories">是否应用到现有历史记录。</param>
        public void SetDefaultHistoryCapacity(int maxUndoEntries, bool applyToExistingHistories = true)
        {
            DefaultHistoryCapacity = Math.Max(0, maxUndoEntries);
            if (!applyToExistingHistories)
            {
                return;
            }

            foreach (var history in _histories.Values)
            {
                history.MaxUndoEntries = DefaultHistoryCapacity;
            }
        }

        /// <summary>
        /// 设置指定历史记录的最大撤销条目数。
        /// </summary>
        /// <param name="historyName">历史记录名称。</param>
        /// <param name="maxUndoEntries">最大撤销条目数。</param>
        public void SetHistoryCapacity(string historyName, int maxUndoEntries)
        {
            GetOrCreateHistory(historyName).MaxUndoEntries = Math.Max(0, maxUndoEntries);
        }

        /// <summary>
        /// 执行命令。
        /// </summary>
        /// <param name="command">要执行的命令。</param>
        /// <param name="historyName">历史记录名称。</param>
        public void Execute(ICommand command, string historyName = DefaultHistoryName)
        {
            GetOrCreateHistory(historyName).Execute(command);
        }

        /// <summary>
        /// 异步执行命令。
        /// </summary>
        /// <param name="command">要执行的命令。</param>
        /// <param name="historyName">历史记录名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>执行任务。</returns>
        public UniTask ExecuteAsync(IAsyncCommand command, string historyName = DefaultHistoryName, CancellationToken cancellationToken = default)
        {
            return GetOrCreateHistory(historyName).ExecuteAsync(command, cancellationToken);
        }

        /// <summary>
        /// 创建命令事务构建器。
        /// </summary>
        /// <param name="transactionName">事务名称。</param>
        /// <returns>命令事务构建器。</returns>
        public CommandTransactionBuilder CreateTransaction(string transactionName)
        {
            return RentTransactionBuilder(transactionName);
        }

        /// <summary>
        /// 租用命令事务构建器（对象池优化）。
        /// </summary>
        /// <param name="transactionName">事务名称。</param>
        /// <returns>命令事务构建器。</returns>
        public CommandTransactionBuilder RentTransactionBuilder(string transactionName)
        {
            var builder = Game.Pool.ReferencePool.Acquire<CommandTransactionBuilder>();
            builder.Initialize(this, transactionName);
            return builder;
        }

        /// <summary>
        /// 释放命令事务构建器。
        /// </summary>
        /// <param name="builder">命令事务构建器。</param>
        public void ReleaseTransactionBuilder(CommandTransactionBuilder builder)
        {
            if (builder == null)
            {
                return;
            }

            Game.Pool.ReferencePool.Release(builder);
        }

        /// <summary>
        /// 执行命令事务。
        /// </summary>
        /// <param name="transaction">命令事务。</param>
        /// <param name="historyName">历史记录名称。</param>
        /// <exception cref="ArgumentNullException">当事务为 null 时抛出。</exception>
        public void ExecuteTransaction(CommandTransaction transaction, string historyName = DefaultHistoryName)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }

            Execute(transaction, historyName);
        }

        /// <summary>
        /// 创建并执行命令事务。
        /// </summary>
        /// <param name="transactionName">事务名称。</param>
        /// <param name="historyName">历史记录名称。</param>
        /// <param name="commands">命令数组。</param>
        public void ExecuteTransaction(string transactionName, string historyName = DefaultHistoryName, params IUndoableCommand[] commands)
        {
            ExecuteTransaction(CommandTransaction.Create(transactionName, commands), historyName);
        }

        /// <summary>
        /// 创建并执行命令事务（使用命令步骤）。
        /// </summary>
        /// <param name="transactionName">事务名称。</param>
        /// <param name="historyName">历史记录名称。</param>
        /// <param name="steps">命令步骤数组。</param>
        public void ExecuteTransaction(string transactionName, string historyName, params CommandStep[] steps)
        {
            ExecuteTransaction(new CommandTransaction(transactionName, steps), historyName);
        }

        /// <summary>
        /// 异步执行命令事务。
        /// </summary>
        /// <param name="transaction">命令事务。</param>
        /// <param name="historyName">历史记录名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>执行任务。</returns>
        /// <exception cref="ArgumentNullException">当事务为 null 时抛出。</exception>
        public UniTask ExecuteTransactionAsync(CommandTransaction transaction, string historyName = DefaultHistoryName, CancellationToken cancellationToken = default)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }

            return ExecuteAsync(transaction, historyName, cancellationToken);
        }

        /// <summary>
        /// 创建并异步执行命令事务。
        /// </summary>
        /// <param name="transactionName">事务名称。</param>
        /// <param name="commands">异步命令数组。</param>
        /// <param name="historyName">历史记录名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>执行任务。</returns>
        public UniTask ExecuteTransactionAsync(string transactionName, IAsyncUndoableCommand[] commands, string historyName = DefaultHistoryName, CancellationToken cancellationToken = default)
        {
            return ExecuteTransactionAsync(CommandTransaction.Create(transactionName, commands), historyName, cancellationToken);
        }

        /// <summary>
        /// 创建并异步执行命令事务（使用命令步骤）。
        /// </summary>
        /// <param name="transactionName">事务名称。</param>
        /// <param name="steps">命令步骤数组。</param>
        /// <param name="historyName">历史记录名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>执行任务。</returns>
        public UniTask ExecuteTransactionAsync(string transactionName, CommandStep[] steps, string historyName = DefaultHistoryName, CancellationToken cancellationToken = default)
        {
            return ExecuteTransactionAsync(new CommandTransaction(transactionName, steps), historyName, cancellationToken);
        }

        /// <summary>
        /// 执行撤销操作。
        /// </summary>
        /// <param name="historyName">历史记录名称。</param>
        public void Undo(string historyName = DefaultHistoryName)
        {
            GetOrCreateHistory(historyName).Undo();
        }

        /// <summary>
        /// 尝试执行撤销操作。
        /// </summary>
        /// <param name="historyName">历史记录名称。</param>
        /// <returns>如果撤销成功则返回 true，否则返回 false。</returns>
        public bool TryUndo(string historyName = DefaultHistoryName)
        {
            return GetOrCreateHistory(historyName).TryUndo();
        }

        /// <summary>
        /// 异步执行撤销操作。
        /// </summary>
        /// <param name="historyName">历史记录名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>撤销任务。</returns>
        public UniTask UndoAsync(string historyName = DefaultHistoryName, CancellationToken cancellationToken = default)
        {
            return GetOrCreateHistory(historyName).UndoAsync(cancellationToken);
        }

        /// <summary>
        /// 尝试异步执行撤销操作。
        /// </summary>
        /// <param name="historyName">历史记录名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>撤销任务，结果表示是否成功。</returns>
        public UniTask<bool> TryUndoAsync(string historyName = DefaultHistoryName, CancellationToken cancellationToken = default)
        {
            return GetOrCreateHistory(historyName).TryUndoAsync(cancellationToken);
        }

        /// <summary>
        /// 执行重做操作。
        /// </summary>
        /// <param name="historyName">历史记录名称。</param>
        public void Redo(string historyName = DefaultHistoryName)
        {
            GetOrCreateHistory(historyName).Redo();
        }

        /// <summary>
        /// 尝试执行重做操作。
        /// </summary>
        /// <param name="historyName">历史记录名称。</param>
        /// <returns>如果重做成功则返回 true，否则返回 false。</returns>
        public bool TryRedo(string historyName = DefaultHistoryName)
        {
            return GetOrCreateHistory(historyName).TryRedo();
        }

        /// <summary>
        /// 异步执行重做操作。
        /// </summary>
        /// <param name="historyName">历史记录名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>重做任务。</returns>
        public UniTask RedoAsync(string historyName = DefaultHistoryName, CancellationToken cancellationToken = default)
        {
            return GetOrCreateHistory(historyName).RedoAsync(cancellationToken);
        }

        /// <summary>
        /// 尝试异步执行重做操作。
        /// </summary>
        /// <param name="historyName">历史记录名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>重做任务，结果表示是否成功。</returns>
        public UniTask<bool> TryRedoAsync(string historyName = DefaultHistoryName, CancellationToken cancellationToken = default)
        {
            return GetOrCreateHistory(historyName).TryRedoAsync(cancellationToken);
        }

        /// <summary>
        /// 检查是否可以执行撤销操作。
        /// </summary>
        /// <param name="historyName">历史记录名称。</param>
        /// <returns>如果可以撤销则返回 true，否则返回 false。</returns>
        public bool CanUndo(string historyName = DefaultHistoryName)
        {
            return GetOrCreateHistory(historyName).CanUndo;
        }

        /// <summary>
        /// 检查是否可以执行重做操作。
        /// </summary>
        /// <param name="historyName">历史记录名称。</param>
        /// <returns>如果可以重做则返回 true，否则返回 false。</returns>
        public bool CanRedo(string historyName = DefaultHistoryName)
        {
            return GetOrCreateHistory(historyName).CanRedo;
        }

        /// <summary>
        /// 清除指定历史记录。
        /// </summary>
        /// <param name="historyName">历史记录名称。</param>
        public void Clear(string historyName = DefaultHistoryName)
        {
            GetOrCreateHistory(historyName).Clear();
        }

        /// <summary>
        /// 清除所有历史记录。
        /// </summary>
        public void ClearAll()
        {
            foreach (var history in _histories.Values)
            {
                history.Clear();
            }
        }

        /// <summary>
        /// 释放命令模块资源。
        /// </summary>
        public void Dispose()
        {
            RemoveDiagnosticsSnapshotProviders();
            ClearAll();
            foreach (var history in _histories.Values)
            {
                UnbindHistory(history);
            }

            _histories.Clear();
        }

        private void HandleCommandExecuted(CommandHistory history, object command)
        {
            _commandExecutedCount++;
            _lastHistoryName = history?.Name;
            _lastCommandType = command?.GetType().FullName;
            if (command is CommandTransaction transaction)
            {
                _transactionExecutedCount++;
                _lastTransactionName = transaction.Name;
            }
            EnsureDiagnosticsSnapshotProviders();

            if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                diagnostics.CaptureSnapshot("Command.LastHistory", _lastHistoryName ?? string.Empty);
                diagnostics.CaptureSnapshot("Command.LastCommandType", _lastCommandType ?? string.Empty);
                diagnostics.CaptureSnapshot("Command.LastTransaction", _lastTransactionName ?? string.Empty);
            }

            CommandExecuted?.Invoke(history, command);
        }

        private void HandleUndoPerformed(CommandHistory history, object command)
        {
            _undoPerformedCount++;
            _lastHistoryName = history?.Name;
            _lastCommandType = command?.GetType().FullName;
            if (command is CommandTransaction transaction)
            {
                _lastTransactionName = transaction.Name;
            }
            EnsureDiagnosticsSnapshotProviders();
            UndoPerformed?.Invoke(history, command);
        }

        private void HandleRedoPerformed(CommandHistory history, object command)
        {
            _redoPerformedCount++;
            _lastHistoryName = history?.Name;
            _lastCommandType = command?.GetType().FullName;
            if (command is CommandTransaction transaction)
            {
                _lastTransactionName = transaction.Name;
            }
            EnsureDiagnosticsSnapshotProviders();
            RedoPerformed?.Invoke(history, command);
        }

        private void HandleCommandFailed(CommandHistory history, object command, Exception exception)
        {
            _commandFailureCount++;
            _lastHistoryName = history?.Name;
            _lastCommandType = command?.GetType().FullName;
            if (command is CommandTransaction transaction)
            {
                _lastTransactionName = transaction.Name;
            }
            EnsureDiagnosticsSnapshotProviders();

            if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                diagnostics.CaptureSnapshot("Command.LastError", exception?.Message ?? string.Empty);
            }

            CommandFailed?.Invoke(history, command, exception);
        }

        private void UnbindHistory(CommandHistory history)
        {
            if (history == null)
            {
                return;
            }

            history.CommandExecuted -= HandleCommandExecuted;
            history.UndoPerformed -= HandleUndoPerformed;
            history.RedoPerformed -= HandleRedoPerformed;
            history.CommandFailed -= HandleCommandFailed;
        }

        private void EnsureDiagnosticsSnapshotProviders()
        {
            if (_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RegisterSnapshotProvider("Command.HistoryCount", () => _histories.Count.ToString());
            diagnostics.RegisterSnapshotProvider("Command.ExecutedCount", () => _commandExecutedCount.ToString());
            diagnostics.RegisterSnapshotProvider("Command.UndoCount", () => _undoPerformedCount.ToString());
            diagnostics.RegisterSnapshotProvider("Command.RedoCount", () => _redoPerformedCount.ToString());
            diagnostics.RegisterSnapshotProvider("Command.FailureCount", () => _commandFailureCount.ToString());
            diagnostics.RegisterSnapshotProvider("Command.TransactionCount", () => _transactionExecutedCount.ToString());
            diagnostics.RegisterSnapshotProvider("Command.LastHistory", () => _lastHistoryName ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Command.LastCommandType", () => _lastCommandType ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Command.LastTransaction", () => _lastTransactionName ?? string.Empty);
            _diagnosticsRegistered = true;
        }

        private void RemoveDiagnosticsSnapshotProviders()
        {
            if (!_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RemoveSnapshotProvider("Command.HistoryCount");
            diagnostics.RemoveSnapshotProvider("Command.ExecutedCount");
            diagnostics.RemoveSnapshotProvider("Command.UndoCount");
            diagnostics.RemoveSnapshotProvider("Command.RedoCount");
            diagnostics.RemoveSnapshotProvider("Command.FailureCount");
            diagnostics.RemoveSnapshotProvider("Command.TransactionCount");
            diagnostics.RemoveSnapshotProvider("Command.LastHistory");
            diagnostics.RemoveSnapshotProvider("Command.LastCommandType");
            diagnostics.RemoveSnapshotProvider("Command.LastTransaction");
            _diagnosticsRegistered = false;
        }
    }
}
