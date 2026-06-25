using System;
using UnityEngine;
using UnityEngine.Audio;

namespace GameDeveloperKit.Sound
{
    /// <summary>
    /// 定义 Sound Mixer Settings 类型。
    /// </summary>
    [Serializable]
    public sealed class SoundMixerSettings
    {
        [SerializeField] private AudioMixer m_Mixer;
        [SerializeField] private SoundTrackMixerBinding[] m_Tracks;
        [SerializeField] private SoundSnapshotBinding[] m_Snapshots;

        /// <summary>
        /// 存储 Mixer。
        /// </summary>
        public AudioMixer Mixer => m_Mixer;

        /// <summary>
        /// 存储 Tracks。
        /// </summary>
        public SoundTrackMixerBinding[] Tracks => m_Tracks;

        /// <summary>
        /// 存储 Snapshots。
        /// </summary>
        public SoundSnapshotBinding[] Snapshots => m_Snapshots;
    }

}
