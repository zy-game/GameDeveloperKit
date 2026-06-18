using System;
using UnityEngine;
using UnityEngine.Audio;

namespace GameDeveloperKit.Sound
{
    /// <summary>
    /// 定义 Sound Snapshot Binding 类型。
    /// </summary>
    [Serializable]
    public sealed class SoundSnapshotBinding
    {
        [SerializeField] private string m_Name;
        [SerializeField] private AudioMixerSnapshot m_Snapshot;

        /// <summary>
        /// 存储 Name。
        /// </summary>
        public string Name => m_Name;

        /// <summary>
        /// 存储 Snapshot。
        /// </summary>
        public AudioMixerSnapshot Snapshot => m_Snapshot;
    }
}
