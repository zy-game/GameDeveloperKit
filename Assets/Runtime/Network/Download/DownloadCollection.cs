using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    public sealed class DownloadCollection : IDisposable
    {
        private readonly DownloadModule _module;
        private readonly ConcurrentDictionary<string, DownloadHandle> _handles
            = new ConcurrentDictionary<string, DownloadHandle>();

        private int _status;
        private int _disposed;

        private readonly object _progressLock = new object();
        private float _totalProgress;

        public DownloadStatus Status
        {
            get => (DownloadStatus)Interlocked.CompareExchange(ref _status, 0, 0);
        }

        public int TotalCount => _handles.Count;

        public int CompletedCount
        {
            get => _handles.Values.Count(h => h.Status == DownloadStatus.Completed);
        }

        public int FailedCount
        {
            get => _handles.Values.Count(h => h.Status == DownloadStatus.Failed);
        }

        public float Progress
        {
            get
            {
                lock (_progressLock)
                    return _totalProgress;
            }
        }

        public long TotalCurrentSpeed => _handles.Values.Sum(h => h.CurrentSpeed);
        public long TotalAverageSpeed => _handles.Values.Sum(h => h.AverageSpeed);

        public IReadOnlyList<DownloadHandle> AllHandles => _handles.Values.ToList();

        public IReadOnlyList<DownloadHandle> CompletedHandles
            => _handles.Values.Where(h => h.Status == DownloadStatus.Completed).ToList();

        public IReadOnlyList<DownloadHandle> FailedHandles
            => _handles.Values.Where(h => h.Status == DownloadStatus.Failed).ToList();

        public IReadOnlyList<DownloadHandle> RunningHandles
            => _handles.Values.Where(h => h.Status == DownloadStatus.Running).ToList();

        public event Action<float> ProgressChanged;
        public event Action<DownloadHandle> ItemCompleted;
        public event Action<bool> AllCompleted;

        internal DownloadCollection(DownloadModule module)
        {
            _module = module;
        }

        public void Add(string url, string version, CancellationToken cancellationToken = default)
        {
            if (Interlocked.CompareExchange(ref _disposed, 0, 0) == 1)
                throw new ObjectDisposedException(nameof(DownloadCollection));

            if (_handles.ContainsKey(url)) return;

            var handle = _module.DownloadAsync(url, version, null, cancellationToken);

            if (_handles.TryAdd(url, handle))
            {
                handle.ProgressChanged += OnHandleProgress;
                handle.Completed += OnHandleCompleted;

                UpdateStatus();
                UpdateProgress();
            }
        }

        public void Remove(string url)
        {
            if (Interlocked.CompareExchange(ref _disposed, 0, 0) == 1)
                return;

            if (_handles.TryRemove(url, out var handle))
            {
                handle.Cancel();
                handle.ProgressChanged -= OnHandleProgress;
                handle.Completed -= OnHandleCompleted;

                UpdateStatus();
                UpdateProgress();
            }
        }

        public bool Contains(string url) => _handles.ContainsKey(url);

        public void PauseAll()
        {
            foreach (var handle in _handles.Values)
            {
                if (handle.Status == DownloadStatus.Running)
                    handle.Pause();
            }
            UpdateStatus();
        }

        public void ResumeAll()
        {
            foreach (var handle in _handles.Values)
            {
                if (handle.Status == DownloadStatus.Paused)
                    handle.Resume();
            }
            UpdateStatus();
        }

        public void CancelAll()
        {
            foreach (var handle in _handles.Values)
            {
                handle.Cancel();
            }
            UpdateStatus();
        }

        public async UniTask<bool> WaitForCompletionAsync()
        {
            var tasks = _handles.Values
                .Select(h => h.WaitForCompletionAsync())
                .ToList();

            if (tasks.Count == 0)
            {
                AllCompleted?.Invoke(true);
                return true;
            }

            var results = await UniTask.WhenAll(tasks);

            bool allSuccess = results.All(r => r.IsSuccess);
            AllCompleted?.Invoke(allSuccess);

            return allSuccess;
        }

        private void OnHandleProgress(float _)
        {
            UpdateProgress();
        }

        private void OnHandleCompleted(DownloadResult result)
        {
            UpdateStatus();
            UpdateProgress();

            var handle = _handles.Values
                .FirstOrDefault(h => h.SavedFilePath == result.FilePath);

            if (handle != null)
            {
                ItemCompleted?.Invoke(handle);
            }
        }

        private void UpdateProgress()
        {
            var handles = _handles.Values.ToList();

            if (handles.Count == 0)
            {
                lock (_progressLock)
                {
                    _totalProgress = 0;
                }
                ProgressChanged?.Invoke(0);
                return;
            }

            float sum = handles.Sum(h => h.Progress);
            float progress = sum / handles.Count;

            lock (_progressLock)
            {
                _totalProgress = progress;
            }

            ProgressChanged?.Invoke(progress);
        }

        private void UpdateStatus()
        {
            var handles = _handles.Values.ToList();

            if (handles.Count == 0)
            {
                Interlocked.Exchange(ref _status, (int)DownloadStatus.None);
                return;
            }

            var statuses = handles.Select(h => h.Status).ToList();

            DownloadStatus newStatus;

            if (statuses.All(s => s == DownloadStatus.Completed ||
                                  s == DownloadStatus.Failed ||
                                  s == DownloadStatus.Canceled))
            {
                newStatus = statuses.All(s => s == DownloadStatus.Completed)
                    ? DownloadStatus.Completed
                    : DownloadStatus.Failed;
            }
            else if (statuses.Any(s => s == DownloadStatus.Running))
            {
                newStatus = DownloadStatus.Running;
            }
            else if (statuses.All(s => s == DownloadStatus.Paused))
            {
                newStatus = DownloadStatus.Paused;
            }
            else if (statuses.Any(s => s == DownloadStatus.Queued))
            {
                newStatus = DownloadStatus.Queued;
            }
            else
            {
                newStatus = DownloadStatus.None;
            }

            Interlocked.Exchange(ref _status, (int)newStatus);
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1)
                return;

            foreach (var handle in _handles.Values)
            {
                handle.ProgressChanged -= OnHandleProgress;
                handle.Completed -= OnHandleCompleted;
                handle.Dispose();
            }
            _handles.Clear();
        }
    }
}
