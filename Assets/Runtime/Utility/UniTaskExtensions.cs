using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit
{
    /// <summary>
    /// UniTask扩展方法，用于异常处理
    /// </summary>
    public static class UniTaskExtensions
    {
        /// <summary>
        /// 将UniTask包装为带异常处理的UniTaskVoid
        /// 用于需要显式处理异常的场景（如UI动画、音频播放等）
        /// </summary>
        /// <param name="task">要执行的任务</param>
        /// <param name="context">上下文信息，用于日志记录</param>
        public static async UniTaskVoid WithExceptionLogging(this UniTask task, string context = null)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                var message = string.IsNullOrEmpty(context)
                    ? "Async operation failed"
                    : $"[{context}] Async operation failed";
                Game.Debug.Error(message, ex);
            }
        }

        /// <summary>
        /// 安全的Forget，捕获并记录异常
        /// 用于后台任务，不需要等待结果，但需要记录异常
        /// </summary>
        /// <param name="task">要执行的任务</param>
        /// <param name="context">上下文信息，用于日志记录</param>
        public static void SafeForget(this UniTask task, string context = null)
        {
            task.Forget(ex =>
            {
                if (ex != null)
                {
                    var message = string.IsNullOrEmpty(context)
                        ? "Async operation failed"
                        : $"[{context}] Async operation failed";
                    Game.Debug.Error(message, ex);
                }
            });
        }

        /// <summary>
        /// 带重试逻辑的异步操作
        /// </summary>
        /// <param name="taskFactory">任务工厂（每次重试创建新Task）</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="delayMs">重试延迟（毫秒），每次重试翻倍</param>
        /// <param name="context">上下文信息</param>
        public static async UniTask<T> WithRetry<T>(
            this Func<UniTask<T>> taskFactory, 
            int maxRetries = 3, 
            int delayMs = 1000,
            string context = null)
        {
            Exception lastException = null;
            
            for (int i = 0; i <= maxRetries; i++)
            {
                try
                {
                    return await taskFactory();
                }
                catch (OperationCanceledException)
                {
                    // 取消操作不重试，直接抛出
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (i < maxRetries)
                    {
                        var delay = delayMs * (1 << i); // 指数退避
                        var msg = string.IsNullOrEmpty(context)
                            ? $"Retry {i + 1}/{maxRetries} after {delay}ms"
                            : $"[{context}] Retry {i + 1}/{maxRetries} after {delay}ms";
                        Game.Debug.Warning(msg);
                        
                        await UniTask.Delay(delay);
                    }
                }
            }
            
            var errorMsg = string.IsNullOrEmpty(context)
                ? $"Operation failed after {maxRetries + 1} attempts"
                : $"[{context}] Operation failed after {maxRetries + 1} attempts";
            throw new Exception(errorMsg, lastException);
        }

        /// <summary>
        /// 带重试逻辑的异步操作（无返回值版本）
        /// </summary>
        public static async UniTask WithRetry(
            this Func<UniTask> taskFactory, 
            int maxRetries = 3, 
            int delayMs = 1000,
            string context = null)
        {
            Exception lastException = null;
            
            for (int i = 0; i <= maxRetries; i++)
            {
                try
                {
                    await taskFactory();
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (i < maxRetries)
                    {
                        var delay = delayMs * (1 << i);
                        var msg = string.IsNullOrEmpty(context)
                            ? $"Retry {i + 1}/{maxRetries} after {delay}ms"
                            : $"[{context}] Retry {i + 1}/{maxRetries} after {delay}ms";
                        Game.Debug.Warning(msg);
                        
                        await UniTask.Delay(delay);
                    }
                }
            }
            
            var errorMsg = string.IsNullOrEmpty(context)
                ? $"Operation failed after {maxRetries + 1} attempts"
                : $"[{context}] Operation failed after {maxRetries + 1} attempts";
            throw new Exception(errorMsg, lastException);
        }
    }
}
