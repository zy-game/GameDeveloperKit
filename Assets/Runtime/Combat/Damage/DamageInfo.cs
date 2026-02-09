using System;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 伤害类型
    /// </summary>
    [Flags]
    public enum DamageType
    {
        None = 0,
        Physical = 1 << 0,
        Magical = 1 << 1,
        Fire = 1 << 2,
        Ice = 1 << 3,
        Lightning = 1 << 4,
        Poison = 1 << 5,
        True = 1 << 6,
        Heal = 1 << 7
    }

    /// <summary>
    /// 伤害信息
    /// </summary>
    public struct DamageInfo
    {
        /// <summary>
        /// 基础伤害
        /// </summary>
        public float BaseDamage;

        /// <summary>
        /// 最终伤害
        /// </summary>
        public float FinalDamage;

        /// <summary>
        /// 伤害类型
        /// </summary>
        public DamageType Type;

        /// <summary>
        /// 来源对象
        /// </summary>
        public object Source;

        /// <summary>
        /// 施加者对象
        /// </summary>
        public object Causer;

        /// <summary>
        /// 是否暴击
        /// </summary>
        public bool IsCritical;

        /// <summary>
        /// 暴击倍率
        /// </summary>
        public float CritMultiplier;

        /// <summary>
        /// 是否被格挡
        /// </summary>
        public bool IsBlocked;

        /// <summary>
        /// 是否被闪避
        /// </summary>
        public bool IsDodged;

        /// <summary>
        /// 伤害标签
        /// </summary>
        public TagContainer Tags;

        /// <summary>
        /// 创建伤害信息
        /// </summary>
        public static DamageInfo Create(float damage, DamageType type, object source = null, object causer = null)
        {
            return new DamageInfo
            {
                BaseDamage = damage,
                FinalDamage = damage,
                Type = type,
                Source = source,
                Causer = causer,
                IsCritical = false,
                CritMultiplier = 1f,
                IsBlocked = false,
                IsDodged = false,
                Tags = new TagContainer()
            };
        }
    }
}
