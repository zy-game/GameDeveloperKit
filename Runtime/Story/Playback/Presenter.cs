using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Logic;

namespace GameDeveloperKit.Story.Playback
{
    /// <summary>
    /// 剧情帧表现器。
    /// </summary>
    public interface IFramePresenter
    {
        /// <summary>
        /// 呈现剧情帧。
        /// </summary>
        /// <param name="frame">剧情帧。</param>
        /// <param name="presenter">剧情表现协调器。</param>
        void Present(Frame frame, Presenter presenter);

        /// <summary>
        /// 清理剧情帧。
        /// </summary>
        /// <param name="frame">剧情帧。</param>
        void Clear(Frame frame);
    }

    /// <summary>
    /// 剧情表现协调器，负责把运行帧派发给 UI 和命令执行器。
    /// </summary>
    public sealed class Presenter : IDisposable
    {
        private const char CommandKeySeparator = '\u001f';

        private readonly StoryModule m_Module;
        private readonly List<ICommandHandler> m_CommandHandlers = new List<ICommandHandler>();
        private readonly Dictionary<string, ICommandHandle> m_CommandHandles =
            new Dictionary<string, ICommandHandle>(StringComparer.Ordinal);
        private readonly Dictionary<ICommandHandle, string> m_HandleKeys =
            new Dictionary<ICommandHandle, string>();
        private readonly HashSet<string> m_DispatchedCommandKeys =
            new HashSet<string>(StringComparer.Ordinal);

        private IFramePresenter m_FramePresenter;
        private Frame m_CurrentFrame;
        private bool m_Disposed;

        /// <summary>
        /// 初始化剧情表现协调器。
        /// </summary>
        /// <param name="module">剧情模块。</param>
        /// <param name="framePresenter">剧情帧表现器。</param>
        public Presenter(StoryModule module, IFramePresenter framePresenter = null)
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
        public Frame CurrentFrame => m_CurrentFrame ?? m_Module.CurrentFrame;

        /// <summary>
        /// 最近一次表现错误。
        /// </summary>
        public Exception LastError { get; private set; }

        /// <summary>
        /// 活跃命令句柄。
        /// </summary>
        public IReadOnlyCollection<ICommandHandle> ActiveCommandHandles =>
            new List<ICommandHandle>(m_CommandHandles.Values);

        /// <summary>
        /// 设置剧情帧表现器。
        /// </summary>
        /// <param name="framePresenter">剧情帧表现器。</param>
        public void SetFramePresenter(IFramePresenter framePresenter)
        {
            EnsureNotDisposed();
            m_FramePresenter = framePresenter;
        }

        /// <summary>
        /// 添加命令执行器。
        /// </summary>
        /// <param name="handler">命令执行器。</param>
        public void AddCommandHandler(ICommandHandler handler)
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
        public bool RemoveCommandHandler(ICommandHandler handler)
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
        /// <param name="volumeId">卷 ID。</param>
        /// <param name="episodeId">剧情段 ID。</param>
        /// <returns>当前帧。</returns>
        public Frame Start(Program program, string volumeId, string episodeId)
        {
            EnsureNotDisposed();
            StopActiveCommands();
            var runner = m_Module.Start(program, volumeId, episodeId);
            return PresentFrame(runner.CurrentFrame);
        }

        /// <summary>
        /// 启动已注册剧情程序。
        /// </summary>
        /// <param name="storyId">剧情 ID。</param>
        /// <param name="volumeId">卷 ID。</param>
        /// <param name="episodeId">剧情段 ID。</param>
        /// <returns>当前帧。</returns>
        public Frame StartEpisode(string storyId, string volumeId, string episodeId)
        {
            EnsureNotDisposed();
            StopActiveCommands();
            var runner = m_Module.StartEpisode(storyId, volumeId, episodeId);
            return PresentFrame(runner.CurrentFrame);
        }

        /// <summary>
        /// 从快照恢复并呈现。
        /// </summary>
        /// <param name="snapshot">剧情快照。</param>
        /// <returns>当前帧。</returns>
        public Frame Restore(Snapshot snapshot)
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
        public Frame PresentCurrentFrame()
        {
            EnsureNotDisposed();
            EnsureRunner();
            return PresentFrame(m_Module.CurrentFrame);
        }

        /// <summary>
        /// 继续剧情。
        /// </summary>
        /// <returns>当前帧。</returns>
        public Frame Continue()
        {
            return Advance(() => m_Module.Continue());
        }

        /// <summary>
        /// 选择选项。
        /// </summary>
        /// <param name="choiceId">选项 ID。</param>
        /// <returns>当前帧。</returns>
        public Frame Select(string choiceId)
        {
            return Advance(() => m_Module.Select(choiceId));
        }

