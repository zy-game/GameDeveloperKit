using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// AVProVideo Story 视频预热队列。
    /// </summary>
    public sealed class StoryAvProVideoPreloadQueue : IDisposable
    {
        private readonly Dictionary<string, PreloadEntry> m_Entries =
            new Dictionary<string, PreloadEntry>(StringComparer.Ordinal);
        private readonly Queue<string> m_Order = new Queue<string>();
        private readonly Transform m_Parent;
        private readonly bool m_DontDestroyOnLoad;

        private bool m_Disposed;

        /// <summary>
        /// 初始化 AVProVideo Story 视频预热队列。
        /// </summary>
        /// <param name="parent">播放器对象父节点。</param>
        /// <param name="dontDestroyOnLoad">没有父节点时是否跨场景保留。</param>
        /// <param name="capacity">队列容量。</param>
        public StoryAvProVideoPreloadQueue(
            Transform parent = null,
            bool dontDestroyOnLoad = true,
            int capacity = 2)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
            }

            m_Parent = parent;
            m_DontDestroyOnLoad = dontDestroyOnLoad;
            Capacity = capacity;
        }

        /// <summary>
        /// 队列容量。
        /// </summary>
        public int Capacity { get; }

        /// <summary>
        /// 当前队列条目数量。
        /// </summary>
        public int Count => m_Entries.Count;

        /// <summary>
        /// 预热视频。
        /// </summary>
        /// <param name="command">剧情命令。</param>
        /// <param name="source">视频来源。</param>
        /// <param name="clipPath">视频路径。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>预热句柄。</returns>
        public UniTask<StoryAvProVideoPreloadHandle> PreloadAsync(
            StoryCommand command,
            string source,
            string clipPath,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (StoryVideoPathResolver.TryResolve(source, clipPath, out var resolvedPath, out var errorMessage))
            {
                return PreloadResolvedAsync(command, source, clipPath, resolvedPath, cancellationToken);
            }

            var exception = new GameException(
                $"Story video preload path is invalid. command:{command.CommandId} source:{source} path:{clipPath} reason:{errorMessage}");
            return UniTask.FromResult(StoryAvProVideoPreloadHandle.CreateFailed(command, source, clipPath, exception));
        }

        /// <summary>
        /// 尝试取得预热播放器。
        /// </summary>
        /// <param name="source">视频来源。</param>
        /// <param name="clipPath">视频路径。</param>
        /// <param name="playback">播放实例。</param>
        /// <returns>成功取得时返回 true。</returns>
        public bool TryAcquire(
            string source,
            string clipPath,
            out StoryAvProVideoPlayback playback)
        {
            EnsureNotDisposed();
            playback = null;
            var key = BuildKey(source, clipPath);
            if (m_Entries.TryGetValue(key, out var entry) is false ||
                entry.Handle.CanAcquire is false)
            {
                return false;
            }

            m_Entries.Remove(key);
            DetachEntry(entry);
            playback = entry.Playback;
            return playback != null;
        }

        /// <summary>
        /// 释放指定预热视频。
        /// </summary>
        /// <param name="source">视频来源。</param>
        /// <param name="clipPath">视频路径。</param>
        public void Release(string source, string clipPath)
        {
            if (m_Disposed)
            {
                return;
            }

            var key = BuildKey(source, clipPath);
            if (m_Entries.TryGetValue(key, out var entry) is false)
            {
                return;
            }

            m_Entries.Remove(key);
            DisposeEntry(entry, true);
        }

        /// <summary>
        /// 清空队列。
        /// </summary>
        public void Clear()
        {
            if (m_Disposed)
            {
                return;
            }

            var entries = new List<PreloadEntry>(m_Entries.Values);
            m_Entries.Clear();
            m_Order.Clear();
            for (var i = 0; i < entries.Count; i++)
            {
                DisposeEntry(entries[i], true);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            Clear();
            m_Disposed = true;
        }

        internal async UniTask<StoryAvProVideoPreloadHandle> PreloadResolvedAsync(
            StoryCommand command,
            string source,
            string clipPath,
            string resolvedPath,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            EnsureNotDisposed();
            var key = BuildKey(source, clipPath);
            if (m_Entries.TryGetValue(key, out var existing))
            {
                return await WaitForHandleAsync(existing, cancellationToken);
            }

            EvictIfNeeded();

            var handle = new StoryAvProVideoPreloadHandle(command, source, clipPath, resolvedPath);
            var playback = StoryAvProVideoPlayback.CreatePreloaded(
                command,
                clipPath,
                resolvedPath,
                m_Parent,
                m_DontDestroyOnLoad);
            var entry = new PreloadEntry(key, handle, playback);
            AttachEntry(entry);
            m_Entries[key] = entry;
            m_Order.Enqueue(key);

            if (playback.Preload() is false)
            {
                DisposeEntry(entry, false);
                m_Entries.Remove(key);
                return handle;
            }

            return await WaitForHandleAsync(entry, cancellationToken);
        }

        private async UniTask<StoryAvProVideoPreloadHandle> WaitForHandleAsync(
            PreloadEntry entry,
            CancellationToken cancellationToken)
        {
            try
            {
                return await entry.Handle.WaitUntilReadyAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CancelEntry(entry.Key);
                return entry.Handle;
            }
        }

        private void EvictIfNeeded()
        {
            while (m_Entries.Count >= Capacity && m_Order.Count > 0)
            {
                var key = m_Order.Dequeue();
                if (m_Entries.TryGetValue(key, out var entry) is false)
                {
                    continue;
                }

                m_Entries.Remove(key);
                DisposeEntry(entry, true);
                return;
            }
        }

        private void CancelEntry(string key)
        {
            if (m_Disposed || m_Entries.TryGetValue(key, out var entry) is false)
            {
                return;
            }

            m_Entries.Remove(key);
            DisposeEntry(entry, true);
        }

        private void AttachEntry(PreloadEntry entry)
        {
            entry.Playback.ReadyToPlay += OnPlaybackReadyToPlay;
            entry.Playback.FirstFrameReady += OnPlaybackFirstFrameReady;
            entry.Playback.PreloadFailed += OnPlaybackPreloadFailed;
        }

        private void DetachEntry(PreloadEntry entry)
        {
            entry.Playback.ReadyToPlay -= OnPlaybackReadyToPlay;
            entry.Playback.FirstFrameReady -= OnPlaybackFirstFrameReady;
            entry.Playback.PreloadFailed -= OnPlaybackPreloadFailed;
        }

        private void DisposeEntry(PreloadEntry entry, bool cancel)
        {
            DetachEntry(entry);
            if (cancel)
            {
                entry.Handle.Cancel();
            }

            entry.Playback.Dispose();
        }

        private void OnPlaybackReadyToPlay(StoryAvProVideoPlayback playback)
        {
            var entry = FindEntry(playback);
            entry?.Handle.ReadyToPlay();
        }

        private void OnPlaybackFirstFrameReady(StoryAvProVideoPlayback playback)
        {
            var entry = FindEntry(playback);
            entry?.Handle.FirstFrameReady();
        }

        private void OnPlaybackPreloadFailed(StoryAvProVideoPlayback playback, Exception exception)
        {
            var entry = FindEntry(playback);
            if (entry == null)
            {
                return;
            }

            entry.Handle.Fail(exception);
            m_Entries.Remove(entry.Key);
            DisposeEntry(entry, false);
        }

        private PreloadEntry FindEntry(StoryAvProVideoPlayback playback)
        {
            if (playback == null)
            {
                return null;
            }

            foreach (var entry in m_Entries.Values)
            {
                if (ReferenceEquals(entry.Playback, playback))
                {
                    return entry;
                }
            }

            return null;
        }

        private void EnsureNotDisposed()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(StoryAvProVideoPreloadQueue));
            }
        }

        private static string BuildKey(string source, string clipPath)
        {
            return (source ?? string.Empty).Trim() + "\u001f" + (clipPath ?? string.Empty).Trim();
        }

        private sealed class PreloadEntry
        {
            public PreloadEntry(
                string key,
                StoryAvProVideoPreloadHandle handle,
                StoryAvProVideoPlayback playback)
            {
                Key = key;
                Handle = handle;
                Playback = playback;
            }

            public string Key { get; }

            public StoryAvProVideoPreloadHandle Handle { get; }

            public StoryAvProVideoPlayback Playback { get; }
        }
    }
}
