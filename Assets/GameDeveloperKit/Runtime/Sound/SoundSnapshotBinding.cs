using System;
using UnityEngine;
using UnityEngine.Audio;

namespace GameDeveloperKit.Sound
{
    [Serializable]
    public sealed class SoundSnapshotBinding
    {
        [SerializeField] private string m_Name;
        [SerializeField] private AudioMixerSnapshot m_Snapshot;
        public string Name => m_Name;
        public AudioMixerSnapshot Snapshot => m_Snapshot;
    }
}