        /// <summary>
        /// 完成命令。
        /// </summary>
        /// <param name="commandId">命令 ID。</param>
        /// <param name="outcomeId">结果 ID。</param>
        /// <returns>当前帧。</returns>
        public Frame CompleteCommand(string commandId, string outcomeId)
        {
            return Advance(() => m_Module.CompleteCommand(commandId, outcomeId));
        }

        /// <summary>
        /// 推进等待时间。
        /// </summary>
        /// <param name="time">时间增量。</param>
        /// <returns>当前帧。</returns>
        public Frame Evaluate(double time)
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

        private Frame Advance(Func<Frame> advance)
        {
            EnsureNotDisposed();
            EnsureRunner();
            return PresentFrame(advance());
        }

        private Frame PresentFrame(Frame frame)
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

        private void DispatchCommands(Frame frame)
        {
            if (frame.Tracks == null || frame.Tracks.Count == 0)
            {
                return;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track?.Kind != FrameTrackKind.Command || track.Command == null)
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

        private void DispatchCommand(Frame frame, FrameTrack track)
        {
            var key = BuildCommandKey(track);
            if (m_DispatchedCommandKeys.Contains(key) || m_CommandHandles.ContainsKey(key))
            {
                return;
            }

            var handler = FindCommandHandler(track.Command);
            if (handler == null)
            {
                if (LogicCommandCodec.IsLogicCommand(track.Command))
                {
                    throw new GameException(
                        $"Story logic command handler is not registered. " +
                        $"story:{frame.Program?.StoryId ?? "<unknown>"} " +
                        $"volume:{frame.Volume?.VolumeId ?? "<unknown>"} " +
                        $"episode:{frame.Episode?.EpisodeId ?? "<unknown>"} " +
                        $"step:{track.Step?.StepId ?? "<unknown>"} " +
                        $"logic:{track.Command.Name} command:{track.Command.CommandId}");
                }

                return;
            }

            m_DispatchedCommandKeys.Add(key);

            ICommandHandle handle;
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

        private ICommandHandler FindCommandHandler(global::GameDeveloperKit.Story.Model.Command command)
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

        private void RegisterHandle(string key, ICommandHandle handle)
        {
            m_CommandHandles[key] = handle;
            m_HandleKeys[handle] = key;
            handle.Completed += OnCommandCompleted;
            handle.Canceled += OnCommandCanceled;
            handle.Stopped += OnCommandStopped;
            handle.Failed += OnCommandFailed;
        }

        private void ProcessTerminalHandle(ICommandHandle handle)
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

        private void OnCommandCompleted(ICommandHandle handle)
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

        private void OnCommandCanceled(ICommandHandle handle)
        {
            RemoveHandle(handle);
        }

        private void OnCommandStopped(ICommandHandle handle)
        {
            RemoveHandle(handle);
        }

        private void OnCommandFailed(ICommandHandle handle)
        {
            LastError = handle?.Error;
            RemoveHandle(handle);
        }

        private void RemoveHandle(ICommandHandle handle)
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

        private void FinishActiveCommands(Action<ICommandHandle> finish)
        {
            var handles = new List<ICommandHandle>(m_CommandHandles.Values);
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

        private void StopBlockingCommandsMissingFromFrame(Frame frame)
        {
            if (m_CommandHandles.Count == 0)
            {
                return;
            }

            var nextCommandKeys = CollectCommandKeys(frame);
            var handles = new List<ICommandHandle>(m_CommandHandles.Values);
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

        private static HashSet<string> CollectCommandKeys(Frame frame)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (frame?.Tracks == null)
            {
                return keys;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track?.Kind == FrameTrackKind.Command && track.Command != null)
                {
                    keys.Add(BuildCommandKey(track));
                }
            }

            return keys;
        }

        private RuntimeContext CreateContext(Frame frame, FrameTrack track)
        {
            var runner = m_Module.CurrentRunner;
            return new RuntimeContext(
                frame.Program,
                frame.Volume,
                frame.Episode,
                track.Step,
                runner?.CurrentTime ?? 0d,
                runner?.VariableStore,
                runner?.History ?? Array.Empty<HistoryEntry>());
        }

        private static string BuildCommandKey(FrameTrack track)
        {
            var branchId = string.IsNullOrWhiteSpace(track.BranchId) ? string.Empty : track.BranchId;
            return branchId + CommandKeySeparator + track.Command.CommandId;
        }

        private static bool RequiresCommandCompletion(global::GameDeveloperKit.Story.Model.Command command)
        {
            return command != null && (command.WaitForCompletion || command.OutcomePorts.Count > 0);
        }

        private static bool ShouldStopWhenMissingFromFrame(global::GameDeveloperKit.Story.Model.Command command)
        {
            return command != null &&
                   (string.Equals(command.Name, MediaCommandNames.PlayAudio, StringComparison.Ordinal) ||
                    string.Equals(command.Name, MediaCommandNames.ShowImage, StringComparison.Ordinal));
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
                throw new ObjectDisposedException(nameof(Presenter));
            }
        }
    }
}
