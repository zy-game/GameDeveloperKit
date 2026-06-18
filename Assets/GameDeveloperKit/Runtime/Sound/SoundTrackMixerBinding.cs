using System;
using UnityEngine;
using UnityEngine.Audio;

namespace GameDeveloperKit.Sound
{
    /// <summary>
    /// 定义 Sound Track Mixer Binding 类型。
    /// </summary>
    [Serializable]
    public sealed class SoundTrackMixerBinding
    {
        [SerializeField] private SoundTrack m_Track;
        [SerializeField] private AudioMixerGroup m_Output;
        [SerializeField] private string m_VolumeParameter;
        [SerializeField] private float m_DefaultVolume = 1f;
        [SerializeField] private int m_MaxConcurrent = 16;

        /// <summary>
        /// 存储 Track。
        /// </summary>
        public SoundTrack Track => m_Track;

        /// <summary>
        /// 存储 Output。
        /// </summary>
        public AudioMixerGroup Output => m_Output;

        /// <summary>
        /// 存储 Volume Parameter。
        /// </summary>
        public string VolumeParameter => m_VolumeParameter;

        /// <summary>
        /// 存储 Default Volume。
        /// </summary>
        public float DefaultVolume => m_DefaultVolume;

        /// <summary>
        /// 存储 Max Concurrent。
        /// </summary>
        public int MaxConcurrent => m_MaxConcurrent;
    }
}
