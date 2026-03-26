using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Downloader;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 下载任务，负责单个下载请求的执行、重试、断点续传和状态维护。
    /// </summary>
    internal sealed class DownloadTask : IDownloadTask
    {
        private readonly object _syncRoot = new();
        private readonly Func<string, string, bool> _verifyFile;
        private readonly CancellationTokenSource _internalCancellationTokenSource = new();
        private readonly DownloadRequest _request;
        private readonly string[] _candidateUrls;
        private IDownload _download;
        private readonly string _workingSavePath;
        private readonly bool _usesTemporaryFile;
        private readonly bool _hadExistingPartialFile;
        private readonly List<string> _attemptedUrlHistory = new();
        private UniTaskCompletionSource<DownloadResult> _completionSource;
        private bool _started;
        private bool _isSubscribed;
        private DownloadResult _result;
        private DateTimeOffset _lastProgressTimestamp;
        private long _lastProgressBytes;
        private int _attemptCount;
        private long _throttledDownloadedBytes;
        private DateTimeOffset _throttleWindowStart;
        private readonly string _resumeStatePath;
        private string _currentUrl;
        private bool _cleanupPerformed;
        private bool _commitSucceeded;

        /// <summary>
        /// 初始化下载任务实例。
        /// </summary>
        /// <param name="request">下载请求。</param>
        /// <param name="verifyFile">文件校验委托。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="request"/> 或 <paramref name="verifyFile"/> 为空时抛出。</exception>
        public DownloadTask(DownloadRequest request, Func<string, string, bool> verifyFile)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _verifyFile = verifyFile ?? throw new ArgumentNullException(nameof(verifyFile));

            DownloadModule.ValidateRequest(_request);
            DownloadModule.PrepareSavePath(_request);
            _resumeStatePath = GetResumeStatePath(_request.SavePath);
            _workingSavePath = ResolveWorkingSavePath(_request, _resumeStatePath);
            _candidateUrls = DownloadModule.ToArray(_request);
            _usesTemporaryFile = !string.Equals(_workingSavePath, _request.SavePath, StringComparison.OrdinalIgnoreCase);
            _hadExistingPartialFile = File.Exists(_workingSavePath) && new FileInfo(_workingSavePath).Length > 0;
            PrepareExistingFileForResume();
            if (TryLoadResumeState(_resumeStatePath, out var resumeState))
            {
                _attemptCount = Math.Max(0, resumeState.AttemptCount);
            }

            if (File.Exists(_workingSavePath))
            {
                var existingLength = new FileInfo(_workingSavePath).Length;
                DownloadedBytes = existingLength;
                TotalBytes = _request.ExpectedSizeBytes > 0 ? _request.ExpectedSizeBytes : existingLength;
                Progress = CalculateProgress(existingLength, TotalBytes);
            }

            _currentUrl = GetFirstUrl();
            _download = DownloadModule.CreateDownloader(_request, _workingSavePath, _currentUrl);
            Status = DownloadStatus.Pending;
        }

        /// <summary>
        /// 获取当前下载状态。
        /// </summary>
        public DownloadStatus Status { get; private set; }

        /// <summary>
        /// 获取下载请求。
        /// </summary>
        public DownloadRequest Request => _request;

        /// <summary>
        /// 获取当前下载结果。
        /// </summary>
        public DownloadResult Result => _result;

        /// <summary>
        /// 获取当前下载进度（0.0-1.0）。
        /// </summary>
        public double Progress { get; private set; }

        /// <summary>
        /// 获取已下载字节数。
        /// </summary>
        public long DownloadedBytes { get; private set; }

        /// <summary>
        /// 获取总字节数。
        /// </summary>
        public long TotalBytes { get; private set; }

        /// <summary>
        /// 获取当前下载速度（字节/秒）。
        /// </summary>
        public double SpeedBytesPerSecond { get; private set; }

        /// <summary>
        /// 获取预计剩余时间（秒）。
        /// </summary>
        public double EstimatedRemainingSeconds { get; private set; }

        /// <summary>
        /// 获取当前尝试次数。
        /// </summary>
        public int AttemptCount => _attemptCount;

        /// <summary>
        /// 获取任务优先级。
        /// </summary>
        public int Priority => _request.Policy?.Priority ?? 0;

        /// <summary>
        /// 获取限速值（字节/秒）。
        /// </summary>
        public long MaxBytesPerSecond => Math.Max(0L, _request.Policy?.MaxBytesPerSecond ?? 0L);

        /// <summary>
        /// 获取当前正在使用的下载地址。
        /// </summary>
        public string CurrentUrl => _currentUrl ?? GetFirstUrl();

        /// <summary>
        /// 获取回退源地址次数。
        /// </summary>
        public int FallbackCount => Math.Max(0, _attemptedUrlHistory.Count - 1);

        /// <summary>
        /// 当任务开始时触发。
        /// </summary>
        public event Action<IDownloadTask> Started;

        /// <summary>
        /// 当任务进度变化时触发。
        /// </summary>
        public event Action<IDownloadTask> ProgressChanged;

        /// <summary>
        /// 当任务完成时触发。
        /// </summary>
        public event Action<IDownloadTask> Completed;

        /// <summary>
        /// 异步启动下载任务。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>下载结果。</returns>
        public UniTask<DownloadResult> StartAsync(CancellationToken cancellationToken = default)
        {
            lock (_syncRoot)
            {
                if (_started)
                {
                    return _completionSource.Task;
                }

                _completionSource = new UniTaskCompletionSource<DownloadResult>();
                _started = true;
            }

            Started?.Invoke(this);
            RunAsync(cancellationToken).ForgetWithDiagnostics("DownloadTask.RunAsyncFailed", _request?.SavePath, nameof(DownloadTask));
            return _completionSource.Task;
        }

        /// <summary>
        /// 暂停当前下载任务。
        /// </summary>
        public void Pause()
        {
            var shouldPause = false;
            lock (_syncRoot)
            {
                if (Status == DownloadStatus.Downloading)
                {
                    shouldPause = true;
                }
            }

            if (!shouldPause)
            {
                return;
            }

            _download.Pause();
            UpdateState(DownloadStatus.Paused, DownloadedBytes, TotalBytes, Progress);
        }

        /// <summary>
        /// 恢复已暂停的下载任务。
        /// </summary>
        public void Resume()
        {
            var shouldResume = false;
            lock (_syncRoot)
            {
                if (Status == DownloadStatus.Paused)
                {
                    shouldResume = true;
                }
            }

            if (!shouldResume)
            {
                return;
            }

            _download.Resume();
            UpdateState(DownloadStatus.Downloading, DownloadedBytes, TotalBytes, Progress);
        }

        /// <summary>
        /// 取消当前下载任务。
        /// </summary>
        public void Cancel()
        {
            var shouldCancel = false;
            lock (_syncRoot)
            {
                if (!IsCompleted(Status))
                {
                    shouldCancel = true;
                }
            }

            if (!shouldCancel)
            {
                return;
            }

            _internalCancellationTokenSource.Cancel();
            _download.Stop();
            UpdateState(DownloadStatus.Cancelled, DownloadedBytes, TotalBytes, Progress);
        }

        private async UniTask RunAsync(CancellationToken cancellationToken)
        {
            var subscribed = false;
            try
            {
                var existingResult = TryCompleteFromExistingFile();
                if (existingResult != null)
                {
                    _result = existingResult;
                    UpdateState(existingResult.Status, existingResult.DownloadedBytes, existingResult.TotalBytes, existingResult.Status == DownloadStatus.Succeeded ? 1d : 0d);
                    _completionSource.TrySetResult(existingResult);
                    Completed?.Invoke(this);
                    return;
                }

                Subscribe();
                subscribed = true;
                PersistResumeState();
                UpdateState(DownloadStatus.Downloading, DownloadedBytes, TotalBytes, Progress);

                var result = await RunWithRetryAsync(cancellationToken);

                _result = result;
                DeleteResumeState();
                UpdateState(result.Status, result.DownloadedBytes, result.TotalBytes, result.Status == DownloadStatus.Succeeded ? 1d : Progress);
                _completionSource.TrySetResult(result);
                Completed?.Invoke(this);
            }
            catch (OperationCanceledException)
            {
                var result = new DownloadResult
                {
                    Status = DownloadStatus.Cancelled,
                    Stage = FrameworkOperationStage.Cancelled,
                    SavePath = _request.SavePath,
                    WorkingSavePath = _workingSavePath,
                    DownloadedBytes = DownloadedBytes,
                    TotalBytes = TotalBytes,
                    IsResumed = _hadExistingPartialFile,
                    AttemptCount = AttemptCount,
                    SourceUrl = CurrentUrl,
                    AttemptedUrls = GetAttemptedUrls(),
                    FallbackCount = FallbackCount,
                    UsedTemporaryFile = _usesTemporaryFile,
                    FailureKind = "Cancelled"
                };

                if (ShouldCleanupTemporaryFileOnCancel())
                {
                    _cleanupPerformed = CleanupWorkingFile();
                    DeleteResumeState();
                }

                result.CleanupPerformed = _cleanupPerformed;
                _result = result;
                PersistResumeState();
                UpdateState(result.Status, result.DownloadedBytes, result.TotalBytes, Progress);
                _completionSource.TrySetResult(result);
                Completed?.Invoke(this);
            }
            catch (Exception exception)
            {
                var result = new DownloadResult
                {
                    Status = DownloadStatus.Failed,
                    Stage = FrameworkOperationStage.Failed,
                    SavePath = _request.SavePath,
                    WorkingSavePath = _workingSavePath,
                    ErrorMessage = exception.Message,
                    Error = FrameworkError.FromException("DownloadFailed", exception, FrameworkFailureCategory.Download, true, _request.SavePath, FrameworkOperationStage.Failed),
                    DownloadedBytes = DownloadedBytes,
                    TotalBytes = TotalBytes,
                    IsResumed = _hadExistingPartialFile,
                    AttemptCount = AttemptCount,
                    SourceUrl = CurrentUrl,
                    AttemptedUrls = GetAttemptedUrls(),
                    FallbackCount = FallbackCount,
                    UsedTemporaryFile = _usesTemporaryFile,
                    FailureKind = ResolveFailureKind(exception)
                };

                if (ShouldCleanupTemporaryFileOnFailure())
                {
                    _cleanupPerformed = CleanupWorkingFile();
                    DeleteResumeState();
                }

                result.CleanupPerformed = _cleanupPerformed;
                _result = result;
                PersistResumeState();
                UpdateState(result.Status, result.DownloadedBytes, result.TotalBytes, Progress);
                _completionSource.TrySetResult(result);
                Completed?.Invoke(this);
            }
            finally
            {
                if (subscribed)
                {
                    Unsubscribe();
                }

                _download.Stop();
                _internalCancellationTokenSource.Dispose();
            }
        }

        private void PrepareExistingFileForResume()
        {
            if (!File.Exists(_workingSavePath))
            {
                return;
            }

            var fileInfo = new FileInfo(_workingSavePath);
            if (_request.ExpectedSizeBytes > 0)
            {
                if (fileInfo.Length > _request.ExpectedSizeBytes)
                {
                    File.Delete(_workingSavePath);
                    return;
                }

                if (fileInfo.Length == _request.ExpectedSizeBytes && !_verifyFile.Invoke(_workingSavePath, _request.ExpectedHash))
                {
                    File.Delete(_workingSavePath);
                }
            }
        }

        private DownloadResult TryCompleteFromExistingFile()
        {
            if (!DownloadModule.CanReuseExistingFile(_request))
            {
                return null;
            }

            var result = DownloadModule.CreateExistingFileResult(_request, _verifyFile);
            return result.Status == DownloadStatus.Succeeded ? result : null;
        }

        private DownloadResult BuildCompletionResult()
        {
            var fileInfo = new FileInfo(_workingSavePath);
            var length = fileInfo.Exists ? fileInfo.Length : DownloadedBytes;
            var sizeMatched = _request.ExpectedSizeBytes <= 0 || length == _request.ExpectedSizeBytes;
            var isVerified = fileInfo.Exists && sizeMatched && _verifyFile.Invoke(_workingSavePath, _request.ExpectedHash);
            var totalBytes = _request.ExpectedSizeBytes > 0 ? _request.ExpectedSizeBytes : TotalBytes > 0 ? TotalBytes : length;

            if (fileInfo.Exists && isVerified)
            {
                CommitDownloadedFile();
            }

            var hasCommittedFile = !_usesTemporaryFile ? fileInfo.Exists : File.Exists(_request.SavePath);

            return new DownloadResult
            {
                Status = hasCommittedFile && isVerified ? DownloadStatus.Succeeded : DownloadStatus.Failed,
                Stage = hasCommittedFile && isVerified ? FrameworkOperationStage.Completed : FrameworkOperationStage.Verifying,
                SavePath = _request.SavePath,
                WorkingSavePath = _workingSavePath,
                DownloadedBytes = length,
                TotalBytes = totalBytes,
                IsVerified = isVerified,
                ErrorMessage = hasCommittedFile && isVerified ? null : $"Downloaded file verification failed: {_request.SavePath}",
                Error = hasCommittedFile && isVerified
                    ? null
                    : FrameworkError.Create("DownloadVerificationFailed", $"Downloaded file verification failed: {_request.SavePath}", FrameworkFailureCategory.Validation, true, _request.SavePath, stage: FrameworkOperationStage.Verifying),
                IsResumed = _hadExistingPartialFile,
                AttemptCount = AttemptCount,
                SourceUrl = CurrentUrl,
                AttemptedUrls = GetAttemptedUrls(),
                FallbackCount = FallbackCount,
                UsedTemporaryFile = _usesTemporaryFile,
                CommitSucceeded = _commitSucceeded,
                CleanupPerformed = _cleanupPerformed,
                FailureKind = hasCommittedFile && isVerified ? null : "VerificationFailed"
            };
        }

        private void Subscribe()
        {
            _isSubscribed = true;
            _download.DownloadStarted += HandleDownloadStarted;
            _download.DownloadProgressChanged += HandleDownloadProgressChanged;
            _download.DownloadFileCompleted += HandleDownloadFileCompleted;
        }

        private void Unsubscribe()
        {
            _isSubscribed = false;
            _download.DownloadStarted -= HandleDownloadStarted;
            _download.DownloadProgressChanged -= HandleDownloadProgressChanged;
            _download.DownloadFileCompleted -= HandleDownloadFileCompleted;
        }

        private void HandleDownloadStarted(object sender, DownloadStartedEventArgs args)
        {
            _throttleWindowStart = DateTimeOffset.UtcNow;
            _throttledDownloadedBytes = 0;
            var totalBytes = args?.TotalBytesToReceive ?? _request.ExpectedSizeBytes;
            UpdateState(DownloadStatus.Downloading, DownloadedBytes, totalBytes, CalculateProgress(DownloadedBytes, totalBytes));
        }

        private void HandleDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs args)
        {
            HandleDownloadProgressChangedAsync(args).ForgetWithDiagnostics("DownloadTask.ProgressChangedFailed", _request?.SavePath, nameof(DownloadTask));
        }

        private async UniTask HandleDownloadProgressChangedAsync(DownloadProgressChangedEventArgs args)
        {
            var downloadedBytes = args?.ReceivedBytesSize ?? 0;
            var totalBytes = args?.TotalBytesToReceive ?? _request.ExpectedSizeBytes;
            var progress = args == null ? CalculateProgress(downloadedBytes, totalBytes) : Clamp01(args.ProgressPercentage / 100d);
            await ApplyRateLimitAsync(downloadedBytes, _internalCancellationTokenSource.Token);
            UpdateSpeed(downloadedBytes, totalBytes);
            UpdateState(Status == DownloadStatus.Paused ? DownloadStatus.Paused : DownloadStatus.Downloading, downloadedBytes, totalBytes, progress);
        }

        private void HandleDownloadFileCompleted(object sender, EventArgs args)
        {
            var fileInfo = new FileInfo(_workingSavePath);
            var length = fileInfo.Exists ? fileInfo.Length : DownloadedBytes;
            var totalBytes = _request.ExpectedSizeBytes > 0 ? _request.ExpectedSizeBytes : TotalBytes > 0 ? TotalBytes : length;
            UpdateState(Status, length, totalBytes, 1d);
        }

        private void UpdateState(DownloadStatus status, long downloadedBytes, long totalBytes, double progress)
        {
            var normalizedTotalBytes = totalBytes < 0 ? 0 : totalBytes;
            var normalizedDownloadedBytes = downloadedBytes < 0 ? 0 : downloadedBytes;
            var normalizedProgress = Clamp01(progress);
            var changed = false;

            lock (_syncRoot)
            {
                if (Status != status)
                {
                    Status = status;
                    changed = true;
                }

                if (DownloadedBytes != normalizedDownloadedBytes)
                {
                    DownloadedBytes = normalizedDownloadedBytes;
                    changed = true;
                }

                if (TotalBytes != normalizedTotalBytes)
                {
                    TotalBytes = normalizedTotalBytes;
                    changed = true;
                }

                if (Math.Abs(Progress - normalizedProgress) > double.Epsilon)
                {
                    Progress = normalizedProgress;
                    changed = true;
                }
            }

            if (changed)
            {
                PersistResumeState();
                ProgressChanged?.Invoke(this);
            }
        }

        private static double CalculateProgress(long downloadedBytes, long totalBytes)
        {
            if (totalBytes <= 0)
            {
                return 0d;
            }

            return Clamp01((double)downloadedBytes / totalBytes);
        }

        private static double Clamp01(double value)
        {
            if (value <= 0d)
            {
                return 0d;
            }

            if (value >= 1d)
            {
                return 1d;
            }

            return value;
        }

        private void UpdateSpeed(long downloadedBytes, long totalBytes)
        {
            var now = DateTimeOffset.UtcNow;
            if (_lastProgressTimestamp != default)
            {
                var elapsedSeconds = (now - _lastProgressTimestamp).TotalSeconds;
                if (elapsedSeconds > double.Epsilon)
                {
                    var byteDelta = Math.Max(0L, downloadedBytes - _lastProgressBytes);
                    SpeedBytesPerSecond = byteDelta / elapsedSeconds;
                    var remainingBytes = Math.Max(0L, totalBytes - downloadedBytes);
                    EstimatedRemainingSeconds = SpeedBytesPerSecond > double.Epsilon
                        ? remainingBytes / SpeedBytesPerSecond
                        : 0d;
                }
            }

            _lastProgressTimestamp = now;
            _lastProgressBytes = downloadedBytes;
        }

        private void CommitDownloadedFile()
        {
            if (!_usesTemporaryFile || !File.Exists(_workingSavePath))
            {
                _commitSucceeded = true;
                return;
            }

            var finalDirectory = Path.GetDirectoryName(_request.SavePath);
            if (!string.IsNullOrWhiteSpace(finalDirectory))
            {
                Directory.CreateDirectory(finalDirectory);
            }

            var backupPath = _request.SavePath + ".bak";
            if (File.Exists(_request.SavePath))
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                File.Move(_request.SavePath, backupPath);
            }

            try
            {
                File.Move(_workingSavePath, _request.SavePath);
                _commitSucceeded = true;
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            catch
            {
                if (File.Exists(backupPath) && !File.Exists(_request.SavePath))
                {
                    File.Move(backupPath, _request.SavePath);
                }

                throw;
            }
        }

        private async UniTask<DownloadResult> RunWithRetryAsync(CancellationToken cancellationToken)
        {
            Exception lastException = null;
            var maxAttempts = Math.Max(1, (_request.Policy?.RetryCountOverride ?? -1) >= 0
                ? _request.Policy.RetryCountOverride + 1
                : _request.RetryCount + 1);

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _attemptCount = attempt;

                for (var sourceIndex = 0; sourceIndex < _candidateUrls.Length; sourceIndex++)
                {
                    var sourceUrl = _candidateUrls[sourceIndex];
                    EnsureDownloadSource(sourceUrl);

                    try
                    {
                        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _internalCancellationTokenSource.Token);
                        using var stream = await _download.StartAsync(linkedCancellationTokenSource.Token);
                        return BuildCompletionResult();
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        lastException = CreateFinalFailureException(sourceUrl, exception);
                        if (sourceIndex < _candidateUrls.Length - 1)
                        {
                            NotifyFallback(sourceUrl, _candidateUrls[sourceIndex + 1], attempt, exception);
                            continue;
                        }
                    }
                }

                if (attempt < maxAttempts)
                {
                    await UniTask.Delay(Math.Max(0, _request.Policy?.RetryDelayMilliseconds ?? 0), cancellationToken: cancellationToken);
                }
            }

            throw lastException ?? new InvalidOperationException("Download retry loop ended unexpectedly.");
        }

        private void EnsureDownloadSource(string sourceUrl)
        {
            if (string.IsNullOrWhiteSpace(sourceUrl))
            {
                return;
            }

            if (_download != null && string.Equals(_currentUrl, sourceUrl, StringComparison.OrdinalIgnoreCase))
            {
                RecordAttemptedUrl(sourceUrl);
                CaptureCurrentSourceSnapshot();
                return;
            }

            var previousDownload = _download;
            if (_isSubscribed && previousDownload != null)
            {
                previousDownload.DownloadStarted -= HandleDownloadStarted;
                previousDownload.DownloadProgressChanged -= HandleDownloadProgressChanged;
                previousDownload.DownloadFileCompleted -= HandleDownloadFileCompleted;
            }

            previousDownload?.Stop();
            _currentUrl = sourceUrl;
            _download = DownloadModule.CreateDownloader(_request, _workingSavePath, sourceUrl);

            if (_isSubscribed)
            {
                _download.DownloadStarted += HandleDownloadStarted;
                _download.DownloadProgressChanged += HandleDownloadProgressChanged;
                _download.DownloadFileCompleted += HandleDownloadFileCompleted;
            }

            RecordAttemptedUrl(sourceUrl);
            CaptureCurrentSourceSnapshot();
        }

        private string GetFirstUrl()
        {
            return _request.Urls != null && _request.Urls.Count > 0 ? _request.Urls[0] : string.Empty;
        }

        /// <summary>
        /// 尝试从持久化状态中恢复下载请求。
        /// </summary>
        /// <param name="savePath">目标保存路径。</param>
        /// <param name="request">恢复得到的下载请求。</param>
        /// <returns>如果恢复成功则返回 true，否则返回 false。</returns>
        internal static bool TryLoadPersistedRequest(string savePath, out DownloadRequest request)
        {
            request = null;
            var resumeStatePath = GetResumeStatePath(savePath);
            if (!TryLoadResumeState(resumeStatePath, out var state))
            {
                return false;
            }

            if (state == null || state.Urls == null || state.Urls.Length == 0 || string.IsNullOrWhiteSpace(state.SavePath))
            {
                return false;
            }

            request = new DownloadRequest
            {
                Urls = state.Urls,
                SavePath = state.SavePath,
                TimeoutSeconds = state.TimeoutSeconds,
                RetryCount = state.RetryCount,
                ChunkCount = state.ChunkCount,
                Overwrite = state.Overwrite,
                ExpectedHash = state.ExpectedHash,
                ExpectedSizeBytes = state.ExpectedSizeBytes,
                Policy = new DownloadPolicy
                {
                    UseTemporaryFile = state.UseTemporaryFile,
                    TemporaryDirectory = state.TemporaryDirectory,
                    CleanupTemporaryFileOnFailure = state.CleanupTemporaryFileOnFailure,
                    CleanupTemporaryFileOnCancel = state.CleanupTemporaryFileOnCancel,
                    RetryCountOverride = state.RetryCountOverride,
                    RetryDelayMilliseconds = state.RetryDelayMilliseconds,
                    TimeoutSecondsOverride = state.TimeoutSecondsOverride,
                    Priority = state.Priority,
                    MaxBytesPerSecond = state.MaxBytesPerSecond
                }
            };
            return true;
        }

        private static string ResolveWorkingSavePath(DownloadRequest request, string resumeStatePath)
        {
            if (TryLoadResumeState(resumeStatePath, out var state)
                && !string.IsNullOrWhiteSpace(state.WorkingSavePath)
                && File.Exists(state.WorkingSavePath))
            {
                return state.WorkingSavePath;
            }

            if (request.Policy == null || !request.Policy.UseTemporaryFile)
            {
                return request.SavePath;
            }

            var tempDirectory = string.IsNullOrWhiteSpace(request.Policy.TemporaryDirectory)
                ? Path.Combine(Path.GetDirectoryName(request.SavePath) ?? string.Empty, ".downloading")
                : request.Policy.TemporaryDirectory;

            Directory.CreateDirectory(tempDirectory);
            return Path.Combine(tempDirectory, Path.GetFileName(request.SavePath) + ".part");
        }

        private static bool IsCompleted(DownloadStatus status)
        {
            return status == DownloadStatus.Succeeded ||
                   status == DownloadStatus.Failed ||
                   status == DownloadStatus.Cancelled;
        }

        private async UniTask ApplyRateLimitAsync(long downloadedBytes, CancellationToken cancellationToken)
        {
            var limit = MaxBytesPerSecond;
            if (limit <= 0)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            if (_throttleWindowStart == default || (now - _throttleWindowStart).TotalSeconds >= 1d)
            {
                _throttleWindowStart = now;
                _throttledDownloadedBytes = downloadedBytes;
                return;
            }

            var byteDelta = Math.Max(0L, downloadedBytes - _throttledDownloadedBytes);
            if (byteDelta <= limit)
            {
                return;
            }

            var elapsedSeconds = (now - _throttleWindowStart).TotalSeconds;
            var expectedSeconds = byteDelta / (double)limit;
            var delayMilliseconds = (int)Math.Max(1d, (expectedSeconds - elapsedSeconds) * 1000d);
            await UniTask.Delay(delayMilliseconds, cancellationToken: cancellationToken);
        }

        private void PersistResumeState()
        {
            if (Status == DownloadStatus.Succeeded)
            {
                DeleteResumeState();
                return;
            }

            var state = new DownloadResumeState
            {
                SavePath = _request.SavePath,
                WorkingSavePath = _workingSavePath,
                Urls = DownloadModule.ToArray(_request),
                TimeoutSeconds = _request.TimeoutSeconds,
                RetryCount = _request.RetryCount,
                ChunkCount = _request.ChunkCount,
                Overwrite = _request.Overwrite,
                ExpectedHash = _request.ExpectedHash,
                ExpectedSizeBytes = _request.ExpectedSizeBytes,
                UseTemporaryFile = _request.Policy?.UseTemporaryFile ?? false,
                TemporaryDirectory = _request.Policy?.TemporaryDirectory,
                CleanupTemporaryFileOnFailure = _request.Policy?.CleanupTemporaryFileOnFailure ?? true,
                CleanupTemporaryFileOnCancel = _request.Policy?.CleanupTemporaryFileOnCancel ?? false,
                RetryCountOverride = _request.Policy?.RetryCountOverride ?? -1,
                RetryDelayMilliseconds = _request.Policy?.RetryDelayMilliseconds ?? 0,
                TimeoutSecondsOverride = _request.Policy?.TimeoutSecondsOverride ?? -1,
                Priority = _request.Policy?.Priority ?? 0,
                MaxBytesPerSecond = _request.Policy?.MaxBytesPerSecond ?? 0,
                DownloadedBytes = DownloadedBytes,
                AttemptCount = AttemptCount,
                LastUpdatedUtc = DateTimeOffset.UtcNow.ToString("O")
            };

            var directory = Path.GetDirectoryName(_resumeStatePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_resumeStatePath, JsonUtility.ToJson(state, true));
        }

        private string[] GetAttemptedUrls()
        {
            return _attemptedUrlHistory.Count == 0 ? Array.Empty<string>() : _attemptedUrlHistory.ToArray();
        }

        private void RecordAttemptedUrl(string sourceUrl)
        {
            if (string.IsNullOrWhiteSpace(sourceUrl))
            {
                return;
            }

            for (var i = 0; i < _attemptedUrlHistory.Count; i++)
            {
                if (string.Equals(_attemptedUrlHistory[i], sourceUrl, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            _attemptedUrlHistory.Add(sourceUrl);
        }

        private void CaptureCurrentSourceSnapshot()
        {
            if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                diagnostics.CaptureSnapshot("Download.CurrentSourceUrl", CurrentUrl ?? string.Empty);
                diagnostics.CaptureSnapshot("Download.CurrentFallbackCount", FallbackCount.ToString());
            }
        }

        private void NotifyFallback(string failedSourceUrl, string fallbackSourceUrl, int attempt, Exception exception)
        {
            if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                diagnostics.LogWarning($"Download source failed, switching to fallback source (attempt {attempt}).", exception?.Message ?? failedSourceUrl);
                diagnostics.CaptureSnapshot("Download.LastFailedSourceUrl", failedSourceUrl ?? string.Empty);
                diagnostics.CaptureSnapshot("Download.NextFallbackSourceUrl", fallbackSourceUrl ?? string.Empty);
            }
        }

        private Exception CreateFinalFailureException(string failedSourceUrl, Exception exception)
        {
            var message = _candidateUrls.Length > 1
                ? $"Download failed for all configured sources. Last source: {failedSourceUrl}. Error: {exception.Message}"
                : exception.Message;

            return new FrameworkException(FrameworkError.Create("DownloadAllSourcesFailed", message, FrameworkFailureCategory.Download, true, _request.SavePath, exception, FrameworkOperationStage.Downloading));
        }

        private void DeleteResumeState()
        {
            if (File.Exists(_resumeStatePath))
            {
                File.Delete(_resumeStatePath);
            }
        }

        private bool ShouldCleanupTemporaryFileOnFailure()
        {
            return _usesTemporaryFile && (_request.Policy?.CleanupTemporaryFileOnFailure ?? true);
        }

        private bool ShouldCleanupTemporaryFileOnCancel()
        {
            return _usesTemporaryFile && (_request.Policy?.CleanupTemporaryFileOnCancel ?? false);
        }

        private bool CleanupWorkingFile()
        {
            if (!_usesTemporaryFile || string.IsNullOrWhiteSpace(_workingSavePath))
            {
                return false;
            }

            if (!File.Exists(_workingSavePath))
            {
                return false;
            }

            File.Delete(_workingSavePath);
            return true;
        }

        private static string ResolveFailureKind(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                return "Cancelled";
            }

            if (exception is TimeoutException)
            {
                return "Timeout";
            }

            if (exception is IOException)
            {
                return "FileIO";
            }

            if (exception is FrameworkException frameworkException && frameworkException.Error != null)
            {
                return frameworkException.Error.Code;
            }

            return exception?.GetType().Name ?? "Unknown";
        }

        private static string GetResumeStatePath(string savePath)
        {
            return string.IsNullOrWhiteSpace(savePath) ? string.Empty : savePath + ".resume.json";
        }

        private static bool TryLoadResumeState(string resumeStatePath, out DownloadResumeState state)
        {
            state = null;
            if (string.IsNullOrWhiteSpace(resumeStatePath) || !File.Exists(resumeStatePath))
            {
                return false;
            }

            try
            {
                var json = File.ReadAllText(resumeStatePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                state = JsonUtility.FromJson<DownloadResumeState>(json);
                return state != null;
            }
            catch (Exception exception)
            {
                if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
                {
                    diagnostics.LogWarning($"Failed to load download resume state: {resumeStatePath}", exception.Message);
                }

                return false;
            }
        }
    }
}
