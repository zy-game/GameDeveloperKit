using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Command
{
    /// <summary>
    /// 命令模块，负责命令执行历史、撤销和重做。
    /// </summary>
    public sealed partial class CommandModule : GameModuleBase
    {
        private const int DefaultHistoryCapacity = 128;

        private readonly List<ICommand> m_UndoStack = new List<ICommand>();
        private readonly List<ICommand> m_RedoStack = new List<ICommand>();

        /// <summary>
        /// 命令历史状态变化事件。
        /// </summary>
        public event Action<CommandHistorySnapshot> HistoryChanged;

        /// <summary>
        /// 历史容量。小于等于0表示不限制容量。
        /// </summary>
        public int HistoryCapacity { get; set; } = DefaultHistoryCapacity;

        /// <summary>
        /// 当前是否正在执行、撤销或重做命令。
        /// </summary>
        public bool IsExecuting { get; private set; }

        /// <summary>
        /// 是否可以撤销。
        /// </summary>
        public bool CanUndo => m_UndoStack.Count > 0;

        /// <summary>
        /// 是否可以重做。
        /// </summary>
        public bool CanRedo => m_RedoStack.Count > 0;

        /// <summary>
        /// 撤销栈命令数量。
        /// </summary>
        public int UndoCount => m_UndoStack.Count;

        /// <summary>
        /// 重做栈命令数量。
        /// </summary>
        public int RedoCount => m_RedoStack.Count;

        /// <summary>
        /// 启动命令模块。
        /// </summary>
        /// <returns>模块启动任务。</returns>
        public override UniTask Startup()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 关闭命令模块并清理历史。
        /// </summary>
        /// <returns>模块关闭任务。</returns>
        public override UniTask Shutdown()
        {
            Clear();
            ClearCommandRegistry();
            HistoryChanged = null;
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 执行命令。
        /// </summary>
        /// <param name="command">待执行命令。</param>
        /// <returns>执行任务。</returns>
        /// <exception cref="ArgumentNullException">命令为空时抛出。</exception>
        /// <exception cref="GameException">模块正在执行其他命令时抛出。</exception>
        public async UniTask ExecuteAsync(ICommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            ValidateHistoryMode(command.HistoryMode);
            EnsureNotExecuting();
            IsExecuting = true;
            var historyChanged = false;
            try
            {
                await command.ExecuteAsync();
                switch (command.HistoryMode)
                {
                    case CommandHistoryMode.Undoable:
                        ReleaseCommands(m_RedoStack);
                        m_RedoStack.Clear();
                        m_UndoStack.Add(command);
                        TrimUndoStack();
                        historyChanged = true;
                        break;
                    case CommandHistoryMode.Transient:
                        break;
                    case CommandHistoryMode.Barrier:
                        ClearInternal();
                        command.Release();
                        historyChanged = true;
                        break;
                }
            }
            finally
            {
                IsExecuting = false;
                if (historyChanged)
                {
                    RaiseHistoryChanged();
                }
            }
        }

        /// <summary>
        /// 撤销最近执行的命令。
        /// </summary>
        /// <returns>撤销任务。</returns>
        /// <exception cref="GameException">模块正在执行其他命令时抛出。</exception>
        public async UniTask UndoAsync()
        {
            EnsureNotExecuting();
            if (m_UndoStack.Count == 0)
            {
                return;
            }

            IsExecuting = true;
            var command = m_UndoStack[m_UndoStack.Count - 1];
            var historyChanged = false;
            try
            {
                await command.UndoAsync();
                m_UndoStack.RemoveAt(m_UndoStack.Count - 1);
                m_RedoStack.Add(command);
                historyChanged = true;
            }
            finally
            {
                IsExecuting = false;
                if (historyChanged)
                {
                    RaiseHistoryChanged();
                }
            }
        }

        /// <summary>
        /// 重做最近撤销的命令。
        /// </summary>
        /// <returns>重做任务。</returns>
        /// <exception cref="GameException">模块正在执行其他命令时抛出。</exception>
        public async UniTask RedoAsync()
        {
            EnsureNotExecuting();
            if (m_RedoStack.Count == 0)
            {
                return;
            }

            IsExecuting = true;
            var command = m_RedoStack[m_RedoStack.Count - 1];
            var historyChanged = false;
            try
            {
                await command.RedoAsync();
                m_RedoStack.RemoveAt(m_RedoStack.Count - 1);
                m_UndoStack.Add(command);
                TrimUndoStack();
                historyChanged = true;
            }
            finally
            {
                IsExecuting = false;
                if (historyChanged)
                {
                    RaiseHistoryChanged();
                }
            }
        }

        /// <summary>
        /// 清空撤销和重做历史。
        /// </summary>
        public void Clear()
        {
            var historyChanged = m_UndoStack.Count > 0 || m_RedoStack.Count > 0;
            ClearInternal();
            if (historyChanged)
            {
                RaiseHistoryChanged();
            }
        }

        /// <summary>
        /// 获取当前历史状态快照。
        /// </summary>
        /// <returns>历史状态快照。</returns>
        public CommandHistorySnapshot GetSnapshot()
        {
            return new CommandHistorySnapshot(
                CanUndo,
                CanRedo,
                UndoCount,
                RedoCount,
                CanUndo ? m_UndoStack[m_UndoStack.Count - 1].Name : null,
                CanRedo ? m_RedoStack[m_RedoStack.Count - 1].Name : null);
        }

        private void EnsureNotExecuting()
        {
            if (IsExecuting)
            {
                throw new GameException("CommandModule is already executing a command.");
            }
        }

        private static void ValidateHistoryMode(CommandHistoryMode mode)
        {
            if (mode is not CommandHistoryMode.Undoable and not CommandHistoryMode.Transient and not CommandHistoryMode.Barrier)
            {
                throw new GameException($"Unsupported command history mode '{mode}'.");
            }
        }

        private void TrimUndoStack()
        {
            if (HistoryCapacity <= 0)
            {
                return;
            }

            while (m_UndoStack.Count > HistoryCapacity)
            {
                var command = m_UndoStack[0];
                m_UndoStack.RemoveAt(0);
                command.Release();
            }
        }

        private void ClearInternal()
        {
            ReleaseCommands(m_UndoStack);
            ReleaseCommands(m_RedoStack);
            m_UndoStack.Clear();
            m_RedoStack.Clear();
        }

        private static void ReleaseCommands(List<ICommand> commands)
        {
            foreach (var command in commands)
            {
                command.Release();
            }
        }

        private void RaiseHistoryChanged()
        {
            HistoryChanged?.Invoke(GetSnapshot());
        }
    }
}
