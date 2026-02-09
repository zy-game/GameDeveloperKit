using System;

namespace GameDeveloperKit.Editor.Combat
{
    /// <summary>
    /// 标记一个类为战斗系统编辑器Inspector
    /// 用于将数据类型与其对应的Inspector类关联
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class CombatInspectorAttribute : Attribute
    {
        /// <summary>
        /// 此Inspector对应的数据类型
        /// </summary>
        public Type TargetType { get; }

        /// <summary>
        /// 优先级，数值越大优先级越高
        /// 当多个Inspector匹配同一类型时，使用优先级最高的
        /// </summary>
        public int Priority { get; set; } = 0;

        public CombatInspectorAttribute(Type targetType)
        {
            TargetType = targetType;
        }
    }
}
