using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Download
{
    public class DownloadListHandler
    {
        private readonly List<DownloadHandler> m_Items;
        private UniTaskCompletionSource m_CompletionSource = new UniTaskCompletionSource();
        private bool m_Started;
        private bool m_CompletionSignaled;

        public IReadOnlyList<DownloadHandler> Items => m_Items;
        public DownloadStatus Status { get; private set; }
        public float Progress
        {
            get
            {
                if (m_Items.Count == 0)
                {
                    return 1f;
                }

                var progress = 0f;
                foreach (var item in m_Items)
                {
                    progress += item.Progress;
                }
                return progress / m_Items.Count;
            }
        }

        public event Action<DownloadListHandler> ProgressChanged;
        public event Action<DownloadListHandler> Completed;

        internal DownloadListHandler(List<DownloadHandler> items)
        {
            m_Items = items ?? throw new ArgumentNullException(nameof(items));
            Status = DownloadStatus.Waiting;
            foreach (var item in m_Items)
            {
                item.ProgressChanged += OnItemProgressChanged;
                item.Completed += OnItemCompleted;
                item.Failed += OnItemCompleted;
                item.Canceled += OnItemCompleted;
            }
        }

        public UniTask WaitCompletionAsync()
        {
            return m_CompletionSource.Task;
        }

        public async UniTask Pause()
        {
            foreach (var item in m_Items)
            {
                await item.Pause();
            }

            Status = DownloadStatus.Paused;
        }

        public async UniTask Resume()
        {
            if (Status != DownloadStatus.Paused && Status != DownloadStatus.Failed)
            {
                return;
            }

            Status = DownloadStatus.Downloading;
            ResetCompletion();
            foreach (var item in m_Items)
            {
                await item.Resume();
            }

            RunAsync().Forget();
        }

        public async UniTask Cancel()
        {
            foreach (var item in m_Items)
            {
                await item.Cancel();
            }

            Status = DownloadStatus.Canceled;
            SignalCompletion();
        }

        internal void Start()
        {
            if (m_Started)
            {
                return;
            }

            m_Started = true;
            RunAsync().Forget();
        }

        private async UniTaskVoid RunAsync()
        {
            Status = DownloadStatus.Downloading;
            foreach (var item in m_Items)
            {
                if (Status == DownloadStatus.Canceled || Status == DownloadStatus.Paused)
                {
                    if (Status == DownloadStatus.Canceled)
                    {
                        SignalCompletion();
                    }
                    return;
                }

                item.Start();
                await item.WaitCompletionAsync();
            }

            if (Status == DownloadStatus.Canceled || Status == DownloadStatus.Paused)
            {
                if (Status == DownloadStatus.Canceled)
                {
                    SignalCompletion();
                }
                return;
            }

            Status = DownloadStatus.Completed;
            ProgressChanged?.Invoke(this);
            Completed?.Invoke(this);
            SignalCompletion();
        }

        private void OnItemProgressChanged(DownloadHandler handler)
        {
            ProgressChanged?.Invoke(this);
        }

        private void OnItemCompleted(DownloadHandler handler)
        {
            ProgressChanged?.Invoke(this);
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
    }
}
