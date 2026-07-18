using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.File;
using GameDeveloperKit.Operation;
using UnityEngine.Networking;
using UnityDownloadHandlerFile = UnityEngine.Networking.DownloadHandlerFile;
using UnityDownloadHandlerBuffer = UnityEngine.Networking.DownloadHandlerBuffer;

namespace GameDeveloperKit.Download
{
    /// <summary>
    /// 下载处理器
    /// </summary>
    public partial class DownloadHandler : OperationHandle
    {
        /// <summary>
        /// 启用分块下载的大文件阈值。
        /// </summary>
        internal const long LargeFileThreshold = 16L * 1024L * 1024L;
        /// <summary>
        /// 单个下载分块大小。
        /// </summary>
        internal const long ChunkSize = 4L * 1024L * 1024L;
        private const int ChunkConcurrency = 4;
        private readonly List<DownloadChunk> m_Chunks = new List<DownloadChunk>();
        private readonly List<UnityWebRequest> m_ActiveRequests = new List<UnityWebRequest>();
        private FileModule m_FileModule;
        private FileTemporaryHandle m_TemporaryFile;
        private CancellationTokenSource m_TransferCancellation;
        private bool m_CancelRequested;
        private bool m_RunActive;
        private UniTaskCompletionSource m_ResumeSource;
        private UniTaskCompletionSource m_RunFinishedSource;

        /// <summary>
        /// 下载URL
        /// </summary>
        public string Url { get; private set; }
        /// <summary>
        /// 是否使用分块下载
        /// </summary>
        public bool IsChunked { get; private set; }
        /// <summary>
        /// 已完成的分块数量
        /// </summary>
        public int CompletedChunkCount { get; private set; }
        /// <summary>
        /// 分块总数量
        /// </summary>
        public int TotalChunkCount { get; private set; }
        /// <summary>
        /// 下载进度（0~1）
        /// </summary>
        public float Progress => TotalBytes > 0 ? Math.Min(1f, (float)((double)DownloadedBytes / TotalBytes)) : 0f;
        /// <summary>
        /// 已下载字节数
        /// </summary>
        public long DownloadedBytes { get; private set; }
        /// <summary>
        /// 总字节数，-1表示未知
        /// </summary>
        public long TotalBytes { get; private set; } = -1;
        /// <summary>
        /// 下载失败类型，仅在下载失败时有效
        /// </summary>
        public DownloadFailureKind FailureKind { get; private set; }
        /// <summary>
        /// 下载进度变化事件
        /// </summary>
        public event Action<DownloadHandler> ProgressChanged;
        /// <summary>
        /// 下载完成事件
        /// </summary>
        public event Action<DownloadHandler> Completed;
        /// <summary>
        /// 下载失败事件
        /// </summary>
        public event Action<DownloadHandler> Failed;
        /// <summary>
        /// 下载取消事件
        /// </summary>
        public event Action<DownloadHandler> Canceled;

        internal Exception LastObserverException { get; private set; }

        /// <summary>
        /// 初始化 Download Handler。
        /// </summary>
        internal DownloadHandler()
        {
        }
        /// <summary>
        /// 等待下载完成，无论成功、失败还是取消都会完成等待
        /// </summary>
        public new async UniTask WaitCompletionAsync()
        {
            try
            {
                await base.WaitCompletionAsync();
            }
            catch (Exception exception)
            {
                LastObserverException = exception;
            }
        }

