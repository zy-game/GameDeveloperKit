using System;
using UnityEngine;
using UnityEngine.Audio;

namespace GameDeveloperKit.Sound
{
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
}
