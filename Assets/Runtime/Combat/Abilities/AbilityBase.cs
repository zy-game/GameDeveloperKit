using System;
using GameDeveloperKit.Config;
using UnityEngine;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 技能基类定义
    /// </summary>
    [CreateAssetMenu(fileName = "NewAbility", menuName = "Combat/Ability")]
    public class AbilityBase : ScriptableObject, IConfigData
    {
        [Header("基本信息")]
        /// <summary>
        /// 技能名称
        /// </summary>
        public string AbilityName;

        /// <summary>
        /// 技能描述
        /// </summary>
        public string Description;

        /// <summary>
        /// 技能图标
        /// </summary>
        public Sprite Icon;

        [Header("冷却")]
        /// <summary>
        /// 冷却时间
        /// </summary>
        public float Cooldown = 1f;

        [Header("消耗")]
        /// <summary>
        /// 消耗列表
        /// </summary>
        public AbilityCost[] Costs;

        [Header("标签")]
        /// <summary>
        /// 技能自身标签
        /// </summary>
        public string[] AbilityTags;

        /// <summary>
        /// 激活所需标签
        /// </summary>
        public string[] ActivationRequiredTags;

        /// <summary>
        /// 激活阻塞标签
        /// </summary>
        public string[] ActivationBlockedTags;

        /// <summary>
        /// 激活时取消的技能标签
        /// </summary>
        public string[] CancelAbilitiesWithTags;

        /// <summary>
        /// 激活时阻止的技能标签
        /// </summary>
        public string[] BlockAbilitiesWithTags;

        /// <summary>
        /// 激活时授予的标签
        /// </summary>
        public string[] ActivationGrantedTags;

        [Header("效果")]
        /// <summary>
        /// 激活时应用的效果
        /// </summary>
        public GameplayEffect[] EffectsToApply;

        /// <summary>
        /// 冷却效果
        /// </summary>
        public GameplayEffect CooldownEffect;

        [Header("表现")]
        /// <summary>
        /// 激活表现资源
        /// </summary>
        public CueDefinition[] ActivationCues;

        /// <summary>
        /// 配置ID（使用资源名称）
        /// </summary>
        public string Id => name;
    }

    /// <summary>
    /// 技能消耗定义
    /// </summary>
    [Serializable]
    public class AbilityCost
    {
        /// <summary>
        /// 消耗的属性名
        /// </summary>
        public string AttributeName;

        /// <summary>
        /// 消耗数值
        /// </summary>
        public float Cost;
    }
}
