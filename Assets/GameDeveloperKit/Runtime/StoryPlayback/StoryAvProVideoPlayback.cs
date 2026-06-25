using System;
using RenderHeads.Media.AVProVideo;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// AVProVideo Story 视频播放实例。
    /// </summary>
    public sealed class StoryAvProVideoPlayback : IDisposable
    {
        private StoryCommandHandle m_Handle;
        private readonly Transform m_Parent;
        private readonly bool m_DontDestroyOnLoad;

        private GameObject m_GameObject;
        private MediaPlayer m_Player;
        private bool m_MediaOpened;
        private bool m_Preloading;
        private bool m_Terminated;
        private bool m_Cleaned;
        private bool m_FirstFrameReady;
        private bool m_Disposed;

        internal StoryAvProVideoPlayback(
            StoryCommand command,
            StoryCommandHandle handle,
            string clipPath,
            string resolvedPath,
            Transform parent,
            bool dontDestroyOnLoad)
            : this(command, clipPath, resolvedPath, parent, dontDestroyOnLoad)
        {
            AttachHandle(command, handle);
        }

        private StoryAvProVideoPlayback(
            StoryCommand command,
            string clipPath,
            string resolvedPath,
            Transform parent,
            bool dontDestroyOnLoad)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            ClipPath = clipPath;
            ResolvedPath = resolvedPath;
            m_Parent = parent;
            m_DontDestroyOnLoad = dontDestroyOnLoad;
        }

        /// <summary>
        /// 命令。
        /// </summary>
        public StoryCommand Command { get; private set; }

        /// <summary>
        /// 原始视频路径。
        /// </summary>
        public string ClipPath { get; private set; }

        /// <summary>
        /// AVPro 实际打开路径。
        /// </summary>
        public string ResolvedPath { get; private set; }

        /// <summary>
        /// AVPro 媒体播放器。
        /// </summary>
        public MediaPlayer Player => m_Player;

        /// <summary>
        /// 当前视频纹理。
        /// </summary>
        public Texture CurrentTexture => m_Player?.TextureProducer?.GetTexture(0);

        /// <summary>
        /// 当前纹理是否已经有可显示的首帧。
        /// </summary>
        public bool HasFirstFrame
        {
            get
            {
                var textureProducer = m_Player?.TextureProducer;
                if (textureProducer == null || textureProducer.GetTexture(0) == null)
                {
                    return false;
                }

                if (m_FirstFrameReady)
                {
                    return true;
                }

                return textureProducer.SupportsTextureFrameCount() is false ||
                       textureProducer.GetTextureFrameCount() > 0;
            }
        }

        /// <summary>
        /// AVPro 当前输出纹理是否需要垂直翻转。
        /// </summary>
        public bool RequiresVerticalFlip => m_Player?.TextureProducer?.RequiresVerticalFlip() ?? false;

        /// <summary>
        /// 是否正在播放。
        /// </summary>
        public bool IsPlaying { get; private set; }

        /// <summary>
        /// 是否已暂停。
        /// </summary>
        public bool IsPaused => m_Player?.Control?.IsPaused() ?? false;

        /// <summary>
        /// 当前视频是否应显示 seek 控件。
        /// </summary>
        public bool CanShowSeekControls
        {
            get
            {
                return m_Terminated is false &&
                       m_Cleaned is false &&
                       HasTransitionSeekPolicy(Command) &&
                       Command.Arguments.GetBoolean("loop", false) is false &&
                       m_Player?.Control != null;
            }
        }

        /// <summary>
        /// 当前视频是否允许 seek。
        /// </summary>
        public bool CanSeek
        {
            get
            {
                return CanShowSeekControls &&
                       m_Player.Info != null &&
                       m_Player.Control.CanPlay() &&
                       IsValidDuration(DurationSeconds);
            }
        }

        /// <summary>
        /// 当前视频是否允许暂停 / 继续。
        /// </summary>
        public bool CanPause
        {
            get
            {
                return CanShowSeekControls &&
                       m_Player.Control.CanPlay();
            }
        }

        /// <summary>
        /// 当前视频总时长，单位秒。
        /// </summary>
        public double DurationSeconds => m_Player?.Info?.GetDuration() ?? 0d;

        /// <summary>
        /// 当前视频播放时间，单位秒。
        /// </summary>
        public double CurrentTimeSeconds => m_Player?.Control?.GetCurrentTime() ?? 0d;

        internal event Action<StoryAvProVideoPlayback> ReadyToPlay;

        internal event Action<StoryAvProVideoPlayback> FirstFrameReady;

        internal event Action<StoryAvProVideoPlayback, Exception> PreloadFailed;

        internal event Action<StoryAvProVideoPlayback> Terminated;

        internal static StoryAvProVideoPlayback CreatePreloaded(
            StoryCommand command,
            string clipPath,
            string resolvedPath,
            Transform parent,
            bool dontDestroyOnLoad)
        {
            return new StoryAvProVideoPlayback(command, clipPath, resolvedPath, parent, dontDestroyOnLoad);
        }

        internal void AttachHandle(StoryCommand command, StoryCommandHandle handle)
        {
            if (m_Handle != null)
            {
                throw new GameException($"Story video playback is already attached. command:{Command.CommandId}");
            }

            Command = command ?? throw new ArgumentNullException(nameof(command));
            m_Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            m_Handle.Canceled += OnHandleCanceledOrStopped;
            m_Handle.Stopped += OnHandleCanceledOrStopped;
            if (m_Player != null)
            {
                m_Player.Loop = Command.Arguments.GetBoolean("loop", false);
            }
        }

        internal bool Preload()
        {
            if (string.IsNullOrWhiteSpace(ResolvedPath))
            {
                ReportPreloadFailed(new GameException($"Story video path is invalid. command:{Command.CommandId} path:{ClipPath}"));
                return false;
            }

            EnsurePlayer(true);
            m_Preloading = true;
            m_Player.AudioMuted = true;
            var opened = m_Player.OpenMedia(MediaPathType.AbsolutePathOrURL, ResolvedPath, false);
            if (opened is false)
            {
                ReportPreloadFailed(new GameException($"AVPro cannot preload story video. command:{Command.CommandId} path:{ClipPath}"));
                return false;
            }

            m_MediaOpened = true;
            return true;
        }

        internal void Play()
        {
            if (string.IsNullOrWhiteSpace(ResolvedPath))
            {
                Fail(new GameException($"Story video path is invalid. command:{Command.CommandId} path:{ClipPath}"));
                return;
            }

            EnsurePlayer(false);
            if (m_MediaOpened)
            {
                PlayPrepared();
                return;
            }

            m_Player.AudioMuted = false;
            m_Player.Loop = Command.Arguments.GetBoolean("loop", false);
            var opened = m_Player.OpenMedia(MediaPathType.AbsolutePathOrURL, ResolvedPath, true);
            if (opened is false)
            {
                Fail(new GameException($"AVPro cannot open story video. command:{Command.CommandId} path:{ClipPath}"));
                return;
            }

            m_MediaOpened = true;
            IsPlaying = true;
        }

        /// <summary>
        /// 跳转到指定视频时间。
        /// </summary>
        /// <param name="timeSeconds">视频时间，单位秒。</param>
        public void Seek(double timeSeconds)
        {
            if (CanSeek is false)
            {
                throw new GameException($"Story video cannot seek. command:{Command.CommandId}");
            }

            m_Player.Control.Seek(ClampTime(timeSeconds, DurationSeconds));
        }

        /// <summary>
        /// 暂停当前可 seek 视频。
        /// </summary>
        public void Pause()
        {
            if (CanPause is false)
            {
                throw new GameException($"Story video cannot pause. command:{Command.CommandId}");
            }

            m_Player.Pause();
            IsPlaying = false;
        }

        /// <summary>
        /// 继续播放当前可 seek 视频。
        /// </summary>
        public void Resume()
        {
            if (CanPause is false)
            {
                throw new GameException($"Story video cannot resume. command:{Command.CommandId}");
            }

            m_Player.Play();
            IsPlaying = true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            Cleanup();
            DestroyPlayerObject();
        }

        private void PlayPrepared()
        {
            m_Preloading = false;
            if (m_Player == null)
            {
                return;
            }

            m_Player.AudioMuted = false;
            m_Player.Loop = Command.Arguments.GetBoolean("loop", false);
            m_Player.Play();
            IsPlaying = true;
        }

        private void EnsurePlayer(bool preload)
        {
            if (m_Player != null)
            {
                return;
            }

            var prefix = preload ? "StoryAvProVideoPreload" : "StoryAvProVideo";
            m_GameObject = new GameObject($"{prefix}_{Command.CommandId}");
            if (m_Parent != null)
            {
                m_GameObject.transform.SetParent(m_Parent, false);
            }
            else if (m_DontDestroyOnLoad)
            {
                Object.DontDestroyOnLoad(m_GameObject);
            }

            m_Player = m_GameObject.AddComponent<MediaPlayer>();
            m_Player.AutoOpen = false;
            m_Player.AutoStart = false;
            m_Player.Loop = Command.Arguments.GetBoolean("loop", false);
            m_Player.Events.AddListener(OnMediaEvent);
        }

        private void OnMediaEvent(MediaPlayer player, MediaPlayerEvent.EventType eventType, ErrorCode errorCode)
        {
            switch (eventType)
            {
                case MediaPlayerEvent.EventType.ReadyToPlay:
                    ReadyToPlay?.Invoke(this);
                    if (m_Preloading && m_Player != null)
                    {
                        m_Player.Play();
                        IsPlaying = true;
                    }

                    break;
                case MediaPlayerEvent.EventType.Started:
                    IsPlaying = true;
                    break;
                case MediaPlayerEvent.EventType.FirstFrameReady:
                    MarkFirstFrameReady();
                    if (m_Preloading && m_Player != null)
                    {
                        m_Player.Pause();
                        IsPlaying = false;
                    }
                    else
                    {
                        IsPlaying = true;
                    }

                    break;
                case MediaPlayerEvent.EventType.FinishedPlaying:
                    if (m_Handle != null)
                    {
                        Complete();
                    }

                    break;
                case MediaPlayerEvent.EventType.Error:
                    var exception = new GameException($"AVPro story video error. command:{Command.CommandId} error:{errorCode}");
                    if (m_Handle != null)
                    {
                        Fail(exception);
                    }
                    else
                    {
                        ReportPreloadFailed(exception);
                    }

                    break;
            }
        }

        private void MarkFirstFrameReady()
        {
            if (m_FirstFrameReady)
            {
                return;
            }

            m_FirstFrameReady = true;
            FirstFrameReady?.Invoke(this);
        }

        private void ReportPreloadFailed(Exception exception)
        {
            m_Preloading = false;
            IsPlaying = false;
            PreloadFailed?.Invoke(this, exception);
        }

        private void OnHandleCanceledOrStopped(IStoryCommandHandle handle)
        {
            Terminate();
        }

        private void Complete()
        {
            IsPlaying = false;
            if (StoryMediaCommandUtility.IsTerminal(m_Handle) is false)
            {
                m_Handle.Complete(StoryMediaCommandUtility.GetCompletedOutcome(Command));
            }

            Terminate();
        }

        private void Fail(Exception exception)
        {
            IsPlaying = false;
            if (StoryMediaCommandUtility.IsTerminal(m_Handle) is false)
            {
                m_Handle.Fail(exception);
            }

            Terminate();
        }

        private void Terminate()
        {
            if (m_Terminated)
            {
                return;
            }

            m_Terminated = true;
            Cleanup();
            Terminated?.Invoke(this);
        }

        private void Cleanup()
        {
            if (m_Cleaned)
            {
                return;
            }

            m_Cleaned = true;
            if (m_Handle != null)
            {
                m_Handle.Canceled -= OnHandleCanceledOrStopped;
                m_Handle.Stopped -= OnHandleCanceledOrStopped;
            }

            if (m_Player != null)
            {
                m_Player.Events.RemoveListener(OnMediaEvent);
                m_Player.Stop();
                m_Player.CloseMedia();
            }

            m_MediaOpened = false;
            m_Preloading = false;
            IsPlaying = false;
        }

        private void DestroyPlayerObject()
        {
            if (m_GameObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(m_GameObject);
            }
            else
            {
                Object.DestroyImmediate(m_GameObject);
            }

            m_Player = null;
            m_GameObject = null;
        }

        private static bool HasTransitionSeekPolicy(StoryCommand command)
        {
            return string.Equals(
                command?.Arguments.GetString(StoryMediaCommandNames.VideoSeekPolicyArgument),
                StoryMediaCommandNames.VideoSeekPolicyTransition,
                StringComparison.Ordinal);
        }

        private static double ClampTime(double timeSeconds, double durationSeconds)
        {
            if (double.IsNaN(timeSeconds) || double.IsInfinity(timeSeconds))
            {
                return 0d;
            }

            if (timeSeconds < 0d)
            {
                return 0d;
            }

            return timeSeconds > durationSeconds ? durationSeconds : timeSeconds;
        }

        private static bool IsValidDuration(double durationSeconds)
        {
            return durationSeconds > 0d &&
                   double.IsNaN(durationSeconds) is false &&
                   double.IsInfinity(durationSeconds) is false;
        }
    }
}
