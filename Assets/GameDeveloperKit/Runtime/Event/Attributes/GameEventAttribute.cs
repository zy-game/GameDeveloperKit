using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 游戏事件特性，用于标记事件处理程序类并声明它们绑定到的事件类型。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class GameEventAttribute : Attribute
    {
        /// <summary>
        /// 使用事件名称初始化特性的新实例。
        /// </summary>
        /// <param name="eventName">事件名称。</param>
        public GameEventAttribute(string eventName)
        {
            EventName = eventName;
        }

        /// <summary>
        /// 使用事件ID初始化特性的新实例。
        /// </summary>
        /// <param name="eventId">事件ID。</param>
        public GameEventAttribute(int eventId)
        {
            EventId = eventId;
            EventName = eventId.ToString();
        }

        /// <summary>
        /// 使用事件类型初始化特性的新实例。
        /// </summary>
        /// <param name="eventType">事件类型。</param>
        public GameEventAttribute(Type eventType)
        {
            EventType = eventType;
        }

        /// <summary>
        /// 获取事件名称。
        /// </summary>
        public string EventName { get; }
        /// <summary>
        /// 获取事件ID。
        /// </summary>
        public int? EventId { get; }
        /// <summary>
        /// 获取事件类型。
        /// </summary>
        public Type EventType { get; }
    }
}
