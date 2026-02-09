using GameDeveloperKit.Config;
using UnityEngine;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 角色配置 - 纯框架层配置
    /// 不包含业务属性（Team、Type 等由使用者扩展）
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Character Config", fileName = "CharacterConfig")]
    public class CharacterConfig : ScriptableObject, IConfigData
    {
        [Header("Basic")]
        [Tooltip("角色名称")]
        public string CharacterName = "Character";
        
        [Tooltip("角色模型预制体")]
        public GameObject Prefab;
        
        [Header("Health Attributes")]
        [Tooltip("最大生命值")]
        public float MaxHealth = 100f;
        
        [Tooltip("生命回复速度（每秒）")]
        public float HealthRegen = 0f;
        
        [Header("Combat Attributes")]
        [Tooltip("攻击力")]
        public float Attack = 10f;
        
        [Tooltip("防御力")]
        public float Defense = 5f;
        
        [Tooltip("暴击率（0-1）")]
        [Range(0f, 1f)]
        public float CritRate = 0.05f;
        
        [Tooltip("暴击伤害倍率")]
        public float CritDamage = 1.5f;
        
        [Header("Resource Attributes")]
        [Tooltip("最大法力值")]
        public float MaxMana = 100f;
        
        [Tooltip("法力回复速度（每秒）")]
        public float ManaRegen = 1f;
        
        [Header("Movement Attributes")]
        [Tooltip("是否启用移动")]
        public bool EnableMovement = true;
        
        [Tooltip("移动速度（m/s）")]
        public float MoveSpeed = 5f;
        
        [Tooltip("跳跃高度（m）")]
        public float JumpHeight = 1.5f;
        
        [Tooltip("重力加速度（m/s²）")]
        public float Gravity = -20f;
        
        [Tooltip("加速度")]
        public float Acceleration = 10f;
        
        [Tooltip("质量（kg）")]
        public float Mass = 70f;
        
        [Header("Abilities")]
        [Tooltip("初始技能列表")]
        public AbilityBase[] InitialAbilities;
        
        [Header("Movement Settings")]
        [Tooltip("奔跑阈值速度（m/s）")]
        public float RunThreshold = 3f;
        
        [Tooltip("旋转平滑度")]
        public float RotationSharpness = 10f;
        
        [Tooltip("冲刺速度（m/s）")]
        public float DashSpeed = 15f;
        
        [Tooltip("冲刺距离（m）")]
        public float DashDistance = 5f;
        
        [Tooltip("冲刺冷却时间（s）")]
        public float DashCooldown = 1f;

        /// <summary>
        /// 配置ID（使用资源名称）
        /// </summary>
        public string Id => name;
    }
}
