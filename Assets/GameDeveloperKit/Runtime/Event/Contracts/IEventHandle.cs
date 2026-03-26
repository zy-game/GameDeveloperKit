namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 事件处理程序接口。
    /// </summary>
    public interface IEventHandle
    {
        /// <summary>
        /// 处理事件。
        /// </summary>
        /// <param name="context">事件上下文。</param>
        void Handle(IEventContext context);
    }
}
