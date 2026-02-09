using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 激活中的效果实例
    /// </summary>
    public class ActiveEffect : IReference
    {
        private static int _nextId;

        /// <summary>
        /// 效果唯一标识
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// 效果定义资源
        /// </summary>
        public GameplayEffect Definition { get; private set; }

        /// <summary>
        /// 效果来源
        /// </summary>
        public object Source { get; private set; }

        /// <summary>
        /// 效果目标
        /// </summary>
        public object Target { get; private set; }

        /// <summary>
        /// 效果等级
        /// </summary>
        public int Level { get; private set; }

        /// <summary>
        /// 当前层数
        /// </summary>
        public int StackCount { get; private set; }

        /// <summary>
        /// 剩余持续时间（秒）
        /// </summary>
        public float RemainingDuration { get; private set; }

        /// <summary>
        /// 周期计时器
        /// </summary>
        public float PeriodTimer { get; private set; }

        /// <summary>
        /// 是否已过期
        /// </summary>
        public bool IsExpired { get; private set; }

        /// <summary>
        /// 授予的标签
        /// </summary>
        public TagContainer GrantedTags { get; } = new();

        /// <summary>
        /// 已应用的属性修改器
        /// </summary>
        public List<AttributeModifier> AppliedModifiers { get; } = new();

        public event Action<ActiveEffect> OnStackChanged;
        public event Action<ActiveEffect> OnExpired;
        public event Action<ActiveEffect> OnPeriodTick;

        /// <summary>
        /// 创建效果实例
        /// </summary>
        public static ActiveEffect Create(GameplayEffect definition, object source, object target, int level = 1)
        {
            var effect = ReferencePool.Acquire<ActiveEffect>();
            effect.Id = ++_nextId;
            effect.Definition = definition;
            effect.Source = source;
            effect.Target = target;
            effect.Level = level;
            effect.StackCount = 1;
            effect.IsExpired = false;

            if (definition.DurationType == EffectDurationType.Duration)
            {
                effect.RemainingDuration = definition.Duration;
            }
            else
            {
                effect.RemainingDuration = -1f;
            }

            effect.PeriodTimer = definition.ExecuteOnApply ? 0f : definition.Period;

            if (definition.GrantedTags != null)
            {
                foreach (var tagName in definition.GrantedTags)
                {
                    effect.GrantedTags.AddTag(GameplayTag.Get(tagName));
                }
            }

            return effect;
        }

        /// <summary>
        /// 增加堆叠层数
        /// </summary>
        public void AddStack(int count = 1)
        {
            int oldCount = StackCount;
            StackCount = Math.Min(StackCount + count, Definition.MaxStacks);
            if (StackCount != oldCount)
            {
                OnStackChanged?.Invoke(this);
            }
        }

        /// <summary>
        /// 减少堆叠层数
        /// </summary>
        public void RemoveStack(int count = 1)
        {
            int oldCount = StackCount;
            StackCount = Math.Max(StackCount - count, 0);
            if (StackCount != oldCount)
            {
                OnStackChanged?.Invoke(this);
            }
            if (StackCount <= 0)
            {
                Expire();
            }
        }

        /// <summary>
        /// 刷新持续时间
        /// </summary>
        public void RefreshDuration()
        {
            if (Definition.DurationType == EffectDurationType.Duration)
            {
                RemainingDuration = Definition.Duration;
            }
        }

        /// <summary>
        /// 每帧更新周期与持续时间
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (IsExpired)
                return;

            if (Definition.DurationType == EffectDurationType.Duration)
            {
                RemainingDuration -= deltaTime;
                if (RemainingDuration <= 0f)
                {
                    Expire();
                    return;
                }
            }

            if (Definition.IsPeriodic)
            {
                PeriodTimer -= deltaTime;
                if (PeriodTimer <= 0f)
                {
                    PeriodTimer += Definition.Period;
                    OnPeriodTick?.Invoke(this);
                }
            }
        }

        /// <summary>
        /// 使效果过期
        /// </summary>
        public void Expire()
        {
            if (IsExpired)
                return;

            IsExpired = true;
            OnExpired?.Invoke(this);
        }

        /// <summary>
        /// 清理对象状态用于回收
        /// </summary>
        public void OnClearup()
        {
            Id = 0;
            Definition = null;
            Source = null;
            Target = null;
            Level = 0;
            StackCount = 0;
            RemainingDuration = 0;
            PeriodTimer = 0;
            IsExpired = false;
            GrantedTags.Clear();
            AppliedModifiers.Clear();
            OnStackChanged = null;
            OnExpired = null;
            OnPeriodTick = null;
        }
    }
}
