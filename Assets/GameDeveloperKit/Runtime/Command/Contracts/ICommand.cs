namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 命令接口，定义可执行命令的基本契约。
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// 执行命令。
        /// </summary>
        void Execute();
    }
}
