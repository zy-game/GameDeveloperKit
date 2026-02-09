namespace GameDeveloperKit.World
{
    /// <summary>
    /// 系统基础标记接口
    /// 所有系统必须实现此接口，具体功能通过继承其他接口实现：
    /// - INormalSystem: 系统生命周期管理（OnStartup/OnShutdown）
    /// - IUpdateSystem: 每帧更新时调用
    /// - ISetupSystem: 实体满足Group条件时调用（必须配合IGroupSystem）
    /// - ITeardownSystem: 实体即将不满足Group条件时调用（必须配合IGroupSystem）
    /// - IGroupSystem: 声明Include/Exclude条件，用于高性能过滤实体
    /// 
    /// IGroupSystem使用Include/Exclude定义过滤条件：
    /// - Include: 实体必须拥有所有指定组件
    /// - Exclude: 实体不能拥有任何指定组件
    /// - Setup/Teardown/Update都使用相同的过滤规则
    /// </summary>
    public interface ISystem
    {
    }
}