        /// <summary>
        /// 暂停下载，只有在下载中或等待中才会生效
        /// </summary>
        public UniTask Pause()
        {
            SetPause();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 将下载操作设置为暂停状态。
        /// </summary>
        public void SetPause()
        {
            if (PauseExecution())
            {
                m_ResumeSource = new UniTaskCompletionSource();
                m_TransferCancellation?.Cancel();
            }
        }

        /// <summary>
        /// 打开已完成下载结果的只读流。调用方负责释放返回的流。
        /// </summary>
        public UniTask<Stream> OpenReadAsync()
        {
            EnsureResultAvailable();
            return m_TemporaryFile.OpenReadAsync();
        }

        /// <summary>
        /// 读取已完成下载结果的全部字节。
        /// </summary>
        public async UniTask<byte[]> ReadAsync()
        {
            EnsureResultAvailable();
            using (var stream = await m_TemporaryFile.OpenReadAsync())
            {
                if (stream.Length > int.MaxValue)
                {
                    throw new GameException("Downloaded result is too large for byte[] read. Use OpenReadAsync instead.");
                }

                var data = new byte[(int)stream.Length];
                var offset = 0;
                while (offset < data.Length)
                {
                    var read = await stream.ReadAsync(data, offset, data.Length - offset);
                    if (read == 0)
                    {
                        throw new EndOfStreamException(
                            $"Downloaded result ended after {offset} of {data.Length} bytes: {Url}");
                    }

                    offset += read;
                }

                return data;
            }
        }

        /// <summary>
        /// 将已完成下载结果复制到FileModule虚拟文件。
        /// </summary>
        public async UniTask SaveAsync(string virtualPath, string version)
        {
            EnsureResultAvailable();
            using (var stream = await m_TemporaryFile.OpenReadAsync())
            {
                await m_FileModule.WriteAsync(virtualPath, version, stream);
            }
        }

        internal UniTask SaveVerifiedAsync(
            string virtualPath,
            string version,
            Func<Stream, UniTask> verifier)
        {
            EnsureResultAvailable();
            return m_FileModule.ImportTemporaryAsync(
                m_TemporaryFile,
                virtualPath,
                version,
                verifier);
        }

        internal void ReleaseResult()
        {
            if (!IsDone)
            {
                throw new GameException("An active download result cannot be released.");
            }

            ReleaseTemporaryFiles();
        }

        /// <summary>
        /// 恢复暂停中的下载。
        /// </summary>
        public UniTask Resume()
        {
            SetResume();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 恢复下载操作。
        /// </summary>
        public void SetResume()
        {
            if (!ResumeExecution())
            {
                return;
            }

            FailureKind = DownloadFailureKind.None;
            m_TransferCancellation?.Dispose();
            m_TransferCancellation = new CancellationTokenSource();
            m_ResumeSource?.TrySetResult();
            m_ResumeSource = null;
        }
        /// <summary>
        /// 取消下载，任何状态下调用都会尝试取消下载，并删除临时文件
        /// </summary>
        public async UniTask Cancel()
        {
            if (IsDone)
            {
                await WaitRunFinishedAsync();
                ReleaseTemporaryFiles();
                return;
            }

            var runFinishedSource = m_RunFinishedSource;
            SetCancel();
            if (runFinishedSource != null)
            {
                await runFinishedSource.Task;
            }
            else
            {
                await WaitCompletionAsync();
            }
        }

        /// <summary>
        /// 取消下载操作。
        /// </summary>
        public override void SetCancel()
        {
            if (IsDone || m_CancelRequested)
            {
                return;
            }

            m_CancelRequested = true;
            m_TransferCancellation?.Cancel();
            m_ResumeSource?.TrySetResult();
            m_ResumeSource = null;
            AbortActiveRequests();
            if (!m_RunActive)
            {
                CompleteCancellation();
            }
        }

        /// <summary>
        /// 执行下载操作句柄。
        /// </summary>
        /// <param name="args">操作参数。</param>
        public override void Execute(params object[] args)
        {
            Initialize(args);
            m_CancelRequested = false;
            m_RunActive = true;
            m_TransferCancellation?.Dispose();
            m_TransferCancellation = new CancellationTokenSource();
            m_RunFinishedSource = new UniTaskCompletionSource();
            RunAsync().Forget(UnityEngine.Debug.LogException);
        }

        /// <summary>
        /// 初始化 member。
        /// </summary>
        private void Initialize(params object[] args)
        {
            if (args == null || args.Length < 2)
            {
                throw new ArgumentException("DownloadHandler requires url and FileModule arguments.", nameof(args));
            }

            var url = args[0] as string;
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("Download url cannot be empty.", nameof(args));
            }

            var fileModule = args[1] as FileModule;
            if (fileModule == null)
            {
                throw new ArgumentException("Download FileModule cannot be null.", nameof(args));
            }

            Url = url;
            m_FileModule = fileModule;
            m_TemporaryFile = fileModule.CreateTemporaryFile("download", url);
            LastObserverException = null;
        }
        /// <summary>
        /// 下载主流程
        /// </summary>
        private async UniTask RunAsync()
        {
            try
            {
                while (!IsDone)
                {
                    try
                    {
                        RaiseProgressChanged();

                        var probe = await ProbeAsync();
                        if (IsTerminal())
                        {
                            if (await WaitForResumeAsync())
                            {
                                continue;
                            }

                            return;
                        }

                        TotalBytes = probe.TotalBytes;
                        var useChunked = probe.SupportsRange && probe.TotalBytes >= LargeFileThreshold;
                        if (useChunked)
                        {
                            await DownloadChunkedAsync(probe.TotalBytes);
                        }
                        else
                        {
                            await DownloadSingleStreamAsync(probe.SupportsRange);
                        }

                        if (IsTerminal())
                        {
                            if (await WaitForResumeAsync())
                            {
                                continue;
                            }

                            return;
                        }

                        DownloadedBytes = TotalBytes > 0 ? TotalBytes : DownloadedBytes;
                        RaiseProgressChanged();
                        CommitSucceeded();
                        return;
                    }
                    catch (Exception exception)
                    {
                        if (Status == OperationStatus.Paused)
                        {
                            if (await WaitForResumeAsync())
                            {
                                continue;
                            }

                            return;
                        }

                        if (m_CancelRequested || Status == OperationStatus.Cancelled)
                        {
                            return;
                        }

                        CompleteFailure(exception, DownloadFailureKind.Network);
                        return;
                    }
                }
            }
            finally
            {
                m_RunActive = false;
                m_TransferCancellation?.Dispose();
                m_TransferCancellation = null;
                if (m_CancelRequested && !IsDone)
                {
                    CompleteCancellation();
                }

                m_RunFinishedSource?.TrySetResult();
            }
        }

        /// <summary>
        /// 等待当前暂停结束，并指示同一次下载 execution 是否应继续。
        /// </summary>
        private async UniTask<bool> WaitForResumeAsync()
        {
            if (Status != OperationStatus.Paused)
            {
                return false;
            }

            var resumeSource = m_ResumeSource;
            if (resumeSource == null)
            {
                throw new GameException("Paused download has no resume signal.");
            }

            await resumeSource.Task;
            return Status == OperationStatus.Running;
        }
        /// <summary>
        /// 探测下载信息，获取总字节数和是否支持分块下载
        /// </summary>
        private async UniTask<(long TotalBytes, bool SupportsRange)> ProbeAsync()
        {
            using (var request = UnityWebRequest.Head(Url))
            {
                request.downloadHandler = new UnityDownloadHandlerBuffer();
                await SendRequestAsync(request, 0, 0);
                if (m_CancelRequested || Status == OperationStatus.Paused)
                {
                    return (-1, false);
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw CreateDownloadException(request);
                }

                var length = GetContentLength(request);
                var acceptsRanges = request.GetResponseHeader("Accept-Ranges");
                return (length, string.Equals(acceptsRanges, "bytes", StringComparison.OrdinalIgnoreCase));
            }
        }
        /// <summary>
        /// 使用单连接下载，适用于小文件或不支持分块下载的情况
        /// </summary>
        /// <param name="supportsRange">supports Range 参数。</param>
        private async UniTask DownloadSingleStreamAsync(bool supportsRange)
        {
            var existingLength = m_TemporaryFile.Length;
            var append = supportsRange && existingLength > 0;
            using (var request = UnityWebRequest.Get(Url))
            {
                if (append)
                {
                    request.SetRequestHeader("Range", $"bytes={existingLength}-");
                }

                request.downloadHandler = new UnityDownloadHandlerFile(m_TemporaryFile.NativePath, append);
                await SendRequestAsync(request, existingLength, TotalBytes > 0 ? TotalBytes - existingLength : 0);
                if (m_CancelRequested || Status == OperationStatus.Paused)
                {
                    return;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw CreateDownloadException(request);
                }

                if (append && request.responseCode != 206)
                {
                    await m_TemporaryFile.DeleteAsync();
                    await DownloadSingleStreamAsync(false);
                    return;
                }

                DownloadedBytes = m_TemporaryFile.Length;
                if (TotalBytes < 0)
                {
                    TotalBytes = DownloadedBytes;
                }

                RaiseProgressChanged();
            }
        }
        /// <summary>
        /// 使用分块下载，适用于大文件且支持分块下载的情况
        /// </summary>
        /// <param name="totalBytes">total Bytes 参数。</param>
        private async UniTask DownloadChunkedAsync(long totalBytes)
        {
            IsChunked = true;
            EnsureChunks(totalBytes);
            await DownloadChunksAsync();
            if (m_CancelRequested || Status is OperationStatus.Paused or OperationStatus.Cancelled)
            {
                return;
            }

            await MergeChunksAsync();
            if (m_CancelRequested)
            {
                return;
            }

            DownloadedBytes = totalBytes;
            CompletedChunkCount = TotalChunkCount;
            RaiseProgressChanged();
        }
        /// <summary>
        /// 下载分块的主流程，内部会控制并发数量，并在每个分块下载完成后更新进度
        /// </summary>
        private async UniTask DownloadChunksAsync()
        {
            var running = new List<UniTask>();
            foreach (var chunk in m_Chunks)
            {
                if (m_CancelRequested || Status is OperationStatus.Paused or OperationStatus.Cancelled)
                {
                    return;
                }

                if (IsChunkComplete(chunk))
                {
                    chunk.Status = OperationStatus.Succeeded;
                    continue;
                }

                running.Add(DownloadChunkAsync(chunk));
                if (running.Count >= ChunkConcurrency)
                {
                    await UniTask.WhenAll(running);
                    running.Clear();
                    if (m_CancelRequested || Status is OperationStatus.Paused or OperationStatus.Cancelled)
                    {
                        return;
                    }
                }
            }

            if (running.Count > 0)
            {
                await UniTask.WhenAll(running);
            }
        }
        /// <summary>
        /// 下载单个分块的流程，内部会处理下载请求、更新分块状态和下载进度，并在下载完成后验证分块完整性
        /// </summary>
        /// <exception cref="DownloadException"></exception>
        private async UniTask DownloadChunkAsync(DownloadChunk chunk)
        {
            if (m_CancelRequested || Status is OperationStatus.Paused or OperationStatus.Cancelled)
            {
                return;
            }

            chunk.Status = OperationStatus.Running;
            var existingLength = chunk.TemporaryFile.Length;
            if (existingLength > chunk.Size)
            {
                throw new DownloadException(
                    $"Chunk {chunk.Index} exceeds its expected length.",
                    DownloadFailureKind.InvalidResponse);
            }

            var requestStart = chunk.Start + existingLength;
            using (var request = UnityWebRequest.Get(Url))
            {
                request.SetRequestHeader("Range", $"bytes={requestStart}-{chunk.End}");
                request.downloadHandler = new UnityDownloadHandlerFile(
                    chunk.TemporaryFile.NativePath,
                    existingLength > 0);
                await SendRequestAsync(
                    request,
                    requestStart,
                    chunk.Size - existingLength);
                if (m_CancelRequested || Status == OperationStatus.Paused)
                {
                    return;
                }

                if (request.result != UnityWebRequest.Result.Success || request.responseCode != 206)
                {
                    throw CreateDownloadException(request);
                }

                if (!IsChunkComplete(chunk))
                {
                    throw new DownloadException($"Chunk {chunk.Index} length mismatch.", DownloadFailureKind.InvalidResponse);
                }

                chunk.Status = OperationStatus.Succeeded;
                UpdateChunkProgress();
                RaiseProgressChanged();
            }
        }
        /// <summary>
        /// 合并分块文件的流程，内部会验证所有分块的完整性，并将分块内容合并到最终文件中，最后删除分块文件
        /// </summary>
        /// <exception cref="DownloadException"></exception>
        private async UniTask MergeChunksAsync()
        {
            foreach (var chunk in m_Chunks)
            {
                if (!IsChunkComplete(chunk))
                {
                    throw new DownloadException($"Chunk {chunk.Index} is incomplete.", DownloadFailureKind.InvalidResponse);
                }
            }

            var parts = new List<(FileTemporaryHandle Handle, long Length)>(m_Chunks.Count);
            foreach (var chunk in m_Chunks)
            {
                parts.Add((chunk.TemporaryFile, chunk.Size));
            }

            await m_TemporaryFile.MergeFromAsync(parts, m_TransferCancellation.Token);
            foreach (var chunk in m_Chunks)
            {
                await chunk.TemporaryFile.ReleaseAsync();
            }
        }
        /// <summary>
        /// 确保分块列表已初始化，如果已经有分块信息则更新下载进度，否则根据总字节数和分块大小创建分块列表，并初始化下载进度
        /// </summary>
        /// <param name="totalBytes">total Bytes 参数。</param>
        private void EnsureChunks(long totalBytes)
        {
            if (m_Chunks.Count > 0)
            {
                UpdateChunkProgress();
                return;
            }

            for (long start = 0, index = 0; start < totalBytes; start += ChunkSize, index++)
            {
                var end = Math.Min(start + ChunkSize - 1, totalBytes - 1);
                m_Chunks.Add(new DownloadChunk
                {
                    Index = (int)index,
                    Start = start,
                    End = end,
                    TemporaryFile = m_FileModule.CreateTemporaryFile("download-part", $"{Url}#{index}"),
                    Status = OperationStatus.Pending
                });
            }

            TotalChunkCount = m_Chunks.Count;
            UpdateChunkProgress();
        }
        /// <summary>
        /// 更新下载进度的流程，内部会遍历分块列表，统计已下载的字节数和已完成的分块数量，并更新相应的属性
        /// </summary>
        private void UpdateChunkProgress()
        {
            long downloaded = 0;
            var completedCount = 0;
            foreach (var chunk in m_Chunks)
            {
                if (!chunk.TemporaryFile.Exists)
                {
                    continue;
                }

                var length = chunk.TemporaryFile.Length;
                downloaded += Math.Min(length, chunk.Size);
                if (length == chunk.Size)
                {
                    completedCount++;
                }
            }

            DownloadedBytes = downloaded;
            CompletedChunkCount = completedCount;
            TotalChunkCount = m_Chunks.Count;
        }
        /// <summary>
        /// 判断分块文件是否完整的流程，内部会检查分块文件是否存在，并且文件大小是否与分块大小一致，返回结果表示分块是否完整
        /// </summary>
        private bool IsChunkComplete(DownloadChunk chunk)
        {
            return chunk.TemporaryFile.Exists && chunk.TemporaryFile.Length == chunk.Size;
        }

        private void CommitSucceeded()
        {
            SetResult();
            NotifyObservers(Completed);
        }

        private void CommitFailed(Exception exception, DownloadFailureKind defaultKind)
        {
            FailureKind = ClassifyFailure(exception, defaultKind);
            SetException(exception);
            NotifyObservers(Failed);
        }

        private void CompleteFailure(Exception exception, DownloadFailureKind defaultKind)
        {
            try
            {
                ReleaseTemporaryFiles();
            }
            catch (Exception cleanupException)
            {
                exception = new AggregateException(
                    "Download failed and temporary cleanup also failed.",
                    exception,
                    cleanupException);
                defaultKind = DownloadFailureKind.FileIO;
            }

            CommitFailed(exception, defaultKind);
        }
        /// <summary>
        /// 根据异常类型分类下载失败的类型，优先使用DownloadException中的类型，如果是IOException则归类为文件IO错误，如果是GameException则使用提供的备用类型，否则归类为网络错误
        /// </summary>
        /// <param name="defaultKind">无法从异常识别类型时使用的默认类型。</param>
        private DownloadFailureKind ClassifyFailure(Exception exception, DownloadFailureKind defaultKind)
        {
            if (exception is DownloadException downloadException)
            {
                return downloadException.FailureKind;
            }

            if (exception is IOException)
            {
                return DownloadFailureKind.FileIO;
            }

            if (exception is GameException)
            {
                return defaultKind;
            }

            return DownloadFailureKind.Network;
        }
        /// <summary>
        /// 判断当前状态是否是下载的终止状态，终止状态包括暂停、取消和失败，如果当前状态是这些状态之一，则返回true，否则返回false
        /// </summary>
        private bool IsTerminal()
        {
            return m_CancelRequested || Status is OperationStatus.Paused or OperationStatus.Cancelled or OperationStatus.Failed;
        }
        /// <summary>
        /// 触发下载进度变化事件，内部会调用ProgressChanged事件的订阅者，并传递当前下载处理器作为参数，通知外部下载进度已经更新
        /// </summary>
        private void RaiseProgressChanged()
        {
            try
            {
                SetProgress(Progress);
            }
            catch (Exception exception)
            {
                RecordObserverException(exception);
            }

            NotifyObservers(ProgressChanged);
        }

        /// <summary>
        /// 设置测试用下载字节统计。
        /// </summary>
        /// <param name="downloadedBytes">已下载字节数。</param>
        /// <param name="totalBytes">总字节数。</param>
        internal void SetBytesForTest(long downloadedBytes, long totalBytes)
        {
            DownloadedBytes = downloadedBytes;
            TotalBytes = totalBytes;
        }

        /// <summary>
        /// 设置测试用下载失败类型。
        /// </summary>
        /// <param name="failureKind">下载失败类型。</param>
        internal void SetFailureKindForTest(DownloadFailureKind failureKind)
        {
            FailureKind = failureKind;
        }

        /// <summary>
        /// 触发测试用下载进度变化事件。
        /// </summary>
        internal void RaiseProgressForTest()
        {
            RaiseProgressChanged();
        }
        /// <summary>
        /// 触发下载取消事件，内部会设置下载失败类型为已取消，并调用Canceled事件的订阅者，通知外部下载已经被取消
        /// </summary>
        private void CompleteCancellation()
        {
            try
            {
                ReleaseTemporaryFiles();
            }
            catch (Exception exception)
            {
                CommitFailed(exception, DownloadFailureKind.FileIO);
                return;
            }

            FailureKind = DownloadFailureKind.Canceled;
            base.SetCancel();
            NotifyObservers(Canceled);
        }

        private void NotifyObservers(Action<DownloadHandler> observers)
        {
            if (observers == null)
            {
                return;
            }

            foreach (Action<DownloadHandler> observer in observers.GetInvocationList())
            {
                try
                {
                    observer(this);
                }
                catch (Exception exception)
                {
                    RecordObserverException(exception);
                }
            }
        }

        private void RecordObserverException(Exception exception)
        {
            LastObserverException = LastObserverException == null
                ? exception
                : new AggregateException("Multiple download observers threw exceptions.", LastObserverException, exception);
        }
        /// <summary>
        /// 释放主下载文件和所有分块文件。
        /// </summary>
        private void ReleaseTemporaryFiles()
        {
            List<Exception> exceptions = null;
            foreach (var chunk in m_Chunks)
            {
                try
                {
                    chunk.TemporaryFile?.ReleaseAsync().GetAwaiter().GetResult();
                }
                catch (Exception exception)
                {
                    exceptions ??= new List<Exception>();
                    exceptions.Add(exception);
                }
            }

            try
            {
                m_TemporaryFile?.ReleaseAsync().GetAwaiter().GetResult();
                m_TemporaryFile = null;
            }
            catch (Exception exception)
            {
                exceptions ??= new List<Exception>();
                exceptions.Add(exception);
            }

            if (exceptions != null)
            {
                throw new AggregateException("One or more download temporary files could not be released.", exceptions);
            }
        }

        private void EnsureResultAvailable()
        {
            if (Status != OperationStatus.Succeeded || m_TemporaryFile == null)
            {
                throw new GameException("Download result is only available while a succeeded handler retains its temporary file.");
            }
        }
        /// <summary>
        /// 从UnityWebRequest的响应头中获取内容长度的流程，内部会尝试从响应头中获取Content-Length字段，并将其解析为长整数，如果解析成功则返回该值，否则返回-1表示未知长度
        /// </summary>
        private static long GetContentLength(UnityWebRequest request)
        {
            var contentLength = request.GetResponseHeader("Content-Length");
            return long.TryParse(contentLength, out var length) ? length : -1;
        }
        /// <summary>
        /// 发送下载请求的流程，内部会发送UnityWebRequest请求，并在请求过程中持续检查取消和暂停状态，如果请求完成后状态不是成功，则抛出下载异常，如果请求成功则更新下载进度，确保在下载过程中能够正确响应取消和暂停操作，并在下载完成后能够正确处理结果
        /// </summary>
        /// <param name="baseDownloadedBytes">base Downloaded Bytes 参数。</param>
        /// <param name="expectedBytes">expected Bytes 参数。</param>
        private async UniTask SendRequestAsync(UnityWebRequest request, long baseDownloadedBytes, long expectedBytes)
        {
            var operation = request.SendWebRequest();
            m_ActiveRequests.Add(request);
            try
            {
                while (!operation.isDone)
                {
                    if (m_CancelRequested || Status == OperationStatus.Paused)
                    {
                        request.Abort();
                        while (!operation.isDone)
                        {
                            await UniTask.Yield();
                        }

                        return;
                    }

                    if (expectedBytes > 0)
                    {
                        DownloadedBytes = baseDownloadedBytes + (long)(expectedBytes * request.downloadProgress);
                        RaiseProgressChanged();
                    }

                    await UniTask.Yield();
                }
            }
            finally
            {
                m_ActiveRequests.Remove(request);
            }
        }

        private void AbortActiveRequests()
        {
            var requests = m_ActiveRequests.ToArray();
            foreach (var request in requests)
            {
                request.Abort();
            }
        }

        internal void CompleteSucceededForTest()
        {
            CommitSucceeded();
        }

        internal void CompleteFailedForTest(Exception exception)
        {
            CommitFailed(exception, DownloadFailureKind.Network);
        }

        internal UniTask WaitRunFinishedAsync()
        {
            return m_RunFinishedSource?.Task ?? UniTask.CompletedTask;
        }
        /// <summary>
        /// 根据UnityWebRequest的结果创建下载异常的流程，内部会检查请求的结果，如果请求失败则根据错误信息和结果类型创建一个DownloadException对象，并返回该对象，确保在下载过程中能够正确捕获和处理各种类型的错误情况
        /// </summary>
        private static Exception CreateDownloadException(UnityWebRequest request)
        {
            var message = request.error ?? $"Unexpected response code {request.responseCode}.";
            var kind = request.result == UnityWebRequest.Result.ProtocolError
                ? DownloadFailureKind.HttpStatus
                : request.result == UnityWebRequest.Result.DataProcessingError
                    ? DownloadFailureKind.InvalidResponse
                    : DownloadFailureKind.Network;
            return new DownloadException(message, kind);
        }
    }
}
