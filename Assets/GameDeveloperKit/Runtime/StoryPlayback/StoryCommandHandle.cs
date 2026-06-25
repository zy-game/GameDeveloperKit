using System;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情命令执行句柄。
    /// </summary>
    public interface IStoryCommandHandle
    {
        /// <summary>
        /// 命令。
        /// </summary>
        StoryCommand Command { get; }

        /// <summary>
        /// 是否已完成。
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// 是否已取消。
        /// </summary>
        bool IsCanceled { get; }

        /// <summary>
        /// 是否已停止。
        /// </summary>
        bool IsStopped { get; }

        /// <summary>
        /// 执行错误。
        /// </summary>
        Exception Error { get; }

        /// <summary>
        /// 完成结果 ID。
        /// </summary>
        string OutcomeId { get; }

        /// <summary>
        /// 完成事件。
        /// </summary>
        event Action<IStoryCommandHandle> Completed;

        /// <summary>
        /// 取消事件。
        /// </summary>
        event Action<IStoryCommandHandle> Canceled;

        /// <summary>
        /// 停止事件。
        /// </summary>
        event Action<IStoryCommandHandle> Stopped;

        /// <summary>
        /// 失败事件。
        /// </summary>
        event Action<IStoryCommandHandle> Failed;

        /// <summary>
        /// 标记命令完成。
        /// </summary>
        /// <param name="outcomeId">结果 ID。</param>
        void Complete(string outcomeId = null);

        /// <summary>
        /// 取消命令。
        /// </summary>
        void Cancel();

        /// <summary>
        /// 停止命令。
        /// </summary>
        void Stop();

        /// <summary>
        /// 标记命令失败。
        /// </summary>
        /// <param name="exception">错误。</param>
        void Fail(Exception exception);
    }

    /// <summary>
    /// 默认剧情命令执行句柄。
    /// </summary>
    public sealed class StoryCommandHandle : IStoryCommandHandle
    {
        /// <summary>
        /// 初始化剧情命令执行句柄。
        /// </summary>
        /// <param name="command">命令。</param>
        public StoryCommandHandle(StoryCommand command)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
        }

        /// <inheritdoc />
        public StoryCommand Command { get; }

        /// <inheritdoc />
        public bool IsCompleted { get; private set; }

        /// <inheritdoc />
        public bool IsCanceled { get; private set; }

        /// <inheritdoc />
        public bool IsStopped { get; private set; }

        /// <inheritdoc />
        public Exception Error { get; private set; }

        /// <inheritdoc />
        public string OutcomeId { get; private set; }

        /// <inheritdoc />
        public event Action<IStoryCommandHandle> Completed;

        /// <inheritdoc />
        public event Action<IStoryCommandHandle> Canceled;

        /// <inheritdoc />
        public event Action<IStoryCommandHandle> Stopped;

        /// <inheritdoc />
        public event Action<IStoryCommandHandle> Failed;

        /// <inheritdoc />
        public void Complete(string outcomeId = null)
        {
            if (IsTerminal)
            {
                return;
            }

            OutcomeId = outcomeId;
            IsCompleted = true;
            Completed?.Invoke(this);
        }

        /// <inheritdoc />
        public void Cancel()
        {
            if (IsTerminal)
            {
                return;
            }

            IsCanceled = true;
            Canceled?.Invoke(this);
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (IsTerminal)
            {
                return;
            }

            IsStopped = true;
            Stopped?.Invoke(this);
        }

        /// <inheritdoc />
        public void Fail(Exception exception)
        {
            if (IsTerminal)
            {
                return;
            }

            Error = exception ?? throw new ArgumentNullException(nameof(exception));
            Failed?.Invoke(this);
        }

        private bool IsTerminal => IsCompleted || IsCanceled || IsStopped || Error != null;
    }
}
