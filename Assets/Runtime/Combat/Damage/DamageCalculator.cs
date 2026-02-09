using System;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 伤害接收者接口
    /// </summary>
    public interface IDamageReceiver
    {
        /// <summary>
        /// 获取当前生命值
        /// </summary>
        float GetHealth();

        /// <summary>
        /// 获取最大生命值
        /// </summary>
        float GetMaxHealth();

        /// <summary>
        /// 获取防御力
        /// </summary>
        float GetDefense();

        /// <summary>
        /// 获取指定类型抗性
        /// </summary>
        float GetResistance(DamageType type);

        /// <summary>
        /// 应用伤害
        /// </summary>
        void ApplyDamage(DamageInfo damage);

        /// <summary>
        /// 是否已死亡
        /// </summary>
        bool IsDead { get; }
    }

    /// <summary>
    /// 伤害计算器
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary>
        /// 自定义伤害计算器
        /// </summary>
        public static Func<DamageInfo, IDamageReceiver, DamageInfo> CustomCalculator;

        /// <summary>
        /// 计算最终伤害
        /// </summary>
        public static DamageInfo Calculate(DamageInfo damage, IDamageReceiver target)
        {
            if (CustomCalculator != null)
            {
                return CustomCalculator(damage, target);
            }

            return DefaultCalculate(damage, target);
        }

        /// <summary>
        /// 默认伤害计算
        /// </summary>
        public static DamageInfo DefaultCalculate(DamageInfo damage, IDamageReceiver target)
        {
            if (target == null)
            {
                damage.FinalDamage = damage.BaseDamage;
                return damage;
            }

            float finalDamage = damage.BaseDamage;

            if (damage.IsCritical)
            {
                finalDamage *= damage.CritMultiplier;
            }

            if ((damage.Type & DamageType.True) == 0)
            {
                float defense = target.GetDefense();
                float resistance = target.GetResistance(damage.Type);

                float defenseReduction = defense / (defense + 100f);
                finalDamage *= (1f - defenseReduction);

                finalDamage *= (1f - resistance);
            }

            damage.FinalDamage = Math.Max(0f, finalDamage);
            return damage;
        }

        /// <summary>
        /// 应用伤害到目标
        /// </summary>
        public static void ApplyDamage(IDamageReceiver target, DamageInfo damage)
        {
            if (target == null || target.IsDead)
                return;

            damage = Calculate(damage, target);

            if (damage.IsDodged || damage.IsBlocked)
                return;

            target.ApplyDamage(damage);
        }

        /// <summary>
        /// 检查是否暴击
        /// </summary>
        public static bool RollCritical(float critRate)
        {
            return UnityEngine.Random.value < critRate;
        }
    }
}
