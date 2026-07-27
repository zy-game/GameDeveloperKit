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
        private readonly Dictionary<string, PreloadOperation> m_PreloadOperations =
            new Dictionary<string, PreloadOperation>(StringComparer.Ordinal);
        private readonly List<VideoPlayableHandle> m_Active = new List<VideoPlayableHandle>();
        private readonly SemaphoreSlim m_PreloadGate = new SemaphoreSlim(1, 1);
        private bool m_Disposed;

        public event Action<VideoPlayableHandle> PlaybackStarted;

        public event Action<VideoPlayableHandle> PlaybackTextureChanged;

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

            if (m_PreloadOperations.TryGetValue(request.Path, out var pending))
            {
                await pending.Completion.Task.AttachExternalCancellation(cancellationToken);
                return;
            }

            var operation = new PreloadOperation(cancellationToken);
            m_PreloadOperations.Add(request.Path, operation);
            VideoPlayableHandle handle = null;
            var enteredGate = false;
            try
            {
                await m_PreloadGate.WaitAsync(operation.Cancellation.Token);
                enteredGate = true;
                ThrowIfDisposed();
                if (m_Preloads.TryGetValue(request.Path, out existing))
                {
                    await existing.WaitUntilReadyAsync(operation.Cancellation.Token);
                    operation.Completion.TrySetResult();
                    return;
                }

                handle = CreateHandle(request, true);
                m_Preloads.Add(request.Path, handle);
                handle.Preload();
                await handle.WaitUntilReadyAsync(operation.Cancellation.Token);
                operation.Completion.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                ReleaseCachedHandle(request.Path, handle);
                operation.Completion.TrySetCanceled();
                throw;
            }
            catch (Exception exception)
            {
                ReleaseCachedHandle(request.Path, handle);
                operation.Completion.TrySetException(exception);
                throw;
            }
            finally
            {
                if (enteredGate)
                {
                    m_PreloadGate.Release();
                }

                if (m_PreloadOperations.TryGetValue(request.Path, out pending) &&
                    ReferenceEquals(pending, operation))
                {
                    m_PreloadOperations.Remove(request.Path);
                }

                operation.Dispose();
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
                CancelPendingPreload(request.Path);
                handle = CreateHandle(request, false);
            }

            StartHandle(handle, value => value.Play());
            return UniTask.FromResult(handle);
        }

        public bool ReleasePreload(string path)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Video preload path cannot be empty.", nameof(path));
            }

            var released = CancelPendingPreload(path);
            if (m_Preloads.TryGetValue(path, out var handle) is false)
            {
                return released;
            }

            m_Preloads.Remove(path);
            handle.Dispose();
            return true;
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

            var hadFirstFrame = handle.HasFirstFrame;
            handle.Terminated += OnTerminated;
            handle.FirstFrameReady += OnFirstFrameReady;
            handle.TextureChanged += OnTextureChanged;
            m_Active.Add(handle);
            try
            {
                start(handle);
                if (hadFirstFrame)
                {
                    OnFirstFrameReady(handle);
                }
            }
            catch
            {
                handle.Terminated -= OnTerminated;
                handle.FirstFrameReady -= OnFirstFrameReady;
                handle.TextureChanged -= OnTextureChanged;
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
            foreach (var operation in new List<PreloadOperation>(m_PreloadOperations.Values))
            {
                operation.Cancel();
            }

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
            m_PreloadOperations.Clear();
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
            handle.TextureChanged -= OnTextureChanged;
            m_Active.Remove(handle);
            handle.Dispose();
        }

        private void OnTextureChanged(VideoPlayableHandle handle)
        {
            PlaybackTextureChanged?.Invoke(handle);
        }

        private bool CancelPendingPreload(string path)
        {
            if (m_PreloadOperations.TryGetValue(path, out var operation) is false)
            {
                return false;
            }

            operation.Cancel();
            return true;
        }

        private void ReleaseCachedHandle(string path, VideoPlayableHandle expected)
        {
            if (expected == null ||
                m_Preloads.TryGetValue(path, out var cached) is false ||
                ReferenceEquals(cached, expected) is false)
            {
                return;
            }

            m_Preloads.Remove(path);
            expected.Dispose();
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

        private sealed class PreloadOperation : IDisposable
        {
            internal PreloadOperation(CancellationToken cancellationToken)
            {
                Cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            }

            internal CancellationTokenSource Cancellation { get; }

            internal UniTaskCompletionSource Completion { get; } = new UniTaskCompletionSource();

            internal void Cancel()
            {
                if (Cancellation.IsCancellationRequested is false)
                {
                    Cancellation.Cancel();
                }
            }

            public void Dispose()
            {
                Cancellation.Dispose();
            }
        }
    }

}
