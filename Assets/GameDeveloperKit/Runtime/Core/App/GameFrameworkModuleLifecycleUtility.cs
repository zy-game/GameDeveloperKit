using System;
using System.Threading;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 提供框架模块生命周期状态切换的辅助方法。
    /// </summary>
    internal static class GameFrameworkModuleLifecycleUtility
    {
        /// <summary>
        /// 尝试进入初始化状态。
        /// </summary>
        /// <param name="moduleName">模块名称。</param>
        /// <param name="status">模块当前状态。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>如果成功进入初始化状态则返回 true；如果模块已就绪则返回 false。</returns>
        /// <exception cref="InvalidOperationException">当模块当前状态不允许初始化时抛出。</exception>
        public static bool TryEnterInitialization(string moduleName, ref GameFrameworkModuleStatus status, CancellationToken cancellationToken = default)
        {
            if (status == GameFrameworkModuleStatus.Ready)
            {
                return false;
            }

            switch (status)
            {
                case GameFrameworkModuleStatus.Initializing:
                    throw new InvalidOperationException($"Module '{moduleName}' is already initializing.");
                case GameFrameworkModuleStatus.ShuttingDown:
                    throw new InvalidOperationException($"Module '{moduleName}' is shutting down and can not be initialized.");
                case GameFrameworkModuleStatus.Disposed:
                    throw new InvalidOperationException($"Module '{moduleName}' is already disposed and can not be initialized again.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            status = GameFrameworkModuleStatus.Initializing;
            return true;
        }

        /// <summary>
        /// 将模块状态标记为初始化完成。
        /// </summary>
        /// <param name="status">模块当前状态。</param>
        public static void CompleteInitialization(ref GameFrameworkModuleStatus status)
        {
            status = GameFrameworkModuleStatus.Ready;
        }

        /// <summary>
        /// 将模块状态标记为初始化失败。
        /// </summary>
        /// <param name="status">模块当前状态。</param>
        public static void FailInitialization(ref GameFrameworkModuleStatus status)
        {
            status = GameFrameworkModuleStatus.Failed;
        }

        /// <summary>
        /// 尝试进入关闭状态。
        /// </summary>
        /// <param name="moduleName">模块名称。</param>
        /// <param name="status">模块当前状态。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>如果成功进入关闭状态则返回 true；如果模块已释放或正在关闭则返回 false。</returns>
        public static bool TryEnterShutdown(string moduleName, ref GameFrameworkModuleStatus status, CancellationToken cancellationToken = default)
        {
            if (status == GameFrameworkModuleStatus.Disposed
                || status == GameFrameworkModuleStatus.ShuttingDown)
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            status = GameFrameworkModuleStatus.ShuttingDown;
            return true;
        }
    }
}
