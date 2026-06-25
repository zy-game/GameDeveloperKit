using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Sound
{
    public sealed partial class SoundModule : GameModuleBase
    {
        /// <summary>
        /// 初始化 Settings。
        /// </summary>
        private void InitializeSettings()
        {
            foreach (SoundTrack track in Enum.GetValues(typeof(SoundTrack)))
            {
                m_TrackVolumes[track] = 1f;
            }

            m_TrackBindings.Clear();
            m_Snapshots.Clear();
            if (m_Settings == null)
            {
                return;
            }

            var trackBindings = m_Settings.Tracks;
            if (trackBindings != null)
            {
                foreach (var binding in trackBindings)
                {
                    if (binding == null)
                    {
                        continue;
                    }

                    m_TrackBindings[binding.Track] = binding;
                    m_TrackVolumes[binding.Track] = Mathf.Clamp01(binding.DefaultVolume);
                }
            }

            var snapshotBindings = m_Settings.Snapshots;
            if (snapshotBindings == null)
            {
                return;
            }

            foreach (var binding in snapshotBindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.Name) || binding.Snapshot == null)
                {
                    continue;
                }

                m_Snapshots[binding.Name] = binding.Snapshot;
            }
        }

        /// <summary>
        /// 应用 Settings 到已创建的 AudioSource。
        /// </summary>
        private void ApplySettingsToSources()
        {
            foreach (var source in m_PrimarySources.Values)
            {
                ApplyRuntimeSourceSettings(source);
            }

            foreach (var source in m_PooledSources)
            {
                ApplyRuntimeSourceSettings(source);
            }

            foreach (var source in m_ActiveSources.Values)
            {
                ApplyRuntimeSourceSettings(source);
            }
        }

        /// <summary>
        /// 应用 Runtime Source Settings。
        /// </summary>
        private void ApplyRuntimeSourceSettings(SoundRuntimeSource source)
        {
            if (source?.AudioSource == null)
            {
                return;
            }

            source.AudioSource.outputAudioMixerGroup = GetOutput(source.Track);
            ApplySourceVolume(source);
        }

        /// <summary>
        /// 创建 Primary Source。
        /// </summary>
        private void CreatePrimarySource(SoundTrack track)
        {
            m_PrimarySources[track] = CreateRuntimeSource(track, false);
        }

        /// <summary>
        /// 获取 Primary Source。
        /// </summary>
        private SoundRuntimeSource GetPrimarySource(SoundTrack track)
        {
            if (!m_PrimarySources.TryGetValue(track, out var source))
            {
                source = CreateRuntimeSource(track, false);
                m_PrimarySources[track] = source;
            }

            return source;
        }

        /// <summary>
        /// 获取 Pooled Source。
        /// </summary>
        private SoundRuntimeSource GetPooledSource(SoundTrack track)
        {
            foreach (var source in m_PooledSources)
            {
                if (!source.InUse && source.Track == track)
                {
                    return source;
                }
            }

            var pooledSource = CreateRuntimeSource(track, true);
            m_PooledSources.Add(pooledSource);
            return pooledSource;
        }

        /// <summary>
        /// 创建 Runtime Source。
        /// </summary>
        private SoundRuntimeSource CreateRuntimeSource(SoundTrack track, bool pooled)
        {
            var sourceObject = new GameObject($"{track}AudioSource");
            sourceObject.transform.SetParent(m_Root.transform, false);
            var audioSource = sourceObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.outputAudioMixerGroup = GetOutput(track);
            return new SoundRuntimeSource
            {
                AudioSource = audioSource,
                Track = track,
                Pooled = pooled,
            };
        }

        /// <summary>
        /// 获取 Output。
        /// </summary>
        private AudioMixerGroup GetOutput(SoundTrack track)
        {
            return m_TrackBindings.TryGetValue(track, out var binding) ? binding.Output : null;
        }

        /// <summary>
        /// 获取 Max Concurrent。
        /// </summary>
        private int GetMaxConcurrent(SoundTrack track)
        {
            if (m_TrackBindings.TryGetValue(track, out var binding) && binding.MaxConcurrent > 0)
            {
                return binding.MaxConcurrent;
            }

            return DefaultMaxConcurrent;
        }

        /// <summary>
        /// 获取 Track Volume。
        /// </summary>
        private float GetTrackVolume(SoundTrack track)
        {
            return m_TrackVolumes.TryGetValue(track, out var volume) ? volume : 1f;
        }

        /// <summary>
        /// 执行 Apply Source Volume。
        /// </summary>
        private void ApplySourceVolume(SoundRuntimeSource source)
        {
            if (source?.AudioSource == null)
            {
                return;
            }

            var masterVolume = HasVolumeParameter(SoundTrack.Master) ? 1f : GetTrackVolume(SoundTrack.Master);
            var trackVolume = HasVolumeParameter(source.Track) ? 1f : GetTrackVolume(source.Track);
            source.AudioSource.volume = Mathf.Clamp01(source.Volume * masterVolume * trackVolume);
        }

        /// <summary>
        /// 执行 Apply Mixer Volume。
        /// </summary>
        private void ApplyMixerVolume(SoundTrack track, float volume)
        {
            if (!m_TrackBindings.TryGetValue(track, out var binding) || string.IsNullOrWhiteSpace(binding.VolumeParameter))
            {
                return;
            }

            SetMixerFloat(binding.VolumeParameter, LinearToDecibel(volume));
        }

        /// <summary>
        /// 查询是否存在 Volume Parameter。
        /// </summary>
        private bool HasVolumeParameter(SoundTrack track)
        {
            return m_TrackBindings.TryGetValue(track, out var binding) &&
                   m_Settings != null &&
                   m_Settings.Mixer != null &&
                   !string.IsNullOrWhiteSpace(binding.VolumeParameter);
        }

        /// <summary>
        /// 执行 Linear To Decibel。
        /// </summary>
        private static float LinearToDecibel(float volume)
        {
            return volume <= 0f ? MinDecibel : Mathf.Log10(Mathf.Clamp01(volume)) * 20f;
        }
    }
}
