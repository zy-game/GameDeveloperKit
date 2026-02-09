using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;

namespace GameDeveloperKit.Audio
{
    /// <summary>
    /// 音轨（管理单个音效的完整生命周期）
    /// </summary>
    public class AudioTrack : IDisposable
    {
        private GameObject _gameObject;
        private AudioSource _audioSource;
        private AudioConfig _config;
        private string _clipName;
        private AssetHandle<AudioClip> _assetHandle;
        private bool _isDisposed;
        private float _targetVolume;
        private bool _isFading;
        private bool _isLoaded;
        private bool _isFromPool;
        private float[] _spectrumData;

        public bool IsPlaying => _audioSource != null && _audioSource.isPlaying;
        public bool IsPaused { get; private set; }
        public bool IsLoaded => _isLoaded;
        public AudioClip Clip => _audioSource?.clip;
        public string ClipName => _clipName;
        public string GroupName => _config?.GroupName;

        public float Time
        {
            get => _audioSource?.time ?? 0f;
            set
            {
                if (_audioSource != null)
                {
                    _audioSource.time = value;
                }
            }
        }

        public float Volume
        {
            get => _audioSource?.volume ?? 0f;
            set
            {
                if (_audioSource != null)
                {
                    _audioSource.volume = Mathf.Clamp01(value);
                    _targetVolume = value;
                }
            }
        }

        public float Pitch
        {
            get => _audioSource?.pitch ?? 1f;
            set
            {
                if (_audioSource != null)
                {
                    _audioSource.pitch = Mathf.Clamp(value, 0.5f, 2f);
                }
            }
        }

        private AudioTrack()
        {
        }

        internal static async UniTask<AudioTrack> CreateAsync(string clipName, AudioConfig config, Transform parent, AudioSourcePool pool = null)
        {
            var track = new AudioTrack();
            track._clipName = clipName;
            track._config = config ?? new AudioConfig();

            try
            {
                track._assetHandle = await Game.Resource.LoadAssetAsync<AudioClip>(clipName);

                if (track._assetHandle == null || track._assetHandle.Asset == null)
                {
                    Game.Debug.Error($"Failed to load audio clip '{clipName}': Asset is null");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Game.Debug.Error($"Failed to load audio clip '{clipName}': {ex.Message}");
                return null;
            }

            var clip = track._assetHandle.Asset;
            track._isLoaded = true;

            if (pool != null)
            {
                track._audioSource = pool.Get();
                track._isFromPool = true;
                track._gameObject = track._audioSource.gameObject;
            }
            else
            {
                track._gameObject = new GameObject($"AudioTrack_{clip.name}");
                track._audioSource = track._gameObject.AddComponent<AudioSource>();
                track._isFromPool = false;
            }

            if (parent != null)
            {
                track._gameObject.transform.SetParent(parent);
            }

            track.ConfigureAudioSource(clip);
            track.ApplyVariations();

            if (track._config.EnableSpectrum)
            {
                track._spectrumData = new float[256];
            }

            return track;
        }

        internal static AudioTrack Create(AudioClip clip, AudioConfig config, Transform parent, AudioSourcePool pool = null)
        {
            if (clip == null)
            {
                Game.Debug.Error("AudioClip is null");
                return null;
            }

            var track = new AudioTrack();
            track._config = config ?? new AudioConfig();
            track._isLoaded = true;

            if (pool != null)
            {
                track._audioSource = pool.Get();
                track._isFromPool = true;
                track._gameObject = track._audioSource.gameObject;
            }
            else
            {
                track._gameObject = new GameObject($"AudioTrack_{clip.name}");
                track._audioSource = track._gameObject.AddComponent<AudioSource>();
                track._isFromPool = false;
            }

            if (parent != null)
            {
                track._gameObject.transform.SetParent(parent);
            }

            track.ConfigureAudioSource(clip);
            track.ApplyVariations();

            if (track._config.EnableSpectrum)
            {
                track._spectrumData = new float[256];
            }

            return track;
        }

        private void ConfigureAudioSource(AudioClip clip)
        {
            _audioSource.clip = clip;
            _audioSource.volume = _config.Volume;
            _audioSource.pitch = _config.Pitch;
            _audioSource.loop = _config.Loop;
            _audioSource.spatialBlend = _config.SpatialBlend;
            _audioSource.priority = _config.Priority;
            _audioSource.outputAudioMixerGroup = _config.MixerGroup;
            _audioSource.minDistance = _config.MinDistance;
            _audioSource.maxDistance = _config.MaxDistance;

            if (_config.Position.HasValue)
            {
                _gameObject.transform.position = _config.Position.Value;
            }

            _targetVolume = _config.Volume;
        }

        private void ApplyVariations()
        {
            if (_config.PitchVariation > 0)
            {
                float variation = UnityEngine.Random.Range(-_config.PitchVariation, _config.PitchVariation);
                _audioSource.pitch = Mathf.Clamp(_config.Pitch + variation, 0.5f, 2f);
            }

            if (_config.VolumeVariation > 0)
            {
                float variation = UnityEngine.Random.Range(-_config.VolumeVariation, _config.VolumeVariation);
                _audioSource.volume = Mathf.Clamp01(_config.Volume + variation);
                _targetVolume = _audioSource.volume;
            }
        }

        public void Play()
        {
            if (_audioSource == null || _isDisposed || !_isLoaded) return;

            if (_config.StartDelay > 0)
            {
                PlayWithDelayAsync(_config.StartDelay).Forget();
                return;
            }

            _audioSource.Play();
            IsPaused = false;

            if (_config.FadeInDuration > 0)
            {
                FadeInAsync(_config.FadeInDuration).Forget();
            }

            if (_config.AutoDestroy && !_config.Loop)
            {
                AutoDestroyAsync().Forget();
            }
        }

        private async UniTaskVoid PlayWithDelayAsync(float delay)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay));

