using System;

namespace GameDeveloperKit.World
{
    /// <summary>
    /// 系统优先级特性，数值越小优先级越高
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class SystemPriorityAttribute : Attribute
    {
        public SystemPriorityAttribute(int priority = 0)
        {
            Priority = priority;
        }

        public int Priority { get; }
    }
}