using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 框架统一异常类型，包含结构化错误信息。
    /// </summary>
    [Serializable]
    public sealed class GameFrameworkException : Exception
    {
        public string Code { get; }

        public string Category { get; }

        public bool IsRetryable { get; }

        public string ExceptionType { get; }

        public string Context { get; }

        public string Stage { get; }

        public GameFrameworkException(
            string code,
            string message,
            string category,
            bool isRetryable = false,
            string context = null,
            Exception innerException = null,
            string stage = "Failed")
            : base(message ?? string.Empty, innerException)
        {
            Code = string.IsNullOrWhiteSpace(code) ? "Unknown" : code;
            Category = category ?? string.Empty;
            IsRetryable = isRetryable;
            Context = context;
            Stage = string.IsNullOrWhiteSpace(stage) ? "Failed" : stage;
            ExceptionType = innerException?.GetType().FullName;
        }

        public static GameFrameworkException Create(
            string code,
            string message,
            string category,
            bool isRetryable = false,
            string context = null,
            Exception exception = null,
            string stage = "Failed")
        {
            return new GameFrameworkException(code, message, category, isRetryable, context, exception, stage);
        }

        public static GameFrameworkException FromException(
            string code,
            Exception exception,
            string category,
            bool isRetryable = false,
            string context = null,
            string stage = "Failed")
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            return new GameFrameworkException(code, exception.Message, category, isRetryable, context, exception, stage);
        }
    }
}
