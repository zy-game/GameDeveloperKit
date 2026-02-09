using System;
using GameDeveloperKit.Config;
using UnityEngine;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 表现定义资源 - 可视化配置特效、音效等
    /// </summary>
    [CreateAssetMenu(fileName = "NewCue", menuName = "Combat/Cue Definition")]
    public class CueDefinition : ScriptableObject, IConfigData
    {
        [Header("基本信息")]
        /// <summary>
        /// 表现名称
        /// </summary>
        public string CueName;

        /// <summary>
        /// 表现描述
        /// </summary>
        [TextArea(2, 4)]
        public string Description;

        [Header("触发标签")]
        [Tooltip("响应的Cue标签，支持多个")]
        /// <summary>
        /// 响应标签列表
        /// </summary>
        public string[] CueTags;

        [Header("视觉效果")]
        [Tooltip("生成的特效预制体")]
        /// <summary>
        /// 特效预制体
        /// </summary>
        public GameObject EffectPrefab;
        [Tooltip("特效生成位置")]
        /// <summary>
        /// 特效生成位置
        /// </summary>
        public CueSpawnLocation SpawnLocation = CueSpawnLocation.Target;
        [Tooltip("特效位置偏移")]
        /// <summary>
        /// 位置偏移
        /// </summary>
        public Vector3 PositionOffset;
        [Tooltip("特效旋转偏移")]
        /// <summary>
        /// 旋转偏移
        /// </summary>
        public Vector3 RotationOffset;
        [Tooltip("特效缩放")]
        /// <summary>
        /// 缩放
        /// </summary>
        public Vector3 Scale = Vector3.one;
        [Tooltip("是否跟随目标")]
        /// <summary>
        /// 是否附加到目标
        /// </summary>
        public bool AttachToTarget;

        [Header("音效")]
        [Tooltip("播放的音效")]
        /// <summary>
        /// 音效资源
        /// </summary>
        public AudioClip SoundEffect;
        [Tooltip("音量")]
        /// <summary>
        /// 音量
        /// </summary>
        [Range(0f, 1f)]
        public float Volume = 1f;
        [Tooltip("音调")]
        /// <summary>
        /// 音调
        /// </summary>
        [Range(0.5f, 2f)]
        public float Pitch = 1f;
        [Tooltip("是否3D音效")]
        /// <summary>
        /// 是否为3D音效
        /// </summary>
        public bool Is3DSound = true;

        [Header("时间")]
        [Tooltip("延迟执行时间")]
        /// <summary>
        /// 延迟时间
        /// </summary>
        public float Delay;
        [Tooltip("特效持续时间（0表示自动销毁或永久）")]
        /// <summary>
        /// 持续时间
        /// </summary>
        public float Duration;

        [Header("条件")]
        [Tooltip("只在这些标签存在时触发")]
        /// <summary>
        /// 必需标签
        /// </summary>
        public string[] RequiredTags;
        [Tooltip("这些标签存在时不触发")]
        /// <summary>
        /// 阻塞标签
        /// </summary>
        public string[] BlockedTags;

        [Header("自定义处理器")]
        [Tooltip("自定义处理器类型的完整名称（包含命名空间），留空则使用默认处理")]
        /// <summary>
        /// 自定义处理器类型名
        /// </summary>
        public string CustomHandlerTypeName;

        /// <summary>
        /// 配置ID（使用资源名称）
        /// </summary>
        public string Id => name;
    }

    /// <summary>
    /// 特效生成位置
    /// </summary>
    public enum CueSpawnLocation
    {
        [Tooltip("在目标位置生成")]
        Target,
        [Tooltip("在来源位置生成")]
        Source,
        [Tooltip("在指定世界坐标生成")]
        WorldLocation,
        [Tooltip("在来源到目标之间生成")]
        BetweenSourceAndTarget
    }
}
