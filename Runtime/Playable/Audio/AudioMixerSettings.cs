using System;
using UnityEngine;
using UnityEngine.Audio;

namespace GameDeveloperKit.Playable
{
    [Serializable]
    public sealed class AudioMixerSettings
    {
        [SerializeField] private AudioMixer m_Mixer;
        [SerializeField] private AudioTrackMixerBinding[] m_Tracks;
        [SerializeField] private AudioSnapshotBinding[] m_Snapshots;

        public AudioMixer Mixer => m_Mixer;

        public AudioTrackMixerBinding[] Tracks => m_Tracks;

        public AudioSnapshotBinding[] Snapshots => m_Snapshots;
    }

    [Serializable]
    public sealed class AudioTrackMixerBinding
    {
        [SerializeField] private AudioTrack m_Track;
        [SerializeField] private AudioMixerGroup m_Output;
        [SerializeField] private string m_VolumeParameter;
        [SerializeField] private float m_DefaultVolume = 1f;
        [SerializeField] private int m_MaxConcurrent = 16;

        public AudioTrack Track => m_Track;

        public AudioMixerGroup Output => m_Output;

        public string VolumeParameter => m_VolumeParameter;

        public float DefaultVolume => m_DefaultVolume;

        public int MaxConcurrent => m_MaxConcurrent;
    }

    [Serializable]
    public sealed class AudioSnapshotBinding
    {
        [SerializeField] private string m_Name;
        [SerializeField] private AudioMixerSnapshot m_Snapshot;

        public string Name => m_Name;

        public AudioMixerSnapshot Snapshot => m_Snapshot;
    }
}
