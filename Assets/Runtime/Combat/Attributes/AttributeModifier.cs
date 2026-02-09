using System;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 修改器操作类型
    /// </summary>
    public enum ModifierOp
    {
        /// <summary>
        /// 加法: BaseValue + Value
        /// </summary>
        Add,

        /// <summary>
        /// 乘法加成: BaseValue * (1 + Value)
        /// </summary>
        PercentAdd,

        /// <summary>
        /// 乘法: BaseValue * Value
        /// </summary>
        Multiply,

        /// <summary>
        /// 覆盖: Value
        /// </summary>
        Override
    }

    /// <summary>
    /// 属性修改器
    /// </summary>
    public class AttributeModifier : IReference
    {
        private static int _nextId;

        /// <summary>
        /// 修改器唯一标识
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// 作用的属性名
        /// </summary>
        public string AttributeName { get; private set; }

        /// <summary>
        /// 修改方式
        /// </summary>
        public ModifierOp Operation { get; private set; }

        /// <summary>
        /// 修改数值
        /// </summary>
        public float Value { get; private set; }

        /// <summary>
        /// 修改器优先级
        /// </summary>
        public int Priority { get; private set; }

        /// <summary>
        /// 修改来源
        /// </summary>
        public object Source { get; private set; }

        /// <summary>
        /// 创建并初始化属性修改器
        /// </summary>
        public static AttributeModifier Create(string attributeName, ModifierOp op, float value, int priority = 0, object source = null)
        {
            var modifier = ReferencePool.Acquire<AttributeModifier>();
            modifier.Id = ++_nextId;
            modifier.AttributeName = attributeName;
            modifier.Operation = op;
            modifier.Value = value;
            modifier.Priority = priority;
            modifier.Source = source;
            return modifier;
        }

        /// <summary>
        /// 清理对象状态用于回收
        /// </summary>
        public void OnClearup()
        {
            Id = 0;
            AttributeName = null;
            Operation = ModifierOp.Add;
            Value = 0;
            Priority = 0;
            Source = null;
        }
    }
}
