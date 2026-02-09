using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Network
{
    public sealed class DownloadModule : IModule, IDownloadManager
    {
        private readonly ConcurrentDictionary<string, DownloadHandle> _activeDownloads = new ConcurrentDictionary<string, DownloadHandle>();

        private readonly DownloadQueue _queue = new DownloadQueue();
        private SemaphoreSlim _concurrencySemaphore;

        private DownloadConfig _defaultConfig = new DownloadConfig();
        private int _maxConcurrentDownloads = 3;
        private int _isProcessingQueue = 0;

        public void OnStartup()
        {
            _concurrencySemaphore = new SemaphoreSlim(_maxConcurrentDownloads, _maxConcurrentDownloads);
        }

        public void OnUpdate(float elapseSeconds)
        {
        }

        public void OnClearup()
        {
            foreach (var handle in _activeDownloads.Values)
            {
                handle.Dispose();
            }

            _activeDownloads.Clear();
            _concurrencySemaphore?.Dispose();
        }

        /// <summary>
        /// 设置最大并发下载数
        /// </summary>
        public void SetMaxConcurrentDownloads(int max)
        {
            if (max <= 0) throw new ArgumentException("Max concurrent downloads must be > 0");

            _maxConcurrentDownloads = max;

            var oldSemaphore = _concurrencySemaphore;
            _concurrencySemaphore = new SemaphoreSlim(max, max);
            oldSemaphore?.Dispose();
        }

        /// <summary>
        /// 设置默认配置
        /// </summary>
        public void SetDefaultConfig(DownloadConfig config)
        {
            _defaultConfig = config ?? new DownloadConfig();
        }

        /// <summary>
        /// 下载文件（异步）
        /// </summary>
        public DownloadHandle DownloadAsync(string url, string version,
            DownloadConfig config = null, CancellationToken cancellationToken = default)
        {
            var key = GetDownloadKey(url, version);

            if (_activeDownloads.TryGetValue(key, out var existing))
            {
                return existing;
            }

            config ??= _defaultConfig;
            var handle = new DownloadHandle(url, version, config, cancellationToken);

            handle.Completed += result => OnDownloadCompleted(key, result);

            var added = _activeDownloads.TryAdd(key, handle);

            if (added)
            {
                _queue.Enqueue(handle, config.Priority);
                ProcessQueueAsync().Forget();
            }
            else
            {
                handle.Dispose();
                _activeDownloads.TryGetValue(key, out handle);
            }

            return handle;
        }

        /// <summary>
        /// 创建下载集合
        /// </summary>
        public DownloadCollection CreateCollection()
        {
            return new DownloadCollection(this);
        }

        /// <summary>
        /// 获取下载句柄
        /// </summary>
        public DownloadHandle GetDownload(string url, string version)
        {
            var key = GetDownloadKey(url, version);
            _activeDownloads.TryGetValue(key, out var handle);
            return handle;
        }

        /// <summary>
        /// 检查下载是否存在
        /// </summary>
        public bool HasDownload(string url, string version)
        {
            var key = GetDownloadKey(url, version);
            return _activeDownloads.ContainsKey(key);
        }

        /// <summary>
        /// 获取活动下载数量
        /// </summary>
        public int GetActiveDownloadCount()
        {
            return _activeDownloads.Values.Count(h => h.Status == DownloadStatus.Running);
        }

        /// <summary>
        /// 获取队列中的下载数量
        /// </summary>
        public int GetQueuedDownloadCount()
        {
            return _queue.Count;
        }

        /// <summary>
        /// 取消所有下载
        /// </summary>
        public void CancelAll()
        {
            foreach (var handle in _activeDownloads.Values)
            {
                handle.Cancel();
            }
        }

        /// <summary>
        /// 暂停所有下载
        /// </summary>
        public void PauseAll()
        {
            foreach (var handle in _activeDownloads.Values)
            {
                if (handle.Status == DownloadStatus.Running)
                    handle.Pause();
            }
        }

        /// <summary>
        /// 恢复所有下载
        /// </summary>
        public void ResumeAll()
        {
            foreach (var handle in _activeDownloads.Values)
            {
                if (handle.Status == DownloadStatus.Paused)
                    handle.Resume();
            }
        }

        private async UniTaskVoid ProcessQueueAsync()
        {
            if (Interlocked.CompareExchange(ref _isProcessingQueue, 1, 0) == 1)
                return;

            try
            {
                while (_queue.Count > 0)
                {
                    await _concurrencySemaphore.WaitAsync();

                    var handle = _queue.Dequeue();
                    if (handle == null)
                    {
                        _concurrencySemaphore.Release();
                        break;
                    }

                    if (handle.Status == DownloadStatus.Canceled)
                    {
                        _concurrencySemaphore.Release();
                        continue;
                    }

                    ExecuteDownloadAsync(handle).Forget();
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessingQueue, 0);
            }
        }

        private async UniTaskVoid ExecuteDownloadAsync(DownloadHandle handle)
        {
            try
            {
                await handle.ExecuteAsync();
            }
            finally
            {
                _concurrencySemaphore.Release();
                ProcessQueueAsync().Forget();
            }
        }

        private void OnDownloadCompleted(string key, DownloadResult result)
        {
            _activeDownloads.TryRemove(key, out _);
        }

        private string GetDownloadKey(string url, string version)
        {
            return $"{url}|{version}";
        }
    }
}