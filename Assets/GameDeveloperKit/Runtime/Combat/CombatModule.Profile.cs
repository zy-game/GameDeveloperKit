using System;
using GameDeveloperKit.Logger;
using GameDeveloperKit.Timer;
using UnityEngine;

namespace GameDeveloperKit.Combat
{
    public sealed partial class CombatModule
    {
        /// <summary>
        /// 定义 Combat Profile Handle 类型。
        /// </summary>
        private sealed class CombatProfileHandle : ProfileHandle
        {
            /// <summary>
            /// 存储 Module。
            /// </summary>
            private readonly CombatModule m_Module;

            /// <summary>
            /// 初始化 Combat Profile Handle。
            /// </summary>
            /// <param name="module">module 参数。</param>
            public CombatProfileHandle(CombatModule module)
            {
                m_Module = module ?? throw new ArgumentNullException(nameof(module));
            }

            /// <summary>
            /// 存储 Name。
            /// </summary>
            public override string Name => "Combat";

            /// <summary>
            /// 绘制 member。
            /// </summary>
            protected internal override void Draw()
            {
                var world = m_Module.World;
                if (world == null)
                {
                    GUILayout.Label("World: none");
                }
                else
                {
                    GUILayout.Label($"World Tick: {world.Tick}");
                    GUILayout.Label($"World Time: {world.Time:0.000}s");
                    GUILayout.Label($"Frame Rate: {world.FrameRate}");
                    GUILayout.Label($"Fixed Delta: {world.FixedDeltaTime:0.000}s");
                }

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
        }
    }
}
