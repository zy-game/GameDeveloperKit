using System;
using GameDeveloperKit.Debugger;
using GameDeveloperKit.Timer;
using UnityEngine;

namespace GameDeveloperKit.Combat
{
    public sealed partial class CombatModule
    {
        private sealed class CombatProfileHandle : ProfileHandle
        {
            private readonly CombatModule m_Module;

            /// <summary>
            /// 初始化 Combat Profile Handle。
            /// </summary>
            public CombatProfileHandle(CombatModule module)
            {
                m_Module = module ?? throw new ArgumentNullException(nameof(module));
            }
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
