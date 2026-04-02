using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Cysharp.Threading.Tasks;
using Downloader;
using System.Linq;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 下载模块，支持并发下载、断点续传、进度跟踪和批量操作。
    /// </summary>
    public sealed class DownloadModule : IDownloadModule, IGameFrameworkLifecycleModule
    {
        private readonly Queue<DownloadTask> _queuedTasks = new();
        private readonly HashSet<DownloadTask> _runningTasks = new();
        private readonly HashSet<DownloadTask> _trackedTasks = new();
        private readonly object _taskSyncRoot = new();
        private int _maxConcurrentTasks = 2;
        private double _aggregateProgress;
        private long _aggregateDownloadedBytes;
        private long _aggregateTotalBytes;
        private bool _isInitialized;
        private bool _diagnosticsRegistered;
        private int _completedTaskCount;
        private int _failedTaskCount;
        private int _cancelledTaskCount;
        private double _lastSpeedBytesPerSecond;
        private double _lastEstimatedRemainingSeconds;

        /// <summary>
        /// 获取或设置最大并发下载数。
        /// </summary>
        public int MaxConcurrentTasks
        {
            get => _maxConcurrentTasks;
            set
            {
                _maxConcurrentTasks = Math.Max(1, value);
                TryStartQueuedTasks();
            }
        }

        /// <summary>
        /// 获取当前正在运行的下载任务数。
        /// </summary>
        public int RunningTaskCount
        {
            get
            {
                lock (_taskSyncRoot)
                {
                    return _runningTasks.Count;
                }
            }
        }

        /// <summary>
        /// 获取队列中等待的下载任务数。
        /// </summary>
        public int QueuedTaskCount
        {
            get
            {
                lock (_taskSyncRoot)
                {
                    return _queuedTasks.Count;
                }
            }
        }

        /// <summary>
        /// 获取总体下载进度（0.0-1.0）。
        /// </summary>
        public double AggregateProgress
        {
            get
            {
                lock (_taskSyncRoot)
                {
                    return _aggregateProgress;
                }
            }
        }

        /// <summary>
        /// 获取总已下载字节数。
        /// </summary>
        public long AggregateDownloadedBytes
        {
            get
            {
                lock (_taskSyncRoot)
                {
                    return _aggregateDownloadedBytes;
                }
            }
        }

        /// <summary>
        /// 获取总下载字节数。
        /// </summary>
        public long AggregateTotalBytes
        {
            get
            {
                lock (_taskSyncRoot)
                {
                    return _aggregateTotalBytes;
                }
            }
        }

        /// <summary>
        /// 获取模块状态。
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 当下载任务开始时触发。
        /// </summary>
        public event Action<IDownloadTask> TaskStarted;

        /// <summary>
        /// 当下载任务进度改变时触发。
        /// </summary>
        public event Action<IDownloadTask> TaskProgressChanged;

        /// <summary>
        /// 当下载任务完成时触发。
        /// </summary>
        public event Action<IDownloadTask> TaskCompleted;

        /// <summary>
        /// 当总体下载进度改变时触发。
        /// </summary>
        public event Action<IDownloadModule> AggregateProgressChanged;

        /// <summary>
        /// 异步初始化下载模块。
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
        /// 异步关闭下载模块。
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
        /// 创建下载任务。
        /// </summary>
        /// <param name="request">下载请求。</param>
        /// <returns>下载任务。</returns>
        public IDownloadTask CreateTask(DownloadRequest request)
        {
            return CreateTaskInternal(request);
        }

        /// <summary>
        /// 将下载任务加入队列。
        /// </summary>
        /// <param name="request">下载请求。</param>
        /// <returns>下载任务。</returns>
        public IDownloadTask Enqueue(DownloadRequest request)
        {
            var task = CreateTaskInternal(request);
            lock (_taskSyncRoot)
            {
                _queuedTasks.Enqueue(task);
            }

            TryStartQueuedTasks();
            return task;
        }

        /// <summary>
        /// 批量加入下载任务到队列并等待完成。
        /// </summary>
        /// <param name="requests">下载请求列表。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>批量下载结果。</returns>
        public async UniTask<DownloadBatchResult> EnqueueBatchAsync(IReadOnlyList<DownloadRequest> requests, CancellationToken cancellationToken = default)
        {
            if (requests == null || requests.Count == 0)
            {
                return DownloadBatchResultUtility.CreateEmptySuccess();
            }

            var tasks = new IDownloadTask[requests.Count];
            for (var i = 0; i < requests.Count; i++)
            {
                tasks[i] = Enqueue(requests[i]);
            }

            var results = new List<DownloadResult>(requests.Count);
            long downloadedBytes = 0;
            long totalBytes = 0;

            for (var i = 0; i < tasks.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await tasks[i].StartAsync(cancellationToken);
                results.Add(result);
                downloadedBytes += result.DownloadedBytes;
                totalBytes += result.TotalBytes;

                if (result.Status != DownloadStatus.Succeeded)
                {
                    return DownloadBatchResultUtility.CreateFailure(results, result, downloadedBytes, totalBytes);
                }
            }

            return DownloadBatchResultUtility.CreateSuccess(results, downloadedBytes, totalBytes);
        }

        /// <summary>
        /// 直接执行下载任务。
        /// </summary>
        /// <param name="request">下载请求。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>下载结果。</returns>
        public UniTask<DownloadResult> DownloadAsync(DownloadRequest request, CancellationToken cancellationToken = default)
        {
            return CreateTask(request).StartAsync(cancellationToken);
        }

        /// <summary>
        /// 尝试加载持久化的下载请求。
        /// </summary>
        /// <param name="savePath">保存路径。</param>
        /// <param name="request">输出下载请求。</param>
        /// <returns>如果加载成功则返回 true，否则返回 false。</returns>
        public bool TryLoadPersistedRequest(string savePath, out DownloadRequest request)
        {
            return DownloadTask.TryLoadPersistedRequest(savePath, out request);
        }

        /// <summary>
        /// 恢复持久化的下载任务。
        /// </summary>
        /// <param name="savePath">保存路径。</param>
        /// <returns>下载任务。</returns>
        /// <exception cref="GameFrameworkException">当持久化状态不存在时抛出。</exception>
        public IDownloadTask ResumePersistedTask(string savePath)
        {
            if (!TryLoadPersistedRequest(savePath, out var request))
            {
                throw GameFrameworkException.Create("DownloadResumeStateMissing", $"Persisted download state not found: {savePath}", "Download", false, savePath, stage: "Preparing");
            }

            var task = Enqueue(request);
            if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                diagnostics.CaptureSnapshot("Download.LastResumePath", savePath ?? string.Empty);
            }

            return task;
        }

        /// <summary>
        /// 释放下载模块资源。
        /// </summary>
        public void Dispose()
        {
            RemoveDiagnosticsSnapshotProviders();
            lock (_taskSyncRoot)
            {
                while (_queuedTasks.Count > 0)
                {
                    _queuedTasks.Dequeue().Cancel();
                }

                foreach (var runningTask in _runningTasks)
                {
                    runningTask.Cancel();
                }

                _runningTasks.Clear();
                _trackedTasks.Clear();
                _aggregateProgress = 0d;
                _aggregateDownloadedBytes = 0;
                _aggregateTotalBytes = 0;
            }

            _isInitialized = false;
        }

        /// <summary>
        /// 验证文件的哈希值。
        /// </summary>
        /// <param name="filePath">文件路径。</param>
        /// <param name="expectedHash">期望的 SHA256 哈希值。</param>
        /// <returns>如果验证成功则返回 true，否则返回 false。</returns>
        public bool VerifyFile(string filePath, string expectedHash)
        {
            if (string.IsNullOrWhiteSpace(expectedHash))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(stream);
            var actualHash = BitConverter.ToString(hash).Replace("-", string.Empty);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 批量下载（顺序执行）。
        /// </summary>
        /// <param name="requests">下载请求列表。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>批量下载结果。</returns>
        public async UniTask<DownloadBatchResult> DownloadBatchAsync(IReadOnlyList<DownloadRequest> requests, CancellationToken cancellationToken = default)
        {
            if (requests == null || requests.Count == 0)
            {
                return DownloadBatchResultUtility.CreateEmptySuccess();
            }

            var results = new List<DownloadResult>(requests.Count);
            long downloadedBytes = 0;
            long totalBytes = 0;

            for (var i = 0; i < requests.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await DownloadAsync(requests[i], cancellationToken);
                results.Add(result);
                downloadedBytes += result.DownloadedBytes;
                totalBytes += result.TotalBytes;

                if (result.Status != DownloadStatus.Succeeded)
                {
                    return DownloadBatchResultUtility.CreateFailure(results, result, downloadedBytes, totalBytes);
                }
            }

            return DownloadBatchResultUtility.CreateSuccess(results, downloadedBytes, totalBytes);
        }

        /// <summary>
        /// 克隆下载请求，避免任务执行过程中直接修改原始请求对象。
        /// </summary>
        /// <param name="request">原始下载请求。</param>
        /// <returns>克隆后的下载请求。</returns>
        internal static DownloadRequest CloneRequest(DownloadRequest request)
        {
            return DownloadRequestUtility.Clone(request);
        }

        /// <summary>
        /// 验证下载请求是否合法。
        /// </summary>
        /// <param name="request">下载请求。</param>
        /// <exception cref="GameFrameworkException">当下载请求为空、下载地址无效或保存路径为空时抛出。</exception>
        internal static void ValidateRequest(DownloadRequest request)
        {
            DownloadRequestUtility.Validate(request);
        }

        /// <summary>
        /// 准备下载保存路径所在目录。
        /// </summary>
        /// <param name="request">下载请求。</param>
        internal static void PrepareSavePath(DownloadRequest request)
        {
            DownloadRequestUtility.PrepareSavePath(request);
        }

        /// <summary>
        /// 判断是否可以复用已存在的目标文件。
        /// </summary>
        /// <param name="request">下载请求。</param>
        /// <returns>如果目标文件已存在且不允许覆盖则返回 true，否则返回 false。</returns>
        internal static bool CanReuseExistingFile(DownloadRequest request)
        {
            return DownloadRequestUtility.CanReuseExistingFile(request);
        }

        /// <summary>
        /// 为可复用的现有文件创建下载结果。
        /// </summary>
        /// <param name="request">下载请求。</param>
        /// <param name="verifyFile">文件校验委托。</param>
        /// <returns>基于现有文件生成的下载结果。</returns>
        internal static DownloadResult CreateExistingFileResult(DownloadRequest request, Func<string, string, bool> verifyFile)
        {
            return DownloadRequestUtility.CreateExistingFileResult(request, verifyFile);
        }

        /// <summary>
        /// 创建底层下载器实例。
        /// </summary>
        /// <param name="request">下载请求。</param>
        /// <param name="workingSavePath">工作文件保存路径。</param>
        /// <param name="sourceUrl">本次使用的下载地址；为空时使用请求中的首个地址。</param>
        /// <returns>底层下载器实例。</returns>
        internal static IDownload CreateDownloader(DownloadRequest request, string workingSavePath, string sourceUrl = null)
        {
            var downloadUrl = string.IsNullOrWhiteSpace(sourceUrl)
                ? (request.Urls != null && request.Urls.Count > 0 ? request.Urls[0] : string.Empty)
                : sourceUrl;
            return DownloadBuilder.New()
                .WithConfiguration(CreateConfiguration(request))
                .WithFileLocation(workingSavePath)
                .Build(new DownloadPackage
                {
                    Urls = new[] { downloadUrl },
                    FileName = Path.GetFileName(workingSavePath)
                });
        }

        /// <summary>
        /// 根据下载请求创建底层下载配置。
        /// </summary>
        /// <param name="request">下载请求。</param>
        /// <returns>下载配置。</returns>
        internal static DownloadConfiguration CreateConfiguration(DownloadRequest request)
        {
            var chunkCount = Math.Max(1, request.ChunkCount);
            var retryCount = request.Policy != null && request.Policy.RetryCountOverride >= 0
                ? request.Policy.RetryCountOverride
                : request.RetryCount;
            var timeoutSeconds = request.Policy != null && request.Policy.TimeoutSecondsOverride > 0
                ? request.Policy.TimeoutSecondsOverride
                : request.TimeoutSeconds;
            return new DownloadConfiguration
            {
                ChunkCount = chunkCount,
                Timeout = Math.Max(1, timeoutSeconds),
                MaxTryAgainOnFailover = Math.Max(0, retryCount),
                ParallelDownload = chunkCount > 1,
                RangeDownload = true,
                ClearPackageOnCompletionWithFailure = false
            };
        }

        /// <summary>
        /// 将下载请求中的地址列表转换为数组。
        /// </summary>
        /// <param name="request">下载请求。</param>
        /// <returns>下载地址数组。</returns>
        internal static string[] ToArray(DownloadRequest request)
        {
            return DownloadRequestUtility.ToArray(request);
        }

        private DownloadTask CreateTaskInternal(DownloadRequest request)
        {
            var task = new DownloadTask(CloneRequest(request), VerifyFile);
            lock (_taskSyncRoot)
            {
                _trackedTasks.Add(task);
            }

            task.Started += HandleTaskStarted;
            task.ProgressChanged += HandleTaskProgressChanged;
            task.Completed += HandleTaskCompleted;
            RecalculateAggregateProgress();
            return task;
        }

        private void HandleTaskStarted(IDownloadTask task)
        {
            TaskStarted?.Invoke(task);
        }

        private void HandleTaskProgressChanged(IDownloadTask task)
        {
            _lastSpeedBytesPerSecond = task?.SpeedBytesPerSecond ?? 0d;
            _lastEstimatedRemainingSeconds = task?.EstimatedRemainingSeconds ?? 0d;

            if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                diagnostics.CaptureSnapshot("Download.LastSpeedBytesPerSecond", _lastSpeedBytesPerSecond.ToString("F2"));
                diagnostics.CaptureSnapshot("Download.LastEstimatedRemainingSeconds", _lastEstimatedRemainingSeconds.ToString("F2"));
            }

            RecalculateAggregateProgress();
            TaskProgressChanged?.Invoke(task);
        }

        private void HandleTaskCompleted(IDownloadTask task)
        {
            if (task is DownloadTask downloadTask)
            {
                lock (_taskSyncRoot)
                {
                    _runningTasks.Remove(downloadTask);
                    _trackedTasks.Remove(downloadTask);
                }

                downloadTask.Started -= HandleTaskStarted;
                downloadTask.ProgressChanged -= HandleTaskProgressChanged;
                downloadTask.Completed -= HandleTaskCompleted;
            }

            if (task?.Result != null)
            {
                switch (task.Result.Status)
                {
                    case DownloadStatus.Succeeded:
                        _completedTaskCount++;
                        break;
                    case DownloadStatus.Cancelled:
                        _cancelledTaskCount++;
                        break;
                    case DownloadStatus.Failed:
                        _failedTaskCount++;
                        break;
                }

                if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
                {
                    diagnostics.CaptureSnapshot("Download.LastSavePath", task.Result.SavePath ?? string.Empty);
                    diagnostics.CaptureSnapshot("Download.LastStage", task.Result.Stage.ToString());
                    diagnostics.CaptureSnapshot("Download.LastError", task.Result.ErrorMessage ?? string.Empty);
                    diagnostics.CaptureSnapshot("Download.LastAttemptCount", task.Result.AttemptCount.ToString());
                    diagnostics.CaptureSnapshot("Download.LastPriority", task.Priority.ToString());
                    diagnostics.CaptureSnapshot("Download.LastSourceUrl", task.Result.SourceUrl ?? string.Empty);
                    diagnostics.CaptureSnapshot("Download.LastFallbackCount", task.Result.FallbackCount.ToString());
                }
            }

            RecalculateAggregateProgress();
            TaskCompleted?.Invoke(task);
            TryStartQueuedTasks();
        }

        private void TryStartQueuedTasks()
        {
            while (true)
            {
                DownloadTask taskToStart;
                lock (_taskSyncRoot)
                {
                    if (_runningTasks.Count >= _maxConcurrentTasks || _queuedTasks.Count == 0)
                    {
                        return;
                    }

                    taskToStart = DequeueHighestPriorityTask();
                    _runningTasks.Add(taskToStart);
                }

                taskToStart.StartAsync().ForgetWithDiagnostics("DownloadModule.TaskStartFailed", nameof(DownloadModule), nameof(DownloadModule));
            }
        }

        private void RecalculateAggregateProgress()
        {
            long downloadedBytes = 0;
            long totalBytes = 0;
            double progress = 0d;
            var count = 0;

            lock (_taskSyncRoot)
            {
                foreach (var task in _trackedTasks)
                {
                    downloadedBytes += task.DownloadedBytes;
                    totalBytes += task.TotalBytes;
                    progress += task.Progress;
                    count++;
                }

                _aggregateDownloadedBytes = downloadedBytes;
                _aggregateTotalBytes = totalBytes;
                _aggregateProgress = count == 0 ? 0d : progress / count;
            }

            AggregateProgressChanged?.Invoke(this);
        }

        private void RegisterDiagnosticsSnapshotProviders()
        {
            if (_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RegisterSnapshotProvider("Download.CompletedTaskCount", () => _completedTaskCount.ToString());
            diagnostics.RegisterSnapshotProvider("Download.FailedTaskCount", () => _failedTaskCount.ToString());
            diagnostics.RegisterSnapshotProvider("Download.CancelledTaskCount", () => _cancelledTaskCount.ToString());
            diagnostics.RegisterSnapshotProvider("Download.AggregateProgress", () => AggregateProgress.ToString("F3"));
            diagnostics.RegisterSnapshotProvider("Download.AggregateDownloadedBytes", () => AggregateDownloadedBytes.ToString());
            diagnostics.RegisterSnapshotProvider("Download.AggregateTotalBytes", () => AggregateTotalBytes.ToString());
            diagnostics.RegisterSnapshotProvider("Download.LastSpeedBytesPerSecond", () => _lastSpeedBytesPerSecond.ToString("F2"));
            diagnostics.RegisterSnapshotProvider("Download.LastEstimatedRemainingSeconds", () => _lastEstimatedRemainingSeconds.ToString("F2"));
            diagnostics.RegisterSnapshotProvider("Download.LastAttemptCount", () => "0");
            diagnostics.RegisterSnapshotProvider("Download.LastPriority", () => "0");
            diagnostics.RegisterSnapshotProvider("Download.LastResumePath", () => string.Empty);
            diagnostics.RegisterSnapshotProvider("Download.LastSourceUrl", () => string.Empty);
            diagnostics.RegisterSnapshotProvider("Download.LastFallbackCount", () => "0");
            diagnostics.RegisterSnapshotProvider("Download.CurrentSourceUrl", () => string.Empty);
            diagnostics.RegisterSnapshotProvider("Download.CurrentFallbackCount", () => "0");
            diagnostics.RegisterSnapshotProvider("Download.LastFailedSourceUrl", () => string.Empty);
            diagnostics.RegisterSnapshotProvider("Download.NextFallbackSourceUrl", () => string.Empty);
            _diagnosticsRegistered = true;
        }

        private void RemoveDiagnosticsSnapshotProviders()
        {
            if (!_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RemoveSnapshotProvider("Download.CompletedTaskCount");
            diagnostics.RemoveSnapshotProvider("Download.FailedTaskCount");
            diagnostics.RemoveSnapshotProvider("Download.CancelledTaskCount");
            diagnostics.RemoveSnapshotProvider("Download.AggregateProgress");
            diagnostics.RemoveSnapshotProvider("Download.AggregateDownloadedBytes");
            diagnostics.RemoveSnapshotProvider("Download.AggregateTotalBytes");
            diagnostics.RemoveSnapshotProvider("Download.LastSpeedBytesPerSecond");
            diagnostics.RemoveSnapshotProvider("Download.LastEstimatedRemainingSeconds");
            diagnostics.RemoveSnapshotProvider("Download.LastAttemptCount");
            diagnostics.RemoveSnapshotProvider("Download.LastPriority");
            diagnostics.RemoveSnapshotProvider("Download.LastResumePath");
            diagnostics.RemoveSnapshotProvider("Download.LastSourceUrl");
            diagnostics.RemoveSnapshotProvider("Download.LastFallbackCount");
            diagnostics.RemoveSnapshotProvider("Download.CurrentSourceUrl");
            diagnostics.RemoveSnapshotProvider("Download.CurrentFallbackCount");
            diagnostics.RemoveSnapshotProvider("Download.LastFailedSourceUrl");
            diagnostics.RemoveSnapshotProvider("Download.NextFallbackSourceUrl");
            _diagnosticsRegistered = false;
        }

        private DownloadTask DequeueHighestPriorityTask()
        {
            if (_queuedTasks.Count == 0)
            {
                return null;
            }

            if (_queuedTasks.Count == 1)
            {
                return _queuedTasks.Dequeue();
            }

            var ordered = _queuedTasks.OrderByDescending(static task => task.Priority).ToArray();
            _queuedTasks.Clear();
            for (var i = 1; i < ordered.Length; i++)
            {
                _queuedTasks.Enqueue(ordered[i]);
            }

            return ordered[0];
        }
    }
}




