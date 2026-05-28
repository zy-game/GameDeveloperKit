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

        public void Stop()
        {
            m_Stop?.Invoke(this);
        }

        public void Pause()
        {
            m_Pause?.Invoke(this);
        }

        public void Resume()
        {
            m_Resume?.Invoke(this);
        }

        public UniTask WaitForCompleteAsync()
        {
            return m_CompletionSource.Task;
        }

        public void Release()
        {
            if (Status == SoundStatus.Released)
            {
                return;
            }

            Stop();
            SetStatus(SoundStatus.Released);
        }

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
