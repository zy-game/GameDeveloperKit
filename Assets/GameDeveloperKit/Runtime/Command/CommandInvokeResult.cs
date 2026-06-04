using System;

namespace GameDeveloperKit.Command
{
    /// <summary>
    /// 通过命令名执行命令的结果。
    /// </summary>
    public readonly struct CommandInvokeResult
    {
        private CommandInvokeResult(
            bool succeeded,
            bool disabled,
            string commandName,
            string message,
            Exception exception,
            ICommand command)
        {
            Succeeded = succeeded;
            Disabled = disabled;
            CommandName = commandName;
            Message = message;
            Exception = exception;
            Command = command;
        }

        /// <summary>
        /// 命令是否成功执行。
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// 执行是否因调试入口关闭而被禁用。
        /// </summary>
        public bool Disabled { get; }

        /// <summary>
        /// 请求执行的命令名。
        /// </summary>
        public string CommandName { get; }

        /// <summary>
        /// 可展示的执行结果信息。
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 执行失败时的异常。
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 成功执行的命令实例。
        /// </summary>
        public ICommand Command { get; }

        public static CommandInvokeResult Success(string commandName, ICommand command)
        {
            return new CommandInvokeResult(
                true,
                false,
                commandName,
                $"Command '{commandName}' executed.",
                null,
                command);
        }

        public static CommandInvokeResult Failed(string commandName, string message, Exception exception = null)
        {
            return new CommandInvokeResult(false, false, commandName, message, exception, null);
        }

        public static CommandInvokeResult DisabledResult(string message)
        {
            return new CommandInvokeResult(false, true, null, message, null, null);
        }
    }
}
