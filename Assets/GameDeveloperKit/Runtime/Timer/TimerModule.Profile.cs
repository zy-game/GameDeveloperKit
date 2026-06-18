using System;
using System.Collections.Generic;
using GameDeveloperKit.Logger;
using UnityEngine;

namespace GameDeveloperKit.Timer
{
    public sealed partial class TimerModule
    {
        /// <summary>
        /// 定义 Timer Profile Handle 类型。
        /// </summary>
        private sealed class TimerProfileHandle : ProfileHandle
        {
            /// <summary>
            /// 存储 Module。
            /// </summary>
            private readonly TimerModule m_Module;

            /// <summary>
            /// 初始化 Timer Profile Handle。
            /// </summary>
            /// <param name="module">module 参数。</param>
            public TimerProfileHandle(TimerModule module)
            {
                m_Module = module ?? throw new ArgumentNullException(nameof(module));
            }

            /// <summary>
            /// 存储 Name。
            /// </summary>
            public override string Name => "Timer";

            /// <summary>
            /// 绘制 member。
            /// </summary>
            protected internal override void Draw()
            {
                var snapshot = m_Module.Snapshot();
                GUILayout.Label($"Tick: {snapshot.Tick}");
                GUILayout.Label($"Time: {snapshot.Time:0.000}s / Unscaled: {snapshot.UnscaledTime:0.000}s");
                GUILayout.Label($"Delta: {snapshot.DeltaTime:0.000}s / Unscaled: {snapshot.UnscaledDeltaTime:0.000}s");
                GUILayout.Label($"Delay: {snapshot.Delays.Count}  Countdown: {snapshot.Countdowns.Count}  Interval: {snapshot.Intervals.Count}  Update: {snapshot.Updates.Count}");
                DrawUpdateHandles(snapshot.Updates);
            }

            /// <summary>
            /// 绘制 Update Handles。
            /// </summary>
            /// <param name="handles">handles 参数。</param>
            private static void DrawUpdateHandles(IReadOnlyList<TimerUpdateHandle> handles)
            {
                foreach (var handle in handles)
                {
                    var tag = string.IsNullOrEmpty(handle.Tag) ? "(untagged)" : handle.Tag;
                    var error = handle.HasError ? handle.LastException.Message : "none";
                    GUILayout.Label($"{tag} kind={handle.TickKind} enabled={handle.Enabled} last={handle.LastTick} paused={handle.IsPaused} error={error}");
                }
            }
        }
    }
}
