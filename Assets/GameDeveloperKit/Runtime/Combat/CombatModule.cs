using System;
using GameDeveloperKit.Logger;
using GameDeveloperKit.Timer;
using UnityEngine;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 战斗 ECS 模块。
    /// </summary>
    [ModuleDependency(typeof(TimerModule))]
    public sealed partial class CombatModule : GameModuleBase
    {
        /// <summary>
        /// 战斗运行时根对象名称。
        /// </summary>
        internal const string RootName = "GameDeveloperKit.CombatRoot";

        /// <summary>
        /// 存储 Update Handle。
        /// </summary>
        private FixedUpdateTimerHandle m_UpdateHandle;
        /// <summary>
        /// 存储 Profile Handle。
        /// </summary>
        private readonly CombatProfileHandle m_ProfileHandle;

        /// <summary>
        /// 初始化 Combat Module。
        /// </summary>
        public CombatModule()
        {
            m_ProfileHandle = new CombatProfileHandle(this);
        }

        /// <summary>
        /// 默认战斗世界。
        /// </summary>
        public World World { get; private set; }

        /// <summary>
        /// 启动战斗模块。
        /// </summary>
        public override void Startup()
        {
            if (World != null)
            {
                RegisterUpdateHandle();
                return;
            }

            if (!App.TryGetRegistered<TimerModule>(out var timer))
            {
                throw new GameException("TimerModule is required to update CombatModule. Register TimerModule before starting CombatModule.");
            }

            World = new World();
            RegisterUpdateHandle(timer);
            TryRegisterDebugProfile();
        }

        /// <summary>
        /// 关闭战斗模块。
        /// </summary>
        public override void Shutdown()
        {
            TryUnregisterDebugProfile();
            UnregisterUpdateHandle();
            World?.Dispose();
            World = null;
        }

        /// <summary>
        /// 按真实帧时间更新默认战斗世界。
        /// </summary>
        /// <param name="deltaTime">真实帧时间。</param>
        private void UpdateWorld(float deltaTime)
        {
            World?.Update(deltaTime);
        }

        /// <summary>
        /// 注册 Update Handle。
        /// </summary>
        private void RegisterUpdateHandle()
        {
            if (!App.TryGetRegistered<TimerModule>(out var timer))
            {
                throw new GameException("TimerModule is required to update CombatModule. Register TimerModule before starting CombatModule.");
            }

            RegisterUpdateHandle(timer);
        }

        /// <summary>
        /// 注册 Update Handle。
        /// </summary>
        /// <param name="timer">计时器模块。</param>
        private void RegisterUpdateHandle(TimerModule timer)
        {
            if (m_UpdateHandle != null &&
                !m_UpdateHandle.IsCancelled &&
                !m_UpdateHandle.IsCompleted &&
                m_UpdateHandle.Module != null)
            {
                return;
            }

            if (timer == null)
            {
                throw new ArgumentNullException(nameof(timer));
            }

            m_UpdateHandle = timer.OnFixedUpdate(context => UpdateWorld(context.DeltaTime), this, "CombatModule.Update");
        }

        /// <summary>
        /// 注销 Update Handle。
        /// </summary>
        private void UnregisterUpdateHandle()
        {
            if (m_UpdateHandle == null)
            {
                return;
            }

            m_UpdateHandle.Cancel();
            m_UpdateHandle = null;
        }

        /// <summary>
        /// 注册 Debug Profile。
        /// </summary>
        /// <param name="debug">debug 参数。</param>
        internal void RegisterDebugProfile(DebugModule debug)
        {
            if (debug == null)
            {
                throw new ArgumentNullException(nameof(debug));
            }

            debug.RegisterProfile(m_ProfileHandle);
        }

        /// <summary>
        /// 注销 Debug Profile。
        /// </summary>
        /// <param name="debug">debug 参数。</param>
        internal void UnregisterDebugProfile(DebugModule debug)
        {
            if (debug == null)
            {
                throw new ArgumentNullException(nameof(debug));
            }

            debug.UnregisterProfile(m_ProfileHandle);
        }

        /// <summary>
        /// 尝试注册 Debug Profile。
        /// </summary>
        private void TryRegisterDebugProfile()
        {
            if (App.TryGetRegistered<DebugModule>(out var debug))
            {
                RegisterDebugProfile(debug);
            }
        }

        /// <summary>
        /// 尝试注销 Debug Profile。
        /// </summary>
        private void TryUnregisterDebugProfile()
        {
            if (App.TryGetRegistered<DebugModule>(out var debug))
            {
                UnregisterDebugProfile(debug);
            }
        }

    }
}
