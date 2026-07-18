using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Combat;
using GameDeveloperKit.Command;
using GameDeveloperKit.Config;
using GameDeveloperKit.Cache;
using GameDeveloperKit.Data;
using GameDeveloperKit.Download;
using GameDeveloperKit.Event;
using GameDeveloperKit.File;
using GameDeveloperKit.Input;
using GameDeveloperKit.Localization;
using GameDeveloperKit.Debugger;
using GameDeveloperKit.Network;
using GameDeveloperKit.Operation;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Procedure;
using GameDeveloperKit.Resource;
using GameDeveloperKit.Story;
using GameDeveloperKit.Timer;
using GameDeveloperKit.UI;
using UnityEngine;

namespace GameDeveloperKit
{
    /// <summary>
    /// 框架入口，提供模块的全局访问点和生命周期控制。
    /// </summary>
    public static class App
    {
        private static AppState s_State;

        static App()
        {
            ResetStaticState();
        }

        public static EventModule Event => GetModule<EventModule>();
        public static ResourceModule Resource => GetModule<ResourceModule>();
        public static FileModule File => GetModule<FileModule>();
        public static DownloadModule Download => GetModule<DownloadModule>();
        public static NetworkModule Network => GetModule<NetworkModule>();
        public static ConfigModule Config => GetModule<ConfigModule>();
        public static DataModule Data => GetModule<DataModule>();
        public static DebugModule Debug => GetModule<DebugModule>();
        public static LocalizationModule Localization => GetModule<LocalizationModule>();
        public static InputModule Input => GetModule<InputModule>();
        public static CacheModule Cache => GetModule<CacheModule>();
        public static CommandModule Command => GetModule<CommandModule>();
        public static UIModule UI => GetModule<UIModule>();
        public static OperationModule Operation => GetModule<OperationModule>();
        public static PlayableModule Playable => GetModule<PlayableModule>();
        public static ProcedureModule Procedure => GetModule<ProcedureModule>();
        public static TimerModule Timer => GetModule<TimerModule>();
        public static StoryModule Story => GetModule<StoryModule>();
        public static CombatModule Combat => GetModule<CombatModule>();

        /// <summary>
        /// 初始化框架状态。模块在首次访问时按需创建。
        /// </summary>
        public static UniTask Initialize()
        {
            return s_State.Lifecycle.Initialize();
        }

        /// <summary>
        /// 获取或创建模块，并递归启动声明的模块依赖。
        /// </summary>
        public static TModule GetModule<TModule>() where TModule : class, IGameModule, new()
        {
            return s_State.Registry.GetModule<TModule>();
        }

        /// <summary>
        /// 尝试获取已注册模块（不触发创建）。
        /// </summary>
        public static bool TryGetRegistered<T>(out T module) where T : class, IGameModule
        {
            return s_State.Registry.TryGetRegistered(out module);
        }

        /// <summary>
        /// 注册模块（触发依赖解析和启动）。
        /// </summary>
        public static void Register<T>() where T : class, IGameModule, new()
        {
            s_State.Registry.Register<T>();
        }

        /// <summary>
        /// 卸载模块。
        /// </summary>
        public static UniTask Unregister<T>() where T : IGameModule
        {
            return s_State.Registry.Unregister<T>();
        }

        /// <summary>
        /// 关闭所有已启动模块。
        /// </summary>
        public static UniTask Shutdown()
        {
            return s_State.Lifecycle.Shutdown();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_State = new AppState();
        }

        private sealed class AppState
        {
            public AppState()
            {
                Registry = new ModuleRegistry();
                Lifecycle = new ModuleLifecycle(Registry);
                Registry.SetShuttingDownCheck(() => Lifecycle.IsShuttingDown);
            }

            public ModuleRegistry Registry { get; }

            public ModuleLifecycle Lifecycle { get; }
        }
    }
}
