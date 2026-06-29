using System;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// AVProVideo Story 视频预热句柄。
    /// </summary>
    public sealed class StoryAvProVideoPreloadHandle
    {
        private readonly UniTaskCompletionSource<StoryAvProVideoPreloadHandle> m_ReadyCompletion =
            new UniTaskCompletionSource<StoryAvProVideoPreloadHandle>();

        internal StoryAvProVideoPreloadHandle(
            StoryCommand command,
            string source,
            string clipPath,
            string resolvedPath)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            Source = source;
            ClipPath = clipPath;
            ResolvedPath = resolvedPath;
            Status = StoryAvProVideoPreloadStatus.Pending;
        }

        /// <summary>
        /// 预热对应的命令。
        /// </summary>
        public StoryCommand Command { get; }

        /// <summary>
        /// 视频来源。
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// 原始视频路径。
        /// </summary>
        public string ClipPath { get; }

        /// <summary>
        /// AVPro 实际打开路径。
        /// </summary>
        public string ResolvedPath { get; }

        /// <summary>
        /// 当前预热状态。
        /// </summary>
        public StoryAvProVideoPreloadStatus Status { get; private set; }

        /// <summary>
        /// 预热错误。
        /// </summary>
        public Exception Error { get; private set; }

        /// <summary>
        /// 是否已进入预热终态。
        /// </summary>
        public bool IsTerminal =>
            Status == StoryAvProVideoPreloadStatus.Failed ||
            Status == StoryAvProVideoPreloadStatus.Canceled;

        /// <summary>
        /// 是否可以被播放命令接管。
        /// </summary>
        public bool CanAcquire =>
            Status == StoryAvProVideoPreloadStatus.ReadyToPlay ||
            Status == StoryAvProVideoPreloadStatus.FirstFrameReady;

        /// <summary>
        /// 状态变化事件。
        /// </summary>
        public event Action<StoryAvProVideoPreloadHandle> StatusChanged;

        internal static StoryAvProVideoPreloadHandle CreateFailed(
            StoryCommand command,
            string source,
            string clipPath,
            Exception exception)
        {
            var handle = new StoryAvProVideoPreloadHandle(command, source, clipPath, null);
            handle.Fail(exception);
            return handle;
        }

        internal void ReadyToPlay()
        {
            if (Status != StoryAvProVideoPreloadStatus.Pending)
            {
                return;
            }

            SetStatus(StoryAvProVideoPreloadStatus.ReadyToPlay, null);
            m_ReadyCompletion.TrySetResult(this);
        }

        internal void FirstFrameReady()
        {
            if (Status == StoryAvProVideoPreloadStatus.Failed ||
                Status == StoryAvProVideoPreloadStatus.Canceled)
            {
                return;
            }

            SetStatus(StoryAvProVideoPreloadStatus.FirstFrameReady, null);
            m_ReadyCompletion.TrySetResult(this);
        }

        internal void Fail(Exception exception)
        {
            if (Status == StoryAvProVideoPreloadStatus.Failed ||
                Status == StoryAvProVideoPreloadStatus.Canceled)
            {
                return;
            }

            SetStatus(StoryAvProVideoPreloadStatus.Failed, exception ?? new GameException("Story video preload failed."));
            m_ReadyCompletion.TrySetResult(this);
        }

        internal void Cancel()
        {
            if (Status == StoryAvProVideoPreloadStatus.Failed ||
                Status == StoryAvProVideoPreloadStatus.Canceled)
            {
                return;
            }

            SetStatus(StoryAvProVideoPreloadStatus.Canceled, null);
            m_ReadyCompletion.TrySetResult(this);
        }

        internal async UniTask<StoryAvProVideoPreloadHandle> WaitUntilReadyAsync(CancellationToken cancellationToken)
        {
            if (Status != StoryAvProVideoPreloadStatus.Pending)
            {
                return this;
            }

            return await m_ReadyCompletion.Task.AttachExternalCancellation(cancellationToken);
        }

        private void SetStatus(StoryAvProVideoPreloadStatus status, Exception exception)
        {
            Status = status;
            Error = exception;
            StatusChanged?.Invoke(this);
        }
    }
}
