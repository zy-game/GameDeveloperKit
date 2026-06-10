using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Sound
{
    /// <summary>
    /// 定义 Sound Handle 类型。
    /// </summary>
    public sealed class SoundHandle : IReference
    {
        /// <summary>
        /// 存储 Stop。
        /// </summary>
        private readonly Action<SoundHandle> m_Stop;
        /// <summary>
        /// 存储 Pause。
        /// </summary>
        private readonly Action<SoundHandle> m_Pause;
        /// <summary>
        /// 存储 Resume。
        /// </summary>
        private readonly Action<SoundHandle> m_Resume;
        /// <summary>
        /// 存储 Completion Source。
        /// </summary>
        private readonly UniTaskCompletionSource m_CompletionSource = new UniTaskCompletionSource();

        /// <summary>
        /// 初始化 Sound Handle。
        /// </summary>
        /// <param name="location">location 参数。</param>
        /// <param name="track">track 参数。</param>
        /// <param name="stop">stop 参数。</param>
        /// <param name="pause">pause 参数。</param>
        /// <param name="resume">resume 参数。</param>
        internal SoundHandle(string location, SoundTrack track, Action<SoundHandle> stop, Action<SoundHandle> pause, Action<SoundHandle> resume)
        {
            Location = location;
            Track = track;
            m_Stop = stop;
            m_Pause = pause;
            m_Resume = resume;
        }

        public string Location { get; }

        public SoundTrack Track { get; }

        public SoundStatus Status { get; private set; }

        public float Progress { get; internal set; }

        /// <summary>
        /// 执行 Stop。
        /// </summary>
        public void Stop()
        {
            m_Stop?.Invoke(this);
        }

        /// <summary>
        /// 执行 Pause。
        /// </summary>
        public void Pause()
        {
            m_Pause?.Invoke(this);
        }

        /// <summary>
        /// 执行 Resume。
        /// </summary>
        public void Resume()
        {
            m_Resume?.Invoke(this);
        }

        /// <summary>
        /// 执行 Wait For Complete Async。
        /// </summary>
        /// <returns>操作完成任务。</returns>
        public UniTask WaitForCompleteAsync()
        {
            return m_CompletionSource.Task;
        }

        /// <summary>
        /// 执行 Release。
        /// </summary>
        public void Release()
        {
            if (Status == SoundStatus.Released)
            {
                return;
            }

            Stop();
            SetStatus(SoundStatus.Released);
        }

        /// <summary>
        /// 设置 Status。
        /// </summary>
        /// <param name="status">status 参数。</param>
        internal void SetStatus(SoundStatus status)
        {
            if (Status == SoundStatus.Released && status != SoundStatus.Released)
            {
                return;
            }

            Status = status;
            if (status is SoundStatus.Stopped or SoundStatus.Completed or SoundStatus.Failed or SoundStatus.Released)
            {
                m_CompletionSource.TrySetResult();
            }
        }
    }
}
