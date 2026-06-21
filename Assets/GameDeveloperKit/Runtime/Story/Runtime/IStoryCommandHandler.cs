using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情命令执行句柄。
    /// </summary>
    public interface IStoryCommandHandle
    {
        /// <summary>
        /// 命令。
        /// </summary>
        StoryCommand Command { get; }

        /// <summary>
        /// 是否已完成。
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// 是否已取消。
        /// </summary>
        bool IsCanceled { get; }

        /// <summary>
        /// 是否已停止。
        /// </summary>
        bool IsStopped { get; }

        /// <summary>
        /// 执行错误。
        /// </summary>
        Exception Error { get; }

        /// <summary>
        /// 完成结果 ID。
        /// </summary>
        string OutcomeId { get; }

        /// <summary>
        /// 完成事件。
        /// </summary>
        event Action<IStoryCommandHandle> Completed;

        /// <summary>
        /// 取消事件。
        /// </summary>
        event Action<IStoryCommandHandle> Canceled;

        /// <summary>
        /// 停止事件。
        /// </summary>
        event Action<IStoryCommandHandle> Stopped;

        /// <summary>
        /// 失败事件。
        /// </summary>
        event Action<IStoryCommandHandle> Failed;

        /// <summary>
        /// 标记命令完成。
        /// </summary>
        /// <param name="outcomeId">结果 ID。</param>
        void Complete(string outcomeId = null);

        /// <summary>
        /// 取消命令。
        /// </summary>
        void Cancel();

        /// <summary>
        /// 停止命令。
        /// </summary>
        void Stop();

        /// <summary>
        /// 标记命令失败。
        /// </summary>
        /// <param name="exception">错误。</param>
        void Fail(Exception exception);
    }

    /// <summary>
    /// 默认剧情命令执行句柄。
    /// </summary>
    public sealed class StoryCommandHandle : IStoryCommandHandle
    {
        /// <summary>
        /// 初始化剧情命令执行句柄。
        /// </summary>
        /// <param name="command">命令。</param>
        public StoryCommandHandle(StoryCommand command)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
        }

        /// <inheritdoc />
        public StoryCommand Command { get; }

        /// <inheritdoc />
        public bool IsCompleted { get; private set; }

        /// <inheritdoc />
        public bool IsCanceled { get; private set; }

        /// <inheritdoc />
        public bool IsStopped { get; private set; }

        /// <inheritdoc />
        public Exception Error { get; private set; }

        /// <inheritdoc />
        public string OutcomeId { get; private set; }

        /// <inheritdoc />
        public event Action<IStoryCommandHandle> Completed;

        /// <inheritdoc />
        public event Action<IStoryCommandHandle> Canceled;

        /// <inheritdoc />
        public event Action<IStoryCommandHandle> Stopped;

        /// <inheritdoc />
        public event Action<IStoryCommandHandle> Failed;

        /// <inheritdoc />
        public void Complete(string outcomeId = null)
        {
            if (IsTerminal)
            {
                return;
            }

            OutcomeId = outcomeId;
            IsCompleted = true;
            Completed?.Invoke(this);
        }

        /// <inheritdoc />
        public void Cancel()
        {
            if (IsTerminal)
            {
                return;
            }

            IsCanceled = true;
            Canceled?.Invoke(this);
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (IsTerminal)
            {
                return;
            }

            IsStopped = true;
            Stopped?.Invoke(this);
        }

        /// <inheritdoc />
        public void Fail(Exception exception)
        {
            if (IsTerminal)
            {
                return;
            }

            Error = exception ?? throw new ArgumentNullException(nameof(exception));
            Failed?.Invoke(this);
        }

        private bool IsTerminal => IsCompleted || IsCanceled || IsStopped || Error != null;
    }

    /// <summary>
    /// 剧情命令执行器。
    /// </summary>
    public interface IStoryCommandHandler
    {
        /// <summary>
        /// 判断是否能执行指定命令。
        /// </summary>
        /// <param name="command">命令。</param>
        /// <returns>能执行时返回 true。</returns>
        bool CanHandle(StoryCommand command);

        /// <summary>
        /// 执行命令。
        /// </summary>
        /// <param name="command">命令。</param>
        /// <param name="context">运行上下文。</param>
        /// <returns>命令执行句柄。</returns>
        IStoryCommandHandle Execute(StoryCommand command, StoryRuntimeContext context);
    }

    /// <summary>
    /// Story 内置媒体命令名。
    /// </summary>
    public static class StoryMediaCommandNames
    {
        /// <summary>
        /// 播放视频。
        /// </summary>
        public const string PlayVideo = "play_video";

        /// <summary>
        /// 显示图片。
        /// </summary>
        public const string ShowImage = "show_image";

        /// <summary>
        /// 播放音频。
        /// </summary>
        public const string PlayAudio = "play_audio";

        /// <summary>
        /// 媒体片段参数。
        /// </summary>
        public const string ClipArgument = "clip";

        /// <summary>
        /// 图片参数。
        /// </summary>
        public const string ImageArgument = "image";

        /// <summary>
        /// 默认完成结果。
        /// </summary>
        public const string CompletedOutcome = "completed";
    }

    /// <summary>
    /// Story 媒体命令工具。
    /// </summary>
    public static class StoryMediaCommandUtility
    {
        /// <summary>
        /// 判断命令句柄是否已经进入终态。
        /// </summary>
        /// <param name="handle">命令句柄。</param>
        /// <returns>已结束时返回 true。</returns>
        public static bool IsTerminal(IStoryCommandHandle handle)
        {
            return handle == null ||
                   handle.IsCompleted ||
                   handle.IsCanceled ||
                   handle.IsStopped ||
                   handle.Error != null;
        }

        /// <summary>
        /// 获取默认完成结果。
        /// </summary>
        /// <param name="command">命令。</param>
        /// <returns>命令声明 completed 端口时返回 completed，否则返回 null。</returns>
        public static string GetCompletedOutcome(StoryCommand command)
        {
            if (command?.OutcomePorts == null)
            {
                return null;
            }

            for (var i = 0; i < command.OutcomePorts.Count; i++)
            {
                if (string.Equals(command.OutcomePorts[i], StoryMediaCommandNames.CompletedOutcome, StringComparison.Ordinal))
                {
                    return StoryMediaCommandNames.CompletedOutcome;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 剧情视频命令播放器。
    /// </summary>
    public interface IStoryVideoCommandPlayer
    {
        /// <summary>
        /// 播放视频。
        /// </summary>
        /// <param name="command">命令。</param>
        /// <param name="context">运行上下文。</param>
        /// <param name="clipPath">视频路径。</param>
        /// <returns>命令句柄。</returns>
        IStoryCommandHandle PlayVideo(StoryCommand command, StoryRuntimeContext context, string clipPath);
    }

    /// <summary>
    /// 剧情图片命令播放器。
    /// </summary>
    public interface IStoryImageCommandPlayer
    {
        /// <summary>
        /// 显示图片。
        /// </summary>
        /// <param name="command">命令。</param>
        /// <param name="context">运行上下文。</param>
        /// <param name="imagePath">图片路径。</param>
        /// <returns>命令句柄。</returns>
        IStoryCommandHandle ShowImage(StoryCommand command, StoryRuntimeContext context, string imagePath);
    }

    /// <summary>
    /// 剧情音频命令播放器。
    /// </summary>
    public interface IStoryAudioCommandPlayer
    {
        /// <summary>
        /// 播放音频。
        /// </summary>
        /// <param name="command">命令。</param>
        /// <param name="context">运行上下文。</param>
        /// <param name="clipPath">音频路径。</param>
        /// <returns>命令句柄。</returns>
        IStoryCommandHandle PlayAudio(StoryCommand command, StoryRuntimeContext context, string clipPath);
    }

    /// <summary>
    /// 默认媒体命令执行器，把 Story 媒体命令转发到宿主播放器。
    /// </summary>
    public sealed class StoryMediaCommandHandler : IStoryCommandHandler
    {
        private readonly IStoryVideoCommandPlayer m_VideoCommandPlayer;
        private readonly IStoryImageCommandPlayer m_ImagePlayer;
        private readonly IStoryAudioCommandPlayer m_AudioPlayer;

        /// <summary>
        /// 初始化媒体命令执行器。
        /// </summary>
        /// <param name="videoPlayer">视频播放器。</param>
        /// <param name="imagePlayer">图片播放器。</param>
        /// <param name="audioPlayer">音频播放器。</param>
        public StoryMediaCommandHandler(
            IStoryVideoCommandPlayer videoPlayer = null,
            IStoryImageCommandPlayer imagePlayer = null,
            IStoryAudioCommandPlayer audioPlayer = null)
        {
            m_VideoCommandPlayer = videoPlayer;
            m_ImagePlayer = imagePlayer;
            m_AudioPlayer = audioPlayer;
        }

        /// <inheritdoc />
        public bool CanHandle(StoryCommand command)
        {
            if (command == null)
            {
                return false;
            }

            switch (command.Name)
            {
                case StoryMediaCommandNames.PlayVideo:
                    return m_VideoCommandPlayer != null;
                case StoryMediaCommandNames.ShowImage:
                    return m_ImagePlayer != null;
                case StoryMediaCommandNames.PlayAudio:
                    return m_AudioPlayer != null;
                default:
                    return false;
            }
        }

        /// <inheritdoc />
        public IStoryCommandHandle Execute(StoryCommand command, StoryRuntimeContext context)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            switch (command.Name)
            {
                case StoryMediaCommandNames.PlayVideo:
                    return RequireHandle(
                        command,
                        RequirePlayer(m_VideoCommandPlayer, command).PlayVideo(
                            command,
                            context,
                            GetRequiredArgument(command, StoryMediaCommandNames.ClipArgument)));
                case StoryMediaCommandNames.ShowImage:
                    return RequireHandle(
                        command,
                        RequirePlayer(m_ImagePlayer, command).ShowImage(
                            command,
                            context,
                            GetRequiredArgument(command, StoryMediaCommandNames.ImageArgument)));
                case StoryMediaCommandNames.PlayAudio:
                    return RequireHandle(
                        command,
                        RequirePlayer(m_AudioPlayer, command).PlayAudio(
                            command,
                            context,
                            GetRequiredArgument(command, StoryMediaCommandNames.ClipArgument)));
                default:
                    throw new GameException($"Story media command is not supported. command:{command.Name}");
            }
        }

        private static T RequirePlayer<T>(T player, StoryCommand command) where T : class
        {
            if (player == null)
            {
                throw new GameException($"Story media command player is not registered. command:{command.CommandId} name:{command.Name}");
            }

            return player;
        }

        private static string GetRequiredArgument(StoryCommand command, string key)
        {
            var value = command.Arguments.GetString(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new GameException($"Story media command argument is missing. command:{command.CommandId} name:{command.Name} argument:{key}");
            }

            return value;
        }

        private static IStoryCommandHandle RequireHandle(StoryCommand command, IStoryCommandHandle handle)
        {
            if (handle == null)
            {
                throw new GameException($"Story media command player returned null handle. command:{command.CommandId} name:{command.Name}");
            }

            if (ReferenceEquals(handle.Command, command) is false)
            {
                throw new GameException($"Story media command player returned a handle for a different command. command:{command.CommandId} name:{command.Name}");
            }

            return handle;
        }
    }

    /// <summary>
    /// 剧情帧表现器。
    /// </summary>
    public interface IStoryFramePresenter
    {
        /// <summary>
        /// 呈现剧情帧。
        /// </summary>
        /// <param name="frame">剧情帧。</param>
        /// <param name="presenter">剧情表现协调器。</param>
        void Present(StoryFrame frame, StoryPresenter presenter);

        /// <summary>
        /// 清理剧情帧。
        /// </summary>
        /// <param name="frame">剧情帧。</param>
        void Clear(StoryFrame frame);
    }

    /// <summary>
    /// 剧情表现协调器，负责把运行帧派发给 UI 和命令执行器。
    /// </summary>
    public sealed class StoryPresenter : IDisposable
    {
        private const char CommandKeySeparator = '\u001f';

        private readonly StoryModule m_Module;
        private readonly List<IStoryCommandHandler> m_CommandHandlers = new List<IStoryCommandHandler>();
        private readonly Dictionary<string, IStoryCommandHandle> m_CommandHandles =
            new Dictionary<string, IStoryCommandHandle>(StringComparer.Ordinal);
        private readonly Dictionary<IStoryCommandHandle, string> m_HandleKeys =
            new Dictionary<IStoryCommandHandle, string>();
        private readonly HashSet<string> m_DispatchedCommandKeys =
            new HashSet<string>(StringComparer.Ordinal);

        private IStoryFramePresenter m_FramePresenter;
        private StoryFrame m_CurrentFrame;
        private bool m_Disposed;

        /// <summary>
        /// 初始化剧情表现协调器。
        /// </summary>
        /// <param name="module">剧情模块。</param>
        /// <param name="framePresenter">剧情帧表现器。</param>
        public StoryPresenter(StoryModule module, IStoryFramePresenter framePresenter = null)
        {
            m_Module = module ?? throw new ArgumentNullException(nameof(module));
            m_FramePresenter = framePresenter;
        }

        /// <summary>
        /// 剧情模块。
        /// </summary>
        public StoryModule Module => m_Module;

        /// <summary>
        /// 当前帧。
        /// </summary>
        public StoryFrame CurrentFrame => m_CurrentFrame ?? m_Module.CurrentFrame;

        /// <summary>
        /// 最近一次表现错误。
        /// </summary>
        public Exception LastError { get; private set; }

        /// <summary>
        /// 活跃命令句柄。
        /// </summary>
        public IReadOnlyCollection<IStoryCommandHandle> ActiveCommandHandles =>
            new List<IStoryCommandHandle>(m_CommandHandles.Values);

        /// <summary>
        /// 设置剧情帧表现器。
        /// </summary>
        /// <param name="framePresenter">剧情帧表现器。</param>
        public void SetFramePresenter(IStoryFramePresenter framePresenter)
        {
            EnsureNotDisposed();
            m_FramePresenter = framePresenter;
        }

        /// <summary>
        /// 添加命令执行器。
        /// </summary>
        /// <param name="handler">命令执行器。</param>
        public void AddCommandHandler(IStoryCommandHandler handler)
        {
            EnsureNotDisposed();
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (m_CommandHandlers.Contains(handler) is false)
            {
                m_CommandHandlers.Add(handler);
            }
        }

        /// <summary>
        /// 移除命令执行器。
        /// </summary>
        /// <param name="handler">命令执行器。</param>
        /// <returns>成功移除时返回 true。</returns>
        public bool RemoveCommandHandler(IStoryCommandHandler handler)
        {
            EnsureNotDisposed();
            if (handler == null)
            {
                return false;
            }

            return m_CommandHandlers.Remove(handler);
        }

        /// <summary>
        /// 清空命令执行器。
        /// </summary>
        public void ClearCommandHandlers()
        {
            EnsureNotDisposed();
            m_CommandHandlers.Clear();
        }

        /// <summary>
        /// 注册并启动剧情程序。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="chapterId">章节 ID。</param>
        /// <returns>当前帧。</returns>
        public StoryFrame Start(StoryProgram program, string chapterId = null)
        {
            EnsureNotDisposed();
            StopActiveCommands();
            var runner = m_Module.Start(program, chapterId);
            return PresentFrame(runner.CurrentFrame);
        }

        /// <summary>
        /// 启动已注册剧情程序。
        /// </summary>
        /// <param name="storyId">剧情 ID。</param>
        /// <param name="chapterId">章节 ID。</param>
        /// <returns>当前帧。</returns>
        public StoryFrame StartProgram(string storyId, string chapterId = null)
        {
            EnsureNotDisposed();
            StopActiveCommands();
            var runner = m_Module.StartProgram(storyId, chapterId);
            return PresentFrame(runner.CurrentFrame);
        }

        /// <summary>
        /// 从快照恢复并呈现。
        /// </summary>
        /// <param name="snapshot">剧情快照。</param>
        /// <returns>当前帧。</returns>
        public StoryFrame Restore(StorySnapshot snapshot)
        {
            EnsureNotDisposed();
            StopActiveCommands();
            var runner = m_Module.Restore(snapshot);
            return PresentFrame(runner.CurrentFrame);
        }

        /// <summary>
        /// 呈现当前模块帧。
        /// </summary>
        /// <returns>当前帧。</returns>
        public StoryFrame PresentCurrentFrame()
        {
            EnsureNotDisposed();
            EnsureRunner();
            return PresentFrame(m_Module.CurrentFrame);
        }

        /// <summary>
        /// 继续剧情。
        /// </summary>
        /// <returns>当前帧。</returns>
        public StoryFrame Continue()
        {
            return Advance(() => m_Module.Continue());
        }

        /// <summary>
        /// 选择选项。
        /// </summary>
        /// <param name="choiceId">选项 ID。</param>
        /// <returns>当前帧。</returns>
        public StoryFrame Select(string choiceId)
        {
            return Advance(() => m_Module.Select(choiceId));
        }

        /// <summary>
        /// 完成命令。
        /// </summary>
        /// <param name="commandId">命令 ID。</param>
        /// <param name="outcomeId">结果 ID。</param>
        /// <returns>当前帧。</returns>
        public StoryFrame CompleteCommand(string commandId, string outcomeId)
        {
            return Advance(() => m_Module.CompleteCommand(commandId, outcomeId));
        }

        /// <summary>
        /// 推进等待时间。
        /// </summary>
        /// <param name="time">时间增量。</param>
        /// <returns>当前帧。</returns>
        public StoryFrame Evaluate(double time)
        {
            return Advance(() => m_Module.Evaluate(time));
        }

        /// <summary>
        /// 取消全部活跃命令。
        /// </summary>
        public void CancelActiveCommands()
        {
            FinishActiveCommands(handle => handle.Cancel());
        }

        /// <summary>
        /// 停止全部活跃命令。
        /// </summary>
        public void StopActiveCommands()
        {
            FinishActiveCommands(handle => handle.Stop());
        }

        /// <summary>
        /// 停止表现协调器。
        /// </summary>
        public void Stop()
        {
            if (m_Disposed)
            {
                return;
            }

            StopActiveCommands();
            if (m_CurrentFrame != null)
            {
                m_FramePresenter?.Clear(m_CurrentFrame);
            }

            m_CurrentFrame = null;
            m_DispatchedCommandKeys.Clear();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Stop();
            m_Disposed = true;
        }

        private StoryFrame Advance(Func<StoryFrame> advance)
        {
            EnsureNotDisposed();
            EnsureRunner();
            return PresentFrame(advance());
        }

        private StoryFrame PresentFrame(StoryFrame frame)
        {
            LastError = null;
            if (ReferenceEquals(m_CurrentFrame, frame) is false)
            {
                StopBlockingCommandsMissingFromFrame(frame);
                if (m_CurrentFrame != null)
                {
                    m_FramePresenter?.Clear(m_CurrentFrame);
                }

                m_DispatchedCommandKeys.Clear();
            }

            m_CurrentFrame = frame;
            if (frame == null)
            {
                return null;
            }

            m_FramePresenter?.Present(frame, this);
            DispatchCommands(frame);
            return m_CurrentFrame;
        }

        private void DispatchCommands(StoryFrame frame)
        {
            if (frame.Tracks == null || frame.Tracks.Count == 0)
            {
                return;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track?.Kind != StoryFrameTrackKind.Command || track.Command == null)
                {
                    continue;
                }

                DispatchCommand(frame, track);
                if (ReferenceEquals(m_CurrentFrame, frame) is false)
                {
                    return;
                }
            }
        }

        private void DispatchCommand(StoryFrame frame, StoryFrameTrack track)
        {
            var key = BuildCommandKey(track);
            if (m_DispatchedCommandKeys.Contains(key) || m_CommandHandles.ContainsKey(key))
            {
                return;
            }

            var handler = FindCommandHandler(track.Command);
            if (handler == null)
            {
                return;
            }

            m_DispatchedCommandKeys.Add(key);

            IStoryCommandHandle handle;
            try
            {
                handle = handler.Execute(track.Command, CreateContext(frame, track));
            }
            catch (Exception ex)
            {
                m_DispatchedCommandKeys.Remove(key);
                LastError = ex;
                throw;
            }

            if (handle == null)
            {
                return;
            }

            if (ReferenceEquals(handle.Command, track.Command) is false)
            {
                throw new GameException($"Story command handler returned a handle for a different command. command:{track.Command.CommandId}");
            }

            RegisterHandle(key, handle);
            ProcessTerminalHandle(handle);
        }

        private IStoryCommandHandler FindCommandHandler(StoryCommand command)
        {
            for (var i = 0; i < m_CommandHandlers.Count; i++)
            {
                var handler = m_CommandHandlers[i];
                if (handler != null && handler.CanHandle(command))
                {
                    return handler;
                }
            }

            return null;
        }

        private void RegisterHandle(string key, IStoryCommandHandle handle)
        {
            m_CommandHandles[key] = handle;
            m_HandleKeys[handle] = key;
            handle.Completed += OnCommandCompleted;
            handle.Canceled += OnCommandCanceled;
            handle.Stopped += OnCommandStopped;
            handle.Failed += OnCommandFailed;
        }

        private void ProcessTerminalHandle(IStoryCommandHandle handle)
        {
            if (handle.IsCompleted)
            {
                OnCommandCompleted(handle);
                return;
            }

            if (handle.IsCanceled)
            {
                OnCommandCanceled(handle);
                return;
            }

            if (handle.IsStopped)
            {
                OnCommandStopped(handle);
                return;
            }

            if (handle.Error != null)
            {
                OnCommandFailed(handle);
            }
        }

        private void OnCommandCompleted(IStoryCommandHandle handle)
        {
            if (handle == null || m_HandleKeys.ContainsKey(handle) is false)
            {
                return;
            }

            RemoveHandle(handle);
            if (RequiresCommandCompletion(handle.Command) is false)
            {
                return;
            }

            try
            {
                PresentFrame(m_Module.CompleteCommand(handle.Command.CommandId, handle.OutcomeId));
            }
            catch (Exception ex)
            {
                LastError = ex;
            }
        }

        private void OnCommandCanceled(IStoryCommandHandle handle)
        {
            RemoveHandle(handle);
        }

        private void OnCommandStopped(IStoryCommandHandle handle)
        {
            RemoveHandle(handle);
        }

        private void OnCommandFailed(IStoryCommandHandle handle)
        {
            LastError = handle?.Error;
            RemoveHandle(handle);
        }

        private void RemoveHandle(IStoryCommandHandle handle)
        {
            if (handle == null || m_HandleKeys.TryGetValue(handle, out var key) is false)
            {
                return;
            }

            handle.Completed -= OnCommandCompleted;
            handle.Canceled -= OnCommandCanceled;
            handle.Stopped -= OnCommandStopped;
            handle.Failed -= OnCommandFailed;
            m_HandleKeys.Remove(handle);
            m_CommandHandles.Remove(key);
        }

        private void FinishActiveCommands(Action<IStoryCommandHandle> finish)
        {
            var handles = new List<IStoryCommandHandle>(m_CommandHandles.Values);
            for (var i = 0; i < handles.Count; i++)
            {
                var handle = handles[i];
                if (handle == null)
                {
                    continue;
                }

                finish(handle);
                ProcessTerminalHandle(handle);
            }
        }

        private void StopBlockingCommandsMissingFromFrame(StoryFrame frame)
        {
            if (m_CommandHandles.Count == 0)
            {
                return;
            }

            var nextCommandKeys = CollectCommandKeys(frame);
            var handles = new List<IStoryCommandHandle>(m_CommandHandles.Values);
            for (var i = 0; i < handles.Count; i++)
            {
                var handle = handles[i];
                if (handle == null ||
                    m_HandleKeys.TryGetValue(handle, out var key) is false ||
                    nextCommandKeys.Contains(key))
                {
                    continue;
                }

                var command = handle.Command;
                if (RequiresCommandCompletion(command) is false &&
                    ShouldStopWhenMissingFromFrame(command) is false)
                {
                    continue;
                }

                handle.Stop();
                ProcessTerminalHandle(handle);
            }
        }

        private static HashSet<string> CollectCommandKeys(StoryFrame frame)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (frame?.Tracks == null)
            {
                return keys;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track?.Kind == StoryFrameTrackKind.Command && track.Command != null)
                {
                    keys.Add(BuildCommandKey(track));
                }
            }

            return keys;
        }

        private StoryRuntimeContext CreateContext(StoryFrame frame, StoryFrameTrack track)
        {
            var runner = m_Module.CurrentRunner;
            return new StoryRuntimeContext(
                frame.Program,
                frame.Chapter,
                track.Step,
                runner?.CurrentTime ?? 0d,
                runner?.VariableStore,
                runner?.History ?? Array.Empty<HistoryEntry>());
        }

        private static string BuildCommandKey(StoryFrameTrack track)
        {
            var branchId = string.IsNullOrWhiteSpace(track.BranchId) ? string.Empty : track.BranchId;
            return branchId + CommandKeySeparator + track.Command.CommandId;
        }

        private static bool RequiresCommandCompletion(StoryCommand command)
        {
            return command != null && (command.WaitForCompletion || command.OutcomePorts.Count > 0);
        }

        private static bool ShouldStopWhenMissingFromFrame(StoryCommand command)
        {
            return command != null &&
                   (string.Equals(command.Name, StoryMediaCommandNames.PlayAudio, StringComparison.Ordinal) ||
                    string.Equals(command.Name, StoryMediaCommandNames.ShowImage, StringComparison.Ordinal));
        }

        private void EnsureRunner()
        {
            if (m_Module.CurrentRunner == null)
            {
                throw new GameException("Story program has not started.");
            }
        }

        private void EnsureNotDisposed()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(StoryPresenter));
            }
        }
    }
}
