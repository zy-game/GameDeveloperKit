using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Playable
{
    public sealed class AudioPlayableHandle : PlayableHandle
    {
        private readonly Action<AudioPlayableHandle> m_Stop;
        private readonly Action<AudioPlayableHandle> m_Pause;
        private readonly Action<AudioPlayableHandle> m_Resume;
        private CancellationTokenRegistration m_CancellationRegistration;

        internal AudioPlayableHandle(
            string location,
            AudioTrack track,
            Action<AudioPlayableHandle> stop,
            Action<AudioPlayableHandle> pause,
            Action<AudioPlayableHandle> resume)
        {
            Location = location;
            Track = track;
            m_Stop = stop;
            m_Pause = pause;
            m_Resume = resume;
        }

        public string Location { get; }

        public AudioTrack Track { get; }

        public float Progress { get; internal set; }

        internal void Start(CancellationToken cancellationToken)
        {
            SetPlaying();
            if (cancellationToken.CanBeCanceled)
            {
                m_CancellationRegistration = cancellationToken.Register(
                    static state =>
                    {
                        var request = (CancellationRequest)state;
                        request.Handle.RequestCancel(request.CancellationToken);
                    },
                    new CancellationRequest(this, cancellationToken));
            }
        }

        internal void RequestCancel(CancellationToken cancellationToken = default)
        {
            if (PlayerLoopHelper.MainThreadId == Thread.CurrentThread.ManagedThreadId)
            {
                Cancel(cancellationToken);
                return;
            }

            UniTask.Post(() => Cancel(cancellationToken));
        }

        internal bool Complete()
        {
            m_CancellationRegistration.Dispose();
            return SetCompleted();
        }

        internal bool Cancel(CancellationToken cancellationToken = default)
        {
            m_Stop?.Invoke(this);
            m_CancellationRegistration.Dispose();
            return SetCanceled(cancellationToken);
        }

        internal bool Fail(Exception exception)
        {
            m_CancellationRegistration.Dispose();
            return SetFailed(exception);
        }

        protected override void OnPause()
        {
            m_Pause?.Invoke(this);
        }

        protected override void OnResume()
        {
            m_Resume?.Invoke(this);
        }

        protected override void OnStop()
        {
            m_CancellationRegistration.Dispose();
            m_Stop?.Invoke(this);
        }

        private sealed class CancellationRequest
        {
            public CancellationRequest(AudioPlayableHandle handle, CancellationToken cancellationToken)
            {
                Handle = handle;
                CancellationToken = cancellationToken;
            }

            public AudioPlayableHandle Handle { get; }

            public CancellationToken CancellationToken { get; }
        }
    }
}
