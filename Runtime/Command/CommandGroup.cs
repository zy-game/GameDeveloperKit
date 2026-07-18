using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Command
{
    /// <summary>
    /// 把多个命令组合成一个历史条目。
    /// </summary>
    public sealed class CommandGroup : CommandBase
    {
        /// <summary>
        /// 命令组包含的全部子命令。
        /// </summary>
        private readonly List<ICommand> m_Commands = new List<ICommand>();

        /// <summary>
        /// 本次执行过程中已经成功执行的子命令，用于失败回滚。
        /// </summary>
        private readonly List<ICommand> m_ExecutedCommands = new List<ICommand>();

        /// <summary>
        /// 命令组展示名称。
        /// </summary>
        private readonly string m_Name;

        /// <summary>
        /// 初始化命令组。
        /// </summary>
        /// <param name="name">命令组名称。</param>
        /// <param name="commands">子命令列表。</param>
        /// <exception cref="ArgumentException">命令组名称为空或命令列表为空时抛出。</exception>
        /// <exception cref="ArgumentNullException">子命令为空时抛出。</exception>
        public CommandGroup(string name, params ICommand[] commands)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Command group name cannot be empty.", nameof(name));
            }

            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            if (commands.Length == 0)
            {
                throw new ArgumentException("Command group must contain at least one command.", nameof(commands));
            }

            m_Name = name;
            foreach (var command in commands)
            {
                if (command == null)
                {
                    throw new ArgumentNullException(nameof(commands));
                }

                m_Commands.Add(command);
            }
        }

        /// <summary>
        /// 命令组名称。
        /// </summary>
        public override string Name => m_Name;

        /// <summary>
        /// 命令组成功执行后进入撤销栈。
        /// </summary>
        public override CommandHistoryMode HistoryMode => CommandHistoryMode.Undoable;

        /// <summary>
        /// 按顺序执行子命令。
        /// </summary>
        /// <returns>执行任务。</returns>
        public override async UniTask ExecuteAsync()
        {
            m_ExecutedCommands.Clear();
            try
            {
                foreach (var command in m_Commands)
                {
                    await command.ExecuteAsync();
                    m_ExecutedCommands.Add(command);
                }
            }
            catch (Exception)
            {
                Exception rollbackException = null;
                for (var i = m_ExecutedCommands.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        await m_ExecutedCommands[i].UndoAsync();
                    }
                    catch (Exception undoException)
                    {
                        rollbackException = undoException;
                        break;
                    }
                }

                m_ExecutedCommands.Clear();
                if (rollbackException != null)
                {
                    throw new GameException($"Command group '{Name}' failed and rollback also failed.", rollbackException);
                }

                throw;
            }
        }

        /// <summary>
        /// 按反向顺序撤销子命令。
        /// </summary>
        /// <returns>撤销任务。</returns>
        public override async UniTask UndoAsync()
        {
            for (var i = m_Commands.Count - 1; i >= 0; i--)
            {
                await m_Commands[i].UndoAsync();
            }
        }

        /// <summary>
        /// 按顺序重做子命令。
        /// </summary>
        /// <returns>重做任务。</returns>
        public override async UniTask RedoAsync()
        {
            foreach (var command in m_Commands)
            {
                await command.RedoAsync();
            }
        }

        /// <summary>
        /// 释放命令组及其子命令。
        /// </summary>
        public override void Release()
        {
            foreach (var command in m_Commands)
            {
                command.Release();
            }

            m_Commands.Clear();
            m_ExecutedCommands.Clear();
        }
    }
}
