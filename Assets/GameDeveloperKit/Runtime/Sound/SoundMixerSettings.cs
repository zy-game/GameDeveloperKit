using System;
using UnityEngine;
using UnityEngine.Audio;

namespace GameDeveloperKit.Sound
{
    public sealed class SoundMixerSettings : ScriptableObject
    {
        [SerializeField] private AudioMixer m_Mixer;
        [SerializeField] private SoundTrackMixerBinding[] m_Tracks;
        [SerializeField] private SoundSnapshotBinding[] m_Snapshots;

        public AudioMixer Mixer => m_Mixer;

        public SoundTrackMixerBinding[] Tracks => m_Tracks;

        public SoundSnapshotBinding[] Snapshots => m_Snapshots;
    }

    [Serializable]
    public sealed class SoundTrackMixerBinding
    {
        [SerializeField] private SoundTrack m_Track;
        [SerializeField] private AudioMixerGroup m_Output;
        [SerializeField] private string m_VolumeParameter;
        [SerializeField] private float m_DefaultVolume = 1f;
        [SerializeField] private int m_MaxConcurrent = 16;

        public SoundTrack Track => m_Track;

        public AudioMixerGroup Output => m_Output;

        public string VolumeParameter => m_VolumeParameter;

        public float DefaultVolume => m_DefaultVolume;

        public int MaxConcurrent => m_MaxConcurrent;
    }

    [Serializable]
    public sealed class SoundSnapshotBinding
    {
        [SerializeField] private string m_Name;
        [SerializeField] private AudioMixerSnapshot m_Snapshot;

        public string Name => m_Name;

        public AudioMixerSnapshot Snapshot => m_Snapshot;
    }
}
