namespace GameDeveloperKit.Event
{
    /// <summary>
    /// 强类型事件处理器基类，用于声明事件处理器处理的事件参数类型。
    /// </summary>
    /// <typeparam name="TEvent">事件参数类型。</typeparam>
    public interface IEventHandleBase<TEvent> where TEvent : ArgsBase
    {
        /// <summary>
        /// 处理强类型事件。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="eventData">事件参数。</param>
        void Handle(object sender, TEvent eventData);
    }
}
