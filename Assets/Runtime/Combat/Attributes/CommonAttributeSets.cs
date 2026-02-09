namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 生命属性集
    /// </summary>
    public class HealthAttributeSet : AttributeSet
    {
        public const string Health = "Health";
        public const string MaxHealth = "MaxHealth";
        public const string HealthRegen = "HealthRegen";

        public HealthAttributeSet()
        {
            DefineAttribute(MaxHealth, 100f, 1f, float.MaxValue);
            DefineAttribute(Health, 100f, 0f, float.MaxValue);
            DefineAttribute(HealthRegen, 0f);
        }

        /// <summary>
        /// 当前生命值
        /// </summary>
        public float GetHealth() => GetCurrentValue(Health);

        /// <summary>
        /// 当前最大生命值
        /// </summary>
        public float GetMaxHealth() => GetCurrentValue(MaxHealth);

        /// <summary>
        /// 当前生命回复值
        /// </summary>
        public float GetHealthRegen() => GetCurrentValue(HealthRegen);

        /// <summary>
        /// 设置生命值基础值
        /// </summary>
        public void SetHealth(float value) => SetBaseValue(Health, value);

        /// <summary>
        /// 设置最大生命基础值
        /// </summary>
        public void SetMaxHealth(float value) => SetBaseValue(MaxHealth, value);
    }

    /// <summary>
    /// 战斗属性集
    /// </summary>
    public class CombatAttributeSet : AttributeSet
    {
        public const string Attack = "Attack";
        public const string Defense = "Defense";
        public const string CritRate = "CritRate";
        public const string CritDamage = "CritDamage";
        public const string AttackSpeed = "AttackSpeed";
        public const string MoveSpeed = "MoveSpeed";

        public CombatAttributeSet()
        {
            DefineAttribute(Attack, 10f, 0f);
            DefineAttribute(Defense, 5f, 0f);
            DefineAttribute(CritRate, 0.05f, 0f, 1f);
            DefineAttribute(CritDamage, 1.5f, 1f);
            DefineAttribute(AttackSpeed, 1f, 0.1f, 10f);
            DefineAttribute(MoveSpeed, 5f, 0f);
        }

        /// <summary>
        /// 当前攻击力
        /// </summary>
        public float GetAttack() => GetCurrentValue(Attack);

        /// <summary>
        /// 当前防御力
        /// </summary>
        public float GetDefense() => GetCurrentValue(Defense);

        /// <summary>
        /// 当前暴击率
        /// </summary>
        public float GetCritRate() => GetCurrentValue(CritRate);

        /// <summary>
        /// 当前暴击伤害倍率
        /// </summary>
        public float GetCritDamage() => GetCurrentValue(CritDamage);
    }

    /// <summary>
    /// 资源属性集（法力、能量等）
    /// </summary>
    public class ResourceAttributeSet : AttributeSet
    {
        public const string Mana = "Mana";
        public const string MaxMana = "MaxMana";
        public const string ManaRegen = "ManaRegen";
        public const string Energy = "Energy";
        public const string MaxEnergy = "MaxEnergy";

        public ResourceAttributeSet()
        {
            DefineAttribute(MaxMana, 100f, 0f);
            DefineAttribute(Mana, 100f, 0f);
            DefineAttribute(ManaRegen, 1f);
            DefineAttribute(MaxEnergy, 100f, 0f);
            DefineAttribute(Energy, 100f, 0f);
        }

        /// <summary>
        /// 当前法力值
        /// </summary>
        public float GetMana() => GetCurrentValue(Mana);

        /// <summary>
        /// 当前最大法力
        /// </summary>
        public float GetMaxMana() => GetCurrentValue(MaxMana);

        /// <summary>
        /// 当前能量值
        /// </summary>
        public float GetEnergy() => GetCurrentValue(Energy);

        /// <summary>
        /// 当前最大能量
        /// </summary>
        public float GetMaxEnergy() => GetCurrentValue(MaxEnergy);
    }
}
