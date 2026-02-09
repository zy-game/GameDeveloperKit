using System;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 属性值，包含基础值和当前值
    /// </summary>
    [Serializable]
    public struct AttributeValue
    {
        /// <summary>
        /// 基础值
        /// </summary>
        public float BaseValue;

        /// <summary>
        /// 当前值
        /// </summary>
        public float CurrentValue;

        /// <summary>
        /// 最小值
        /// </summary>
        public float MinValue;

        /// <summary>
        /// 最大值
        /// </summary>
        public float MaxValue;

        /// <summary>
        /// 初始化属性值
        /// </summary>
        public AttributeValue(float baseValue, float min = float.MinValue, float max = float.MaxValue)
        {
            BaseValue = baseValue;
            CurrentValue = baseValue;
            MinValue = min;
            MaxValue = max;
        }

        /// <summary>
        /// 设置基础值
        /// </summary>
        public void SetBaseValue(float value)
        {
            BaseValue = value;
        }

        /// <summary>
        /// 设置当前值并进行范围限制
        /// </summary>
        public void SetCurrentValue(float value)
        {
            CurrentValue = Math.Clamp(value, MinValue, MaxValue);
        }

        /// <summary>
        /// 对当前值做范围限制
        /// </summary>
        public void ClampCurrent()
        {
            CurrentValue = Math.Clamp(CurrentValue, MinValue, MaxValue);
        }

        /// <summary>
        /// 直接读取当前值
        /// </summary>
        public static implicit operator float(AttributeValue attr) => attr.CurrentValue;
    }
}
