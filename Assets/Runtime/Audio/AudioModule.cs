using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Log;
using UnityEngine;
using UnityEngine.Audio;
using ZLinq;

namespace GameDeveloperKit.Audio
{
    public sealed class AudioModule : IModule, IAudioManager
    {
        private readonly List<AudioTrack> _activeTracks = new List<AudioTrack>();
        private readonly Dictionary<string, AudioGroup> _groups = new Dictionary<string, AudioGroup>();

        private Transform _audioRoot;
        private AudioSourcePool _audioSourcePool;
        private AudioMixerController _mixerController;
        private float _masterVolume = 1f;
        private bool _isMuted;

        public void OnStartup()
        {
            var rootObj = new GameObject("AudioModule");
            rootObj.AddComponent<AudioListener>();
            UnityEngine.Object.DontDestroyOnLoad(rootObj);
            _audioRoot = rootObj.transform;

            _audioSourcePool = new AudioSourcePool(_audioRoot, initialSize: 10);
            
            // 注册调试面板
            if (Game.Debug is LoggerModule loggerModule)
            {
                loggerModule.RegisterPanel(new AudioDebugPanel());
            }
        }

        public void OnUpdate(float elapseSeconds)
        {
            // 清理已停止的track
            for (int i = _activeTracks.Count - 1; i >= 0; i--)
            {
                var track = _activeTracks[i];

                if (!track.IsPlaying && !track.IsPaused)
                {
                    _activeTracks.RemoveAt(i);
                    track.Dispose();
                }
            }

            // 使用ZLinq避免装箱
            foreach (var group in _groups.Values.AsValueEnumerable())
            {
                group.Update();
            }
        }

        public void OnClearup()
        {
            StopAll();

            _activeTracks.Clear();
            _groups.Clear();

            _audioSourcePool?.Clear();

            if (_audioRoot != null)
            {
                UnityEngine.Object.Destroy(_audioRoot.gameObject);
                _audioRoot = null;
            }
        }

        public void SetAudioMixer(AudioMixer mixer)
        {
            _mixerController = new AudioMixerController(mixer);
        }

        public async UniTask<AudioTrack> PlayAsync(string clipName, AudioConfig config = null)
        {
            config ??= new AudioConfig();

            if (config.MixerGroup == null && _mixerController != null)
            {
                config.MixerGroup = _mixerController.GetGroup("Master");
            }

            var track = await AudioTrack.CreateAsync(clipName, config, config.Parent ?? _audioRoot, _audioSourcePool);

            if (track == null) return null;

            // UniTask主线程恢复，不需要lock
            _activeTracks.Add(track);

            if (!string.IsNullOrEmpty(config.GroupName))
            {
                var group = GetOrCreateGroup(config.GroupName);
                group.AddTrack(track);
            }

            track.Play();

            return track;
        }

        public AudioTrack Play(AudioClip clip, AudioConfig config = null)
        {
            if (clip == null) return null;

            config ??= new AudioConfig();

            if (config.MixerGroup == null && _mixerController != null)
            {
                config.MixerGroup = _mixerController.GetGroup("Master");
            }

            var track = AudioTrack.Create(clip, config, config.Parent ?? _audioRoot, _audioSourcePool);

            if (track == null) return null;

            _activeTracks.Add(track);

            if (!string.IsNullOrEmpty(config.GroupName))
            {
                var group = GetOrCreateGroup(config.GroupName);
                group.AddTrack(track);
            }

            track.Play();

            return track;
        }

        public void Stop(AudioTrack track)
        {
            if (track == null) return;

            track.Stop();
            _activeTracks.Remove(track);
        }

        public void StopAll()
        {
            foreach (var track in _activeTracks)
            {
                track.Stop();
                track.Dispose();
            }

            _activeTracks.Clear();
        }

        public void Pause(AudioTrack track)
        {
            track?.Pause();
        }

        public void PauseAll()
        {
            foreach (var track in _activeTracks)
            {
                track.Pause();
            }
        }

        public void Resume(AudioTrack track)
        {
            track?.Resume();
        }

        public void ResumeAll()
        {
            foreach (var track in _activeTracks)
            {
                track.Resume();
            }
        }

        public void SetMasterVolume(float volume)
        {
            _masterVolume = Mathf.Clamp01(volume);
            _mixerController?.SetMasterVolume(volume);
        }

        public void SetGroupVolume(string groupName, float volume)
        {
            _mixerController?.SetVolume($"{groupName}Volume", volume);
        }

        public float GetMasterVolume()
        {
            return _mixerController?.GetMasterVolume() ?? _masterVolume;
        }

        public float GetGroupVolume(string groupName)
        {
            return _mixerController?.GetVolume($"{groupName}Volume") ?? 1f;
        }

        public void SetMute(bool mute)
        {
            _isMuted = mute;
            _mixerController?.SetMasterVolume(mute ? 0f : _masterVolume);
        }

        public AudioMixerGroup GetMixerGroup(string groupName)
        {
            return _mixerController?.GetGroup(groupName);
        }

        public AudioGroup CreateGroup(string groupName)
        {
            if (_groups.ContainsKey(groupName))
            {
                return _groups[groupName];
            }

            var group = new AudioGroup(groupName);
            _groups[groupName] = group;
            return group;
        }

        public AudioGroup GetGroup(string groupName)
        {
            return _groups.TryGetValue(groupName, out var group) ? group : null;
        }

        public void StopGroup(string groupName)
        {
            var group = GetGroup(groupName);
            group?.StopAll();
        }

        public void PauseGroup(string groupName)
        {
            var group = GetGroup(groupName);
            group?.PauseAll();
        }

        public void ResumeGroup(string groupName)
        {
            var group = GetGroup(groupName);
            group?.ResumeAll();
        }

        public int GetActiveTrackCount()
        {
            return _activeTracks.Count;
        }

        public (int available, int total) GetPoolStats()
        {
            return (_audioSourcePool.AvailableCount, _audioSourcePool.TotalCreated);
        }

        internal void ReturnAudioSource(AudioSource audioSource)
        {
            _audioSourcePool?.Return(audioSource);
        }

        private AudioGroup GetOrCreateGroup(string groupName)
        {
            if (!_groups.TryGetValue(groupName, out var group))
            {
                group = new AudioGroup(groupName);
                _groups[groupName] = group;
            }

            return group;
        }
    }
}
