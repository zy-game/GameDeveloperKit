namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情运行时模块，负责注册并推进剧情程序。
    /// </summary>
    public sealed partial class StoryModule : GameModuleBase
    {
        /// <summary>
        /// 启动剧情模块并清空运行状态。
        /// </summary>
        public override void Startup()
        {
            m_Programs.Clear();
            CurrentRunner = null;
            FunctionResolver = null;
        }

        /// <summary>
        /// 关闭剧情模块并释放当前运行状态。
        /// </summary>
        public override void Shutdown()
        {
            m_Programs.Clear();
            CurrentRunner = null;
            FunctionResolver = null;
        }
    }
}
