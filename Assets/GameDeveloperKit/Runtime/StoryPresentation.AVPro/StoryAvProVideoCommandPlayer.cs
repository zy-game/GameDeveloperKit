using System;
using System.Collections.Generic;
using System.IO;
using RenderHeads.Media.AVProVideo;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 使用 AVProVideo 播放 Story 视频命令。
    /// </summary>
    public sealed class StoryAvProVideoCommandPlayer : IStoryVideoCommandPlayer, IDisposable
    {
        private readonly List<StoryAvProVideoPlayback> m_Playbacks = new List<StoryAvProVideoPlayback>();
        private readonly Transform m_Parent;
        private readonly bool m_DontDestroyOnLoad;

        private bool m_Disposed;

        /// <summary>
        /// 初始化 AVProVideo Story 视频播放器。
        /// </summary>
        /// <param name="parent">播放器对象父节点。</param>
        /// <param name="dontDestroyOnLoad">没有父节点时是否跨场景保留。</param>
        public StoryAvProVideoCommandPlayer(Transform parent = null, bool dontDestroyOnLoad = true)
        {
            m_Parent = parent;
            m_DontDestroyOnLoad = dontDestroyOnLoad;
            PathResolver = ResolveMediaPath;
        }

        /// <summary>
        /// 视频路径解析器。
        /// </summary>
        public Func<string, string> PathResolver { get; set; }

        /// <summary>
        /// 当前活跃播放。
        /// </summary>
        public IReadOnlyList<StoryAvProVideoPlayback> ActivePlaybacks => new List<StoryAvProVideoPlayback>(m_Playbacks);

        /// <summary>
        /// 播放开始事件。
        /// </summary>
        public event Action<StoryAvProVideoPlayback> PlaybackStarted;

        /// <inheritdoc />
        public IStoryCommandHandle PlayVideo(StoryCommand command, StoryRuntimeContext context, string clipPath)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (string.IsNullOrWhiteSpace(clipPath))
            {
                throw new ArgumentException("Clip path cannot be empty.", nameof(clipPath));
            }

            EnsureNotDisposed();
            var handle = new StoryCommandHandle(command);
            StoryAvProVideoPlayback playback = null;
            try
            {
                var resolvedPath = (PathResolver ?? ResolveMediaPath)(clipPath);
                playback = new StoryAvProVideoPlayback(command, handle, clipPath, resolvedPath, m_Parent, m_DontDestroyOnLoad);
                playback.FirstFrameReady += OnPlaybackFirstFrameReady;
                playback.Terminated += OnPlaybackTerminated;
                m_Playbacks.Add(playback);
                playback.Play();
            }
            catch (Exception exception)
            {
                if (playback != null)
                {
                    playback.FirstFrameReady -= OnPlaybackFirstFrameReady;
                    playback.Terminated -= OnPlaybackTerminated;
                    m_Playbacks.Remove(playback);
                }

                playback?.Dispose();
                if (StoryMediaCommandUtility.IsTerminal(handle) is false)
                {
                    handle.Fail(exception);
                }
            }

            return handle;
        }

        private void OnPlaybackFirstFrameReady(StoryAvProVideoPlayback playback)
        {
            if (playback == null)
            {
                return;
            }

            PlaybackStarted?.Invoke(playback);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            var playbacks = new List<StoryAvProVideoPlayback>(m_Playbacks);
            for (var i = 0; i < playbacks.Count; i++)
            {
                playbacks[i]?.Dispose();
            }

            m_Playbacks.Clear();
        }

        private void OnPlaybackTerminated(StoryAvProVideoPlayback playback)
        {
            if (playback == null)
            {
                return;
            }

            playback.Terminated -= OnPlaybackTerminated;
            playback.FirstFrameReady -= OnPlaybackFirstFrameReady;
            m_Playbacks.Remove(playback);
            playback.Dispose();
        }

        private void EnsureNotDisposed()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(StoryAvProVideoCommandPlayer));
            }
        }

        private static string ResolveMediaPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            if (assetPath.IndexOf("://", StringComparison.Ordinal) >= 0)
            {
                return assetPath;
            }

            if (assetPath.Length > 4 &&
                assetPath[4] == ':' &&
                assetPath.StartsWith("guid", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var normalized = assetPath.Replace('\\', '/');
            if (normalized.StartsWith("Assets/StreamingAssets/", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(
                    Application.streamingAssetsPath,
                    normalized.Substring("Assets/StreamingAssets/".Length));
            }

            if (normalized.StartsWith("StreamingAssets/", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(
                    Application.streamingAssetsPath,
                    normalized.Substring("StreamingAssets/".Length));
            }

            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                return string.IsNullOrWhiteSpace(projectRoot)
                    ? Path.GetFullPath(assetPath)
                    : Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            }

            return Path.IsPathRooted(assetPath) ? assetPath : Path.GetFullPath(assetPath);
        }
    }

    /// <summary>
    /// AVProVideo Story 视频播放实例。
    /// </summary>
    public sealed class StoryAvProVideoPlayback : IDisposable
    {
        private readonly StoryCommandHandle m_Handle;
        private readonly Transform m_Parent;
        private readonly bool m_DontDestroyOnLoad;

        private GameObject m_GameObject;
        private MediaPlayer m_Player;
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
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            m_Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            ClipPath = clipPath;
            ResolvedPath = resolvedPath;
            m_Parent = parent;
            m_DontDestroyOnLoad = dontDestroyOnLoad;
            m_Handle.Canceled += OnHandleCanceledOrStopped;
            m_Handle.Stopped += OnHandleCanceledOrStopped;
        }

        /// <summary>
        /// 命令。
        /// </summary>
        public StoryCommand Command { get; }

        /// <summary>
        /// 原始视频路径。
        /// </summary>
        public string ClipPath { get; }

        /// <summary>
        /// AVPro 实际打开路径。
        /// </summary>
        public string ResolvedPath { get; }

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

        internal event Action<StoryAvProVideoPlayback> FirstFrameReady;

        internal event Action<StoryAvProVideoPlayback> Terminated;

        internal void Play()
        {
            if (string.IsNullOrWhiteSpace(ResolvedPath))
            {
                Fail(new GameException($"Story video path is invalid. command:{Command.CommandId} path:{ClipPath}"));
                return;
            }

            EnsurePlayer();
            var opened = m_Player.OpenMedia(MediaPathType.AbsolutePathOrURL, ResolvedPath, true);
            if (opened is false)
            {
                Fail(new GameException($"AVPro cannot open story video. command:{Command.CommandId} path:{ClipPath}"));
                return;
            }

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

        private void EnsurePlayer()
        {
            if (m_Player != null)
            {
                return;
            }

            m_GameObject = new GameObject($"StoryAvProVideo_{Command.CommandId}");
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
                case MediaPlayerEvent.EventType.Started:
                    IsPlaying = true;
                    break;
                case MediaPlayerEvent.EventType.FirstFrameReady:
                    IsPlaying = true;
                    MarkFirstFrameReady();
                    break;
                case MediaPlayerEvent.EventType.FinishedPlaying:
                    Complete();
                    break;
                case MediaPlayerEvent.EventType.Error:
                    Fail(new GameException($"AVPro story video error. command:{Command.CommandId} error:{errorCode}"));
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
            m_Handle.Canceled -= OnHandleCanceledOrStopped;
            m_Handle.Stopped -= OnHandleCanceledOrStopped;
            if (m_Player != null)
            {
                m_Player.Events.RemoveListener(OnMediaEvent);
                m_Player.Stop();
                m_Player.CloseMedia();
            }

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
    }
}
