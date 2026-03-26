namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 定义游戏框架模块的生命周期状态。
    /// </summary>
    /// <remarks>
    /// 状态值反映了模块在生命周期中的当前位置，用于监控模块状态和处理错误。
    /// 状态转换遵循固定的顺序：Created → Initializing → (Ready | Failed) → ShuttingDown → Disposed。
    /// </remarks>
    public enum GameFrameworkModuleStatus
    {
        /// <summary>
        /// 模块已创建但尚未初始化。
        /// </summary>
        /// <remarks>
        /// 这是模块的初始状态，在模块实例构造完成后立即进入此状态。
        /// 此状态下模块尚未准备好提供服务。
        /// </remarks>
        Created = 0,

        /// <summary>
        /// 模块正在初始化中。
        /// </summary>
        /// <remarks>
        /// 在调用 InitializeAsync 方法后进入此状态。
        /// 模块在此状态下执行初始化逻辑，准备服务。
        /// 此状态下模块尚未准备好提供服务。
        /// </remarks>
        Initializing = 1,

        /// <summary>
        /// 模块已成功初始化并准备好提供服务。
        /// </summary>
        /// <remarks>
        /// 在初始化成功完成后进入此状态。
        /// 此状态下模块可以正常响应请求并提供服务。
        /// 这是模块的预期工作状态。
        /// </remarks>
        Ready = 2,

        /// <summary>
        /// 模块初始化失败或运行时出现严重错误。
        /// </summary>
        /// <remarks>
        /// 在初始化过程中或运行时发生不可恢复的错误时进入此状态。
        /// 此状态下模块无法正常提供服务，可能需要重新初始化或重启应用。
        /// </remarks>
        Failed = 3,

        /// <summary>
        /// 模块正在关闭中。
        /// </summary>
        /// <remarks>
        /// 在调用 ShutdownAsync 方法后进入此状态。
        /// 模块在此状态下执行清理逻辑，释放资源。
        /// 此状态下模块应该停止接受新的服务请求。
        /// </remarks>
        ShuttingDown = 4,

        /// <summary>
        /// 模块已关闭并释放所有资源。
        /// </summary>
        /// <remarks>
        /// 在关闭操作完成后进入此状态。
        /// 这是模块的最终状态，模块在此状态下无法再恢复或提供服务。
        /// 要重新使用模块，需要创建新的实例。
        /// </remarks>
        Disposed = 5
    }
}
