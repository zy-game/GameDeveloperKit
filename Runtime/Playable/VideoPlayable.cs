using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RenderHeads.Media.AVProVideo;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Playable
{
    public sealed class VideoPlayableOptions
    {
        public bool Loop { get; set; }

        public bool Seekable { get; set; }

        public Transform Parent { get; set; }

        public bool DontDestroyOnLoad { get; set; } = true;
    }

    public sealed class VideoPlayableRequest
    {
        public VideoPlayableRequest(string path, VideoPlayableOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty.", nameof(path));
            }

            Path = path;
            Options = options ?? new VideoPlayableOptions();
        }

        public string Path { get; }

        public VideoPlayableOptions Options { get; }
    }

    public sealed class VideoPlayable : PlayableBase<VideoPlayableRequest, VideoPlayableHandle>
    {
        private readonly Dictionary<string, VideoPlayableHandle> m_Preloads =
            new Dictionary<string, VideoPlayableHandle>(StringComparer.Ordinal);
        private readonly List<VideoPlayableHandle> m_Active = new List<VideoPlayableHandle>();
        private bool m_Disposed;

        public event Action<VideoPlayableHandle> PlaybackStarted;

        public IReadOnlyList<VideoPlayableHandle> ActiveHandles => m_Active;

        public async UniTask PreloadAsync(
            VideoPlayableRequest request,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateRequest(request);
            if (m_Preloads.TryGetValue(request.Path, out var existing))
            {
                await existing.WaitUntilReadyAsync(cancellationToken);
                return;
            }

            var handle = CreateHandle(request, true);
            m_Preloads.Add(request.Path, handle);
            try
            {
                handle.Preload();
                await handle.WaitUntilReadyAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                m_Preloads.Remove(request.Path);
                handle.Dispose();
                throw;
            }
        }

        public override UniTask<VideoPlayableHandle> PlayAsync(
            VideoPlayableRequest request,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateRequest(request);
            cancellationToken.ThrowIfCancellationRequested();
            VideoPlayableHandle handle;
            if (m_Preloads.TryGetValue(request.Path, out handle))
            {
                m_Preloads.Remove(request.Path);
                handle.ApplyOptions(request.Options);
            }
            else
            {
                handle = CreateHandle(request, false);
            }

            StartHandle(handle, value => value.Play());
            return UniTask.FromResult(handle);
        }

        internal void StartHandle(VideoPlayableHandle handle, Action<VideoPlayableHandle> start)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (start == null)
            {
                throw new ArgumentNullException(nameof(start));
            }

            handle.Terminated += OnTerminated;
            handle.FirstFrameReady += OnFirstFrameReady;
            m_Active.Add(handle);
            try
            {
                start(handle);
            }
            catch
            {
                handle.Terminated -= OnTerminated;
                handle.FirstFrameReady -= OnFirstFrameReady;
                m_Active.Remove(handle);
                handle.Dispose();
                throw;
            }
        }

        public override void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            foreach (var handle in new List<VideoPlayableHandle>(m_Active))
            {
                handle.Dispose();
            }

            foreach (var handle in m_Preloads.Values)
            {
                handle.Dispose();
            }

            m_Active.Clear();
            m_Preloads.Clear();
        }

        private VideoPlayableHandle CreateHandle(VideoPlayableRequest request, bool preloading)
        {
            return new VideoPlayableHandle(request.Path, request.Options, preloading);
        }

        private void OnFirstFrameReady(VideoPlayableHandle handle)
        {
            PlaybackStarted?.Invoke(handle);
        }

        private void OnTerminated(VideoPlayableHandle handle)
        {
            handle.Terminated -= OnTerminated;
            handle.FirstFrameReady -= OnFirstFrameReady;
            m_Active.Remove(handle);
            handle.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(VideoPlayable));
            }
        }

        private static void ValidateRequest(VideoPlayableRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
        }
    }

    public sealed class VideoPlayableHandle : PlayableHandle
    {
        private readonly GameObject m_GameObject;
        private readonly MediaPlayer m_Player;
        private readonly UniTaskCompletionSource m_Ready = new UniTaskCompletionSource();
        private bool m_Preloading;
        private bool m_FirstFrame;
        private bool m_Terminated;

        internal VideoPlayableHandle(string path, VideoPlayableOptions options, bool preloading)
        {
            Path = path;
            m_Preloading = preloading;
            m_GameObject = new GameObject(preloading ? "VideoPlayablePreload" : "VideoPlayable");
            ApplyParent(options);
            m_Player = m_GameObject.AddComponent<MediaPlayer>();
            m_Player.AutoOpen = false;
            m_Player.AutoStart = false;
            m_Player.Events.AddListener(OnMediaEvent);
            ApplyOptions(options);
        }

        public string Path { get; }

        public Texture Texture => m_Player?.TextureProducer?.GetTexture(0);

        public bool HasFirstFrame => m_FirstFrame && Texture != null;

        public bool RequiresVerticalFlip => m_Player?.TextureProducer?.RequiresVerticalFlip() ?? false;

        public bool CanSeek => Seekable && IsValidDuration(DurationSeconds) && m_Player?.Control?.CanPlay() == true;

        public bool CanPause => Status is PlayableStatus.Playing or PlayableStatus.Paused;

        public bool IsPaused => Status == PlayableStatus.Paused;

        public bool Seekable { get; private set; }

        public double DurationSeconds => m_Player?.Info?.GetDuration() ?? 0d;

        public double CurrentTimeSeconds => m_Player?.Control?.GetCurrentTime() ?? 0d;

        public event Action<VideoPlayableHandle> FirstFrameReady;

        internal event Action<VideoPlayableHandle> Terminated;

        internal void ApplyOptions(VideoPlayableOptions options)
        {
            options ??= new VideoPlayableOptions();
            Seekable = options.Seekable;
            if (m_Player != null)
            {
                m_Player.Loop = options.Loop;
            }
        }

        internal void Preload()
        {
            m_Player.AudioMuted = true;
            Open(false);
        }

        internal void Play()
        {
            if (Status == PlayableStatus.Preparing)
            {
                SetPlaying();
            }

            m_Player.AudioMuted = false;
            if (m_Preloading)
            {
                m_Preloading = false;
                m_Player.Play();
            }
            else
            {
                Open(true);
            }
        }

        internal UniTask WaitUntilReadyAsync(CancellationToken cancellationToken)
        {
            return m_Ready.Task.AttachExternalCancellation(cancellationToken);
        }

        public void Seek(double timeSeconds)
        {
            if (!CanSeek)
            {
                throw new GameException($"Video cannot seek: {Path}");
            }

            m_Player.Control.Seek(Math.Max(0d, Math.Min(timeSeconds, DurationSeconds)));
        }

        protected override void OnPause()
        {
            m_Player.Pause();
        }

        protected override void OnResume()
        {
            m_Player.Play();
        }

        protected override void OnStop()
        {
            Terminate();
        }

        protected override void OnDispose()
        {
            try
            {
                m_Player.Events.RemoveListener(OnMediaEvent);
                try
                {
                    m_Player.Stop();
                }
                finally
                {
                    m_Player.CloseMedia();
                }
            }
            finally
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(m_GameObject);
                }
                else
                {
                    Object.DestroyImmediate(m_GameObject);
                }
            }
        }

        private void Open(bool autoPlay)
        {
            if (!m_Player.OpenMedia(MediaPathType.AbsolutePathOrURL, Path, autoPlay))
            {
                throw new GameException($"AVPro cannot open video: {Path}");
            }
        }

        private void OnMediaEvent(MediaPlayer player, MediaPlayerEvent.EventType eventType, ErrorCode errorCode)
        {
            switch (eventType)
            {
                case MediaPlayerEvent.EventType.ReadyToPlay:
                    m_Ready.TrySetResult();
                    if (m_Preloading)
                    {
                        m_Player.Play();
                    }
                    break;
                case MediaPlayerEvent.EventType.FirstFrameReady:
                    if (!m_FirstFrame)
                    {
                        m_FirstFrame = true;
                        FirstFrameReady?.Invoke(this);
                    }

                    if (m_Preloading)
                    {
                        m_Player.Pause();
                    }
                    break;
                case MediaPlayerEvent.EventType.FinishedPlaying:
                    SetCompleted();
                    Terminate();
                    break;
                case MediaPlayerEvent.EventType.Error:
                    var exception = new GameException($"AVPro video error. path:{Path} error:{errorCode}");
                    m_Ready.TrySetException(exception);
                    SetFailed(exception);
                    Terminate();
                    break;
            }
        }

        private void Terminate()
        {
            if (m_Terminated)
            {
                return;
            }

            m_Terminated = true;
            Terminated?.Invoke(this);
        }

        private void ApplyParent(VideoPlayableOptions options)
        {
            if (options?.Parent != null)
            {
                m_GameObject.transform.SetParent(options.Parent, false);
            }
            else if (options?.DontDestroyOnLoad != false)
            {
                Object.DontDestroyOnLoad(m_GameObject);
            }
        }

        private static bool IsValidDuration(double duration)
        {
            return duration > 0d && !double.IsNaN(duration) && !double.IsInfinity(duration);
        }
    }
}
