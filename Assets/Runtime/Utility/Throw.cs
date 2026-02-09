using System;

namespace GameDeveloperKit
{
    /// <summary>
    /// 异常抛出工具类，提供统一的异常处理机制
    /// </summary>
    public static class Throw
    {
        /// <summary>
        /// GameFramework专用异常类型
        /// </summary>
        public sealed class GameFrameworkException : Exception
        {
            public GameFrameworkException(string message) : base(message) { }
            public GameFrameworkException(string message, Exception innerException) : base(message, innerException) { }
        }
        
        /// <summary>
        /// 条件断言，条件为false时抛出异常
        /// </summary>
        /// <param name="condition">断言条件</param>
        /// <param name="message">异常消息</param>
        public static void Asserts(bool condition, string message = null)
        {
            if (!condition)
            {
                var ex = new GameFrameworkException(message ?? "Assertion failed");
                Game.Debug.Error(message ?? "Assertion failed", ex);
                throw ex;
            }
        }

        /// <summary>
        /// 条件断言，条件为false时抛出指定的异常
        /// </summary>
        /// <param name="condition">断言条件</param>
        /// <param name="exception">要抛出的异常对象</param>
        public static void Asserts(bool condition, Exception exception)
        {
            if (!condition)
                throw exception;
        }

        /// <summary>
        /// 抛出参数为空异常
        /// </summary>
        public static void IfNull<T>(T obj, string paramName) where T : class
        {
            if (obj == null)
            {
                var ex = new ArgumentNullException(paramName);
                Game.Debug.Error($"Parameter '{paramName}' is null", ex);
                throw ex;
            }
        }

        /// <summary>
        /// 抛出参数异常
        /// </summary>
        public static void IfArgumentInvalid(bool condition, string paramName, string message)
        {
            if (condition)
            {
                var ex = new ArgumentException(message, paramName);
                Game.Debug.Error($"Invalid argument '{paramName}': {message}", ex);
                throw ex;
            }
        }

        /// <summary>
        /// 抛出无效操作异常
        /// </summary>
        public static void InvalidOperation(string message)
        {
            var ex = new InvalidOperationException(message);
            Game.Debug.Error(message, ex);
            throw ex;
        }

        /// <summary>
        /// 直接抛出GameFramework异常
        /// </summary>
        /// <param name="message">异常消息</param>
        public static void Exception(string message)
        {
            var ex = new GameFrameworkException(message);
            Game.Debug.Error(message, ex);
            throw ex;
        }

        /// <summary>
        /// 抛出包装异常
        /// </summary>
        public static void Exception(string message, Exception innerException)
        {
            var ex = new GameFrameworkException(message, innerException);
            Game.Debug.Error(message, ex);
            throw ex;
        }
    }
}