            if (_isDisposed) return;

            _audioSource.Play();
            IsPaused = false;

            if (_config.FadeInDuration > 0)
            {
                FadeInAsync(_config.FadeInDuration).Forget();
            }

            if (_config.AutoDestroy && !_config.Loop)
            {
                AutoDestroyAsync().Forget();
            }
        }

        public void Stop()
        {
            if (_audioSource == null || _isDisposed) return;

            if (_config.FadeOutDuration > 0 && _audioSource.isPlaying)
            {
                FadeOutAndStopAsync(_config.FadeOutDuration).Forget();
            }
            else
            {
                _audioSource.Stop();
            }
        }

        public void Pause()
        {
            if (_audioSource == null || _isDisposed) return;

            _audioSource.Pause();
            IsPaused = true;
        }

        public void Resume()
        {
            if (_audioSource == null || _isDisposed) return;

            _audioSource.UnPause();
            IsPaused = false;
        }

        public float[] GetSpectrumData()
        {
            if (_spectrumData == null || _audioSource == null || _isDisposed)
            {
                return null;
            }

            _audioSource.GetSpectrumData(_spectrumData, 0, _config.FFTWindow);
            return _spectrumData;
        }

        private async UniTaskVoid FadeInAsync(float duration)
        {
            if (_isFading) return;

            _isFading = true;
            float startVolume = 0f;
            _audioSource.volume = startVolume;

            float elapsed = 0f;
            while (elapsed < duration && !_isDisposed)
            {
                elapsed += UnityEngine.Time.deltaTime;
                _audioSource.volume = Mathf.Lerp(startVolume, _targetVolume, elapsed / duration);
                await UniTask.Yield();
            }

            if (!_isDisposed)
            {
                _audioSource.volume = _targetVolume;
            }

            _isFading = false;
        }

        private async UniTaskVoid FadeOutAndStopAsync(float duration)
        {
            if (_isFading) return;

            _isFading = true;
            float startVolume = _audioSource.volume;

            float elapsed = 0f;
            while (elapsed < duration && !_isDisposed)
            {
                elapsed += UnityEngine.Time.deltaTime;
                _audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                await UniTask.Yield();
            }

            if (!_isDisposed)
            {
                _audioSource.Stop();
            }

            _isFading = false;
        }

        private async UniTaskVoid AutoDestroyAsync()
        {
            if (_audioSource == null || _isDisposed) return;

            while (_audioSource.isPlaying && !_isDisposed)
            {
                await UniTask.Yield();
            }

            Dispose();
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;

            if (_audioSource != null)
            {
                _audioSource.Stop();

                if (_isFromPool && Game.Audio != null)
                {
                    ((AudioModule)Game.Audio).ReturnAudioSource(_audioSource);
                }
            }

            if (_gameObject != null && !_isFromPool)
            {
                UnityEngine.Object.Destroy(_gameObject);
            }

            if (_assetHandle != null)
            {
                _assetHandle.Release();
                _assetHandle = null;
            }
        }
    }
}
