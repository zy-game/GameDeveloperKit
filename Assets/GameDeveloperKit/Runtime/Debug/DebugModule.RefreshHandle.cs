using System;
using GameDeveloperKit.Timer;

namespace GameDeveloperKit.Logger
{
    public partial class DebugModule
    {
        /// <summary>
        /// 定义 Debug Refresh Handle 类型。
        /// </summary>
        internal sealed class DebugRefreshHandle : UpdateTimerHandle
        {
            /// <summary>
            /// 初始化 Debug Refresh Handle。
            /// </summary>
            /// <param name="module">module 参数。</param>
            public DebugRefreshHandle(DebugModule module) : base(CreateCallback(module))
            {
            }

            /// <summary>
            /// 创建 Callback。
            /// </summary>
            /// <param name="module">module 参数。</param>
            /// <returns>Callback。</returns>
            private static Action<TimerUpdateContext> CreateCallback(DebugModule module)
            {
                if (module == null)
                {
                    throw new ArgumentNullException(nameof(module));
                }

                return context => module.UpdateMetrics(context.UnscaledDeltaTime);
            }
        }
    }
}
