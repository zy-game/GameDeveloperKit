using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.UI;

namespace GameDeveloperKit.Playable
{
    /// <summary>
    /// 通用视频播放器窗口。业务层通过继承并重载 virtual hook 接入自己的 UI。
    /// </summary>
    public abstract class VideoPlayerWindow : UIWindow
    {
        private VideoPlayableHandle m_Playback;

        public string Title { get; private set; }

        public bool ShowProgress { get; private set; }

        public bool AreControlsVisible { get; private set; } = true;

        public VideoPlayableHandle Playback => m_Playback;

        public double CurrentTimeSeconds => m_Playback?.CurrentTimeSeconds ?? 0d;

        public double DurationSeconds => m_Playback?.DurationSeconds ?? 0d;

        public bool IsPlaying => m_Playback?.Status == PlayableStatus.Playing;

        public float PlaybackRate => m_Playback?.PlaybackRate ?? 1f;

        public event Action BackRequested;

        public event Action<bool> ControlsVisibilityChanged;

        public event Action<VideoPlayableHandle> PlaybackOpened;

        public event Action<VideoPlayableHandle> PlaybackCompleted;

        public event Action<double, double> ProgressChanged;

        public virtual async UniTask<VideoPlayableHandle> PlayAsync(
            string url,
            string title = null,
            bool showProgress = true,
            VideoPlayableOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("Video URL cannot be empty.", nameof(url));
            }

            StopPlayback();
            Title = title ?? string.Empty;
            ShowProgress = showProgress;
            AreControlsVisible = true;
            m_Playback = await App.Playable.Video.PlayAsync(
                new VideoPlayableRequest(url, options),
                cancellationToken);
            OnVideoOpened(m_Playback);
            PlaybackOpened?.Invoke(m_Playback);
            ObserveCompletionAsync(m_Playback).Forget(UnityEngine.Debug.LogException);
            return m_Playback;
        }

        public virtual void TogglePlayback()
        {
            if (m_Playback == null)
            {
                return;
            }

            if (m_Playback.Status == PlayableStatus.Playing)
            {
                m_Playback.Pause();
            }
            else if (m_Playback.Status == PlayableStatus.Paused)
            {
                m_Playback.Resume();
            }

            OnPlaybackStateChanged(m_Playback);
        }

        public virtual void Seek(double timeSeconds)
        {
            if (m_Playback == null || !m_Playback.CanSeek)
            {
                return;
            }

            m_Playback.Seek(timeSeconds);
            OnProgressChanged(m_Playback.CurrentTimeSeconds, m_Playback.DurationSeconds);
        }

        public virtual void SetPlaybackSpeed(float rate)
        {
            if (m_Playback == null)
            {
                return;
            }

            m_Playback.SetPlaybackRate(rate);
            OnPlaybackSpeedChanged(rate);
        }

        public virtual UniTask SetQualityAsync(
            VideoQualitySelection selection,
            CancellationToken cancellationToken = default)
        {
            if (m_Playback == null)
            {
                return UniTask.CompletedTask;
            }

            return SetQualityInternalAsync(selection, cancellationToken);
        }

        public virtual void ToggleControls()
        {
            SetControlsVisible(!AreControlsVisible);
        }

        public virtual void SetControlsVisible(bool visible)
        {
            if (AreControlsVisible == visible)
            {
                return;
            }

            AreControlsVisible = visible;
            OnControlsVisibilityChanged(visible);
            ControlsVisibilityChanged?.Invoke(visible);
        }

        public virtual void RequestBack()
        {
            OnBackRequested();
            BackRequested?.Invoke();
        }

        /// <summary>
        /// 由业务层的窗口更新循环调用，用于刷新进度 UI。
        /// </summary>
        public virtual void Tick()
        {
            if (m_Playback == null || !ShowProgress)
            {
                return;
            }

            OnProgressChanged(m_Playback.CurrentTimeSeconds, m_Playback.DurationSeconds);
        }

        public override void Release()
        {
            StopPlayback();
            Title = null;
            base.Release();
        }

        protected virtual void OnVideoOpened(VideoPlayableHandle playback)
        {
        }

        protected virtual void OnPlaybackStateChanged(VideoPlayableHandle playback)
        {
        }

        protected virtual void OnPlaybackSpeedChanged(float rate)
        {
        }

        protected virtual void OnQualityChanged(VideoQualitySelection selection)
        {
        }

        protected virtual void OnProgressChanged(double currentSeconds, double durationSeconds)
        {
            ProgressChanged?.Invoke(currentSeconds, durationSeconds);
        }

        protected virtual void OnControlsVisibilityChanged(bool visible)
        {
        }

        protected virtual void OnBackRequested()
        {
        }

        protected virtual void OnPlaybackStopped()
        {
        }

        protected virtual void OnVideoPlaybackCompleted(VideoPlayableHandle playback)
        {
        }

        private async UniTask SetQualityInternalAsync(
            VideoQualitySelection selection,
            CancellationToken cancellationToken)
        {
            await m_Playback.SetQualityAsync(selection, cancellationToken);
            OnQualityChanged(selection);
        }

        private void StopPlayback()
        {
            var playback = m_Playback;
            m_Playback = null;
            if (playback == null)
            {
                return;
            }

            playback.Stop();
            playback.Dispose();
            OnPlaybackStopped();
        }

        private async UniTask ObserveCompletionAsync(VideoPlayableHandle playback)
        {
            try
            {
                await playback.WaitForCompletionAsync();
            }
            catch
            {
                return;
            }

            if (!ReferenceEquals(m_Playback, playback) || playback.Status != PlayableStatus.Completed)
            {
                return;
            }

            OnVideoPlaybackCompleted(playback);
            PlaybackCompleted?.Invoke(playback);
        }
    }
}
