using System;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 表示启动后的初始流程配置。
    /// </summary>
    [Serializable]
    public sealed class StartupInitialFlowConfiguration
    {
        /// <summary>
        /// 是否在启动完成后自动进入初始场景。
        /// </summary>
        public bool AutoEnterInitialScene = true;

        /// <summary>
        /// 是否在启动完成后自动进入初始流程。
        /// </summary>
        public bool AutoEnterInitialProcedure = true;

        /// <summary>
        /// 启动完成后要进入的初始场景名称。
        /// </summary>
        public string InitialScene;

        /// <summary>
        /// 初始场景的加载模式。
        /// </summary>
        public LoadSceneMode InitialSceneLoadMode = LoadSceneMode.Single;

        /// <summary>
        /// 启动完成后要进入的初始流程名称。
        /// </summary>
        public string InitialProcedure;
    }
}
