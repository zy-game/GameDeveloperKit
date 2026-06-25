using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story
{
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
