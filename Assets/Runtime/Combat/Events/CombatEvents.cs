using GameDeveloperKit.Events;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 战斗事件基类
    /// </summary>
    public abstract class CombatEventArgs : GameEventArgs
    {
        /// <summary>
        /// 事件来源
        /// </summary>
        public object Source { get; protected set; }

        /// <summary>
        /// 事件目标
        /// </summary>
        public object Target { get; protected set; }
    }

    /// <summary>
    /// 伤害事件
    /// </summary>
    public class DamageEventArgs : CombatEventArgs
    {
        /// <summary>
        /// 事件编号
        /// </summary>
        public static readonly int EventId = typeof(DamageEventArgs).GetHashCode();
        public override int Id => EventId;

        /// <summary>
        /// 伤害信息
        /// </summary>
        public DamageInfo DamageInfo { get; private set; }

        /// <summary>
        /// 伤害前生命值
        /// </summary>
        public float HealthBefore { get; private set; }

        /// <summary>
        /// 伤害后生命值
        /// </summary>
        public float HealthAfter { get; private set; }

        /// <summary>
        /// 是否致命
        /// </summary>
        public bool IsFatal { get; private set; }

        /// <summary>
        /// 创建伤害事件
        /// </summary>
        public static DamageEventArgs Create(object source, object target, DamageInfo damage, float healthBefore, float healthAfter)
        {
            var args = ReferencePool.Acquire<DamageEventArgs>();
            args.Source = source;
            args.Target = target;
            args.DamageInfo = damage;
            args.HealthBefore = healthBefore;
            args.HealthAfter = healthAfter;
            args.IsFatal = healthAfter <= 0f;
            return args;
        }

        /// <summary>
        /// 清理对象状态用于回收
        /// </summary>
        public override void OnClearup()
        {
            Source = null;
            Target = null;
            DamageInfo = default;
            HealthBefore = 0f;
            HealthAfter = 0f;
            IsFatal = false;
        }
    }

    /// <summary>
    /// 技能事件类型
    /// </summary>
    public enum AbilityEventType
    {
        Granted,
        Removed,
        Activated,
        Ended,
        Cancelled,
        CooldownStarted,
        CooldownEnded
    }

    /// <summary>
    /// 技能事件
    /// </summary>
    public class AbilityEventArgs : CombatEventArgs
    {
        /// <summary>
        /// 事件编号
        /// </summary>
        public static readonly int EventId = typeof(AbilityEventArgs).GetHashCode();
        public override int Id => EventId;

        /// <summary>
        /// 关联技能
        /// </summary>
        public AbilitySpec Ability { get; private set; }

        /// <summary>
        /// 事件类型
        /// </summary>
        public AbilityEventType EventType { get; private set; }

        /// <summary>
        /// 创建技能事件
        /// </summary>
        public static AbilityEventArgs Create(object owner, AbilitySpec ability, AbilityEventType eventType)
        {
            var args = ReferencePool.Acquire<AbilityEventArgs>();
            args.Source = owner;
            args.Target = owner;
            args.Ability = ability;
            args.EventType = eventType;
            return args;
        }

        /// <summary>
        /// 清理对象状态用于回收
        /// </summary>
        public override void OnClearup()
        {
            Source = null;
            Target = null;
            Ability = null;
            EventType = AbilityEventType.Granted;
        }
    }

    /// <summary>
    /// 效果事件类型
    /// </summary>
    public enum EffectEventType
    {
        Applied,
        Removed,
        StackChanged,
        PeriodTick
    }

    /// <summary>
    /// 效果事件
    /// </summary>
    public class EffectEventArgs : CombatEventArgs
    {
        /// <summary>
        /// 事件编号
        /// </summary>
        public static readonly int EventId = typeof(EffectEventArgs).GetHashCode();
        public override int Id => EventId;

        /// <summary>
        /// 关联效果
        /// </summary>
        public ActiveEffect Effect { get; private set; }

        /// <summary>
        /// 事件类型
        /// </summary>
        public EffectEventType EventType { get; private set; }

        /// <summary>
        /// 旧层数
        /// </summary>
        public int OldStackCount { get; private set; }

        /// <summary>
        /// 新层数
        /// </summary>
        public int NewStackCount { get; private set; }

        /// <summary>
        /// 创建效果事件
        /// </summary>
        public static EffectEventArgs Create(object target, ActiveEffect effect, EffectEventType eventType, int oldStack = 0, int newStack = 0)
        {
            var args = ReferencePool.Acquire<EffectEventArgs>();
            args.Source = effect?.Source;
            args.Target = target;
            args.Effect = effect;
            args.EventType = eventType;
            args.OldStackCount = oldStack;
            args.NewStackCount = newStack;
            return args;
        }

        /// <summary>
        /// 清理对象状态用于回收
        /// </summary>
        public override void OnClearup()
        {
            Source = null;
            Target = null;
            Effect = null;
            EventType = EffectEventType.Applied;
            OldStackCount = 0;
            NewStackCount = 0;
        }
    }

    /// <summary>
    /// 属性变化事件
    /// </summary>
    public class AttributeChangedEventArgs : CombatEventArgs
    {
        /// <summary>
        /// 事件编号
        /// </summary>
        public static readonly int EventId = typeof(AttributeChangedEventArgs).GetHashCode();
        public override int Id => EventId;

        /// <summary>
        /// 属性名称
        /// </summary>
        public string AttributeName { get; private set; }

        /// <summary>
        /// 旧值
        /// </summary>
        public float OldValue { get; private set; }

        /// <summary>
        /// 新值
        /// </summary>
        public float NewValue { get; private set; }

        /// <summary>
        /// 创建属性变化事件
        /// </summary>
        public static AttributeChangedEventArgs Create(object owner, string attributeName, float oldValue, float newValue)
        {
            var args = ReferencePool.Acquire<AttributeChangedEventArgs>();
            args.Source = owner;
            args.Target = owner;
            args.AttributeName = attributeName;
            args.OldValue = oldValue;
            args.NewValue = newValue;
            return args;
        }

        /// <summary>
        /// 清理对象状态用于回收
        /// </summary>
        public override void OnClearup()
        {
            Source = null;
            Target = null;
            AttributeName = null;
            OldValue = 0f;
            NewValue = 0f;
        }
    }
}
