using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Playable
{
    public sealed class AudioPlayable : PlayableBase<AudioPlayableRequest, AudioPlayableHandle>
    {
        public const string RootName = "GameDeveloperKit.AudioPlayableRoot";
        private const int DefaultMaxConcurrent = 16;
        private const float MinDecibel = -80f;

        private readonly Dictionary<AudioTrack, float> m_TrackVolumes = new Dictionary<AudioTrack, float>();
        private readonly Dictionary<AudioTrack, AudioTrackMixerBinding> m_TrackBindings =
            new Dictionary<AudioTrack, AudioTrackMixerBinding>();
        private readonly Dictionary<string, AudioMixerSnapshot> m_Snapshots =
            new Dictionary<string, AudioMixerSnapshot>();
        private readonly Dictionary<AudioPlayableHandle, AudioRuntimeSource> m_ActiveSources =
            new Dictionary<AudioPlayableHandle, AudioRuntimeSource>();
        private readonly List<AudioRuntimeSource> m_Sources = new List<AudioRuntimeSource>();

        private readonly GameObject m_Root;
        private AudioMixerSettings m_Settings;
        private AudioPlayableHandle m_PreparingMusic;
        private long m_MusicRequestIdentity;
        private long m_Sequence;
        private bool m_Disposed;

        public AudioPlayable()
        {
            m_Root = new GameObject(RootName);
            Object.DontDestroyOnLoad(m_Root);
            InitializeSettings();
        }

        public override async UniTask<AudioPlayableHandle> PlayAsync(
            AudioPlayableRequest request,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var options = NormalizeOptions(request.Options);
            ValidateOptions(options);
            var handle = new AudioPlayableHandle(
                request.Location,
                options.Track,
                StopHandle,
                PauseHandle,
                ResumeHandle);

            var requestIdentity = BeginRequest(options.Track, handle);
            using var loadingCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            handle.BindPreparationCancellation(loadingCancellation);

            using var cancellationRegistration = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(static state =>
                {
                    var tuple = (Tuple<AudioPlayableHandle, CancellationToken>)state;
                    tuple.Item1.RequestCancel(tuple.Item2);
                }, Tuple.Create(handle, cancellationToken))
                : default;

            AudioClipLease clipLease;
            try
            {
                clipLease = await LoadAudioClipAsync(request, loadingCancellation.Token);
            }
            catch (Exception exception)
            {
                ClearPreparingMusic(handle);
                if (handle.Status == PlayableStatus.Canceled)
                {
                    return handle;
                }

                handle.Fail(exception);
                throw;
            }
            finally
            {
                handle.ClearPreparationCancellation();
            }

            if (handle.Status == PlayableStatus.Canceled ||
                cancellationToken.IsCancellationRequested ||
                !CanCommitRequest(options.Track, requestIdentity))
            {
                clipLease.Dispose();
                ClearPreparingMusic(handle);
                handle.Cancel(cancellationToken);
                return handle;
            }

            ClearPreparingMusic(handle);
            try
            {
                if (options.Track == AudioTrack.Music)
                {
                    StopActiveMusic();
                }
                else
                {
                    EnforceMaxConcurrent(options.Track, options.Priority);
                }
            }
            catch (Exception exception)
            {
                clipLease.Dispose();
                handle.Fail(exception);
                throw;
            }

            var source = GetSource(options.Track);
            try
            {
                StartSource(source, handle, clipLease, options);
                handle.Start(cancellationToken);
                WatchCompletionAsync(source, source.Version).Forget(Debug.LogException);
                return handle;
            }
            catch (Exception exception)
            {
                DetachSource(source);
                handle.Fail(exception);
                throw new GameException($"Failed to play audio clip: {request.Location}", exception);
            }
        }

        public void SetVolume(AudioTrack track, float volume)
        {
            ValidateTrack(track, true);
            ValidateUnitValue(volume, nameof(volume));
            m_TrackVolumes[track] = volume;
            ApplyMixerVolume(track, volume);
            foreach (var source in m_ActiveSources.Values)
            {
                if (track == AudioTrack.Master || source.Track == track)
                {
                    ApplySourceVolume(source);
                }
            }
        }

        public float GetVolume(AudioTrack track)
        {
            ValidateTrack(track, true);
            return GetTrackVolume(track);
        }

        public void StopTrack(AudioTrack track)
        {
            ValidateTrack(track, true);
            foreach (var source in GetActiveSources(track))
            {
                source.Handle?.Stop();
            }
        }

        public void PauseTrack(AudioTrack track)
        {
            ValidateTrack(track, true);
            foreach (var source in GetActiveSources(track))
            {
                source.Handle?.Pause();
            }
        }

        public void ResumeTrack(AudioTrack track)
        {
            ValidateTrack(track, true);
            foreach (var source in GetActiveSources(track))
            {
                source.Handle?.Resume();
            }
        }

        public bool SetMixerFloat(string parameter, float value)
        {
            ValidateParameter(parameter);
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Mixer value must be finite.");
            }

            return m_Settings != null && m_Settings.Mixer != null && m_Settings.Mixer.SetFloat(parameter, value);
        }

        public bool TransitionSnapshot(string snapshotName, float duration)
        {
            ValidateParameter(snapshotName);
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), "Snapshot duration must be finite and non-negative.");
            }

            if (!m_Snapshots.TryGetValue(snapshotName, out var snapshot) || snapshot == null)
            {
                return false;
            }

            snapshot.TransitionTo(duration);
            return true;
        }

        public void ConfigureMixer(AudioMixerSettings settings)
        {
            ThrowIfDisposed();
            m_Settings = settings;
            InitializeSettings();
            foreach (var source in m_Sources)
            {
                source.AudioSource.outputAudioMixerGroup = GetOutput(source.Track);
                ApplySourceVolume(source);
            }
        }

        public override void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            m_MusicRequestIdentity++;
            m_PreparingMusic?.Cancel();
            m_PreparingMusic = null;

            foreach (var source in m_Sources.ToArray())
            {
                source.Handle?.Stop();
                DetachSource(source);
                if (source.AudioSource != null)
                {
                    Object.DestroyImmediate(source.AudioSource.gameObject);
                }
            }

            m_ActiveSources.Clear();
            m_Sources.Clear();
            m_TrackBindings.Clear();
            m_TrackVolumes.Clear();
            m_Snapshots.Clear();
            m_Settings = null;
            if (m_Root != null)
            {
                Object.DestroyImmediate(m_Root);
            }
        }

        private void StartSource(
            AudioRuntimeSource source,
            AudioPlayableHandle handle,
            AudioClipLease clipLease,
            AudioPlayableOptions options)
        {
            var clip = clipLease.Clip;
            source.Version++;
            source.FadeVersion++;
            source.Handle = handle;
            source.ClipLease = clipLease;
            source.Track = options.Track;
            source.Primary = options.Track == AudioTrack.Music;
            source.InUse = true;
            source.Volume = options.Volume;
            source.FadeGain = options.FadeIn > 0f ? 0f : 1f;
            source.FadeIn = options.FadeIn;
            source.FadeOut = options.FadeOut;
            source.NaturalFadeOutStarted = false;
            source.Priority = options.Priority;
            source.Sequence = ++m_Sequence;

            var audioSource = source.AudioSource;
            audioSource.clip = clip;
            audioSource.loop = options.Loop;
            audioSource.priority = options.Priority;
            audioSource.outputAudioMixerGroup = GetOutput(options.Track);
            audioSource.spatialBlend = options.Position.HasValue ? 1f : 0f;
            audioSource.transform.localPosition = options.Position ?? Vector3.zero;
            ApplySourceVolume(source);
            m_ActiveSources.Add(handle, source);
            audioSource.Play();

            if (options.FadeIn > 0f)
            {
                FadeAsync(source, source.Version, source.FadeVersion, 0f, 1f, options.FadeIn)
                    .Forget(Debug.LogException);
            }
        }

        private async UniTask WatchCompletionAsync(AudioRuntimeSource source, int version)
        {
            while (source.InUse && source.Version == version && source.Handle != null)
            {
                var handle = source.Handle;
                if (handle.Status == PlayableStatus.Playing)
                {
                    if (!source.AudioSource.isPlaying)
                    {
                        CompleteSource(source);
                        return;
                    }

                    var clip = source.AudioSource.clip;
                    if (clip != null && clip.length > 0f)
                    {
                        handle.Progress = Mathf.Clamp01(source.AudioSource.time / clip.length);
                        var remaining = clip.length - source.AudioSource.time;
                        if (!source.AudioSource.loop &&
                            source.FadeOut > 0f &&
                            !source.NaturalFadeOutStarted &&
                            remaining <= source.FadeOut)
                        {
                            source.NaturalFadeOutStarted = true;
                            source.FadeVersion++;
                            FadeAsync(
                                source,
                                version,
                                source.FadeVersion,
                                source.FadeGain,
                                0f,
                                Mathf.Max(remaining, 0.0001f)).Forget(Debug.LogException);
                        }
                    }
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        private async UniTask FadeAsync(
            AudioRuntimeSource source,
            int sourceVersion,
            int fadeVersion,
            float from,
            float to,
            float duration,
            bool detachWhenFinished = false)
        {
            var elapsed = 0f;
            while (source.InUse &&
                   source.Version == sourceVersion &&
                   source.FadeVersion == fadeVersion &&
                   elapsed < duration)
            {
                source.FadeGain = Mathf.Lerp(from, to, elapsed / duration);
                ApplySourceVolume(source);
                await UniTask.Yield(PlayerLoopTiming.Update);
                elapsed += Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            }

            if (!source.InUse || source.Version != sourceVersion || source.FadeVersion != fadeVersion)
            {
                return;
            }

            source.FadeGain = to;
            ApplySourceVolume(source);
            if (detachWhenFinished)
            {
                DetachSource(source);
            }
        }

        private void StopHandle(AudioPlayableHandle handle)
        {
            if (handle == null || !m_ActiveSources.TryGetValue(handle, out var source))
            {
                return;
            }

            m_ActiveSources.Remove(handle);
            source.Version++;
            source.FadeVersion++;
            source.Handle = null;
            if (source.FadeOut <= 0f || !source.AudioSource.isPlaying)
            {
                DetachSource(source);
                return;
            }

            FadeAsync(
                source,
                source.Version,
                source.FadeVersion,
                source.FadeGain,
                0f,
                source.FadeOut,
                true).Forget(Debug.LogException);
        }

        private void PauseHandle(AudioPlayableHandle handle)
        {
            if (handle == null || !m_ActiveSources.TryGetValue(handle, out var source))
            {
                return;
            }

            source.FadeVersion++;
            source.AudioSource.Pause();
        }

        private void ResumeHandle(AudioPlayableHandle handle)
        {
            if (handle == null || !m_ActiveSources.TryGetValue(handle, out var source))
            {
                return;
            }

            source.AudioSource.UnPause();
            if (source.NaturalFadeOutStarted && !source.AudioSource.loop)
            {
                var clip = source.AudioSource.clip;
                var remaining = clip == null ? 0f : Mathf.Max(clip.length - source.AudioSource.time, 0f);
                if (remaining <= 0f)
                {
                    CompleteSource(source);
                    return;
                }

                source.FadeVersion++;
                FadeAsync(
                    source,
                    source.Version,
                    source.FadeVersion,
                    source.FadeGain,
                    0f,
                    remaining).Forget(Debug.LogException);
            }
            else if (source.FadeGain < 1f)
            {
                source.FadeVersion++;
                FadeAsync(
                    source,
                    source.Version,
                    source.FadeVersion,
                    source.FadeGain,
                    1f,
                    Mathf.Max(source.FadeIn * (1f - source.FadeGain), 0.0001f)).Forget(Debug.LogException);
            }
        }

        private void CompleteSource(AudioRuntimeSource source)
        {
            var handle = source.Handle;
            if (handle != null)
            {
                m_ActiveSources.Remove(handle);
                handle.Progress = 1f;
            }

            DetachSource(source);
            handle?.Complete();
        }

        private void DetachSource(AudioRuntimeSource source)
        {
            if (source == null || !source.InUse)
            {
                return;
            }

            var handle = source.Handle;
            var clipLease = source.ClipLease;
            source.Version++;
            source.FadeVersion++;
            source.InUse = false;
            source.Handle = null;
            source.ClipLease = null;
            source.Volume = 1f;
            source.FadeGain = 1f;
            source.FadeIn = 0f;
            source.FadeOut = 0f;
            source.NaturalFadeOutStarted = false;
            source.Priority = 0;

            if (handle != null)
            {
                m_ActiveSources.Remove(handle);
            }

            if (source.AudioSource != null)
            {
                source.AudioSource.Stop();
                source.AudioSource.clip = null;
                source.AudioSource.loop = false;
                source.AudioSource.spatialBlend = 0f;
                source.AudioSource.transform.localPosition = Vector3.zero;
                source.AudioSource.volume = 1f;
            }

            clipLease?.Dispose();
        }

        private AudioRuntimeSource GetSource(AudioTrack track)
        {
            var primary = track == AudioTrack.Music;
            foreach (var source in m_Sources)
            {
                if (!source.InUse && source.Primary == primary && source.Track == track)
                {
                    return source;
                }
            }

            var sourceObject = new GameObject($"{track}AudioSource");
            sourceObject.transform.SetParent(m_Root.transform, false);
            var audioSource = sourceObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            var result = new AudioRuntimeSource
            {
                AudioSource = audioSource,
                Track = track,
                Primary = primary,
            };
            m_Sources.Add(result);
            return result;
        }

        private void StopActiveMusic()
        {
            foreach (var source in GetActiveSources(AudioTrack.Music))
            {
                source.Handle?.Stop();
            }
        }

        private void EnforceMaxConcurrent(AudioTrack track, int priority)
        {
            var active = GetActiveSources(track);
            var maxConcurrent = GetMaxConcurrent(track);
            if (maxConcurrent <= 0 || active.Count < maxConcurrent)
            {
                return;
            }

            AudioRuntimeSource candidate = null;
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
                throw new GameException($"Audio track '{track}' has reached max concurrent limit.");
            }

            candidate.Handle?.Stop();
        }

        private List<AudioRuntimeSource> GetActiveSources(AudioTrack track)
        {
            var result = new List<AudioRuntimeSource>();
            foreach (var source in m_ActiveSources.Values)
            {
                if (track == AudioTrack.Master || source.Track == track)
                {
                    result.Add(source);
                }
            }

            return result;
        }

        private static async UniTask<AudioClipLease> LoadAudioClipAsync(
            AudioPlayableRequest request,
            CancellationToken cancellationToken)
        {
            if (request.LocationKind != AudioLocationKind.Resource)
            {
                return await LoadExternalAudioClipAsync(request, cancellationToken);
            }

            AssetHandle assetHandle;
            try
            {
                assetHandle = await App.Resource.LoadAssetAsync(request.Location);
            }
            catch (Exception exception)
            {
                throw new GameException($"Failed to load audio clip: {request.Location}", exception);
            }

            var clip = assetHandle?.GetAsset<AudioClip>();
            if (clip != null)
            {
                return AudioClipLease.FromAsset(assetHandle, clip);
            }

            var error = assetHandle?.Error;
            assetHandle?.Release();
            throw new GameException($"Asset is not an AudioClip: {request.Location}", error);
        }

        private static async UniTask<AudioClipLease> LoadExternalAudioClipAsync(
            AudioPlayableRequest request,
            CancellationToken cancellationToken)
        {
            var address = request.LocationKind == AudioLocationKind.Url
                ? request.Location
                : ResolveStreamingAssetsAddress(request.Location);
            var audioType = ResolveAudioType(address);
            using (var webRequest = UnityWebRequestMultimedia.GetAudioClip(address, audioType))
            using (cancellationToken.Register(webRequest.Abort))
            {
                try
                {
                    await webRequest.SendWebRequest();
                }
                catch (Exception exception)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    throw new GameException($"Failed to load audio clip: {request.Location}", exception);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    throw new GameException($"Failed to load audio clip: {request.Location}. {webRequest.error}");
                }

                var clip = DownloadHandlerAudioClip.GetContent(webRequest);
                if (clip == null)
                {
                    throw new GameException($"Downloaded media is not an AudioClip: {request.Location}");
                }

                return AudioClipLease.FromTemporary(clip);
            }
        }

        private static string ResolveStreamingAssetsAddress(string location)
        {
            var root = Application.streamingAssetsPath.Replace('\\', '/').TrimEnd('/');
            var relative = location.Replace('\\', '/');
            if (root.IndexOf("://", StringComparison.Ordinal) >= 0)
            {
                return root + "/" + relative;
            }

            return new Uri(System.IO.Path.Combine(root, relative)).AbsoluteUri;
        }

        private static AudioType ResolveAudioType(string address)
        {
            var path = Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri.AbsolutePath : address;
            var extension = System.IO.Path.GetExtension(path);
            if (string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase)) return AudioType.MPEG;
            if (string.Equals(extension, ".ogg", StringComparison.OrdinalIgnoreCase)) return AudioType.OGGVORBIS;
            if (string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase)) return AudioType.WAV;
            if (string.Equals(extension, ".aif", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".aiff", StringComparison.OrdinalIgnoreCase)) return AudioType.AIFF;
            throw new GameException($"Audio format is unsupported: {extension}");
        }

        private void InitializeSettings()
        {
            m_TrackVolumes.Clear();
            foreach (AudioTrack track in Enum.GetValues(typeof(AudioTrack)))
            {
                m_TrackVolumes.Add(track, 1f);
            }

            m_TrackBindings.Clear();
            m_Snapshots.Clear();
            if (m_Settings == null)
            {
                return;
            }

            if (m_Settings.Tracks != null)
            {
                foreach (var binding in m_Settings.Tracks)
                {
                    if (binding == null)
                    {
                        continue;
                    }

                    m_TrackBindings[binding.Track] = binding;
                    m_TrackVolumes[binding.Track] = Mathf.Clamp01(binding.DefaultVolume);
                    ApplyMixerVolume(binding.Track, m_TrackVolumes[binding.Track]);
                }
            }

            if (m_Settings.Snapshots == null)
            {
                return;
            }

            foreach (var binding in m_Settings.Snapshots)
            {
                if (binding != null && !string.IsNullOrWhiteSpace(binding.Name) && binding.Snapshot != null)
                {
                    m_Snapshots[binding.Name] = binding.Snapshot;
                }
            }
        }

        private void ApplySourceVolume(AudioRuntimeSource source)
        {
            if (source?.AudioSource == null)
            {
                return;
            }

            var master = HasVolumeParameter(AudioTrack.Master) ? 1f : GetTrackVolume(AudioTrack.Master);
            var track = HasVolumeParameter(source.Track) ? 1f : GetTrackVolume(source.Track);
            source.AudioSource.volume = Mathf.Clamp01(source.Volume * source.FadeGain * master * track);
        }

        private void ApplyMixerVolume(AudioTrack track, float volume)
        {
            if (!m_TrackBindings.TryGetValue(track, out var binding) ||
                string.IsNullOrWhiteSpace(binding.VolumeParameter) ||
                m_Settings?.Mixer == null)
            {
                return;
            }

            m_Settings.Mixer.SetFloat(binding.VolumeParameter, LinearToDecibel(volume));
        }

        private bool HasVolumeParameter(AudioTrack track)
        {
            return m_TrackBindings.TryGetValue(track, out var binding) &&
                   m_Settings?.Mixer != null &&
                   !string.IsNullOrWhiteSpace(binding.VolumeParameter);
        }

        private AudioMixerGroup GetOutput(AudioTrack track)
        {
            return m_TrackBindings.TryGetValue(track, out var binding) ? binding.Output : null;
        }

        private int GetMaxConcurrent(AudioTrack track)
        {
            return m_TrackBindings.TryGetValue(track, out var binding) && binding.MaxConcurrent > 0
                ? binding.MaxConcurrent
                : DefaultMaxConcurrent;
        }

        private float GetTrackVolume(AudioTrack track)
        {
            return m_TrackVolumes.TryGetValue(track, out var volume) ? volume : 1f;
        }

        private void ClearPreparingMusic(AudioPlayableHandle handle)
        {
            if (ReferenceEquals(m_PreparingMusic, handle))
            {
                m_PreparingMusic = null;
            }
        }

        internal long BeginRequest(AudioTrack track, AudioPlayableHandle handle)
        {
            if (track != AudioTrack.Music)
            {
                return 0L;
            }

            var requestIdentity = ++m_MusicRequestIdentity;
            var previousPreparing = m_PreparingMusic;
            m_PreparingMusic = handle;
            previousPreparing?.Cancel();
            return requestIdentity;
        }

        internal bool CanCommitRequest(AudioTrack track, long requestIdentity)
        {
            return track != AudioTrack.Music || requestIdentity == m_MusicRequestIdentity;
        }

        private void ThrowIfDisposed()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(AudioPlayable));
            }
        }

        private static AudioPlayableOptions NormalizeOptions(AudioPlayableOptions options)
        {
            options ??= new AudioPlayableOptions();
            return new AudioPlayableOptions
            {
                Track = options.Track,
                Loop = options.Loop,
                Volume = options.Volume,
                FadeIn = options.FadeIn,
                FadeOut = options.FadeOut,
                Priority = options.Priority,
                Position = options.Position,
            };
        }

        private static void ValidateOptions(AudioPlayableOptions options)
        {
            ValidateTrack(options.Track, false);
            ValidateUnitValue(options.Volume, nameof(options.Volume));
            ValidateDuration(options.FadeIn, nameof(options.FadeIn));
            ValidateDuration(options.FadeOut, nameof(options.FadeOut));
            if (options.Priority < 0 || options.Priority > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(options.Priority), "Priority must be between 0 and 256.");
            }
        }

        private static void ValidateTrack(AudioTrack track, bool allowMaster)
        {
            if (!Enum.IsDefined(typeof(AudioTrack), track) || (!allowMaster && track == AudioTrack.Master))
            {
                throw new ArgumentException("Audio track is not valid.", nameof(track));
            }
        }

        private static void ValidateUnitValue(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and between 0 and 1.");
            }
        }

        private static void ValidateDuration(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Duration must be finite and non-negative.");
            }
        }

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

        private static float LinearToDecibel(float volume)
        {
            return volume <= 0f ? MinDecibel : Mathf.Log10(volume) * 20f;
        }
    }
}
