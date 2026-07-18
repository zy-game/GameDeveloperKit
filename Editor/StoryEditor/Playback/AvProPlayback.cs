using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Playback;
using GameDeveloperKit.Story.Protocol;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Playback
{
    internal sealed class AvProPlayback : IDisposable
    {
        private VideoPlayable m_Video;
        private VideoPlayableHandle m_Handle;
        private string m_CurrentCommandId;
        private string m_CurrentSource;
        private string m_CurrentAssetPath;
        private string m_ErrorMessage;
        private bool m_IsFinished;
        private bool m_Disposed;

        public bool IsPlaying => m_Handle?.Status == PlayableStatus.Playing;

        public bool IsFinished => m_IsFinished || m_Handle?.Status == PlayableStatus.Completed;

        public string ErrorMessage => m_ErrorMessage;

        public string CurrentCommandId => m_CurrentCommandId;

        public string CurrentSource => m_CurrentSource;

        public string CurrentAssetPath => m_CurrentAssetPath;

        public string CurrentResolvedPath => m_Handle?.Path;

        public Texture CurrentTexture => m_Handle?.Texture;

        public bool CanSeek => m_Handle?.CanSeek == true;

        public bool CanSelectQuality => m_Handle?.CanSelectQuality == true;

        public VideoPlayableHandle Handle => m_Handle;

        public double DurationSeconds => m_Handle?.DurationSeconds ?? 0d;

        public double CurrentTimeSeconds => m_Handle?.CurrentTimeSeconds ?? 0d;

        public bool HasFirstFrame => m_Handle?.HasFirstFrame == true;

        public bool RequiresVerticalFlip => m_Handle?.RequiresVerticalFlip == true;

        public bool IsCurrent(string commandId, string source, string assetPath)
        {
            return string.Equals(m_CurrentCommandId, commandId, StringComparison.Ordinal) &&
                   string.Equals(m_CurrentSource, source, StringComparison.Ordinal) &&
                   string.Equals(m_CurrentAssetPath, assetPath, StringComparison.Ordinal);
        }

        public bool Play(global::GameDeveloperKit.Story.Model.Command command, string assetPath)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (VideoReferenceCodec.TryDeserializeCommand(command.Arguments, out var reference, out _, out var error) is false)
            {
                Stop();
                m_ErrorMessage = $"视频引用无效：{error}";
                return false;
            }

            var source = reference.Primary.Source == MediaSource.Cdn
                ? MediaCommandNames.VideoSourceCdn
                : MediaCommandNames.VideoSourceStreamingAssets;
            assetPath = reference.Primary.Location;
            if (IsCurrent(command.CommandId, source, assetPath) && string.IsNullOrWhiteSpace(m_ErrorMessage) && IsFinished is false)
            {
                return true;
            }

            Stop();
            m_CurrentCommandId = command.CommandId;
            m_CurrentSource = source;
            m_CurrentAssetPath = assetPath;
            try
            {
                var request = VideoRequestFactory.Create(
                    reference,
                    command.Arguments.GetBoolean("loop", false),
                    command.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument, false));
                m_Video = new VideoPlayable();
                m_Handle = m_Video.PlayAsync(request).GetAwaiter().GetResult();
                ObserveCompletionAsync(m_Handle).Forget(Debug.LogException);
                m_ErrorMessage = string.Empty;
                m_IsFinished = false;
                return true;
            }
            catch (Exception exception)
            {
                m_ErrorMessage = $"AVPro 无法打开视频：{exception.Message}";
                return false;
            }
        }

        public void Stop()
        {
            m_Handle?.Stop();
            m_Video?.Dispose();
            m_Handle = null;
            m_Video = null;
            m_IsFinished = false;
            m_ErrorMessage = string.Empty;
            m_CurrentCommandId = null;
            m_CurrentSource = null;
            m_CurrentAssetPath = null;
        }

        public void Seek(double timeSeconds)
        {
            if (CanSeek is false)
            {
                throw new GameException($"Story editor video cannot seek. command:{m_CurrentCommandId}");
            }

            m_Handle.Seek(timeSeconds);
            m_IsFinished = false;
        }

        public UniTask SetQualityAsync(VideoQualitySelection selection)
        {
            if (m_Handle == null)
            {
                throw new GameException("Story editor video is not active.");
            }

            return m_Handle.SetQualityAsync(selection);
        }

        public void Update()
        {
            if (m_Handle?.Status == PlayableStatus.Completed)
            {
                m_IsFinished = true;
            }
            else if (m_Handle?.Status == PlayableStatus.Failed)
            {
                m_ErrorMessage = m_Handle.Error?.Message;
            }
        }

        private async UniTask ObserveCompletionAsync(VideoPlayableHandle handle)
        {
            try
            {
                await handle.WaitForCompletionAsync();
                if (ReferenceEquals(m_Handle, handle))
                {
                    m_IsFinished = handle.Status == PlayableStatus.Completed;
                }
            }
            catch (Exception exception)
            {
                if (ReferenceEquals(m_Handle, handle))
                {
                    m_ErrorMessage = exception.Message;
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
            Stop();
        }
    }
}
