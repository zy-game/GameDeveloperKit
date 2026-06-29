using System;
using GameDeveloperKit.Story;
using RenderHeads.Media.AVProVideo;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor
{
    internal sealed class StoryEditorAvProPlayback : IDisposable
    {
        private GameObject m_GameObject;
        private MediaPlayer m_Player;
        private string m_CurrentCommandId;
        private string m_CurrentSource;
        private string m_CurrentAssetPath;
        private string m_CurrentResolvedPath;
        private string m_ErrorMessage;
        private bool m_CurrentAllowsSeek;
        private bool m_IsFinished;
        private bool m_IsPlaying;
        private bool m_FirstFrameReady;
        private bool m_Disposed;

        public bool IsPlaying => m_IsPlaying;

        public bool IsFinished => m_IsFinished;

        public string ErrorMessage => m_ErrorMessage;

        public string CurrentCommandId => m_CurrentCommandId;

        public string CurrentSource => m_CurrentSource;

        public string CurrentAssetPath => m_CurrentAssetPath;

        public string CurrentResolvedPath => m_CurrentResolvedPath;

        public Texture CurrentTexture => m_Player?.TextureProducer?.GetTexture(0);

        public bool CanSeek
        {
            get
            {
                return m_CurrentAllowsSeek &&
                       m_Player?.Control != null &&
                       m_Player.Info != null &&
                       m_Player.Control.CanPlay() &&
                       IsValidDuration(DurationSeconds);
            }
        }

        public double DurationSeconds => m_Player?.Info?.GetDuration() ?? 0d;

        public double CurrentTimeSeconds => m_Player?.Control?.GetCurrentTime() ?? 0d;

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

        public bool RequiresVerticalFlip => m_Player?.TextureProducer?.RequiresVerticalFlip() ?? false;

        public bool IsCurrent(string commandId, string source, string assetPath)
        {
            return string.Equals(m_CurrentCommandId, commandId, StringComparison.Ordinal) &&
                   string.Equals(m_CurrentSource, source, StringComparison.Ordinal) &&
                   string.Equals(m_CurrentAssetPath, assetPath, StringComparison.Ordinal);
        }

        public bool Play(StoryCommand command, string assetPath)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var source = command.Arguments.GetString(StoryMediaCommandNames.VideoSourceArgument);
            if (IsCurrent(command.CommandId, source, assetPath) &&
                string.IsNullOrWhiteSpace(m_ErrorMessage) &&
                m_IsFinished is false)
            {
                return true;
            }

            Stop();
            m_CurrentCommandId = command.CommandId;
            m_CurrentSource = source;
            m_CurrentAssetPath = assetPath;
            m_CurrentAllowsSeek = HasTransitionSeekPolicy(command) &&
                                  command.Arguments.GetBoolean("loop", false) is false;
            if (StoryVideoPathResolver.TryResolve(source, assetPath, out m_CurrentResolvedPath, out var errorMessage) is false)
            {
                m_ErrorMessage = $"视频路径无效：{errorMessage}";
                return false;
            }

            EnsurePlayer();
            m_ErrorMessage = string.Empty;
            m_IsFinished = false;
            m_IsPlaying = false;
            m_FirstFrameReady = false;

            var opened = m_Player.OpenMedia(MediaPathType.AbsolutePathOrURL, m_CurrentResolvedPath, true);
            if (opened is false)
            {
                m_ErrorMessage = $"AVPro 无法打开视频：{assetPath}";
                return false;
            }

            m_IsPlaying = true;
            return true;
        }

        public void Stop()
        {
            m_IsPlaying = false;
            m_IsFinished = false;
            m_ErrorMessage = string.Empty;
            m_FirstFrameReady = false;
            m_CurrentCommandId = null;
            m_CurrentSource = null;
            m_CurrentAssetPath = null;
            m_CurrentResolvedPath = null;
            m_CurrentAllowsSeek = false;

            if (m_Player != null)
            {
                m_Player.Stop();
                m_Player.CloseMedia();
            }
        }

        public void Seek(double timeSeconds)
        {
            if (CanSeek is false)
            {
                throw new GameException($"Story editor video cannot seek. command:{m_CurrentCommandId}");
            }

            m_Player.Control.Seek(ClampTime(timeSeconds, DurationSeconds));
            m_IsFinished = false;
        }

        public void Update()
        {
            if (m_Player == null)
            {
                return;
            }

#if UNITY_EDITOR
            m_Player.EditorUpdate();
#endif

            if (m_Player.Control != null)
            {
                m_IsPlaying = m_Player.Control.IsPlaying();
                if (m_Player.Control.IsFinished())
                {
                    m_IsFinished = true;
                    m_IsPlaying = false;
                }
            }
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            if (m_Player != null)
            {
                m_Player.Events.RemoveListener(OnMediaEvent);
                m_Player.CloseMedia();
            }

            if (m_GameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(m_GameObject);
            }

            m_Player = null;
            m_GameObject = null;
        }

        private void EnsurePlayer()
        {
            if (m_Player != null)
            {
                return;
            }

            m_GameObject = new GameObject("StoryEditorAvProPlayback")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            m_Player = m_GameObject.AddComponent<MediaPlayer>();
            m_Player.hideFlags = HideFlags.HideAndDontSave;
            m_Player.AutoOpen = false;
            m_Player.AutoStart = false;
            m_Player.Loop = false;
            m_Player.Events.AddListener(OnMediaEvent);
        }

        private void OnMediaEvent(MediaPlayer player, MediaPlayerEvent.EventType eventType, ErrorCode errorCode)
        {
            switch (eventType)
            {
                case MediaPlayerEvent.EventType.Started:
                    m_IsPlaying = true;
                    break;
                case MediaPlayerEvent.EventType.FirstFrameReady:
                    m_IsPlaying = true;
                    m_FirstFrameReady = true;
                    break;
                case MediaPlayerEvent.EventType.FinishedPlaying:
                    m_IsFinished = true;
                    m_IsPlaying = false;
                    break;
                case MediaPlayerEvent.EventType.Error:
                    m_ErrorMessage = $"AVPro 播放错误：{errorCode}";
                    m_IsPlaying = false;
                    break;
            }
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
