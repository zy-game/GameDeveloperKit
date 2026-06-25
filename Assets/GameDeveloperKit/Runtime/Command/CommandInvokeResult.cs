using System;

namespace GameDeveloperKit.Command
{
    /// <summary>
    /// 通过命令名执行命令的结果。
    /// </summary>
    public readonly struct CommandInvokeResult
    {
        /// <summary>
        /// 初始化 Command Invoke Result。
        /// </summary>
        /// <param name="succeeded">命令是否成功执行。</param>
        /// <param name="disabled">执行入口是否被禁用。</param>
        /// <param name="commandName">命令名。</param>
        /// <param name="message">执行结果信息。</param>
        /// <param name="exception">执行失败时的异常。</param>
        /// <param name="command">成功执行的命令实例。</param>
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

        /// <summary>
        /// 创建命令执行成功结果。
        /// </summary>
        /// <param name="commandName">命令名。</param>
        /// <param name="command">成功执行的命令实例。</param>
        /// <returns>命令执行成功结果。</returns>
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

        /// <summary>
        /// 创建命令执行失败结果。
        /// </summary>
        /// <param name="commandName">命令名。</param>
        /// <param name="message">失败信息。</param>
        /// <param name="exception">执行失败时的异常。</param>
        /// <returns>命令执行失败结果。</returns>
        public static CommandInvokeResult Failed(string commandName, string message, Exception exception = null)
        {
            return new CommandInvokeResult(false, false, commandName, message, exception, null);
        }

        /// <summary>
        /// 执行 Disabled Result。
        /// </summary>
        /// <param name="message">禁用原因。</param>
        public static CommandInvokeResult DisabledResult(string message)
        {
            return new CommandInvokeResult(false, true, null, message, null, null);
        }
    }
}
