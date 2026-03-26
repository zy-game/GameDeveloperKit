using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 提供 UniTask 的诊断扩展方法。
    /// </summary>
    public static class UniTaskDiagnosticsExtensions
    {
        /// <summary>
        /// 以带诊断上报的方式忽略 UniTask 的等待结果。
        /// </summary>
        /// <param name="task">要忽略等待的任务。</param>
        /// <param name="code">异常诊断代码。</param>
        /// <param name="context">异常上下文。</param>
        /// <param name="scope">异常作用域。</param>
        /// <param name="ignoreCancellation">是否忽略取消异常。</param>
        public static void ForgetWithDiagnostics(this UniTask task, string code, string context = null, string scope = null, bool ignoreCancellation = true)
        {
            task.Forget(exception => HandleException(exception, code, context, scope, ignoreCancellation));
        }

        /// <summary>
        /// 以带诊断上报的方式忽略泛型 UniTask 的等待结果。
        /// </summary>
        /// <typeparam name="T">任务结果类型。</typeparam>
        /// <param name="task">要忽略等待的任务。</param>
        /// <param name="code">异常诊断代码。</param>
        /// <param name="context">异常上下文。</param>
        /// <param name="scope">异常作用域。</param>
        /// <param name="ignoreCancellation">是否忽略取消异常。</param>
        public static void ForgetWithDiagnostics<T>(this UniTask<T> task, string code, string context = null, string scope = null, bool ignoreCancellation = true)
        {
            task.Forget(exception => HandleException(exception, code, context, scope, ignoreCancellation));
        }

        private static void HandleException(Exception exception, string code, string context, string scope, bool ignoreCancellation)
        {
            if (exception == null)
            {
                return;
            }

            if (ignoreCancellation && exception is OperationCanceledException)
            {
                return;
            }

            if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                diagnostics.ReportException(string.IsNullOrWhiteSpace(code) ? "UniTaskUnhandledException" : code, exception, context, scope);
                return;
            }

            Debug.LogException(exception);
        }
    }
}
