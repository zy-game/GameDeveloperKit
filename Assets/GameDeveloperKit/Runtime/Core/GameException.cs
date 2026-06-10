using System;

namespace GameDeveloperKit
{
    /// <summary>
    /// 游戏异常类，用于表示游戏运行过程中发生的异常情况。它继承自系统的Exception类，提供了构造函数来创建异常实例，并且可以包含一个错误消息和一个内部异常，以便更详细地描述异常的原因和上下文信息。这种自定义异常类有助于在游戏开发过程中更好地捕获和处理特定的错误情况，提高代码的可读性和维护性。
    /// </summary>
    public class GameException : Exception
    {
        /// <summary>
        /// 使用指定的错误消息初始化GameException实例。
        /// </summary>
        /// <param name="message">message 参数。</param>
        public GameException(string message) : base(message)
        {
        }

        /// <summary>
        /// 使用指定的错误消息和内部异常初始化GameException实例。这个构造函数允许开发者在捕获异常时提供更详细的错误信息，并且可以将原始异常作为内部异常传递，以便在调试和日志记录过程中更好地理解异常的根本原因。这种设计有助于提高代码的健壮性和可维护性，使得在游戏开发过程中能够更有效地处理和诊断异常情况。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <param name="innerException">inner Exception 参数。</param>
        public GameException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}