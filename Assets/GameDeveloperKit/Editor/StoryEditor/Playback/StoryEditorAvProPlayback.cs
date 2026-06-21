using System;
using System.IO;
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
        private string m_CurrentAssetPath;
        private string m_CurrentResolvedPath;
        private string m_ErrorMessage;
        private bool m_IsFinished;
        private bool m_IsPlaying;
        private bool m_FirstFrameReady;
        private bool m_Disposed;

        public bool IsPlaying => m_IsPlaying;

        public bool IsFinished => m_IsFinished;

        public string ErrorMessage => m_ErrorMessage;

        public string CurrentCommandId => m_CurrentCommandId;

        public string CurrentAssetPath => m_CurrentAssetPath;

        public string CurrentResolvedPath => m_CurrentResolvedPath;

        public Texture CurrentTexture => m_Player?.TextureProducer?.GetTexture(0);

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

        public bool IsCurrent(string commandId, string assetPath)
        {
            return string.Equals(m_CurrentCommandId, commandId, StringComparison.Ordinal) &&
                   string.Equals(m_CurrentAssetPath, assetPath, StringComparison.Ordinal);
        }

        public bool Play(StoryCommand command, string assetPath)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (IsCurrent(command.CommandId, assetPath) &&
                string.IsNullOrWhiteSpace(m_ErrorMessage) &&
                m_IsFinished is false)
            {
                return true;
            }

            Stop();
            m_CurrentCommandId = command.CommandId;
            m_CurrentAssetPath = assetPath;
            m_CurrentResolvedPath = ResolveMediaPath(assetPath);
            if (string.IsNullOrWhiteSpace(m_CurrentResolvedPath))
            {
                m_ErrorMessage = "视频路径为空。";
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
            m_CurrentAssetPath = null;
            m_CurrentResolvedPath = null;

            if (m_Player != null)
            {
                m_Player.Stop();
                m_Player.CloseMedia();
            }
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

            if (IsGuidReference(assetPath))
            {
                return null;
            }

            if (assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                assetPath.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase))
            {
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                return string.IsNullOrWhiteSpace(projectRoot)
                    ? Path.GetFullPath(assetPath)
                    : Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            }

            return Path.IsPathRooted(assetPath) ? assetPath : null;
        }

        private static bool IsGuidReference(string assetPath)
        {
            return assetPath.Length > 4 &&
                   assetPath[4] == ':' &&
                   assetPath.StartsWith("guid", StringComparison.OrdinalIgnoreCase);
        }
    }
}
