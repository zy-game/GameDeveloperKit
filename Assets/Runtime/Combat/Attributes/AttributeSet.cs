using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 属性集合基类
    /// </summary>
    public abstract class AttributeSet
    {
        private readonly Dictionary<string, AttributeValue> _attributes = new();
        private readonly Dictionary<string, List<AttributeModifier>> _modifiers = new();

        /// <summary>
        /// 属性值变化事件（name, oldValue, newValue）
        /// </summary>
        public event Action<string, float, float> OnAttributeChanged;

        /// <summary>
        /// 注册一个属性定义
        /// </summary>
        protected void DefineAttribute(string name, float baseValue, float min = float.MinValue, float max = float.MaxValue)
        {
            _attributes[name] = new AttributeValue(baseValue, min, max);
            _modifiers[name] = new List<AttributeModifier>();
        }

        /// <summary>
        /// 检查是否存在指定属性
        /// </summary>
        public bool HasAttribute(string name) => _attributes.ContainsKey(name);

        /// <summary>
        /// 获取属性基础值
        /// </summary>
        public float GetBaseValue(string name)
        {
            return _attributes.TryGetValue(name, out var attr) ? attr.BaseValue : 0f;
        }

        /// <summary>
        /// 获取属性当前值
        /// </summary>
        public float GetCurrentValue(string name)
        {
            return _attributes.TryGetValue(name, out var attr) ? attr.CurrentValue : 0f;
        }

        /// <summary>
        /// 设置基础值并重新计算当前值
        /// </summary>
        public void SetBaseValue(string name, float value)
        {
            if (!_attributes.TryGetValue(name, out var attr))
                return;

            attr.SetBaseValue(value);
            _attributes[name] = attr;
            RecalculateAttribute(name);
        }

        /// <summary>
        /// 添加属性修改器
        /// </summary>
        public void AddModifier(AttributeModifier modifier)
        {
            if (modifier == null || string.IsNullOrEmpty(modifier.AttributeName))
                return;

            if (!_modifiers.TryGetValue(modifier.AttributeName, out var list))
                return;

            list.Add(modifier);
            list.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            RecalculateAttribute(modifier.AttributeName);
        }

        /// <summary>
        /// 移除指定修改器
        /// </summary>
        public bool RemoveModifier(AttributeModifier modifier)
        {
            if (modifier == null || string.IsNullOrEmpty(modifier.AttributeName))
                return false;

            if (!_modifiers.TryGetValue(modifier.AttributeName, out var list))
                return false;

            if (list.Remove(modifier))
            {
                RecalculateAttribute(modifier.AttributeName);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 移除来源匹配的所有修改器
        /// </summary>
        public void RemoveModifiersFromSource(object source)
        {
            foreach (var kvp in _modifiers)
            {
                var list = kvp.Value;
                bool removed = false;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].Source == source)
                    {
                        ReferencePool.Release(list[i]);
                        list.RemoveAt(i);
                        removed = true;
                    }
                }
                if (removed)
                {
                    RecalculateAttribute(kvp.Key);
                }
            }
        }

        /// <summary>
        /// 清除指定属性的全部修改器
        /// </summary>
        public void ClearModifiers(string name)
        {
            if (!_modifiers.TryGetValue(name, out var list))
                return;

            foreach (var mod in list)
            {
                ReferencePool.Release(mod);
            }
            list.Clear();
            RecalculateAttribute(name);
        }

        /// <summary>
        /// 清除所有修改器并刷新所有属性
        /// </summary>
        public void ClearAllModifiers()
        {
            foreach (var kvp in _modifiers)
            {
                foreach (var mod in kvp.Value)
                {
                    ReferencePool.Release(mod);
                }
                kvp.Value.Clear();
            }

            foreach (var name in _attributes.Keys)
            {
                RecalculateAttribute(name);
            }
        }

        /// <summary>
        /// 重新计算指定属性的当前值
        /// </summary>
        protected void RecalculateAttribute(string name)
        {
            if (!_attributes.TryGetValue(name, out var attr))
                return;

            float oldValue = attr.CurrentValue;
            float baseValue = attr.BaseValue;

            if (_modifiers.TryGetValue(name, out var list) && list.Count > 0)
            {
                float addSum = 0f;
                float percentAddSum = 0f;
                float multiplyProduct = 1f;
                float? overrideValue = null;

                foreach (var mod in list)
                {
                    switch (mod.Operation)
                    {
                        case ModifierOp.Add:
                            addSum += mod.Value;
                            break;
                        case ModifierOp.PercentAdd:
                            percentAddSum += mod.Value;
                            break;
                        case ModifierOp.Multiply:
                            multiplyProduct *= mod.Value;
                            break;
                        case ModifierOp.Override:
                            overrideValue = mod.Value;
                            break;
                    }
                }

                if (overrideValue.HasValue)
                {
                    attr.SetCurrentValue(overrideValue.Value);
                }
                else
                {
                    float finalValue = (baseValue + addSum) * (1f + percentAddSum) * multiplyProduct;
                    attr.SetCurrentValue(finalValue);
                }
            }
            else
            {
                attr.SetCurrentValue(baseValue);
            }

            _attributes[name] = attr;

            if (Math.Abs(oldValue - attr.CurrentValue) > float.Epsilon)
            {
                OnAttributeChanged?.Invoke(name, oldValue, attr.CurrentValue);
            }
        }

        /// <summary>
        /// 获取全部属性名
        /// </summary>
        public IEnumerable<string> GetAttributeNames() => _attributes.Keys;
    }
}
