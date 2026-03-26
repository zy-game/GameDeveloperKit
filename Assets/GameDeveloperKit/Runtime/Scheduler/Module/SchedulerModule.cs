using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 调度器模块，提供任务调度、延迟执行和周期性任务功能。
    /// 支持挂载点管理和任务分组。
    /// </summary>
    public sealed partial class SchedulerModule : IGameFrameworkLifecycleModule
    {

        private readonly Queue<ScheduledEntry> _postedActions = new();
        private readonly List<ScheduledEntry> _scheduledEntries = new();
        private readonly SchedulerDriver _driver;
        private int _nextHandleId = 1;
        private GameFrameworkModuleStatus _status = GameFrameworkModuleStatus.Created;
        private bool _diagnosticsRegistered;
        private int _executedCount;
        private int _failureCount;
        private string _lastError;
        private string _lastGroup;
        private string _lastMountPoint;

        /// <summary>
        /// 初始化 SchedulerModule 的新实例。
        /// </summary>
        public SchedulerModule()
        {
            var driverObject = new GameObject("[GameDeveloperKit.Scheduler]");
            UnityEngine.Object.DontDestroyOnLoad(driverObject);
            _driver = driverObject.AddComponent<SchedulerDriver>();
            _driver.Initialize(this);
        }

        /// <summary>
        /// 获取待处理任务数量。
        /// </summary>
        public int PendingCount => _scheduledEntries.Count + PostedCount;

        /// <summary>
        /// 获取已投递任务数量。
        /// </summary>
        public int PostedCount
        {
            get
            {
                lock (_postedActions)
                {
                    return _postedActions.Count;
                }
            }
        }

        /// <summary>
        /// 获取模块状态。
        /// </summary>
        public GameFrameworkModuleStatus Status => _status;

        /// <summary>
        /// 当任务执行失败时触发。
        /// </summary>
        public event Action<ScheduledTaskHandle, Exception> TaskFailed;

        /// <summary>
        /// 异步初始化调度器模块。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>初始化任务。</returns>
        public UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (!GameFrameworkModuleLifecycleUtility.TryEnterInitialization(nameof(SchedulerModule), ref _status, cancellationToken))
            {
                return UniTask.CompletedTask;
            }

            try
            {
                RegisterDiagnosticsSnapshotProviders();
                GameFrameworkModuleLifecycleUtility.CompleteInitialization(ref _status);
                return UniTask.CompletedTask;
            }
            catch
            {
                GameFrameworkModuleLifecycleUtility.FailInitialization(ref _status);
                throw;
            }
        }

        /// <summary>
        /// 异步关闭调度器模块。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>关闭任务。</returns>
        public UniTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            if (!GameFrameworkModuleLifecycleUtility.TryEnterShutdown(nameof(SchedulerModule), ref _status, cancellationToken))
            {
                return UniTask.CompletedTask;
            }

            Dispose();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 投递动作到下一帧执行。
        /// </summary>
        /// <param name="action">要执行的动作。</param>
        public void Post(Action action)
        {
            PostMounted(action, SchedulerMountPoint.Default);
        }

        /// <summary>
        /// 投递分组动作到下一帧执行。
        /// </summary>
        /// <param name="action">要执行的动作。</param>
        /// <param name="group">分组名称。</param>
        /// <param name="tag">任务标签。</param>
        /// <returns>任务句柄。</returns>
        public ScheduledTaskHandle PostGrouped(Action action, string group = null, string tag = null)
        {
            return PostMounted(action, SchedulerMountPoint.Default, group, tag);
        }

        /// <summary>
        /// 投递挂载动作到下一帧执行。
        /// </summary>
        /// <param name="action">要执行的动作。</param>
        /// <param name="mountPoint">挂载点。</param>
        /// <param name="group">分组名称。</param>
        /// <param name="tag">任务标签。</param>
        /// <returns>任务句柄。</returns>
        public ScheduledTaskHandle PostMounted(Action action, SchedulerMountPoint mountPoint, string group = null, string tag = null)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var handle = new ScheduledTaskHandle(_nextHandleId++, mountPoint, group, tag);
            lock (_postedActions)
            {
                _postedActions.Enqueue(new ScheduledEntry
                {
                    Handle = handle,
                    Action = action
                });
            }

            EnsureDiagnosticsSnapshotProviders();
            return handle;
        }

        /// <summary>
        /// 投递启动阶段动作。
        /// </summary>
        /// <param name="action">要执行的动作。</param>
        /// <param name="group">分组名称。</param>
        /// <param name="tag">任务标签。</param>
        /// <returns>任务句柄。</returns>
        public ScheduledTaskHandle PostForStartup(Action action, string group = null, string tag = null)
        {
            return PostMounted(action, SchedulerMountPoint.Startup, group, tag);
        }

        /// <summary>
        /// 投递 UI 更新动作。
        /// </summary>
        /// <param name="action">要执行的动作。</param>
        /// <param name="group">分组名称。</param>
        /// <param name="tag">任务标签。</param>
        /// <returns>任务句柄。</returns>
        public ScheduledTaskHandle PostForUI(Action action, string group = null, string tag = null)
        {
            return PostMounted(action, SchedulerMountPoint.UI, group, tag);
        }

        /// <summary>
        /// 投递流程更新动作。
        /// </summary>
        /// <param name="action">要执行的动作。</param>
        /// <param name="group">分组名称。</param>
        /// <param name="tag">任务标签。</param>
        /// <returns>任务句柄。</returns>
        public ScheduledTaskHandle PostForProcedure(Action action, string group = null, string tag = null)
        {
            return PostMounted(action, SchedulerMountPoint.Procedure, group, tag);
        }

        /// <summary>
        /// 延迟执行动作。
        /// </summary>
        /// <param name="delay">延迟时间。</param>
        /// <param name="action">要执行的动作。</param>
        /// <param name="group">分组名称。</param>
        /// <param name="tag">任务标签。</param>
        /// <returns>任务句柄。</returns>
        public ScheduledTaskHandle Delay(TimeSpan delay, Action action, string group = null, string tag = null)
        {
            return DelayMounted(delay, action, SchedulerMountPoint.Default, group, tag);
        }

        /// <summary>
        /// 周期性执行动作。
        /// </summary>
        /// <param name="interval">执行间隔。</param>
        /// <param name="action">要执行的动作。</param>
        /// <param name="repeatCount">重复次数（-1 表示无限重复）。</param>
        /// <param name="group">分组名称。</param>
        /// <param name="tag">任务标签。</param>
        /// <returns>任务句柄。</returns>
        public ScheduledTaskHandle Schedule(TimeSpan interval, Action action, int repeatCount = -1, string group = null, string tag = null)
        {
            return ScheduleMounted(interval, action, SchedulerMountPoint.Default, repeatCount, group, tag);
        }

        /// <summary>
        /// 延迟执行挂载动作。
        /// </summary>
        /// <param name="delay">延迟时间。</param>
        /// <param name="action">要执行的动作。</param>
        /// <param name="mountPoint">挂载点。</param>
        /// <param name="group">分组名称。</param>
        /// <param name="tag">任务标签。</param>
        /// <returns>任务句柄。</returns>
        public ScheduledTaskHandle DelayMounted(TimeSpan delay, Action action, SchedulerMountPoint mountPoint, string group = null, string tag = null)
        {
            return ScheduleInternal(delay, TimeSpan.Zero, 1, action, mountPoint, group, tag);
        }

        /// <summary>
        /// 周期性执行挂载动作。
        /// </summary>
        /// <param name="interval">执行间隔。</param>
        /// <param name="action">要执行的动作。</param>
        /// <param name="mountPoint">挂载点。</param>
        /// <param name="repeatCount">重复次数（-1 表示无限重复）。</param>
        /// <param name="group">分组名称。</param>
        /// <param name="tag">任务标签。</param>
        /// <returns>任务句柄。</returns>
        public ScheduledTaskHandle ScheduleMounted(TimeSpan interval, Action action, SchedulerMountPoint mountPoint, int repeatCount = -1, string group = null, string tag = null)
        {
            return ScheduleInternal(interval, interval, repeatCount, action, mountPoint, group, tag);
        }

        /// <summary>
        /// 取消任务。
        /// </summary>
        /// <param name="handle">任务句柄。</param>
        /// <returns>如果取消成功则返回 true，否则返回 false。</returns>
        public bool Cancel(ScheduledTaskHandle handle)
        {
            if (handle == null)
            {
                return false;
            }

            handle.IsCancelled = true;
            return true;
        }

        /// <summary>
        /// 取消指定分组的所有任务。
        /// </summary>
        /// <param name="group">分组名称。</param>
        /// <returns>取消的任务数量。</returns>
        public int CancelGroup(string group)
        {
            return CancelByPredicate(handle => string.Equals(handle?.Group, group, StringComparison.Ordinal));
        }

        /// <summary>
        /// 取消指定标签的所有任务。
        /// </summary>
        /// <param name="tag">任务标签。</param>
        /// <returns>取消的任务数量。</returns>
        public int CancelTag(string tag)
        {
            return CancelByPredicate(handle => string.Equals(handle?.Tag, tag, StringComparison.Ordinal));
        }

        /// <summary>
        /// 释放调度器模块资源。
        /// </summary>
        public void Dispose()
        {
            lock (_postedActions)
            {
                _postedActions.Clear();
            }

            RemoveDiagnosticsSnapshotProviders();
            _scheduledEntries.Clear();
            TaskFailed = null;

            if (_driver != null)
            {
                UnityEngine.Object.Destroy(_driver.gameObject);
            }

            _status = GameFrameworkModuleStatus.Disposed;
        }

        private ScheduledTaskHandle ScheduleInternal(TimeSpan delay, TimeSpan interval, int repeatCount, Action action, SchedulerMountPoint mountPoint, string group, string tag)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (repeatCount == 0 || repeatCount < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(repeatCount));
            }

            var handle = new ScheduledTaskHandle(_nextHandleId++, mountPoint, group, tag);
            _scheduledEntries.Add(new ScheduledEntry
            {
                Handle = handle,
                Action = action,
                ExecuteAt = Time.unscaledTimeAsDouble + Math.Max(0d, delay.TotalSeconds),
                Interval = Math.Max(0d, interval.TotalSeconds),
                Repeat = repeatCount != 1,
                RemainingExecutions = repeatCount
            });
            EnsureDiagnosticsSnapshotProviders();
            return handle;
        }

        private void Update(double now)
        {
            ExecutePostedActions();

            for (var i = _scheduledEntries.Count - 1; i >= 0; i--)
            {
                var entry = _scheduledEntries[i];
                if (entry.Handle.IsCancelled)
                {
                    _scheduledEntries.RemoveAt(i);
                    continue;
                }

                if (now < entry.ExecuteAt)
                {
                    continue;
                }

                ExecuteEntry(entry);

                if (entry.RemainingExecutions > 0)
                {
                    entry.RemainingExecutions--;
                }

                if (!entry.Repeat || entry.RemainingExecutions == 0)
                {
                    _scheduledEntries.RemoveAt(i);
                    continue;
                }

                entry.ExecuteAt = now + entry.Interval;
            }
        }

        private void ExecutePostedActions()
        {
            while (true)
            {
                ScheduledEntry entry;
                lock (_postedActions)
                {
                    if (_postedActions.Count == 0)
                    {
                        return;
                    }

                    entry = _postedActions.Dequeue();
                }

                if (entry.Handle != null && entry.Handle.IsCancelled)
                {
                    continue;
                }

                ExecuteEntry(entry);
            }
        }

        private void ExecuteEntry(ScheduledEntry entry)
        {
            try
            {
                _lastGroup = entry.Handle?.Group;
                _lastMountPoint = entry.Handle?.MountPoint.ToString();
                entry.Action?.Invoke();
                _executedCount++;
            }
            catch (Exception exception)
            {
                _failureCount++;
                _lastError = exception.Message;
                TaskFailed?.Invoke(entry.Handle, exception);

                if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
                {
                    diagnostics.CaptureSnapshot("Scheduler.LastError", _lastError ?? string.Empty);
                }
            }
        }

        private int CancelByPredicate(Func<ScheduledTaskHandle, bool> predicate)
        {
            if (predicate == null)
            {
                return 0;
            }

            var count = 0;
            lock (_postedActions)
            {
                foreach (var entry in _postedActions)
                {
                    if (predicate(entry.Handle) && !entry.Handle.IsCancelled)
                    {
                        entry.Handle.IsCancelled = true;
                        count++;
                    }
                }
            }

            for (var i = 0; i < _scheduledEntries.Count; i++)
            {
                var handle = _scheduledEntries[i].Handle;
                if (predicate(handle) && !handle.IsCancelled)
                {
                    handle.IsCancelled = true;
                    count++;
                }
            }

            return count;
        }

        private void EnsureDiagnosticsSnapshotProviders()
        {
            if (_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RegisterSnapshotProvider("Scheduler.PendingCount", () => PendingCount.ToString());
            diagnostics.RegisterSnapshotProvider("Scheduler.PostedCount", () => PostedCount.ToString());
            diagnostics.RegisterSnapshotProvider("Scheduler.ScheduledCount", () => _scheduledEntries.Count.ToString());
            diagnostics.RegisterSnapshotProvider("Scheduler.ExecutedCount", () => _executedCount.ToString());
            diagnostics.RegisterSnapshotProvider("Scheduler.FailureCount", () => _failureCount.ToString());
            diagnostics.RegisterSnapshotProvider("Scheduler.LastGroup", () => _lastGroup ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Scheduler.LastMountPoint", () => _lastMountPoint ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Scheduler.LastError", () => _lastError ?? string.Empty);
            _diagnosticsRegistered = true;
        }

        private void RegisterDiagnosticsSnapshotProviders()
        {
            EnsureDiagnosticsSnapshotProviders();
        }

        private void RemoveDiagnosticsSnapshotProviders()
        {
            if (!_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RemoveSnapshotProvider("Scheduler.PendingCount");
            diagnostics.RemoveSnapshotProvider("Scheduler.PostedCount");
            diagnostics.RemoveSnapshotProvider("Scheduler.ScheduledCount");
            diagnostics.RemoveSnapshotProvider("Scheduler.ExecutedCount");
            diagnostics.RemoveSnapshotProvider("Scheduler.FailureCount");
            diagnostics.RemoveSnapshotProvider("Scheduler.LastGroup");
            diagnostics.RemoveSnapshotProvider("Scheduler.LastMountPoint");
            diagnostics.RemoveSnapshotProvider("Scheduler.LastError");
            _diagnosticsRegistered = false;
        }
    }
}
