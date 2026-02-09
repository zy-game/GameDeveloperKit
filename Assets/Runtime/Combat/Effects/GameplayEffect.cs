using System;
using GameDeveloperKit.Config;
using UnityEngine;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 效果持续类型
    /// </summary>
    public enum EffectDurationType
    {
        /// <summary>
        /// 即时效果
        /// </summary>
        Instant,

        /// <summary>
        /// 持续效果
        /// </summary>
        Duration,

        /// <summary>
        /// 无限效果
        /// </summary>
        Infinite
    }

    /// <summary>
    /// 效果堆叠策略
    /// </summary>
    public enum EffectStackPolicy
    {
        /// <summary>
        /// 不堆叠
        /// </summary>
        None,

        /// <summary>
        /// 刷新持续时间
        /// </summary>
        Refresh,

        /// <summary>
        /// 堆叠层数
        /// </summary>
        Stack,

        /// <summary>
        /// 覆盖
        /// </summary>
        Override
    }

    /// <summary>
    /// 游戏效果定义
    /// </summary>
    [CreateAssetMenu(fileName = "NewEffect", menuName = "Combat/Gameplay Effect")]
    public class GameplayEffect : ScriptableObject, IConfigData
    {
        [Header("基本信息")]
        /// <summary>
        /// 效果名称
        /// </summary>
        public string EffectName;

        /// <summary>
        /// 效果描述
        /// </summary>
        public string Description;

        [Header("持续时间")]
        /// <summary>
        /// 持续类型
        /// </summary>
        public EffectDurationType DurationType = EffectDurationType.Instant;

        /// <summary>
        /// 持续时长
        /// </summary>
        public float Duration = 0f;

        [Header("周期效果")]
        /// <summary>
        /// 是否周期触发
        /// </summary>
        public bool IsPeriodic;

        /// <summary>
        /// 周期间隔
        /// </summary>
        public float Period = 1f;

        /// <summary>
        /// 是否在应用时立刻执行一次
        /// </summary>
        public bool ExecuteOnApply = true;

        [Header("堆叠")]
        /// <summary>
        /// 堆叠策略
        /// </summary>
        public EffectStackPolicy StackPolicy = EffectStackPolicy.None;

        /// <summary>
        /// 最大堆叠层数
        /// </summary>
        public int MaxStacks = 1;

        [Header("属性修改")]
        /// <summary>
        /// 属性修改器配置
        /// </summary>
        public EffectModifierDef[] Modifiers;

        [Header("标签")]
        /// <summary>
        /// 施加时授予的标签
        /// </summary>
        public string[] GrantedTags;

        /// <summary>
        /// 施加所需标签
        /// </summary>
        public string[] RequiredTags;

        /// <summary>
        /// 施加阻塞标签
        /// </summary>
        public string[] BlockedTags;

        /// <summary>
        /// 施加时移除的效果标签
        /// </summary>
        public string[] RemoveEffectsWithTags;

        [Header("表现")]
        /// <summary>
        /// 表现资源
        /// </summary>
        public CueDefinition[] Cues;

        /// <summary>
        /// 配置ID（使用资源名称）
        /// </summary>
        public string Id => name;
    }

    /// <summary>
    /// 效果修改器定义
    /// </summary>
    [Serializable]
    public class EffectModifierDef
    {
        /// <summary>
        /// 作用属性名
        /// </summary>
        public string AttributeName;

        /// <summary>
        /// 修改方式
        /// </summary>
        public ModifierOp Operation;

        /// <summary>
        /// 修改数值
        /// </summary>
        public float Value;

        /// <summary>
        /// 优先级
        /// </summary>
        public int Priority;
    }
}
