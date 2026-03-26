using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 表示游戏框架运行时发生的异常。
    /// </summary>
    /// <remarks>
    /// 此异常类型封装了 FrameworkError 对象，提供结构化的错误信息。
    /// 所有由框架模块抛出的运行时异常都应该使用此类型或其子类。
    /// </remarks>
    public sealed class FrameworkException : Exception
    {
        /// <summary>
        /// 初始化 FrameworkException 的新实例。
        /// </summary>
        /// <param name="error">框架错误信息对象，包含错误的详细信息。</param>
        /// <exception cref="ArgumentNullException">当 error 参数为 null 时抛出。</exception>
        public FrameworkException(FrameworkError error)
            : base(error?.Message)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        /// <summary>
        /// 获取框架错误信息对象。
        /// </summary>
        /// <remarks>
        /// 此对象包含错误的代码、消息、类别、上下文和阶段等详细信息。
        /// 可用于错误处理、日志记录和错误恢复。
        /// </remarks>
        public FrameworkError Error { get; }
    }
}
