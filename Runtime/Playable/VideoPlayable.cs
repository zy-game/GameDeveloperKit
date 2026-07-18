using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Playable
{
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

}
