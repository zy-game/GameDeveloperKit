using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Sound
{
    /// <summary>
    /// 定义 Sound Module 类型。
    /// </summary>
    [ModuleDependency(typeof(ResourceModule))]
    public sealed class SoundModule : GameModuleBase
    {
        /// <summary>
        /// 定义 Root Name 常量。
        /// </summary>
        public const string RootName = "GameDeveloperKit.SoundRoot";
        /// <summary>
        /// 定义 Default Max Concurrent 常量。
        /// </summary>
        private const int DefaultMaxConcurrent = 16;
        /// <summary>
        /// 定义 Min Decibel 常量。
        /// </summary>
        private const float MinDecibel = -80f;

        /// <summary>
        /// 存储 Track Volumes。
        /// </summary>
        private readonly Dictionary<SoundTrack, float> m_TrackVolumes = new Dictionary<SoundTrack, float>();
        /// <summary>
        /// 存储 Track Bindings。
        /// </summary>
        private readonly Dictionary<SoundTrack, SoundTrackMixerBinding> m_TrackBindings = new Dictionary<SoundTrack, SoundTrackMixerBinding>();
        /// <summary>
        /// 存储 Snapshots。
        /// </summary>
        private readonly Dictionary<string, AudioMixerSnapshot> m_Snapshots = new Dictionary<string, AudioMixerSnapshot>();
        /// <summary>
        /// 存储 Primary Sources。
        /// </summary>
        private readonly Dictionary<SoundTrack, SoundRuntimeSource> m_PrimarySources = new Dictionary<SoundTrack, SoundRuntimeSource>();
        /// <summary>
        /// 存储 Active Sources。
        /// </summary>
        private readonly Dictionary<SoundHandle, SoundRuntimeSource> m_ActiveSources = new Dictionary<SoundHandle, SoundRuntimeSource>();
        /// <summary>
        /// 存储 Pooled Sources。
        /// </summary>
        private readonly List<SoundRuntimeSource> m_PooledSources = new List<SoundRuntimeSource>();

        /// <summary>
        /// 存储 Root。
        /// </summary>
        private GameObject m_Root;
        /// <summary>
        /// 存储 Settings。
        /// </summary>
        private SoundMixerSettings m_Settings;
        /// <summary>
        /// 存储 Sequence。
        /// </summary>
        private long m_Sequence;

        /// <summary>
        /// 启动 member。
        /// </summary>
        public override void Startup()
        {
            if (m_Root != null)
            {
                return;
            }

            m_Root = new GameObject(RootName);
            Object.DontDestroyOnLoad(m_Root);

            InitializeSettings();
            CreatePrimarySource(SoundTrack.Music);
            CreatePrimarySource(SoundTrack.Ambience);
            CreatePrimarySource(SoundTrack.Voice);
        }

        /// <summary>
        /// 关闭 member。
        /// </summary>
        public override void Shutdown()
        {
            var sources = new List<SoundRuntimeSource>(m_ActiveSources.Values);
            foreach (var source in sources)
            {
                var assetHandle = DetachSource(source, SoundStatus.Stopped);
                assetHandle?.Release();
            }

            m_ActiveSources.Clear();
            m_PrimarySources.Clear();
            m_PooledSources.Clear();
            m_TrackBindings.Clear();
            m_TrackVolumes.Clear();
            m_Snapshots.Clear();
            m_Settings = null;

            if (m_Root != null)
            {
                Object.DestroyImmediate(m_Root);
                m_Root = null;
            }
        }

        /// <summary>
        /// 执行 Play Music Async。
        /// </summary>
        /// <param name="location">location 参数。</param>
        /// <param name="options">options 参数。</param>
        /// <returns>操作完成任务。</returns>
        public async UniTask<SoundHandle> PlayMusicAsync(string location, SoundPlayOptions options = null)
        {
            ValidateLocation(location);
            var normalizedOptions = NormalizeOptions(options, SoundTrack.Music, true);
            normalizedOptions.Track = SoundTrack.Music;
            ValidatePlaybackVolume(normalizedOptions.Volume);
            ValidateTrack(normalizedOptions.Track);

            if (m_PrimarySources.TryGetValue(normalizedOptions.Track, out var current) &&
                current.InUse &&
                current.Handle != null &&
                current.Handle.Location == location &&
                current.Handle.Status == SoundStatus.Playing)
            {
                return current.Handle;
            }

            if (current != null && current.InUse)
            {
                await CompleteSourceAsync(current, SoundStatus.Stopped);
            }

            var handle = CreateHandle(location, normalizedOptions.Track);
            handle.SetStatus(SoundStatus.Loading);
            var assetHandle = await LoadAudioClipAsync(location);
            var clip = assetHandle.GetAsset<AudioClip>();
            var source = GetPrimarySource(normalizedOptions.Track);
            StartSource(source, handle, assetHandle, clip, normalizedOptions, false, default);
            handle.SetStatus(SoundStatus.Playing);
            WatchCompletionAsync(source, source.Version).Forget();
            return handle;
        }

        /// <summary>
        /// 执行 Play Sfx Async。
        /// </summary>
        /// <param name="location">location 参数。</param>
        /// <param name="options">options 参数。</param>
        /// <returns>操作完成任务。</returns>
        public UniTask<SoundHandle> PlaySfxAsync(string location, SoundPlayOptions options = null)
        {
            return PlaySfxInternalAsync(location, default, false, options);
        }

        /// <summary>
        /// 执行 Play Sfx At Async。
        /// </summary>
        /// <param name="location">location 参数。</param>
        /// <param name="position">position 参数。</param>
        /// <param name="options">options 参数。</param>
        /// <returns>操作完成任务。</returns>
        public UniTask<SoundHandle> PlaySfxAtAsync(string location, Vector3 position, SoundPlayOptions options = null)
        {
            return PlaySfxInternalAsync(location, position, true, options);
        }

        /// <summary>
        /// 执行 Stop。
        /// </summary>
        /// <param name="handle">handle 参数。</param>
        public void Stop(SoundHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            if (!m_ActiveSources.TryGetValue(handle, out var source))
            {
                return;
            }

            CompleteSourceAsync(source, SoundStatus.Stopped).Forget();
        }

        /// <summary>
        /// 执行 Stop Track。
        /// </summary>
        /// <param name="track">track 参数。</param>
        public void StopTrack(SoundTrack track)
        {
            ValidateTrack(track);
            if (track == SoundTrack.Master)
            {
                StopAll();
                return;
            }

            foreach (var source in GetActiveSources(track))
            {
                CompleteSourceAsync(source, SoundStatus.Stopped).Forget();
            }
        }

        /// <summary>
        /// 执行 Pause Track。
        /// </summary>
        /// <param name="track">track 参数。</param>
        public void PauseTrack(SoundTrack track)
        {
            ValidateTrack(track);
            foreach (var source in GetActiveSources(track))
            {
                PauseSource(source);
            }
        }

        /// <summary>
        /// 执行 Resume Track。
        /// </summary>
        /// <param name="track">track 参数。</param>
        public void ResumeTrack(SoundTrack track)
        {
            ValidateTrack(track);
            foreach (var source in GetActiveSources(track))
            {
                ResumeSource(source);
            }
        }

        /// <summary>
        /// 设置 Volume。
        /// </summary>
        /// <param name="track">track 参数。</param>
        /// <param name="volume">volume 参数。</param>
        public void SetVolume(SoundTrack track, float volume)
        {
            ValidateTrack(track);
            ValidateVolume(volume);
            m_TrackVolumes[track] = volume;
            ApplyMixerVolume(track, volume);
            foreach (var source in m_ActiveSources.Values)
            {
                if (track == SoundTrack.Master || source.Track == track)
                {
                    ApplySourceVolume(source);
                }
            }
        }

        /// <summary>
        /// 获取 Volume。
        /// </summary>
        /// <param name="track">track 参数。</param>
        /// <returns>执行结果。</returns>
        public float GetVolume(SoundTrack track)
        {
            ValidateTrack(track);
            return GetTrackVolume(track);
        }

        /// <summary>
        /// 设置 Mixer Float。
        /// </summary>
        /// <param name="parameter">parameter 参数。</param>
        /// <param name="value">value 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        public bool SetMixerFloat(string parameter, float value)
        {
            ValidateParameter(parameter);
            return m_Settings != null && m_Settings.Mixer != null && m_Settings.Mixer.SetFloat(parameter, value);
        }

        /// <summary>
        /// 执行 Transition Snapshot。
        /// </summary>
        /// <param name="snapshotName">snapshot Name 参数。</param>
        /// <param name="duration">duration 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        public bool TransitionSnapshot(string snapshotName, float duration)
        {
            ValidateParameter(snapshotName);
            if (!m_Snapshots.TryGetValue(snapshotName, out var snapshot) || snapshot == null)
            {
                return false;
            }

            snapshot.TransitionTo(Mathf.Max(0f, duration));
            return true;
        }

        /// <summary>
        /// 配置 Sound Mixer Settings。
        /// </summary>
        /// <param name="settings">settings 参数。</param>
        public void ConfigureMixer(SoundMixerSettings settings)
        {
            m_Settings = settings;
            InitializeSettings();
            ApplySettingsToSources();
        }

        /// <summary>
        /// 执行 Play Sfx Internal Async。
        /// </summary>
        /// <param name="location">location 参数。</param>
        /// <param name="position">position 参数。</param>
        /// <param name="usePosition">use Position 参数。</param>
        /// <param name="options">options 参数。</param>
        /// <returns>操作完成任务。</returns>
        private async UniTask<SoundHandle> PlaySfxInternalAsync(string location, Vector3 position, bool usePosition, SoundPlayOptions options)
        {
            ValidateLocation(location);
            var normalizedOptions = NormalizeOptions(options, SoundTrack.Sfx, false);
            normalizedOptions.Track = SoundTrack.Sfx;
            ValidatePlaybackVolume(normalizedOptions.Volume);
            ValidateTrack(normalizedOptions.Track);
            EnforceMaxConcurrent(normalizedOptions.Track, normalizedOptions.Priority);

            var handle = CreateHandle(location, normalizedOptions.Track);
            handle.SetStatus(SoundStatus.Loading);
            var assetHandle = await LoadAudioClipAsync(location);
            var clip = assetHandle.GetAsset<AudioClip>();
            var source = GetPooledSource(normalizedOptions.Track);
            StartSource(source, handle, assetHandle, clip, normalizedOptions, usePosition, position);
            handle.SetStatus(SoundStatus.Playing);
            WatchCompletionAsync(source, source.Version).Forget();
            return handle;
        }

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
        /// <param name="source">source 参数。</param>
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
        /// 创建 Handle。
        /// </summary>
        /// <param name="location">location 参数。</param>
        /// <param name="track">track 参数。</param>
        /// <returns>执行结果。</returns>
        private SoundHandle CreateHandle(string location, SoundTrack track)
        {
            return new SoundHandle(location, track, Stop, PauseHandle, ResumeHandle);
        }

        /// <summary>
        /// 创建 Primary Source。
        /// </summary>
        /// <param name="track">track 参数。</param>
        private void CreatePrimarySource(SoundTrack track)
        {
            m_PrimarySources[track] = CreateRuntimeSource(track, false);
        }

        /// <summary>
        /// 获取 Primary Source。
        /// </summary>
        /// <param name="track">track 参数。</param>
        /// <returns>执行结果。</returns>
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
        /// <param name="track">track 参数。</param>
        /// <returns>执行结果。</returns>
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
        /// <param name="track">track 参数。</param>
        /// <param name="pooled">pooled 参数。</param>
        /// <returns>执行结果。</returns>
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
        /// 执行 Start Source。
        /// </summary>
        /// <param name="source">source 参数。</param>
        /// <param name="handle">handle 参数。</param>
        /// <param name="assetHandle">asset Handle 参数。</param>
        /// <param name="clip">clip 参数。</param>
        /// <param name="options">options 参数。</param>
        /// <param name="usePosition">use Position 参数。</param>
        /// <param name="position">position 参数。</param>
        private void StartSource(
            SoundRuntimeSource source,
            SoundHandle handle,
            AssetHandle assetHandle,
            AudioClip clip,
            SoundPlayOptions options,
            bool usePosition,
            Vector3 position)
        {
            source.Version++;
            source.Handle = handle;
            source.AssetHandle = assetHandle;
            source.AudioClip = clip;
            source.Track = options.Track;
            source.InUse = true;
            source.Volume = options.Volume;
            source.Priority = options.Priority;
            source.Sequence = ++m_Sequence;

            var audioSource = source.AudioSource;
            audioSource.clip = clip;
            audioSource.loop = options.Loop;
            audioSource.priority = Mathf.Clamp(options.Priority, 0, 256);
            audioSource.outputAudioMixerGroup = GetOutput(options.Track);
            audioSource.spatialBlend = usePosition ? 1f : 0f;
            if (usePosition)
            {
                audioSource.transform.position = position;
            }

            ApplySourceVolume(source);
            m_ActiveSources[handle] = source;
            try
            {
                audioSource.Play();
            }
            catch (Exception exception)
            {
                var detachedAssetHandle = DetachSource(source, SoundStatus.Failed);
                UnloadAssetAsync(detachedAssetHandle).Forget();
                throw new GameException($"Failed to play audio clip: {handle.Location}", exception);
            }
        }

        /// <summary>
        /// 加载 Audio Clip Async。
        /// </summary>
        /// <param name="location">location 参数。</param>
        /// <returns>操作完成任务。</returns>
        private async UniTask<AssetHandle> LoadAudioClipAsync(string location)
        {
            AssetHandle assetHandle;
            try
            {
                assetHandle = await App.Resource.LoadAssetAsync(location);
            }
            catch (Exception exception)
            {
                throw new GameException($"Failed to load audio clip: {location}", exception);
            }

            var clip = assetHandle?.GetAsset<AudioClip>();
            if (clip != null)
            {
                return assetHandle;
            }

            await UnloadAssetAsync(assetHandle);
            throw new GameException($"Asset is not an AudioClip: {location}", assetHandle?.Error);
        }

        /// <summary>
        /// 执行 Enforce Max Concurrent。
        /// </summary>
        /// <param name="track">track 参数。</param>
        /// <param name="priority">priority 参数。</param>
        private void EnforceMaxConcurrent(SoundTrack track, int priority)
        {
            var maxConcurrent = GetMaxConcurrent(track);
            if (maxConcurrent <= 0)
            {
                return;
            }

            var active = GetActiveSources(track);
            if (active.Count < maxConcurrent)
            {
                return;
            }

            SoundRuntimeSource candidate = null;
            foreach (var source in active)
            {
                if (source.Priority > priority)
                {
                    continue;
                }

                if (candidate == null ||
                    source.Priority < candidate.Priority ||
                    (source.Priority == candidate.Priority && source.Sequence < candidate.Sequence))
                {
                    candidate = source;
                }
            }

            if (candidate == null)
            {
                throw new GameException($"Sound track '{track}' has reached max concurrent limit.");
            }

            var assetHandle = DetachSource(candidate, SoundStatus.Stopped);
            UnloadAssetAsync(assetHandle).Forget();
        }

        /// <summary>
        /// 执行 Watch Completion Async。
        /// </summary>
        /// <param name="source">source 参数。</param>
        /// <param name="version">version 参数。</param>
        /// <returns>操作完成任务。</returns>
        private async UniTaskVoid WatchCompletionAsync(SoundRuntimeSource source, int version)
        {
            while (source.InUse &&
                   source.Version == version &&
                   source.Handle != null &&
                   source.Handle.Status is SoundStatus.Playing or SoundStatus.Paused)
            {
                if (source.Handle.Status == SoundStatus.Playing && !source.AudioSource.isPlaying)
                {
                    await CompleteSourceAsync(source, SoundStatus.Completed);
                    return;
                }

                if (source.AudioSource.clip != null && source.AudioSource.clip.length > 0f)
                {
                    source.Handle.Progress = Mathf.Clamp01(source.AudioSource.time / source.AudioSource.clip.length);
                }

                await UniTask.Yield();
            }
        }

        /// <summary>
        /// 执行 Complete Source Async。
        /// </summary>
        /// <param name="source">source 参数。</param>
        /// <param name="status">status 参数。</param>
        /// <returns>操作完成任务。</returns>
        private async UniTask CompleteSourceAsync(SoundRuntimeSource source, SoundStatus status)
        {
            if (source == null || !source.InUse)
            {
                return;
            }

            var handle = source.Handle;
            var assetHandle = DetachSource(source, status);

            if (handle != null)
            {
                m_ActiveSources.Remove(handle);
            }

            await UnloadAssetAsync(assetHandle);
        }

        /// <summary>
        /// 执行 Detach Source。
        /// </summary>
        /// <param name="source">source 参数。</param>
        /// <param name="status">status 参数。</param>
        /// <returns>执行结果。</returns>
        private AssetHandle DetachSource(SoundRuntimeSource source, SoundStatus status)
        {
            var handle = source.Handle;
            var assetHandle = source.AssetHandle;

            source.Version++;
            source.InUse = false;
            source.Handle = null;
            source.AssetHandle = null;
            source.AudioClip = null;
            source.Volume = 1f;
            source.Priority = 0;

            if (source.AudioSource != null)
            {
                source.AudioSource.Stop();
                source.AudioSource.clip = null;
                source.AudioSource.loop = false;
                source.AudioSource.spatialBlend = 0f;
                source.AudioSource.transform.localPosition = Vector3.zero;
            }

            if (handle != null)
            {
                m_ActiveSources.Remove(handle);
                handle.Progress = status == SoundStatus.Completed ? 1f : handle.Progress;
                handle.SetStatus(status);
            }

            return assetHandle;
        }

        /// <summary>
        /// 卸载 Asset Async。
        /// </summary>
        /// <param name="assetHandle">asset Handle 参数。</param>
        /// <returns>操作完成任务。</returns>
        private async UniTask UnloadAssetAsync(AssetHandle assetHandle)
        {
            if (assetHandle == null || assetHandle.Info == null)
            {
                return;
            }

            try
            {
                await App.Resource.UnloadAsset(assetHandle);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 执行 Pause Handle。
        /// </summary>
        /// <param name="handle">handle 参数。</param>
        private void PauseHandle(SoundHandle handle)
        {
            if (handle != null && m_ActiveSources.TryGetValue(handle, out var source))
            {
                PauseSource(source);
            }
        }

        /// <summary>
        /// 执行 Resume Handle。
        /// </summary>
        /// <param name="handle">handle 参数。</param>
        private void ResumeHandle(SoundHandle handle)
        {
            if (handle != null && m_ActiveSources.TryGetValue(handle, out var source))
            {
                ResumeSource(source);
            }
        }

        /// <summary>
        /// 执行 Pause Source。
        /// </summary>
        /// <param name="source">source 参数。</param>
        private static void PauseSource(SoundRuntimeSource source)
        {
            if (source?.Handle == null || source.Handle.Status != SoundStatus.Playing)
            {
                return;
            }

            source.AudioSource.Pause();
            source.Handle.SetStatus(SoundStatus.Paused);
        }

        /// <summary>
        /// 执行 Resume Source。
        /// </summary>
        /// <param name="source">source 参数。</param>
        private static void ResumeSource(SoundRuntimeSource source)
        {
            if (source?.Handle == null || source.Handle.Status != SoundStatus.Paused)
            {
                return;
            }

            source.AudioSource.UnPause();
            source.Handle.SetStatus(SoundStatus.Playing);
        }

        /// <summary>
        /// 执行 Stop All。
        /// </summary>
        private void StopAll()
        {
            foreach (var source in new List<SoundRuntimeSource>(m_ActiveSources.Values))
            {
                CompleteSourceAsync(source, SoundStatus.Stopped).Forget();
            }
        }

        /// <summary>
        /// 获取 Active Sources。
        /// </summary>
        /// <param name="track">track 参数。</param>
        /// <returns>执行结果。</returns>
        private List<SoundRuntimeSource> GetActiveSources(SoundTrack track)
        {
            var result = new List<SoundRuntimeSource>();
            foreach (var source in m_ActiveSources.Values)
            {
                if (track == SoundTrack.Master || source.Track == track)
                {
                    result.Add(source);
                }
            }

            return result;
        }

        /// <summary>
        /// 获取 Output。
        /// </summary>
        /// <param name="track">track 参数。</param>
        /// <returns>执行结果。</returns>
        private AudioMixerGroup GetOutput(SoundTrack track)
        {
            return m_TrackBindings.TryGetValue(track, out var binding) ? binding.Output : null;
        }

        /// <summary>
        /// 获取 Max Concurrent。
        /// </summary>
        /// <param name="track">track 参数。</param>
        /// <returns>执行结果。</returns>
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
        /// <param name="track">track 参数。</param>
        /// <returns>执行结果。</returns>
        private float GetTrackVolume(SoundTrack track)
        {
            return m_TrackVolumes.TryGetValue(track, out var volume) ? volume : 1f;
        }

        /// <summary>
        /// 执行 Apply Source Volume。
        /// </summary>
        /// <param name="source">source 参数。</param>
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
        /// <param name="track">track 参数。</param>
        /// <param name="volume">volume 参数。</param>
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
        /// <param name="track">track 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
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
        /// <param name="volume">volume 参数。</param>
        /// <returns>执行结果。</returns>
        private static float LinearToDecibel(float volume)
        {
            return volume <= 0f ? MinDecibel : Mathf.Log10(Mathf.Clamp01(volume)) * 20f;
        }

        /// <summary>
        /// 执行 Normalize Options。
        /// </summary>
        /// <param name="options">options 参数。</param>
        /// <param name="defaultTrack">default Track 参数。</param>
        /// <param name="defaultLoop">default Loop 参数。</param>
        /// <returns>执行结果。</returns>
        private static SoundPlayOptions NormalizeOptions(SoundPlayOptions options, SoundTrack defaultTrack, bool defaultLoop)
        {
            return new SoundPlayOptions
            {
                Track = options == null || options.Track == SoundTrack.Master ? defaultTrack : options.Track,
                Loop = options?.Loop ?? defaultLoop,
                Volume = options?.Volume ?? 1f,
                FadeIn = options?.FadeIn ?? 0f,
                FadeOut = options?.FadeOut ?? 0f,
                Priority = options?.Priority ?? 128,
            };
        }

        /// <summary>
        /// 校验 Location。
        /// </summary>
        /// <param name="location">location 参数。</param>
        private static void ValidateLocation(string location)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("Location cannot be empty.", nameof(location));
            }
        }

        /// <summary>
        /// 校验 Track。
        /// </summary>
        /// <param name="track">track 参数。</param>
        private static void ValidateTrack(SoundTrack track)
        {
            if (!Enum.IsDefined(typeof(SoundTrack), track))
            {
                throw new ArgumentException("Sound track is not valid.", nameof(track));
            }
        }

        /// <summary>
        /// 校验 Volume。
        /// </summary>
        /// <param name="volume">volume 参数。</param>
        private static void ValidateVolume(float volume)
        {
            if (volume < 0f || volume > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be between 0 and 1.");
            }
        }

        /// <summary>
        /// 校验 Playback Volume。
        /// </summary>
        /// <param name="volume">volume 参数。</param>
        private static void ValidatePlaybackVolume(float volume)
        {
            if (volume < 0f || volume > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(volume), "Sound option volume must be between 0 and 1.");
            }
        }

        /// <summary>
        /// 校验 Parameter。
        /// </summary>
        /// <param name="parameter">parameter 参数。</param>
        private static void ValidateParameter(string parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            if (string.IsNullOrWhiteSpace(parameter))
            {
                throw new ArgumentException("Parameter cannot be empty.", nameof(parameter));
            }
        }
    }
}
