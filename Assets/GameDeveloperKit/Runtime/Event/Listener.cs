using System;

namespace GameDeveloperKit.Event
{
    /// <summary>
    /// 事件监听器记录，用于保存事件类型和对应的对象处理器或委托处理器。
    /// </summary>
    internal sealed class Listener
    {
        /// <summary>
        /// 存储 Action Invoker。
        /// </summary>
        private readonly Action<ArgsBase> m_ActionInvoker;

        /// <summary>
        /// 初始化对象形式的事件监听器记录。
        /// </summary>
        /// <param name="eventType">事件参数类型。</param>
        /// <param name="handleBase">事件处理器实例。</param>
        /// <exception cref="ArgumentNullException">事件类型或事件处理器为空时抛出。</exception>
        internal Listener(Type eventType, EventHandleBase handleBase)
        {
            EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
            this.handleBase = handleBase ?? throw new ArgumentNullException(nameof(handleBase));
            m_ActionInvoker = null;
            IsActive = true;
        }

        /// <summary>
        /// 初始化委托形式的事件监听器记录。
        /// </summary>
        /// <param name="eventType">事件参数类型。</param>
        /// <param name="action">事件处理委托。</param>
        /// <exception cref="ArgumentNullException">事件类型或事件处理委托为空时抛出。</exception>
        internal Listener(Type eventType, Delegate action, Action<ArgsBase> actionInvoker)
        {
            EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
            Action = action ?? throw new ArgumentNullException(nameof(action));
            m_ActionInvoker = actionInvoker ?? throw new ArgumentNullException(nameof(actionInvoker));
            IsActive = true;
        }

        /// <summary>
        /// 事件参数类型。
        /// </summary>
        public Type EventType { get; }

        /// <summary>
        /// 对象形式的事件处理器实例。
        /// </summary>
        public EventHandleBase handleBase { get; }

        /// <summary>
        /// 委托形式的事件处理器。
        /// </summary>
        public Delegate Action { get; }

        /// <summary>
        /// 监听器是否处于活动状态。
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// 停用监听器。
        /// </summary>
        public void Deactivate()
        {
            IsActive = false;
        }

        /// <summary>
        /// 执行 Invoke。
        /// </summary>
        /// <param name="sender">sender 参数。</param>
        /// <param name="eventData">event Data 参数。</param>
        public void Invoke(object sender, ArgsBase eventData)
        {
            if (handleBase != null)
            {
                handleBase.Handle(sender, eventData);
                return;
            }

            m_ActionInvoker(eventData);
        }
    }
}
