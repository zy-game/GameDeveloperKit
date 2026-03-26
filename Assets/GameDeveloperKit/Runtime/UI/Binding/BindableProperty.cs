using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 可绑定属性，支持值变更通知和数据绑定。
    /// </summary>
    /// <typeparam name="T">属性值类型。</typeparam>
    public sealed class BindableProperty<T>
    {
        private T _value;

        /// <summary>
        /// 当属性值变更时触发的事件。
        /// </summary>
        public event Action<T> ValueChanged;

        /// <summary>
        /// 当属性值变更时触发的事件（包含旧值）。
        /// </summary>
        public event Action<T, T> ValueChangedWithOldValue;

        /// <summary>
        /// 获取或设置属性值。
        /// </summary>
        public T Value
        {
            get => _value;
            set => SetValue(value);
        }

        /// <summary>
        /// 初始化可绑定属性的新实例。
        /// </summary>
        public BindableProperty()
        {
        }

        /// <summary>
        /// 使用初始值初始化可绑定属性的新实例。
        /// </summary>
        /// <param name="initialValue">初始值。</param>
        public BindableProperty(T initialValue)
        {
            _value = initialValue;
        }

        /// <summary>
        /// 设置属性值但不触发变更事件。
        /// </summary>
        /// <param name="value">新值。</param>
        public void SetValueWithoutNotify(T value)
        {
            _value = value;
        }

        /// <summary>
        /// 设置属性值并触发变更事件（如果值改变）。
        /// </summary>
        /// <param name="value">新值。</param>
        public void SetValue(T value)
        {
            if (Equals(_value, value))
            {
                return;
            }

            var oldValue = _value;
            _value = value;
            ValueChanged?.Invoke(_value);
            ValueChangedWithOldValue?.Invoke(oldValue, _value);
        }

        /// <summary>
        /// 手动触发值变更事件。
        /// </summary>
        public void NotifyValueChanged()
        {
            ValueChanged?.Invoke(_value);
        }

        /// <summary>
        /// 清除所有事件监听器。
        /// </summary>
        public void ClearListeners()
        {
            ValueChanged = null;
            ValueChangedWithOldValue = null;
        }

        /// <summary>
        /// 将可绑定属性隐式转换为其值类型。
        /// </summary>
        /// <param name="property">可绑定属性。</param>
        /// <returns>属性值。</returns>
        public static implicit operator T(BindableProperty<T> property)
        {
            return property == null ? default : property.Value;
        }

        /// <summary>
        /// 返回属性的字符串表示。
        /// </summary>
        /// <returns>属性的字符串表示。</returns>
        public override string ToString()
        {
            return _value?.ToString() ?? "null";
        }
    }
}
