using UnityEngine;
using UnityEngine.Audio;

namespace GameDeveloperKit.Audio
{
    /// <summary>
    /// 音频配置
    /// </summary>
    public class AudioConfig
    {
        /// <summary>
        /// 音量（0-1）
        /// </summary>
        public float Volume { get; set; } = 1f;

        /// <summary>
        /// 音调（0.5-2）
        /// </summary>
        public float Pitch { get; set; } = 1f;

        /// <summary>
        /// 是否循环
        /// </summary>
        public bool Loop { get; set; } = false;

        /// <summary>
        /// 3D 空间混合（0 = 2D, 1 = 3D）
        /// </summary>
        public float SpatialBlend { get; set; } = 0f;

        /// <summary>
        /// 优先级（0-256，越大越优先）
        /// </summary>
        public int Priority { get; set; } = 128;

        /// <summary>
        /// Mixer Group
        /// </summary>
        public AudioMixerGroup MixerGroup { get; set; }

        /// <summary>
        /// 音效组名称（用于分组管理）
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// 3D 位置（仅当 SpatialBlend > 0 时有效）
        /// </summary>
        public Vector3? Position { get; set; }

        /// <summary>
        /// 父节点（用于跟随对象）
        /// </summary>
        public Transform Parent { get; set; }

        /// <summary>
        /// 淡入时间（秒）
        /// </summary>
        public float FadeInDuration { get; set; } = 0f;

        /// <summary>
        /// 淡出时间（秒）
        /// </summary>
        public float FadeOutDuration { get; set; } = 0f;

        /// <summary>
        /// 播放完成后自动销毁
        /// </summary>
        public bool AutoDestroy { get; set; } = true;

        /// <summary>
        /// 最小距离（3D 音效）
        /// </summary>
        public float MinDistance { get; set; } = 1f;

        /// <summary>
        /// 最大距离（3D 音效）
        /// </summary>
        public float MaxDistance { get; set; } = 500f;

        /// <summary>
        /// 音调变化范围（±）
        /// </summary>
        public float PitchVariation { get; set; } = 0f;

        /// <summary>
        /// 音量变化范围（±）
        /// </summary>
        public float VolumeVariation { get; set; } = 0f;

        /// <summary>
        /// 起始延迟（秒）
        /// </summary>
        public float StartDelay { get; set; } = 0f;

        /// <summary>
        /// 是否启用频谱分析
        /// </summary>
        public bool EnableSpectrum { get; set; } = false;

        /// <summary>
        /// FFT 窗口大小（频谱分析）
        /// </summary>
        public FFTWindow FFTWindow { get; set; } = FFTWindow.BlackmanHarris;
    }
}
