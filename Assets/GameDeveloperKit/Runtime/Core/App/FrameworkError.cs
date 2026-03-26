using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 表示框架操作中的错误信息。
    /// </summary>
    [Serializable]
    public sealed class FrameworkError
    {
        /// <summary>
        /// 获取或设置错误代码。
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 获取或设置错误消息。
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 获取或设置失败分类。
        /// </summary>
        public FrameworkFailureCategory Category { get; set; }

        /// <summary>
        /// 获取或设置错误是否可重试。
        /// </summary>
        public bool IsRetryable { get; set; }

        /// <summary>
        /// 获取或设置异常类型名称。
        /// </summary>
        public string ExceptionType { get; set; }

        /// <summary>
        /// 获取或设置错误上下文。
        /// </summary>
        public string Context { get; set; }

        /// <summary>
        /// 获取或设置错误发生时的操作阶段。
        /// </summary>
        public FrameworkOperationStage Stage { get; set; }

        /// <summary>
        /// 创建框架错误对象。
        /// </summary>
        /// <param name="code">错误代码。</param>
        /// <param name="message">错误消息。</param>
        /// <param name="category">失败分类。</param>
        /// <param name="isRetryable">是否允许重试。</param>
        /// <param name="context">错误上下文。</param>
        /// <param name="exception">关联的异常对象。</param>
        /// <param name="stage">错误发生时的操作阶段。</param>
        /// <returns>创建完成的框架错误对象。</returns>
        public static FrameworkError Create(string code, string message, FrameworkFailureCategory category, bool isRetryable = false, string context = null, Exception exception = null, FrameworkOperationStage stage = FrameworkOperationStage.Failed)
        {
            return new FrameworkError
            {
                Code = string.IsNullOrWhiteSpace(code) ? "Unknown" : code,
                Message = message ?? string.Empty,
                Category = category,
                IsRetryable = isRetryable,
                Context = context,
                ExceptionType = exception?.GetType().FullName,
                Stage = stage
            };
        }

        /// <summary>
        /// 根据异常创建框架错误对象。
        /// </summary>
        /// <param name="code">错误代码。</param>
        /// <param name="exception">异常对象。</param>
        /// <param name="category">失败分类。</param>
        /// <param name="isRetryable">是否允许重试。</param>
        /// <param name="context">错误上下文。</param>
        /// <param name="stage">错误发生时的操作阶段。</param>
        /// <returns>创建完成的框架错误对象。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="exception"/> 为 null 时抛出。</exception>
        public static FrameworkError FromException(string code, Exception exception, FrameworkFailureCategory category, bool isRetryable = false, string context = null, FrameworkOperationStage stage = FrameworkOperationStage.Failed)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            return Create(code, exception.Message, category, isRetryable, context, exception, stage);
        }
    }
}
