using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 表示启动阶段的模块初始化配置。
    /// </summary>
    [Serializable]
    public sealed class StartupModuleConfiguration
    {
        /// <summary>
        /// 是否在启动时准备资源模块。
        /// </summary>
        public bool PrepareResourcesOnStartup = true;

        /// <summary>
        /// 是否初始化 UI 模块。
        /// </summary>
        public bool InitializeUI = true;

        /// <summary>
        /// 是否初始化场景模块。
        /// </summary>
        public bool InitializeSceneModule = true;

        /// <summary>
        /// 是否初始化流程模块。
        /// </summary>
        public bool InitializeProcedureModule = true;

        /// <summary>
        /// 启动时覆盖使用的语言标识。
        /// </summary>
        public string OverrideLanguage;
    }
}
