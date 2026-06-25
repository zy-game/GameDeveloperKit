using System;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Sound
{
    public sealed class SoundHandle : IReference
    {
        private readonly Action<SoundHandle> m_Stop;
        private readonly Action<SoundHandle> m_Pause;
        private readonly Action<SoundHandle> m_Resume;
        private readonly UniTaskCompletionSource m_CompletionSource = new UniTaskCompletionSource();

        /// <summary>
        /// 初始化 Sound Handle。
        /// </summary>
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
