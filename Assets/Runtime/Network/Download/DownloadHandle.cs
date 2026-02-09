using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Downloader;
using UnityEngine;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 下载句柄（细粒度锁设计）
    /// </summary>
    public class DownloadHandle : IDisposable
    {
        private const long LargeFileThreshold = 10 * 1024 * 1024; // 10MB

        // 原子状态
        private int _status;
        private int _disposed;
        private int _isPaused;

        // 原子计数器
        private long _totalBytes;
        private long _receivedBytes;

        // 不可变字段
        private readonly string _url;
        private readonly string _version;
        private readonly DownloadConfig _config;
        private readonly CancellationTokenSource _cts;
        private readonly UniTaskCompletionSource<DownloadResult> _completionSource;
        private readonly DownloadSpeedTracker _speedTracker;

        // 需要保护的可变字段
        private readonly object _resultLock = new object();
        private DownloadResult _result;

        private readonly object _downloaderLock = new object();
        private DownloadService _downloader;

        public string Url => _url;
        public string Version => _version;
        public int Priority => _config.Priority;

        public DownloadStatus Status
        {
            get => (DownloadStatus)Interlocked.CompareExchange(ref _status, 0, 0);
            internal set => Interlocked.Exchange(ref _status, (int)value);
        }

        public long TotalBytes => Interlocked.Read(ref _totalBytes);
        public long ReceivedBytes => Interlocked.Read(ref _receivedBytes);

        public float Progress
        {
            get
            {
                var total = Interlocked.Read(ref _totalBytes);
                var received = Interlocked.Read(ref _receivedBytes);
                return total > 0 ? (float)received / total : 0f;
            }
        }

        public string SavedFilePath
        {
            get
            {
                lock (_resultLock)
                    return _result.FilePath;
            }
        }

        public long CurrentSpeed => _speedTracker.CurrentSpeed;
        public long AverageSpeed => _speedTracker.AverageSpeed;

        public event Action<float> ProgressChanged;
        public event Action<DownloadResult> Completed;

        internal DownloadHandle(string url, string version, DownloadConfig config, CancellationToken ct)
        {
            _url = url;
            _version = version;
            _config = config;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _completionSource = new UniTaskCompletionSource<DownloadResult>();
            _speedTracker = new DownloadSpeedTracker();
            _result = new DownloadResult();
            _status = (int)DownloadStatus.None;
            _disposed = 0;
            _isPaused = 0;
        }

        public async UniTask<DownloadResult> WaitForCompletionAsync()
        {
            var status = (DownloadStatus)Interlocked.CompareExchange(ref _status, 0, 0);

            if (status == DownloadStatus.Completed ||
                status == DownloadStatus.Failed ||
                status == DownloadStatus.Canceled)
            {
                lock (_resultLock)
                    return _result;
            }

            return await _completionSource.Task;
        }

        internal async UniTask ExecuteAsync()
        {
            try
            {
                Status = DownloadStatus.Pending;

                var cacheKey = GetCacheKey(_url, _version);
                var cacheDir = Path.Combine(Application.persistentDataPath, "downloads");
                Directory.CreateDirectory(cacheDir);

                var pkgPath = Path.Combine(cacheDir, $"{cacheKey}.pkg");
                var partPath = Path.Combine(cacheDir, $"{cacheKey}.part");
                var savePath = Path.Combine(cacheDir, $"{cacheKey}.complete");

                bool canResume = CheckResumeCapability(pkgPath, partPath);

                if (!canResume)
                {
                    var size = await GetContentLengthAsync(_url);
                    Interlocked.Exchange(ref _totalBytes, size);
                }

                var total = Interlocked.Read(ref _totalBytes);
                
                // 磁盘空间预检查
                if (total > 0)
                {
                    var driveInfo = new DriveInfo(Path.GetPathRoot(cacheDir));
                    var requiredSpace = (long)(total * 1.2); // 预留20%缓冲
                    
                    if (driveInfo.AvailableFreeSpace < requiredSpace)
                    {
                        throw new InsufficientDiskSpaceException(
                            $"Not enough disk space to download {_url}. Required: {requiredSpace / 1024 / 1024}MB, Available: {driveInfo.AvailableFreeSpace / 1024 / 1024}MB",
                            requiredSpace,
                            driveInfo.AvailableFreeSpace);
                    }
                }
                
                var isLargeFile = total >= LargeFileThreshold;
                var dlConfig = CreateDownloaderConfig(isLargeFile);

                lock (_downloaderLock)
                {
                    _downloader = new DownloadService(dlConfig);
                    _downloader.DownloadProgressChanged += OnDownloadProgress;
                    _downloader.DownloadFileCompleted += OnDownloadFileCompleted;
                }

                Status = DownloadStatus.Running;
                await PerformDownloadWithRetryAsync(partPath);

                if (File.Exists(partPath))
                {
                    // 文件完整性校验
                    if (_config.HashType != HashAlgorithmType.None && !string.IsNullOrEmpty(_config.ExpectedHash))
                    {
                        var actualHash = ComputeFileHash(partPath, _config.HashType);
                        if (!string.Equals(actualHash, _config.ExpectedHash, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Delete(partPath);
                            throw new Exception($"File hash mismatch. Expected: {_config.ExpectedHash}, Actual: {actualHash}");
                        }
                    }
                    
                    if (File.Exists(savePath))
                        File.Delete(savePath);

                    File.Move(partPath, savePath);
                    TryDelete(pkgPath);

                    lock (_resultLock)
                    {
                        _result.FilePath = savePath;
                    }

                    Interlocked.Exchange(ref _receivedBytes, total);
                }

                Status = DownloadStatus.Completed;

                lock (_resultLock)
                {
                    _result.IsSuccess = true;
                    _result.Status = DownloadStatus.Completed;
                    _result.TotalBytes = Interlocked.Read(ref _totalBytes);
                    _result.ReceivedBytes = Interlocked.Read(ref _receivedBytes);
                    _result.Progress = 1f;
                    _result.BytesPerSecond = _speedTracker.CurrentSpeed;
                    _result.AverageBytesPerSecond = _speedTracker.AverageSpeed;
                }
            }
            catch (OperationCanceledException)
            {
                Status = DownloadStatus.Canceled;
                lock (_resultLock)
                {
                    _result.Status = DownloadStatus.Canceled;
                    _result.Error = "Download canceled";
                }
            }
            catch (Exception ex)
            {
                Status = DownloadStatus.Failed;
                lock (_resultLock)
                {
                    _result.Status = DownloadStatus.Failed;
                    _result.Error = ex.Message;
                }
            }
            finally
            {
                DownloadResult result;
                lock (_resultLock)
                {
                    result = _result;
                }

                Completed?.Invoke(result);
                _completionSource.TrySetResult(result);

                lock (_downloaderLock)
                {
                    _downloader?.Dispose();
                    _downloader = null;
                }
            }
        }

        private async UniTask PerformDownloadWithRetryAsync(string path)
        {
            int retries = 0;
            while (retries <= _config.MaxRetries)
            {
                try
                {
                    DownloadService downloader;
                    lock (_downloaderLock)
                    {
                        downloader = _downloader;
                    }

                    if (downloader != null)
                    {
                        await downloader.DownloadFileTaskAsync(_url, path, _cts.Token);
                    }
                    return;
                }
                catch (Exception) when (retries < _config.MaxRetries && !_cts.Token.IsCancellationRequested)
                {
                    retries++;
                    var delay = (int)(_config.InitialDelayMs * Math.Pow(_config.BackoffFactor, retries - 1));
                    await UniTask.Delay(delay, cancellationToken: _cts.Token);
                }
            }
            throw new Exception($"Download failed after {_config.MaxRetries + 1} attempts");
        }

        public void Pause()
        {
            var currentStatus = (DownloadStatus)Interlocked.CompareExchange(ref _status, 0, 0);
            var wasPaused = Interlocked.CompareExchange(ref _isPaused, 0, 0);

            if (currentStatus != DownloadStatus.Running || wasPaused == 1) return;

            lock (_downloaderLock)
            {
                _downloader?.Pause();
            }

            Interlocked.Exchange(ref _isPaused, 1);
            Status = DownloadStatus.Paused;
        }

        public void Resume()
        {
            var wasPaused = Interlocked.CompareExchange(ref _isPaused, 1, 1);
            if (wasPaused != 1) return;

            lock (_downloaderLock)
            {
                _downloader?.Resume();
            }

            Interlocked.Exchange(ref _isPaused, 0);
            Status = DownloadStatus.Running;
        }

        public void Cancel()
        {
            _cts?.Cancel();
        }

        private void OnDownloadProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            Interlocked.Exchange(ref _totalBytes, e.TotalBytesToReceive);
            Interlocked.Exchange(ref _receivedBytes, e.ReceivedBytesSize);

            _speedTracker.Update(e.ReceivedBytesSize);

            var total = Interlocked.Read(ref _totalBytes);
            var received = Interlocked.Read(ref _receivedBytes);
            var progress = total > 0 ? (float)received / total : 0f;

            lock (_resultLock)
            {
                _result.TotalBytes = total;
                _result.ReceivedBytes = received;
                _result.Progress = progress;
                _result.BytesPerSecond = _speedTracker.CurrentSpeed;
                _result.AverageBytesPerSecond = _speedTracker.AverageSpeed;
                _result.EstimatedTimeRemaining = _speedTracker.CalculateETA(total, received);
            }

            ProgressChanged?.Invoke(progress);
        }

        private void OnDownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (!e.Cancelled && e.Error == null)
            {
                var total = Interlocked.Read(ref _totalBytes);
                Interlocked.Exchange(ref _receivedBytes, total);
            }
        }

        private bool CheckResumeCapability(string pkgPath, string partPath)
        {
            if (!_config.EnableResume) return false;

            if (File.Exists(pkgPath) && File.Exists(partPath))
            {
                try
                {
                    var pkgJson = File.ReadAllText(pkgPath);
                    var savedVersion = ExtractVersionFromPackage(pkgJson);
                    return savedVersion == _version;
                }
                catch { }
            }

            TryDelete(pkgPath);
            TryDelete(partPath);
            return false;
        }

        private DownloadConfiguration CreateDownloaderConfig(bool isLargeFile)
        {
            if (isLargeFile)
            {
                return new DownloadConfiguration
                {
                    ChunkCount = _config.ChunkCount,
                    ParallelDownload = true,
                    ParallelCount = 4,
                    MaxTryAgainOnFailover = 0,
                    ReserveStorageSpaceBeforeStartingDownload = true,
                    MaximumMemoryBufferBytes = 4 * 1024 * 1024,
                    Timeout = _config.TimeoutSeconds * 1000,
                    BufferBlockSize = 16384
                };
            }
            else
            {
                return new DownloadConfiguration
                {
                    ChunkCount = 1,
                    ParallelDownload = false,
                    MaxTryAgainOnFailover = 0,
                    Timeout = _config.TimeoutSeconds * 1000,
                    BufferBlockSize = 8192
                };
            }
        }

        private async UniTask<long> GetContentLengthAsync(string url)
        {
            try
            {
                var request = new Request(url);
                return await request.GetFileSize().AsUniTask();
            }
            catch
            {
                return 0;
            }
        }

        private string GetCacheKey(string url, string version)
        {
            var combined = $"{url}|{version}";
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(combined));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        private string ExtractVersionFromPackage(string pkgJson)
        {
            if (pkgJson.Contains("\"Version\":"))
            {
                var start = pkgJson.IndexOf("\"Version\":") + 11;
                var end = pkgJson.IndexOf("\"", start);
                if (start > 0 && end > start)
                    return pkgJson.Substring(start, end - start);
            }
            return string.Empty;
        }

        private void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        /// <summary>
        /// 计算文件哈希值
        /// </summary>
        private string ComputeFileHash(string filePath, HashAlgorithmType hashType)
        {
            using (var stream = File.OpenRead(filePath))
            {
                HashAlgorithm algorithm = hashType switch
                {
                    HashAlgorithmType.MD5 => MD5.Create(),
                    HashAlgorithmType.SHA256 => SHA256.Create(),
                    _ => throw new ArgumentException($"Unsupported hash type: {hashType}")
                };
                
                using (algorithm)
                {
                    var hash = algorithm.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLower();
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1)
                return;

            _cts?.Cancel();
            _cts?.Dispose();

            lock (_downloaderLock)
            {
                _downloader?.Dispose();
                _downloader = null;
            }
        }
    }
}
