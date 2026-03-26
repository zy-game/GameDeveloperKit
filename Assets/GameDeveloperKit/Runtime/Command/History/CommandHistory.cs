using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 命令历史类，管理命令的执行、撤销和重做历史。
    /// </summary>
    public sealed partial class CommandHistory
    {
        private readonly Stack<HistoryEntry> _undoEntries = new();
        private readonly Stack<HistoryEntry> _redoEntries = new();
        private bool _isRunning;

        /// <summary>
        /// 初始化命令历史的新实例。
        /// </summary>
        /// <param name="name">历史名称。</param>
        /// <param name="maxUndoEntries">最大撤销条目数。</param>
        /// <exception cref="ArgumentException">当名称为空时抛出。</exception>
        public CommandHistory(string name, int maxUndoEntries = 128)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("History name can not be empty.", nameof(name));
            }

            Name = name;
            MaxUndoEntries = Math.Max(0, maxUndoEntries);
        }

        /// <summary>
        /// 获取历史名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 获取或设置最大撤销条目数。
        /// </summary>
        public int MaxUndoEntries { get; set; }

        /// <summary>
        /// 获取是否正在运行命令。
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 获取是否可以撤销。
        /// </summary>
        public bool CanUndo => _undoEntries.Count > 0;

        /// <summary>
        /// 获取是否可以重做。
        /// </summary>
        public bool CanRedo => _redoEntries.Count > 0;

        /// <summary>
        /// 获取撤销条目数。
        /// </summary>
        public int UndoCount => _undoEntries.Count;

        /// <summary>
        /// 获取重做条目数。
        /// </summary>
        public int RedoCount => _redoEntries.Count;

        /// <summary>
        /// 命令执行事件。
        /// </summary>
        public event Action<CommandHistory, object> CommandExecuted;

        /// <summary>
        /// 撤销执行事件。
        /// </summary>
        public event Action<CommandHistory, object> UndoPerformed;

        /// <summary>
        /// 重做执行事件。
        /// </summary>
        public event Action<CommandHistory, object> RedoPerformed;

        /// <summary>
        /// 命令失败事件。
        /// </summary>
        public event Action<CommandHistory, object, Exception> CommandFailed;

        /// <summary>
        /// 执行命令。
        /// </summary>
        /// <param name="command">要执行的命令。</param>
        /// <exception cref="ArgumentNullException">当命令为null时抛出。</exception>
        public void Execute(ICommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            EnterRunningScope();
            var failedCommand = (object)command;
            try
            {
                command.Execute();

                if (command is IUndoableCommand undoableCommand)
                {
                    _undoEntries.Push(HistoryEntry.Create(undoableCommand));
                    TrimUndoEntriesIfNeeded();
                    _redoEntries.Clear();
                }

                CommandExecuted?.Invoke(this, command);
            }
            catch (Exception exception)
            {
                CommandFailed?.Invoke(this, failedCommand, exception);
                throw;
            }
            finally
            {
                ExitRunningScope();
            }
        }

        /// <summary>
        /// 异步执行命令。
        /// </summary>
        /// <param name="command">要执行的命令。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        /// <exception cref="ArgumentNullException">当命令为null时抛出。</exception>
        public async UniTask ExecuteAsync(IAsyncCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            EnterRunningScope();
            var failedCommand = (object)command;
            try
            {
                await command.ExecuteAsync(cancellationToken);

                if (command is IAsyncUndoableCommand undoableCommand)
                {
                    _undoEntries.Push(HistoryEntry.Create(undoableCommand));
                    TrimUndoEntriesIfNeeded();
                    _redoEntries.Clear();
                }

                CommandExecuted?.Invoke(this, command);
            }
            catch (Exception exception)
            {
                CommandFailed?.Invoke(this, failedCommand, exception);
                throw;
            }
            finally
            {
                ExitRunningScope();
            }
        }

        /// <summary>
        /// 撤销上一个命令。
        /// </summary>
        /// <exception cref="InvalidOperationException">当没有命令可撤销或只能异步撤销时抛出。</exception>
        public void Undo()
        {
            EnterRunningScope();
            object failedCommand = null;
            try
            {
                if (_undoEntries.Count == 0)
                {
                    throw new InvalidOperationException($"History '{Name}' has no commands to undo.");
                }

                var entry = _undoEntries.Peek();
                failedCommand = entry.Command;
                if (entry.IsAsync)
                {
                    throw new InvalidOperationException($"History '{Name}' can only undo the next command asynchronously.");
                }

                _undoEntries.Pop();
                entry.Undo();
                _redoEntries.Push(entry);
                UndoPerformed?.Invoke(this, entry.Command);
            }
            catch (Exception exception)
            {
                CommandFailed?.Invoke(this, failedCommand, exception);
                throw;
            }
            finally
            {
                ExitRunningScope();
            }
        }

        /// <summary>
        /// 尝试撤销上一个命令。
        /// </summary>
        /// <returns>如果成功撤销则返回true，否则返回false。</returns>
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

        /// <summary>
        /// 异步撤销上一个命令。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        /// <exception cref="InvalidOperationException">当没有命令可撤销时抛出。</exception>
        public async UniTask UndoAsync(CancellationToken cancellationToken = default)
        {
            EnterRunningScope();
            object failedCommand = null;
            try
            {
                if (_undoEntries.Count == 0)
                {
                    throw new InvalidOperationException($"History '{Name}' has no commands to undo.");
                }

                var entry = _undoEntries.Pop();
                failedCommand = entry.Command;
                await entry.UndoAsync(cancellationToken);
                _redoEntries.Push(entry);
                UndoPerformed?.Invoke(this, entry.Command);
            }
            catch (Exception exception)
            {
                CommandFailed?.Invoke(this, failedCommand, exception);
                throw;
            }
            finally
            {
                ExitRunningScope();
            }
        }

        /// <summary>
        /// 异步尝试撤销上一个命令。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>如果成功撤销则返回true，否则返回false。</returns>
        public async UniTask<bool> TryUndoAsync(CancellationToken cancellationToken = default)
        {
            if (!CanUndo)
            {
                return false;
            }

            await UndoAsync(cancellationToken);
            return true;
        }

        /// <summary>
        /// 重做上一个撤销的命令。
        /// </summary>
        /// <exception cref="InvalidOperationException">当没有命令可重做或只能异步重做时抛出。</exception>
        public void Redo()
        {
            EnterRunningScope();
            object failedCommand = null;
            try
            {
                if (_redoEntries.Count == 0)
                {
                    throw new InvalidOperationException($"History '{Name}' has no commands to redo.");
                }

                var entry = _redoEntries.Peek();
                failedCommand = entry.Command;
                if (entry.IsAsync)
                {
                    throw new InvalidOperationException($"History '{Name}' can only redo the next command asynchronously.");
                }

                _redoEntries.Pop();
                entry.Execute();
                _undoEntries.Push(entry);
                TrimUndoEntriesIfNeeded();
                RedoPerformed?.Invoke(this, entry.Command);
            }
            catch (Exception exception)
            {
                CommandFailed?.Invoke(this, failedCommand, exception);
                throw;
            }
            finally
            {
                ExitRunningScope();
            }
        }

        /// <summary>
        /// 尝试重做上一个撤销的命令。
        /// </summary>
        /// <returns>如果成功重做则返回true，否则返回false。</returns>
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

        /// <summary>
        /// 异步重做上一个撤销的命令。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        /// <exception cref="InvalidOperationException">当没有命令可重做时抛出。</exception>
        public async UniTask RedoAsync(CancellationToken cancellationToken = default)
        {
            EnterRunningScope();
            object failedCommand = null;
            try
            {
                if (_redoEntries.Count == 0)
                {
                    throw new InvalidOperationException($"History '{Name}' has no commands to redo.");
                }

                var entry = _redoEntries.Pop();
                failedCommand = entry.Command;
                await entry.ExecuteAsync(cancellationToken);
                _undoEntries.Push(entry);
                TrimUndoEntriesIfNeeded();
                RedoPerformed?.Invoke(this, entry.Command);
            }
            catch (Exception exception)
            {
                CommandFailed?.Invoke(this, failedCommand, exception);
                throw;
            }
            finally
            {
                ExitRunningScope();
            }
        }

        /// <summary>
        /// 异步尝试重做上一个撤销的命令。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>如果成功重做则返回true，否则返回false。</returns>
        public async UniTask<bool> TryRedoAsync(CancellationToken cancellationToken = default)
        {
            if (!CanRedo)
            {
                return false;
            }

            await RedoAsync(cancellationToken);
            return true;
        }

        /// <summary>
        /// 清空所有历史记录。
        /// </summary>
        public void Clear()
        {
            _undoEntries.Clear();
            _redoEntries.Clear();
        }

        /// <summary>
        /// 如果需要，裁剪撤销条目以保持最大条目数。
        /// </summary>
        private void TrimUndoEntriesIfNeeded()
        {
            if (MaxUndoEntries < 0)
            {
                MaxUndoEntries = 0;
            }

            if (_undoEntries.Count <= MaxUndoEntries)
            {
                return;
            }

            var retainedCount = Math.Max(0, MaxUndoEntries);
            var entries = _undoEntries.ToArray();
            _undoEntries.Clear();

            for (var i = retainedCount - 1; i >= 0; i--)
            {
                _undoEntries.Push(entries[i]);
            }
        }

        /// <summary>
        /// 进入运行范围。
        /// </summary>
        /// <exception cref="InvalidOperationException">当历史已在运行时抛出。</exception>
        private void EnterRunningScope()
        {
            if (_isRunning)
            {
                throw new InvalidOperationException($"History '{Name}' is already running a command.");
            }

            _isRunning = true;
        }

        /// <summary>
        /// 退出运行范围。
        /// </summary>
        private void ExitRunningScope()
        {
            _isRunning = false;
        }
    }
}
