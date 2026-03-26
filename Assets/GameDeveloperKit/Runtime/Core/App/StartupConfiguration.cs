using System;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 表示游戏启动的配置信息。
    /// </summary>
    /// <remarks>
    /// 此类封装了游戏启动过程中的所有可配置选项，包括资源设置、模块初始化、
    /// 初始流程、覆盖层显示和自定义启动任务等。
    /// </remarks>
    [Serializable]
    public sealed class StartupConfiguration
    {
        /// <summary>
        /// 获取或设置资源系统配置。
        /// </summary>
        /// <remarks>
        /// 此配置定义了资源加载的路径、更新策略和其他资源管理选项。
        /// 如果为 null，则使用 Startup 组件上直接配置的 ResourceSettings。
        /// </remarks>
        public ResourceSettings ResourceSettings;

        /// <summary>
        /// 获取或设置 Startup 对象是否在场景切换时保持不销毁。
        /// </summary>
        /// <remarks>
        /// 如果为 true，Startup 对象将使用 DontDestroyOnLoad 标记，
        /// 确保在场景切换时不会自动销毁。默认值为 true。
        /// </remarks>
        public bool PersistAcrossScenes = true;

        /// <summary>
        /// 获取或设置模块初始化配置。
        /// </summary>
        /// <remarks>
        /// 此配置控制哪些模块需要初始化、初始化顺序和其他模块相关选项。
        /// 包括 UI、场景和流程模块的初始化开关。
        /// </remarks>
        public StartupModuleConfiguration Modules = new();

        /// <summary>
        /// 获取或设置初始流程配置。
        /// </summary>
        /// <remarks>
        /// 此配置定义了启动完成后应该进入的场景和流程。
        /// 包括初始场景名称、加载模式和初始流程名称。
        /// </remarks>
        public StartupInitialFlowConfiguration InitialFlow = new();

        /// <summary>
        /// 获取或设置启动覆盖层配置。
        /// </summary>
        /// <remarks>
        /// 此配置控制启动进度覆盖层的显示样式和行为。
        /// 包括是否显示覆盖层、覆盖层标题文本和完成后是否自动隐藏。
        /// </remarks>
        public StartupOverlayConfiguration Overlay = new();

        /// <summary>
        /// 获取或设置自定义启动任务数组。
        /// </summary>
        /// <remarks>
        /// 这些任务会在框架初始化完成后、进入初始流程前执行。
        /// 每个任务必须实现 IStartupTask 接口。
        /// 可以用于执行游戏特定的初始化逻辑。
        /// </remarks>
        public MonoBehaviour[] StartupTasks = Array.Empty<MonoBehaviour>();
    }
}
