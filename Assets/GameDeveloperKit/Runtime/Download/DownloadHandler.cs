using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;
using UnityDownloadHandlerFile = UnityEngine.Networking.DownloadHandlerFile;
using UnityDownloadHandlerBuffer = UnityEngine.Networking.DownloadHandlerBuffer;

namespace GameDeveloperKit.Download
{
    public class DownloadHandler
    {
        internal const long LargeFileThreshold = 16L * 1024L * 1024L;
        internal const long ChunkSize = 4L * 1024L * 1024L;
        private const int ChunkConcurrency = 4;

        private readonly string m_TempRoot;
        private readonly List<DownloadChunk> m_Chunks = new List<DownloadChunk>();
        private UniTaskCompletionSource m_CompletionSource = new UniTaskCompletionSource();
        private bool m_Started;
        private bool m_CompletionSignaled;
        private bool m_CancelRequested;

        public string Url { get; }
        public string TempPath { get; }
        public bool IsChunked { get; private set; }
        public int CompletedChunkCount { get; private set; }
        public int TotalChunkCount { get; private set; }
        public DownloadStatus Status { get; private set; }
        public float Progress => TotalBytes > 0 ? Math.Min(1f, (float)((double)DownloadedBytes / TotalBytes)) : 0f;
        public long DownloadedBytes { get; private set; }
        public long TotalBytes { get; private set; } = -1;
        public string Error { get; private set; }
        public DownloadFailureKind FailureKind { get; private set; }

        public event Action<DownloadHandler> ProgressChanged;
        public event Action<DownloadHandler> Completed;
        public event Action<DownloadHandler> Failed;
        public event Action<DownloadHandler> Canceled;

        internal DownloadHandler(string url, string tempRoot)
        {
            Url = url;
            m_TempRoot = tempRoot;
            TempPath = Path.Combine(m_TempRoot, GetFileName(url) + ".download");
            Status = DownloadStatus.Waiting;
        }

        public UniTask WaitCompletionAsync()
        {
            return m_CompletionSource.Task;
        }

        public UniTask Pause()
        {
            if (Status == DownloadStatus.Downloading || Status == DownloadStatus.Waiting)
            {
                Status = DownloadStatus.Paused;
            }

            return UniTask.CompletedTask;
        }

        public UniTask Resume()
        {
            if (Status != DownloadStatus.Paused && Status != DownloadStatus.Failed)
            {
                return UniTask.CompletedTask;
            }

            Error = null;
            FailureKind = DownloadFailureKind.None;
            Status = DownloadStatus.Waiting;
            ResetCompletion();
            RunAsync().Forget();
            return UniTask.CompletedTask;
        }

        public async UniTask Cancel()
        {
            if (Status == DownloadStatus.Canceled)
            {
                return;
            }

            m_CancelRequested = true;
            Status = DownloadStatus.Canceled;
            DeleteTempFiles();
            RaiseCanceled();
            SignalCompletion();
            await UniTask.Yield();
        }

        internal void Start()
        {
            if (m_Started && Status != DownloadStatus.Waiting)
            {
                return;
            }

            m_Started = true;
            RunAsync().Forget();
        }

        private async UniTaskVoid RunAsync()
        {
            try
            {
                Directory.CreateDirectory(m_TempRoot);
                m_CancelRequested = false;
                Status = DownloadStatus.Downloading;
                RaiseProgressChanged();

                var probe = await ProbeAsync();
                if (IsTerminal())
                {
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
                    return;
                }

                Status = DownloadStatus.Completed;
                DownloadedBytes = TotalBytes > 0 ? TotalBytes : DownloadedBytes;
                RaiseProgressChanged();
                Completed?.Invoke(this);
                SignalCompletion();
            }
            catch (Exception exception)
            {
                if (Status == DownloadStatus.Canceled || Status == DownloadStatus.Paused)
                {
                    SignalCompletionIfCanceled();
                    return;
                }

                SetFailed(exception, DownloadFailureKind.Network);
            }
        }

