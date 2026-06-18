using System;
using GameDeveloperKit.Logger;
using GameDeveloperKit.Timer;
using UnityEngine;

namespace GameDeveloperKit.Procedure
{
    public sealed partial class ProcedureModule
    {
        /// <summary>
        /// 定义 Procedure Profile Handle 类型。
        /// </summary>
        private sealed class ProcedureProfileHandle : ProfileHandle
        {
            /// <summary>
            /// 存储 Module。
            /// </summary>
            private readonly ProcedureModule m_Module;

            /// <summary>
            /// 初始化 Procedure Profile Handle。
            /// </summary>
            /// <param name="module">module 参数。</param>
            public ProcedureProfileHandle(ProcedureModule module)
            {
                m_Module = module ?? throw new ArgumentNullException(nameof(module));
            }

            /// <summary>
            /// 存储 Name。
            /// </summary>
            public override string Name => "Procedure";

            /// <summary>
            /// 绘制 member。
            /// </summary>
            protected internal override void Draw()
            {
                GUILayout.Label($"Current: {FormatType(m_Module.CurrentType)}");
                GUILayout.Label($"Changing: {m_Module.IsChanging}");
                GUILayout.Label($"Pending: {FormatType(m_Module.PendingChangeType)}");
                DrawUpdateHandle(m_Module.m_UpdateHandle);
            }

            /// <summary>
            /// 绘制 Update Handle。
            /// </summary>
            /// <param name="handle">handle 参数。</param>
            private static void DrawUpdateHandle(TimerUpdateHandle handle)
            {
                if (handle == null)
                {
                    GUILayout.Label("Update Handle: none");
                    return;
                }

                var error = handle.HasError ? handle.LastException.Message : "none";
                GUILayout.Label($"Update Handle: kind={handle.TickKind} enabled={handle.Enabled} last={handle.LastTick} paused={handle.IsPaused} error={error}");
            }

            /// <summary>
            /// 格式化 Type。
            /// </summary>
            /// <param name="type">type 参数。</param>
            /// <returns>执行结果。</returns>
            private static string FormatType(Type type)
            {
                return type == null ? "none" : type.Name;
            }
        }
    }
}
