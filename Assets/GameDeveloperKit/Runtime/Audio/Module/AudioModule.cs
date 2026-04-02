using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 音频管理模块，提供 BGM、音效和语音的播放控制。
    /// 支持音量分组、场景切换自动停止和资源集成。
    /// </summary>
    public sealed class AudioModule : IGameFrameworkLifecycleModule
    {
        private const string SettingsSaveKey = "GameDeveloperKit/Audio/Settings";
        private const string BgmBindingKey = "Audio.Bgm";
        private const string VoiceBindingKey = "Audio.Voice";

        private readonly GameObject _root;
        private readonly AudioSource _bgmSource;
        private readonly AudioSource _voiceSource;
        private readonly List<AudioSource> _sfxSources = new();
        private readonly List<AudioSource> _voicePooledSources = new();
        private readonly Dictionary<AudioSource, AssetHandle> _pooledSourceHandles = new();
        private AudioSettingsData _settings;
        private AssetHandle _bgmHandle;
        private AssetHandle _voiceHandle;
        private CancellationTokenSource _bgmFadeCancellation;
        private bool _isInitialized;
        private bool _diagnosticsRegistered;
        private string _lastClipName;
        private string _lastError;
        private string _lastBgmPackage;
        private string _lastVoicePackage;
        private string _lastSfxPackage;
        private string _lastReleasedPackage;
        private string _lastReleasedLocation;

        /// <summary>
        /// 初始化 AudioModule 的新实例。
        /// </summary>
        public AudioModule()
        {
            _settings = new AudioSettingsData();
            _root = new GameObject("[GameDeveloperKit.Audio]");
            UnityRuntimeUtility.TryDontDestroyOnLoad(_root);

            _bgmSource = CreateSource("Bgm", true);
            _voiceSource = CreateSource("Voice", false);

            ApplyVolumes();
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        /// <summary>
        /// 获取 BGM 是否正在播放。
        /// </summary>
        public bool IsBgmPlaying => _bgmSource.isPlaying;

        /// <summary>
        /// 获取语音是否正在播放。
        /// </summary>
        public bool IsVoicePlaying => _voiceSource.isPlaying || HasActiveSource(_voicePooledSources);

        /// <summary>
        /// 获取主音量。
        /// </summary>
        public float MasterVolume => _settings.MasterVolume;

        /// <summary>
        /// 获取音效播放器数量。
        /// </summary>
        public int SfxSourceCount => _sfxSources.Count;

        /// <summary>
        /// 获取语音播放器数量。
        /// </summary>
        public int VoiceSourceCount => _voicePooledSources.Count + 1;

        /// <summary>
        /// 获取模块状态。
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 获取或设置场景切换时是否停止 BGM。
        /// </summary>
        public bool StopBgmOnSceneChange
        {
            get => _settings.StopBgmOnSceneChange;
            set
            {
                if (_settings.StopBgmOnSceneChange == value)
                {
                    return;
                }

                _settings.StopBgmOnSceneChange = value;
                SaveSettings();
            }
        }

        /// <summary>
        /// 当音量改变时触发。
        /// </summary>
        public event Action<AudioGroup, float> VolumeChanged;

        /// <summary>
        /// 异步初始化音频模块。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>初始化任务。</returns>
        public UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
            {
                return UniTask.CompletedTask;
            }

            try
            {
                Game.EnsureModuleReady<DataModule>();
                _settings = LoadSettings();
                ApplyVolumes();
                RegisterDiagnosticsSnapshotProviders();
                _isInitialized = true;
                return UniTask.CompletedTask;
            }
            catch
            {
                _isInitialized = false;
                throw;
            }
        }

        /// <summary>
        /// 异步关闭音频模块。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>关闭任务。</returns>
        public UniTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
            {
                return UniTask.CompletedTask;
            }

            Dispose();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 播放 BGM。
        /// </summary>
        /// <param name="clip">音频剪辑。</param>
        /// <param name="loop">是否循环播放。</param>
        /// <exception cref="ArgumentNullException">当音频剪辑为 null 时抛出。</exception>
        public void PlayBgm(AudioClip clip, bool loop = true)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            CancelBgmFade();
            ReleaseBgmHandle();
            _bgmSource.loop = loop;
            _bgmSource.clip = clip;
            _bgmSource.Play();
            _lastClipName = clip.name;
        }

        /// <summary>
        /// 播放语音。
        /// </summary>
        /// <param name="clip">音频剪辑。</param>
        /// <param name="loop">是否循环播放。</param>
        /// <exception cref="ArgumentNullException">当音频剪辑为 null 时抛出。</exception>
        public void PlayVoice(AudioClip clip, bool loop = false)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            _lastClipName = clip.name;
            if (loop)
            {
                ReleaseVoiceHandle();
                _voiceSource.loop = true;
                _voiceSource.clip = clip;
                _voiceSource.Play();
                return;
            }

            PlayOnPooledSource(RentVoiceSource(), clip, AudioGroup.Voice, 1f, CancellationToken.None).ForgetWithDiagnostics("AudioModule.PlayVoiceFailed", clip.name, nameof(AudioModule));
        }

        /// <summary>
        /// 播放音效。
        /// </summary>
        /// <param name="clip">音频剪辑。</param>
        /// <param name="volumeScale">音量缩放系数。</param>
        /// <exception cref="ArgumentNullException">当音频剪辑为 null 时抛出。</exception>
        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            _lastClipName = clip.name;
            PlayOnPooledSource(RentSfxSource(), clip, AudioGroup.Sfx, volumeScale, CancellationToken.None).ForgetWithDiagnostics("AudioModule.PlaySfxFailed", clip.name, nameof(AudioModule));
        }

        /// <summary>
        /// 异步播放 BGM（从资源加载）。
        /// </summary>
        /// <param name="clipNameOrPath">音频剪辑名称或路径。</param>
        /// <param name="packageName">资源包名称。</param>
        /// <param name="loop">是否循环播放。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>播放任务。</returns>
        public async UniTask PlayBgmAsync(string clipNameOrPath, string packageName = null, bool loop = true, CancellationToken cancellationToken = default)
        {
            try
            {
                var location = CreateLocation(clipNameOrPath);
                var handle = await LoadClipAsync(location, packageName, cancellationToken);
                PlayBgm(handle.GetAsset<AudioClip>(), loop);
                BindPersistentHandle(BgmBindingKey, ref _bgmHandle, handle, location, packageName);
            }
            catch (Exception exception)
            {
                _lastError = exception.Message;
                throw;
            }
        }

        /// <summary>
        /// 异步淡入播放 BGM。
        /// </summary>
        /// <param name="clipNameOrPath">音频剪辑名称或路径。</param>
        /// <param name="packageName">资源包名称。</param>
        /// <param name="durationSeconds">淡入时长（秒）。</param>
        /// <param name="loop">是否循环播放。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>播放任务。</returns>
        public async UniTask FadeInBgmAsync(string clipNameOrPath, string packageName = null, float durationSeconds = 0.3f, bool loop = true, CancellationToken cancellationToken = default)
        {
            var location = CreateLocation(clipNameOrPath);
            var handle = await LoadClipAsync(location, packageName, cancellationToken);
            var clip = handle.GetAsset<AudioClip>();
            PlayBgm(clip, loop);
            BindPersistentHandle(BgmBindingKey, ref _bgmHandle, handle, location, packageName);
            await FadeBgmVolumeAsync(0f, GetTargetGroupVolume(AudioGroup.Bgm), durationSeconds, cancellationToken);
        }

        /// <summary>
        /// 异步播放语音（从资源加载）。
        /// </summary>
        /// <param name="clipNameOrPath">音频剪辑名称或路径。</param>
        /// <param name="packageName">资源包名称。</param>
        /// <param name="loop">是否循环播放。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>播放句柄。</returns>
        public async UniTask<AudioPlaybackHandle> PlayVoiceAsync(string clipNameOrPath, string packageName = null, bool loop = false, CancellationToken cancellationToken = default)
        {
            try
            {
                var location = CreateLocation(clipNameOrPath);
                var handle = await LoadClipAsync(location, packageName, cancellationToken);
                var clip = handle.GetAsset<AudioClip>();
                _lastClipName = clip?.name ?? clipNameOrPath;
                if (loop)
                {
                    PlayVoice(clip, true);
                    BindPersistentHandle(VoiceBindingKey, ref _voiceHandle, handle, location, packageName);
                    return CreatePlaybackHandle(AudioGroup.Voice, handle.Location, handle.PackageName ?? packageName, StopVoice);
                }

                var source = RentVoiceSource();
                BindTransientSourceHandle(source, handle, location, packageName, AudioGroup.Voice);
                var playbackHandle = CreatePlaybackHandle(AudioGroup.Voice, handle.Location, handle.PackageName ?? packageName, () => ReleaseSourcePlayback(source));
                PlayOnPooledSource(source, clip, AudioGroup.Voice, 1f, cancellationToken).ForgetWithDiagnostics("AudioModule.PlayVoiceAsyncFailed", clipNameOrPath, nameof(AudioModule));
                return playbackHandle;
            }
            catch (Exception exception)
            {
                _lastError = exception.Message;
                throw;
            }
        }

        /// <summary>
        /// 异步播放音效（从资源加载）。
        /// </summary>
        /// <param name="clipNameOrPath">音频剪辑名称或路径。</param>
        /// <param name="packageName">资源包名称。</param>
        /// <param name="volumeScale">音量缩放系数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>播放句柄。</returns>
        public async UniTask<AudioPlaybackHandle> PlaySfxAsync(string clipNameOrPath, string packageName = null, float volumeScale = 1f, CancellationToken cancellationToken = default)
        {
            try
            {
                var location = CreateLocation(clipNameOrPath);
                var handle = await LoadClipAsync(location, packageName, cancellationToken);
                var clip = handle.GetAsset<AudioClip>();
                _lastClipName = clip?.name ?? clipNameOrPath;
                var source = RentSfxSource();
                BindTransientSourceHandle(source, handle, location, packageName, AudioGroup.Sfx);
                var playbackHandle = CreatePlaybackHandle(AudioGroup.Sfx, handle.Location, handle.PackageName ?? packageName, () => ReleaseSourcePlayback(source));
                PlayOnPooledSource(source, clip, AudioGroup.Sfx, volumeScale, cancellationToken).ForgetWithDiagnostics("AudioModule.PlaySfxAsyncFailed", clipNameOrPath, nameof(AudioModule));
                return playbackHandle;
            }
            catch (Exception exception)
            {
                _lastError = exception.Message;
                throw;
            }
        }

        /// <summary>
        /// 停止 BGM。
        /// </summary>
        public void StopBgm()
        {
            CancelBgmFade();
            _bgmSource.Stop();
            _bgmSource.clip = null;
            ReleaseBgmHandle();
        }

        /// <summary>
        /// 暂停 BGM。
        /// </summary>
        public void PauseBgm()
        {
            _bgmSource.Pause();
        }

        /// <summary>
        /// 恢复 BGM 播放。
        /// </summary>
        public void ResumeBgm()
        {
            if (_bgmSource.clip != null)
            {
                _bgmSource.UnPause();
            }
        }

        /// <summary>
        /// 异步淡出 BGM。
        /// </summary>
        /// <param name="durationSeconds">淡出时长（秒）。</param>
        /// <param name="stopAfterFade">淡出后是否停止。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>淡出任务。</returns>
        public UniTask FadeOutBgmAsync(float durationSeconds = 0.3f, bool stopAfterFade = true, CancellationToken cancellationToken = default)
        {
            return FadeOutBgmInternalAsync(durationSeconds, stopAfterFade, cancellationToken);
        }

        /// <summary>
        /// 停止所有语音。
        /// </summary>
        public void StopVoice()
        {
            _voiceSource.Stop();
            _voiceSource.clip = null;
            ReleaseVoiceHandle();

            for (var i = 0; i < _voicePooledSources.Count; i++)
            {
                ReleaseSourcePlayback(_voicePooledSources[i]);
            }
        }

        /// <summary>
        /// 停止所有音效。
        /// </summary>
        public void StopSfx()
        {
            for (var i = 0; i < _sfxSources.Count; i++)
            {
                ReleaseSourcePlayback(_sfxSources[i]);
            }
        }

        /// <summary>
        /// 获取指定音频组的音量。
        /// </summary>
        /// <param name="group">音频组。</param>
        /// <returns>音量值（0.0-1.0）。</returns>
        /// <exception cref="ArgumentOutOfRangeException">当音频组无效时抛出。</exception>
        public float GetGroupVolume(AudioGroup group)
        {
            return group switch
            {
                AudioGroup.Master => _settings.MasterVolume,
                AudioGroup.Bgm => _settings.BgmVolume,
                AudioGroup.Sfx => _settings.SfxVolume,
                AudioGroup.Voice => _settings.VoiceVolume,
                _ => throw new ArgumentOutOfRangeException(nameof(group))
            };
        }

        /// <summary>
        /// 设置指定音频组的音量。
        /// </summary>
        /// <param name="group">音频组。</param>
        /// <param name="volume">音量值（0.0-1.0）。</param>
        /// <param name="save">是否保存到存储。</param>
        /// <exception cref="ArgumentOutOfRangeException">当音频组无效时抛出。</exception>
        public void SetGroupVolume(AudioGroup group, float volume, bool save = true)
        {
            var normalized = Mathf.Clamp01(volume);
            switch (group)
            {
                case AudioGroup.Master:
                    _settings.MasterVolume = normalized;
                    break;
                case AudioGroup.Bgm:
                    _settings.BgmVolume = normalized;
                    break;
                case AudioGroup.Sfx:
                    _settings.SfxVolume = normalized;
                    break;
                case AudioGroup.Voice:
                    _settings.VoiceVolume = normalized;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(group));
            }

            ApplyVolumes();
            VolumeChanged?.Invoke(group, normalized);

            if (save)
            {
                SaveSettings();
            }
        }

        /// <summary>
        /// 保存音频设置到存储。
        /// </summary>
        public void SaveSettings()
        {
            Game.Data.SaveJson(SettingsSaveKey, _settings, true);
        }

        /// <summary>
        /// 释放音频模块资源。
        /// </summary>
        public void Dispose()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            RemoveDiagnosticsSnapshotProviders();
            StopBgm();
            StopVoice();
            StopSfx();
            VolumeChanged = null;
            _isInitialized = false;

            for (var i = 0; i < _sfxSources.Count; i++)
            {
                ReleaseSourcePlayback(_sfxSources[i]);
            }

            for (var i = 0; i < _voicePooledSources.Count; i++)
            {
                ReleaseSourcePlayback(_voicePooledSources[i]);
            }

            _sfxSources.Clear();
            _voicePooledSources.Clear();

            if (_root != null)
            {
                UnityRuntimeUtility.DestroyObject(_root);
            }
        }

        private AudioSource CreateSource(string name, bool loop)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_root.transform, false);

            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            return source;
        }

        private void ApplyVolumes()
        {
            _bgmSource.volume = GetTargetGroupVolume(AudioGroup.Bgm);
            _voiceSource.volume = GetTargetGroupVolume(AudioGroup.Voice);

            for (var i = 0; i < _sfxSources.Count; i++)
            {
                _sfxSources[i].volume = GetTargetGroupVolume(AudioGroup.Sfx);
            }

            for (var i = 0; i < _voicePooledSources.Count; i++)
            {
                _voicePooledSources[i].volume = GetTargetGroupVolume(AudioGroup.Voice);
            }
        }

        private void OnActiveSceneChanged(Scene current, Scene next)
        {
            if (_settings.StopBgmOnSceneChange && _bgmSource.isPlaying)
            {
                FadeOutBgmAsync(0.2f, true).ForgetWithDiagnostics("AudioModule.SceneFadeOutFailed", next.name, nameof(AudioModule));
            }
        }

        private static AudioSettingsData LoadSettings()
        {
            try
            {
                return Game.Data.LoadJson(SettingsSaveKey, new AudioSettingsData()) ?? new AudioSettingsData();
            }
            catch
            {
                return new AudioSettingsData();
            }
        }

        private static ResourceLocation CreateLocation(string clipNameOrPath)
        {
            if (string.IsNullOrWhiteSpace(clipNameOrPath))
            {
                throw new ArgumentException("Clip name or path can not be empty.", nameof(clipNameOrPath));
            }

            if (clipNameOrPath.IndexOf('/') >= 0
                || clipNameOrPath.IndexOf('\\') >= 0)
            {
                return new ResourceLocation
                {
                    FullPath = clipNameOrPath,
                    AssetType = typeof(AudioClip)
                };
            }

            return new ResourceLocation
            {
                Name = clipNameOrPath,
                AssetType = typeof(AudioClip)
            };
        }

        private static async UniTask<AssetHandle> LoadClipAsync(ResourceLocation location, string packageName, CancellationToken cancellationToken)
        {
            if (!Game.HasModule<ResourceModule>())
            {
                throw new InvalidOperationException("Resource module is not available.");
            }

            var resourceModule = Game.Resource;
            if (string.IsNullOrWhiteSpace(packageName))
            {
                return await resourceModule.LoadAssetAsync(location, cancellationToken);
            }

            location = location?.Clone() ?? new ResourceLocation();
            location.PackageName = packageName;
            return await resourceModule.LoadAssetAsync(location, cancellationToken);
        }

        private async UniTask PlayOnPooledSource(AudioSource source, AudioClip clip, AudioGroup group, float volumeScale, CancellationToken cancellationToken)
        {
            if (source == null || clip == null)
            {
                ReleaseSourcePlayback(source);
                return;
            }

            source.loop = false;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volumeScale) * GetTargetGroupVolume(group);
            source.Play();

            try
            {
                if (clip.length > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(clip.length), cancellationToken: cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                ReleaseSourcePlayback(source);
            }
        }

        private async UniTask FadeOutBgmInternalAsync(float durationSeconds, bool stopAfterFade, CancellationToken cancellationToken)
        {
            if (_bgmSource.clip == null)
            {
                return;
            }

            try
            {
                await FadeBgmVolumeAsync(_bgmSource.volume, 0f, durationSeconds, cancellationToken);
                if (stopAfterFade)
                {
                    StopBgm();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTask FadeBgmVolumeAsync(float from, float to, float durationSeconds, CancellationToken cancellationToken)
        {
            CancelBgmFade();
            _bgmFadeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var linkedToken = _bgmFadeCancellation.Token;
            var duration = Mathf.Max(0.01f, durationSeconds);
            var elapsed = 0f;

            _bgmSource.volume = from;
            while (elapsed < duration)
            {
                linkedToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, linkedToken);
            }

            _bgmSource.volume = to;
        }

        private void CancelBgmFade()
        {
            if (_bgmFadeCancellation == null)
            {
                return;
            }

            _bgmFadeCancellation.Cancel();
            _bgmFadeCancellation.Dispose();
            _bgmFadeCancellation = null;
        }

        private AudioSource RentSfxSource()
        {
            for (var i = 0; i < _sfxSources.Count; i++)
            {
                if (!_sfxSources[i].isPlaying)
                {
                    return _sfxSources[i];
                }
            }

            var source = CreateSource($"Sfx.{_sfxSources.Count + 1}", false);
            source.volume = GetTargetGroupVolume(AudioGroup.Sfx);
            _sfxSources.Add(source);
            return source;
        }

        private AudioSource RentVoiceSource()
        {
            for (var i = 0; i < _voicePooledSources.Count; i++)
            {
                if (!_voicePooledSources[i].isPlaying)
                {
                    return _voicePooledSources[i];
                }
            }

            var source = CreateSource($"Voice.{_voicePooledSources.Count + 1}", false);
            source.volume = GetTargetGroupVolume(AudioGroup.Voice);
            _voicePooledSources.Add(source);
            return source;
        }

        private static bool HasActiveSource(List<AudioSource> sources)
        {
            for (var i = 0; i < sources.Count; i++)
            {
                if (sources[i] != null && sources[i].isPlaying)
                {
                    return true;
                }
            }

            return false;
        }

        private void ReleaseSourcePlayback(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.Stop();
            source.clip = null;

            if (_pooledSourceHandles.Remove(source, out var handle))
            {
                CaptureReleasedHandle(handle);
                handle.Release();
            }
        }

        private float GetTargetGroupVolume(AudioGroup group)
        {
            return _settings.MasterVolume * GetGroupVolume(group);
        }

        private void RegisterDiagnosticsSnapshotProviders()
        {
            if (_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RegisterSnapshotProvider("Audio.IsBgmPlaying", () => IsBgmPlaying.ToString());
            diagnostics.RegisterSnapshotProvider("Audio.IsVoicePlaying", () => IsVoicePlaying.ToString());
            diagnostics.RegisterSnapshotProvider("Audio.SfxSourceCount", () => SfxSourceCount.ToString());
            diagnostics.RegisterSnapshotProvider("Audio.VoiceSourceCount", () => VoiceSourceCount.ToString());
            diagnostics.RegisterSnapshotProvider("Audio.LastClip", () => _lastClipName ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Audio.LastError", () => _lastError ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Audio.LastBgmPackage", () => _lastBgmPackage ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Audio.LastVoicePackage", () => _lastVoicePackage ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Audio.LastSfxPackage", () => _lastSfxPackage ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Audio.LastReleasedPackage", () => _lastReleasedPackage ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Audio.LastReleasedLocation", () => _lastReleasedLocation ?? string.Empty);
            _diagnosticsRegistered = true;
        }

        private void RemoveDiagnosticsSnapshotProviders()
        {
            if (!_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RemoveSnapshotProvider("Audio.IsBgmPlaying");
            diagnostics.RemoveSnapshotProvider("Audio.IsVoicePlaying");
            diagnostics.RemoveSnapshotProvider("Audio.SfxSourceCount");
            diagnostics.RemoveSnapshotProvider("Audio.VoiceSourceCount");
            diagnostics.RemoveSnapshotProvider("Audio.LastClip");
            diagnostics.RemoveSnapshotProvider("Audio.LastError");
            diagnostics.RemoveSnapshotProvider("Audio.LastBgmPackage");
            diagnostics.RemoveSnapshotProvider("Audio.LastVoicePackage");
            diagnostics.RemoveSnapshotProvider("Audio.LastSfxPackage");
            diagnostics.RemoveSnapshotProvider("Audio.LastReleasedPackage");
            diagnostics.RemoveSnapshotProvider("Audio.LastReleasedLocation");
            _diagnosticsRegistered = false;
        }

        private void ReleaseBgmHandle()
        {
            if (_bgmHandle != null)
            {
                GetOrAddOwnerTracker(_bgmSource.gameObject).ClearBinding(BgmBindingKey);
                CaptureReleasedHandle(_bgmHandle);
            }

            _bgmHandle?.Release();
            _bgmHandle = null;
        }

        private void ReleaseVoiceHandle()
        {
            if (_voiceHandle != null)
            {
                GetOrAddOwnerTracker(_voiceSource.gameObject).ClearBinding(VoiceBindingKey);
                CaptureReleasedHandle(_voiceHandle);
            }

            _voiceHandle?.Release();
            _voiceHandle = null;
        }

        private void BindPersistentHandle(string slotKey, ref AssetHandle currentHandle, AssetHandle newHandle, ResourceLocation location, string packageName)
        {
            currentHandle = newHandle;
            GetOrAddOwnerTracker(slotKey == BgmBindingKey ? _bgmSource.gameObject : _voiceSource.gameObject).TrackBinding(slotKey, newHandle.CreateReference());
            CaptureLoadBinding(slotKey == BgmBindingKey ? AudioGroup.Bgm : AudioGroup.Voice, newHandle, location, packageName);
        }

        private void BindTransientSourceHandle(AudioSource source, AssetHandle handle, ResourceLocation location, string packageName, AudioGroup group)
        {
            if (source == null || handle == null)
            {
                return;
            }

            if (_pooledSourceHandles.TryGetValue(source, out var previousHandle))
            {
                CaptureReleasedHandle(previousHandle);
                previousHandle.Release();
            }

            _pooledSourceHandles[source] = handle;
            CaptureLoadBinding(group, handle, location, packageName);
        }

        private void CaptureLoadBinding(AudioGroup group, AssetHandle handle, ResourceLocation location, string packageName)
        {
            var resolvedPackage = handle?.PackageName;
            if (string.IsNullOrWhiteSpace(resolvedPackage))
            {
                resolvedPackage = string.IsNullOrWhiteSpace(packageName) ? location?.PackageName : packageName;
            }

            switch (group)
            {
                case AudioGroup.Bgm:
                    _lastBgmPackage = resolvedPackage ?? string.Empty;
                    break;
                case AudioGroup.Sfx:
                    _lastSfxPackage = resolvedPackage ?? string.Empty;
                    break;
                case AudioGroup.Voice:
                    _lastVoicePackage = resolvedPackage ?? string.Empty;
                    break;
            }
        }

        private void CaptureReleasedHandle(AssetHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            _lastReleasedPackage = handle.PackageName ?? string.Empty;
            _lastReleasedLocation = handle.Location?.FullPath ?? handle.Location?.Name ?? string.Empty;
        }

        private static AudioPlaybackHandle CreatePlaybackHandle(AudioGroup group, ResourceLocation location, string packageName, Action stopAction)
        {
            return new AudioPlaybackHandle(group, location, packageName, stopAction);
        }

        private static ResourceOwnerTracker GetOrAddOwnerTracker(GameObject gameObject)
        {
            var tracker = gameObject.GetComponent<ResourceOwnerTracker>();
            if (tracker == null)
            {
                tracker = gameObject.AddComponent<ResourceOwnerTracker>();
            }

            return tracker;
        }
    }
}