        private async UniTask<(long TotalBytes, bool SupportsRange)> ProbeAsync()
        {
            using (var request = UnityWebRequest.Head(Url))
            {
                request.downloadHandler = new UnityDownloadHandlerBuffer();
                await request.SendWebRequest();
                if (m_CancelRequested || Status == DownloadStatus.Paused)
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

        private async UniTask DownloadSingleStreamAsync(bool supportsRange)
        {
            var existingLength = System.IO.File.Exists(TempPath) ? new FileInfo(TempPath).Length : 0;
            var append = supportsRange && existingLength > 0;
            using (var request = UnityWebRequest.Get(Url))
            {
                if (append)
                {
                    request.SetRequestHeader("Range", $"bytes={existingLength}-");
                }

                request.downloadHandler = new UnityDownloadHandlerFile(TempPath, append);
                await SendRequestAsync(request, existingLength, TotalBytes > 0 ? TotalBytes - existingLength : 0);
                if (m_CancelRequested || Status == DownloadStatus.Paused)
                {
                    return;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw CreateDownloadException(request);
                }

                if (append && request.responseCode != 206)
                {
                    DeleteFileIfExists(TempPath);
                    await DownloadSingleStreamAsync(false);
                    return;
                }

                DownloadedBytes = System.IO.File.Exists(TempPath) ? new FileInfo(TempPath).Length : 0;
                if (TotalBytes < 0)
                {
                    TotalBytes = DownloadedBytes;
                }

                RaiseProgressChanged();
            }
        }

        private async UniTask DownloadChunkedAsync(long totalBytes)
        {
            IsChunked = true;
            EnsureChunks(totalBytes);
            await DownloadChunksAsync();
            if (Status == DownloadStatus.Paused || Status == DownloadStatus.Canceled)
            {
                return;
            }

            await MergeChunksAsync();
            DownloadedBytes = totalBytes;
            CompletedChunkCount = TotalChunkCount;
            RaiseProgressChanged();
        }

        private async UniTask DownloadChunksAsync()
        {
            var running = new List<UniTask>();
            foreach (var chunk in m_Chunks)
            {
                if (IsChunkComplete(chunk))
                {
                    chunk.Status = DownloadStatus.Completed;
                    continue;
                }

                running.Add(DownloadChunkAsync(chunk));
                if (running.Count >= ChunkConcurrency)
                {
                    await UniTask.WhenAll(running);
                    running.Clear();
                    if (Status == DownloadStatus.Paused || Status == DownloadStatus.Canceled)
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

        private async UniTask DownloadChunkAsync(DownloadChunk chunk)
        {
            if (Status == DownloadStatus.Paused || Status == DownloadStatus.Canceled)
            {
                return;
            }

            chunk.Status = DownloadStatus.Downloading;
            using (var request = UnityWebRequest.Get(Url))
            {
                request.SetRequestHeader("Range", $"bytes={chunk.Start}-{chunk.End}");
                request.downloadHandler = new UnityDownloadHandlerFile(chunk.PartPath);
                await SendRequestAsync(request, chunk.Start, chunk.Size);
                if (m_CancelRequested || Status == DownloadStatus.Paused)
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

                chunk.Status = DownloadStatus.Completed;
                UpdateChunkProgress();
                RaiseProgressChanged();
            }
        }

        private async UniTask MergeChunksAsync()
        {
            foreach (var chunk in m_Chunks)
            {
                if (!IsChunkComplete(chunk))
                {
                    throw new DownloadException($"Chunk {chunk.Index} is incomplete.", DownloadFailureKind.InvalidResponse);
                }
            }

            using (var output = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                foreach (var chunk in m_Chunks)
                {
                    using (var input = new FileStream(chunk.PartPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        await input.CopyToAsync(output);
                    }
                }
            }

            foreach (var chunk in m_Chunks)
            {
                if (System.IO.File.Exists(chunk.PartPath))
                {
                    System.IO.File.Delete(chunk.PartPath);
                }
            }
        }

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
                    PartPath = $"{TempPath}.{index}.part",
                    Status = DownloadStatus.Waiting
                });
            }

            TotalChunkCount = m_Chunks.Count;
            UpdateChunkProgress();
        }

        private void UpdateChunkProgress()
        {
            long downloaded = 0;
            var completedCount = 0;
            foreach (var chunk in m_Chunks)
            {
                if (!System.IO.File.Exists(chunk.PartPath))
                {
                    continue;
                }

                var length = new FileInfo(chunk.PartPath).Length;
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

        private bool IsChunkComplete(DownloadChunk chunk)
        {
            return System.IO.File.Exists(chunk.PartPath) && new FileInfo(chunk.PartPath).Length == chunk.Size;
        }

        private void SetFailed(Exception exception, DownloadFailureKind fallbackKind)
        {
            Status = DownloadStatus.Failed;
            Error = exception.Message;
            FailureKind = ClassifyFailure(exception, fallbackKind);
            Failed?.Invoke(this);
            SignalCompletion();
        }

        private DownloadFailureKind ClassifyFailure(Exception exception, DownloadFailureKind fallbackKind)
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
                return fallbackKind;
            }

            return DownloadFailureKind.Network;
        }

        private bool IsTerminal()
        {
            return Status == DownloadStatus.Paused || Status == DownloadStatus.Canceled || Status == DownloadStatus.Failed;
        }

        private void SignalCompletionIfCanceled()
        {
            if (Status == DownloadStatus.Canceled)
            {
                SignalCompletion();
            }
        }

        private void SignalCompletion()
        {
            if (m_CompletionSignaled)
            {
                return;
            }

            m_CompletionSignaled = true;
            m_CompletionSource.TrySetResult();
        }

        private void ResetCompletion()
        {
            m_CompletionSignaled = false;
            m_CompletionSource = new UniTaskCompletionSource();
        }

        private void RaiseProgressChanged()
        {
            ProgressChanged?.Invoke(this);
        }

        private void RaiseCanceled()
        {
            FailureKind = DownloadFailureKind.Canceled;
            Canceled?.Invoke(this);
        }

        private void DeleteTempFiles()
        {
            DeleteFileIfExists(TempPath);
            foreach (var chunk in m_Chunks)
            {
                DeleteFileIfExists(chunk.PartPath);
            }
        }

        private static void DeleteFileIfExists(string path)
        {
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }

        private static long GetContentLength(UnityWebRequest request)
        {
            var contentLength = request.GetResponseHeader("Content-Length");
            return long.TryParse(contentLength, out var length) ? length : -1;
        }

        private async UniTask SendRequestAsync(UnityWebRequest request, long baseDownloadedBytes, long expectedBytes)
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (m_CancelRequested || Status == DownloadStatus.Paused)
                {
                    request.Abort();
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

        private static string GetFileName(string url)
        {
            var uri = new Uri(url);
            var name = Path.GetFileName(uri.LocalPath);
            return string.IsNullOrEmpty(name) ? uri.GetHashCode().ToString("X8") : name;
        }

        private sealed class DownloadException : GameException
        {
            public DownloadFailureKind FailureKind { get; }

            public DownloadException(string message, DownloadFailureKind failureKind) : base(message)
            {
                FailureKind = failureKind;
            }
        }
    }
}