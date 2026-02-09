using System;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// 可绑定属性（用于 UIDataBase 中定义数据）
    /// 支持值变化通知
    /// </summary>
    public class BindableProperty<T>
    {
        private T _value;
        
        /// <summary>
        /// 值变化事件
        /// </summary>
        public event Action<T> OnValueChanged;

        /// <summary>
        /// 值变化事件（带新旧值）
        /// </summary>
        public event Action<T, T> OnValueChangedWithOldValue;

        /// <summary>
        /// 属性值
        /// </summary>
        public T Value
        {
            get => _value;
            set => SetValue(value);
        }

        public BindableProperty()
        {
        }

        public BindableProperty(T initialValue)
        {
            _value = initialValue;
        }

        /// <summary>
        /// 设置值（不触发事件）
        /// </summary>
        public void SetValueWithoutNotify(T value)
        {
            _value = value;
        }

        /// <summary>
        /// 设置值（触发事件）
        /// </summary>
        public void SetValue(T value)
        {
            if (!Equals(_value, value))
            {
                T oldValue = _value;
                _value = value;
                OnValueChanged?.Invoke(_value);
                OnValueChangedWithOldValue?.Invoke(oldValue, _value);
            }
        }

        /// <summary>
        /// 强制触发值变化事件（即使值未改变）
        /// </summary>
        public void NotifyValueChanged()
        {
            OnValueChanged?.Invoke(_value);
        }

        /// <summary>
        /// 清理所有事件订阅（在 OnClearup 中调用）
        /// </summary>
        public void ClearListeners()
        {
            OnValueChanged = null;
            OnValueChangedWithOldValue = null;
        }

        /// <summary>
        /// 隐式转换：BindableProperty<T> → T（仅用于读取）
        /// </summary>
        public static implicit operator T(BindableProperty<T> property)
        {
            return property.Value;
        }

        public override string ToString()
        {
            return _value?.ToString() ?? "null";
        }
    }
}
