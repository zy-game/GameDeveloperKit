using System;
using GameDeveloperKit.Timer;

namespace GameDeveloperKit.Procedure
{
    public sealed partial class ProcedureModule
    {
        private sealed class ProcedureUpdateHandle : UpdateTimerHandle
        {
            /// <summary>
            /// 初始化 Procedure Update Handle。
            /// </summary>
            public ProcedureUpdateHandle(ProcedureModule module) : base(CreateCallback(module))
            {
            }

            /// <summary>
            /// 创建 Callback。
            /// </summary>
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
