using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Playable
{
    public abstract class PlayableHandle : IDisposable
    {
        private readonly UniTaskCompletionSource m_Completion = new UniTaskCompletionSource();
        private bool m_Disposed;

        public PlayableStatus Status { get; private set; } = PlayableStatus.Preparing;

        public Exception Error { get; private set; }

        public UniTask WaitForCompletionAsync()
        {
            return m_Completion.Task;
        }

        public void Pause()
        {
            if (Status != PlayableStatus.Playing)
            {
                return;
            }

            try
            {
                OnPause();
                Status = PlayableStatus.Paused;
            }
            catch (Exception exception)
            {
                SetFailed(exception);
                throw;
            }
        }

        public void Resume()
        {
            if (Status != PlayableStatus.Paused)
            {
                return;
            }

            try
            {
                OnResume();
                Status = PlayableStatus.Playing;
            }
            catch (Exception exception)
            {
                SetFailed(exception);
                throw;
            }
        }

        public void Stop()
        {
            if (IsTerminal(Status))
            {
                return;
            }

            try
            {
                OnStop();
                SetTerminal(PlayableStatus.Stopped, null, CancellationToken.None);
            }
            catch (Exception exception)
            {
                SetFailed(exception);
                throw;
            }
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            try
            {
                Stop();
            }
            finally
            {
                OnDispose();
            }
        }

        protected void SetPlaying()
        {
            if (Status != PlayableStatus.Preparing)
            {
                throw new InvalidOperationException($"Playable cannot enter Playing from {Status}.");
            }

            Status = PlayableStatus.Playing;
        }

        protected bool SetCompleted()
        {
            return SetTerminal(PlayableStatus.Completed, null, CancellationToken.None);
        }

        protected bool SetCanceled(CancellationToken cancellationToken = default)
        {
            return SetTerminal(PlayableStatus.Canceled, null, cancellationToken);
        }

        protected bool SetFailed(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            return SetTerminal(PlayableStatus.Failed, exception, CancellationToken.None);
        }

        protected abstract void OnPause();

        protected abstract void OnResume();

        protected abstract void OnStop();

        protected virtual void OnDispose()
        {
        }

        private bool SetTerminal(PlayableStatus status, Exception exception, CancellationToken cancellationToken)
        {
            if (IsTerminal(Status))
            {
                return false;
            }

            Status = status;
            Error = exception;
            if (status == PlayableStatus.Failed)
            {
                m_Completion.TrySetException(exception);
            }
            else if (status == PlayableStatus.Canceled)
            {
                m_Completion.TrySetCanceled(cancellationToken);
            }
            else
            {
                m_Completion.TrySetResult();
            }

            return true;
        }

        private static bool IsTerminal(PlayableStatus status)
        {
            return status is PlayableStatus.Completed or
                PlayableStatus.Stopped or
                PlayableStatus.Canceled or
                PlayableStatus.Failed;
        }
    }
}
