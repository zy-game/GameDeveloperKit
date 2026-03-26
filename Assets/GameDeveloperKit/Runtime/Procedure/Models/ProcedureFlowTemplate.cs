namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 流程模板类，定义游戏流程的初始状态和场景配置。
    /// </summary>
    public sealed class ProcedureFlowTemplate
    {
        /// <summary>
        /// 获取或设置启动状态名称。
        /// </summary>
        public string StartupStateName = "Startup";

        /// <summary>
        /// 获取或设置大厅状态名称。
        /// </summary>
        public string LobbyStateName = "Lobby";

        /// <summary>
        /// 获取或设置战斗状态名称。
        /// </summary>
        public string BattleStateName = "Battle";

        /// <summary>
        /// 获取或设置大厅场景名称。
        /// </summary>
        public string LobbySceneName;

        /// <summary>
        /// 获取或设置大厅资源包名称。
        /// </summary>
        public string LobbyPackageName;

        /// <summary>
        /// 获取或设置战斗场景名称。
        /// </summary>
        public string BattleSceneName;

        /// <summary>
        /// 获取或设置战斗资源包名称。
        /// </summary>
        public string BattlePackageName;

        /// <summary>
        /// 获取或设置启动加载提示信息。
        /// </summary>
        public string StartupLoadingMessage = "Starting...";

        /// <summary>
        /// 获取或设置是否显示启动加载。
        /// </summary>
        public bool ShowStartupLoading = true;

        /// <summary>
        /// 获取或设置是否记住场景。
        /// </summary>
        public bool RememberScenes = true;
    }
}
