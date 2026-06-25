using System;
using UnityEngine;
using UnityEngine.Audio;

namespace GameDeveloperKit.Sound
{
    [Serializable]
    public sealed class SoundMixerSettings
    {
        [SerializeField] private AudioMixer m_Mixer;
        [SerializeField] private SoundTrackMixerBinding[] m_Tracks;
        [SerializeField] private SoundSnapshotBinding[] m_Snapshots;
        public AudioMixer Mixer => m_Mixer;
        public SoundTrackMixerBinding[] Tracks => m_Tracks;
        public SoundSnapshotBinding[] Snapshots => m_Snapshots;
    }

}
