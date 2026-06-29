using System;
using GameDeveloperKit.Timer;

namespace GameDeveloperKit.Debugger
{
    public partial class DebugModule
    {
        internal sealed class DebugRefreshHandle : UpdateTimerHandle
        {
            /// <summary>
            /// 初始化 Debug Refresh Handle。
            /// </summary>
            public DebugRefreshHandle(DebugModule module) : base(CreateCallback(module))
            {
            }

            /// <summary>
            /// 创建 Callback。
            /// </summary>
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
