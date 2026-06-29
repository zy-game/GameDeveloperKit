using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

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

        private StoryAvProVideoPreloadQueue m_PreloadQueue;
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
        }

        /// <summary>
        /// 视频路径解析器。
        /// </summary>
        public Func<string, string> PathResolver { get; set; }

        /// <summary>
        /// AVPro 视频预热队列。
        /// </summary>
        public StoryAvProVideoPreloadQueue PreloadQueue
        {
            get => m_PreloadQueue;
            set
            {
                EnsureNotDisposed();
                if (ReferenceEquals(m_PreloadQueue, value))
                {
                    return;
                }

                m_PreloadQueue?.Dispose();
                m_PreloadQueue = value;
            }
        }

        /// <summary>
        /// 自动前瞻预热视频数量。
        /// </summary>
        public int PreloadLookAheadCount { get; set; }

        /// <summary>
        /// 当前活跃播放。
        /// </summary>
        public IReadOnlyList<StoryAvProVideoPlayback> ActivePlaybacks => new List<StoryAvProVideoPlayback>(m_Playbacks);

        /// <summary>
        /// 播放开始事件。
        /// </summary>
        public event Action<StoryAvProVideoPlayback> PlaybackStarted;

        /// <summary>
        /// 预热视频。
        /// </summary>
        /// <param name="command">剧情命令。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>预热句柄。</returns>
        public UniTask<StoryAvProVideoPreloadHandle> PreloadVideoAsync(
            StoryCommand command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var clipPath = command.Arguments.GetString(StoryMediaCommandNames.ClipArgument);
            if (string.IsNullOrWhiteSpace(clipPath))
            {
                throw new GameException($"Story video command clip is missing. command:{command.CommandId}");
            }

            EnsureNotDisposed();
            var source = GetVideoSource(command);
            var resolvedPath = TryResolveMediaPath(command, clipPath, out var exception);
            if (exception != null)
            {
                return UniTask.FromResult(StoryAvProVideoPreloadHandle.CreateFailed(command, source, clipPath, exception));
            }

            return EnsurePreloadQueue().PreloadResolvedAsync(
                command,
                source,
                clipPath,
                resolvedPath,
                cancellationToken);
        }

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
                var resolvedPath = ResolveMediaPath(command, clipPath);
                var source = GetVideoSource(command);
                if (m_PreloadQueue != null &&
                    m_PreloadQueue.TryAcquire(source, clipPath, out playback))
                {
                    playback.AttachHandle(command, handle);
                }
                else
                {
                    m_PreloadQueue?.Release(source, clipPath);
                    playback = new StoryAvProVideoPlayback(command, handle, clipPath, resolvedPath, m_Parent, m_DontDestroyOnLoad);
                }

                playback.FirstFrameReady += OnPlaybackFirstFrameReady;
                playback.Terminated += OnPlaybackTerminated;
                m_Playbacks.Add(playback);
                playback.Play();
                if (playback.HasFirstFrame)
                {
                    OnPlaybackFirstFrameReady(playback);
                }

                PreloadNextVideos(context, command);
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
            m_PreloadQueue?.Dispose();
            m_PreloadQueue = null;
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

        private string ResolveMediaPath(StoryCommand command, string clipPath)
        {
            var resolvedPath = TryResolveMediaPath(command, clipPath, out var exception);
            if (exception != null)
            {
                throw exception;
            }

            return resolvedPath;
        }

        private string TryResolveMediaPath(StoryCommand command, string clipPath, out GameException exception)
        {
            if (PathResolver != null)
            {
                var customResolvedPath = PathResolver(clipPath);
                if (string.IsNullOrWhiteSpace(customResolvedPath))
                {
                    exception = new GameException($"Story video path is invalid. command:{command.CommandId} path:{clipPath}");
                    return null;
                }

                exception = null;
                return customResolvedPath;
            }

            var source = GetVideoSource(command);
            if (StoryVideoPathResolver.TryResolve(source, clipPath, out var resolvedPath, out var errorMessage))
            {
                exception = null;
                return resolvedPath;
            }

            exception = new GameException(
                $"Story video path is invalid. command:{command.CommandId} source:{source} path:{clipPath} reason:{errorMessage}");
            return null;
        }

        private StoryAvProVideoPreloadQueue EnsurePreloadQueue()
        {
            if (m_PreloadQueue == null)
            {
                m_PreloadQueue = new StoryAvProVideoPreloadQueue(m_Parent, m_DontDestroyOnLoad);
            }

            return m_PreloadQueue;
        }

        private void PreloadNextVideos(StoryRuntimeContext context, StoryCommand currentCommand)
        {
            if (PreloadLookAheadCount <= 0 ||
                m_PreloadQueue == null ||
                context.Chapter?.Steps == null ||
                context.Step == null)
            {
                return;
            }

            var currentIndex = FindStepIndex(context.Chapter.Steps, context.Step.StepId);
            if (currentIndex < 0)
            {
                return;
            }

            var queued = 0;
            for (var i = currentIndex + 1; i < context.Chapter.Steps.Count && queued < PreloadLookAheadCount; i++)
            {
                var command = context.Chapter.Steps[i]?.Data?.Command;
                if (command == null ||
                    ReferenceEquals(command, currentCommand) ||
                    string.Equals(command.Name, StoryMediaCommandNames.PlayVideo, StringComparison.Ordinal) is false)
                {
                    continue;
                }

                var clipPath = command.Arguments.GetString(StoryMediaCommandNames.ClipArgument);
                if (string.IsNullOrWhiteSpace(clipPath))
                {
                    continue;
                }

                try
                {
                    PreloadVideoAsync(command).Forget();
                }
                catch (Exception)
                {
                    // Look-ahead preload is best-effort and must not fail the active video command.
                }

                queued++;
            }
        }

        private static int FindStepIndex(IReadOnlyList<StoryStep> steps, string stepId)
        {
            if (steps == null || string.IsNullOrWhiteSpace(stepId))
            {
                return -1;
            }

            for (var i = 0; i < steps.Count; i++)
            {
                if (string.Equals(steps[i]?.StepId, stepId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string GetVideoSource(StoryCommand command)
        {
            return command?.Arguments.GetString(StoryMediaCommandNames.VideoSourceArgument);
        }
    }
}
