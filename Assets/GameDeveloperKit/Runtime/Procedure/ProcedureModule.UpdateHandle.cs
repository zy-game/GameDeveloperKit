using System;
using GameDeveloperKit.Timer;

namespace GameDeveloperKit.Procedure
{
    public sealed partial class ProcedureModule
    {
        /// <summary>
        /// 定义 Procedure Update Handle 类型。
        /// </summary>
        private sealed class ProcedureUpdateHandle : UpdateTimerHandle
        {
            /// <summary>
            /// 初始化 Procedure Update Handle。
            /// </summary>
            /// <param name="module">module 参数。</param>
            public ProcedureUpdateHandle(ProcedureModule module) : base(CreateCallback(module))
            {
            }

            /// <summary>
            /// 创建 Callback。
            /// </summary>
            /// <param name="module">module 参数。</param>
            /// <returns>Callback。</returns>
            private static Action<TimerUpdateContext> CreateCallback(ProcedureModule module)
            {
                if (module == null)
                {
                    throw new ArgumentNullException(nameof(module));
                }

                return context => module.UpdateCurrent(context.DeltaTime, context.UnscaledDeltaTime);
            }
        }
    }
}
