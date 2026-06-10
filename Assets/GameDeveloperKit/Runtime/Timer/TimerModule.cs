using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Timer
{
    /// <summary>
    /// 计时器模块，按 Unity 生命周期驱动注册的计时器回调。
    /// </summary>
    public sealed partial class TimerModule : GameModuleBase
    {
        /// <summary>
        /// 存储 timer。
        /// </summary>
        private Timer _timer;
        /// <summary>
        /// 存储 handles。
        /// </summary>
        private readonly List<TimerHandle> _handles = new List<TimerHandle>();
        /// <summary>
        /// 存储 dispatch Buffer。
        /// </summary>
        private readonly List<TimerHandle> _dispatchBuffer = new List<TimerHandle>();
        /// <summary>
        /// 存储 callback Handles。
        /// </summary>
        private readonly Dictionary<Action<float>, TimerHandle> _callbackHandles = new Dictionary<Action<float>, TimerHandle>();

        /// <summary>
        /// 当前计时器帧计数。
        /// </summary>
        public long Tick { get; private set; }

        /// <summary>
        /// 当前计时器累计时间。
        /// </summary>
        public double Time { get; private set; }

        /// <summary>
        /// 当前计时器非缩放累计时间。
        /// </summary>
        public double UnscaledTime { get; private set; }

        /// <summary>
        /// 最近一次计时器推进的缩放时间。
        /// </summary>
        public float DeltaTime { get; private set; }

        /// <summary>
        /// 最近一次计时器推进的非缩放时间。
        /// </summary>
        public float UnscaledDeltaTime { get; private set; }

        /// <summary>
        /// 启动计时器模块。
        /// </summary>
        /// <returns>模块启动任务。</returns>
        public override UniTask Startup()
        {
            if (_timer != null)
            {
                return UniTask.CompletedTask;
            }

            ResetClockState();
            _handles.Clear();
            _dispatchBuffer.Clear();
            _callbackHandles.Clear();

            _timer = new GameObject("Timer").AddComponent<Timer>();
            _timer.onUpdate = Update;
            Object.DontDestroyOnLoad(_timer.gameObject);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 关闭计时器模块。
        /// </summary>
        /// <returns>模块关闭任务。</returns>
        public override UniTask Shutdown()
        {
            if (this._timer != null)
            {
                this._timer.onUpdate = null;
                Object.DestroyImmediate(this._timer.gameObject);
                this._timer = null;
            }

            ClearAllTimers();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Unity Update 回调。
        /// </summary>
        /// <param name="deltaTime">delta Time 参数。</param>
        /// <param name="unscaledDeltaTime">unscaled Delta Time 参数。</param>
        internal void Update(float deltaTime, float unscaledDeltaTime)
        {
            Update(TimerTickKind.Update, deltaTime, unscaledDeltaTime);
        }

        /// <summary>
        /// Unity Update 回调。
        /// </summary>
        /// <param name="tickKind">tick Kind 参数。</param>
        /// <param name="deltaTime">delta Time 参数。</param>
        /// <param name="unscaledDeltaTime">unscaled Delta Time 参数。</param>
        internal void Update(TimerTickKind tickKind, float deltaTime, float unscaledDeltaTime)
        {
            ValidateTickKind(tickKind, nameof(tickKind));
            var phaseDeltaTime = Mathf.Max(0f, deltaTime);
            var phaseUnscaledDeltaTime = Mathf.Max(0f, unscaledDeltaTime);

            if (tickKind == TimerTickKind.Update)
            {
                DeltaTime = phaseDeltaTime;
                UnscaledDeltaTime = phaseUnscaledDeltaTime;
                Tick++;
                Time += DeltaTime;
                UnscaledTime += UnscaledDeltaTime;
            }

            var context = new TimerUpdateContext(
                tickKind,
                Tick,
                Time,
                UnscaledTime,
                DeltaTime,
                UnscaledDeltaTime);

            UpdateTimers(tickKind, in context, phaseUnscaledDeltaTime);
        }

        /// <summary>
        /// 执行 Snapshot。
        /// </summary>
        /// <returns>执行结果。</returns>
        public TimerSnapshot Snapshot()
        {
            var delays = new List<TimerDelayHandle>();
            var countdowns = new List<TimerCountdownHandle>();
            var intervals = new List<TimerIntervalHandle>();
            var updates = new List<TimerUpdateHandle>();
            foreach (var handle in _handles)
            {
                if (handle.IsCancelled || handle.IsCompleted)
                {
                    continue;
                }

                if (handle is TimerDelayHandle delay)
                {
                    delays.Add(delay);
                }
                else if (handle is TimerCountdownHandle countdown)
                {
                    countdowns.Add(countdown);
                }
                else if (handle is TimerIntervalHandle interval)
                {
                    intervals.Add(interval);
                }
                else if (handle is TimerUpdateHandle update)
                {
                    updates.Add(update);
                }
            }

            return new TimerSnapshot(Tick, Time, UnscaledTime, DeltaTime, UnscaledDeltaTime, delays, countdowns, intervals, updates);
        }

        /// <summary>
        /// 执行 Delay。
        /// </summary>
        /// <param name="delay">delay 参数。</param>
        /// <param name="callback">callback 参数。</param>
        /// <param name="useUnscaledTime">use Unscaled Time 参数。</param>
        /// <param name="owner">owner 参数。</param>
        /// <param name="tag">tag 参数。</param>
        /// <returns>执行结果。</returns>
        public TimerDelayHandle Delay(float delay, Action callback, bool useUnscaledTime = false, object owner = null, string tag = null)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return Register(new TimerDelayHandle(delay, callback, useUnscaledTime), owner, tag);
        }

        /// <summary>
        /// 执行 Countdown。
        /// </summary>
        /// <param name="duration">duration 参数。</param>
        /// <param name="onTick">on Tick 参数。</param>
        /// <param name="onComplete">on Complete 参数。</param>
        /// <param name="useUnscaledTime">use Unscaled Time 参数。</param>
        /// <param name="owner">owner 参数。</param>
        /// <param name="tag">tag 参数。</param>
        /// <returns>执行结果。</returns>
        public TimerCountdownHandle Countdown(float duration, Action<float> onTick = null, Action onComplete = null, bool useUnscaledTime = false, object owner = null, string tag = null)
        {
            return Register(new TimerCountdownHandle(duration, onTick, onComplete, useUnscaledTime), owner, tag);
        }

        /// <summary>
        /// 执行 Interval。
        /// </summary>
        /// <param name="interval">interval 参数。</param>
        /// <param name="callback">callback 参数。</param>
        /// <param name="useUnscaledTime">use Unscaled Time 参数。</param>
        /// <param name="owner">owner 参数。</param>
        /// <param name="tag">tag 参数。</param>
        /// <returns>执行结果。</returns>
        public TimerIntervalHandle Interval(float interval, Action<float> callback, bool useUnscaledTime = false, object owner = null, string tag = null)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return Register(new TimerIntervalHandle(interval, callback, useUnscaledTime), owner, tag);
        }

        /// <summary>
        /// 处理 Update 回调。
        /// </summary>
        /// <param name="callback">callback 参数。</param>
        /// <param name="owner">owner 参数。</param>
        /// <param name="tag">tag 参数。</param>
        /// <returns>执行结果。</returns>
        public UpdateTimerHandle OnUpdate(Action callback, object owner = null, string tag = null)
        {
            return Register(new UpdateTimerHandle(callback), owner, tag);
        }

        /// <summary>
        /// 处理 Update 回调。
        /// </summary>
        /// <param name="callback">callback 参数。</param>
        /// <param name="owner">owner 参数。</param>
        /// <param name="tag">tag 参数。</param>
        /// <returns>执行结果。</returns>
        public UpdateTimerHandle OnUpdate(Action<TimerUpdateContext> callback, object owner = null, string tag = null)
        {
            return Register(new UpdateTimerHandle(callback), owner, tag);
        }

        /// <summary>
        /// 处理 Late Update 回调。
        /// </summary>
        /// <param name="callback">callback 参数。</param>
        /// <param name="owner">owner 参数。</param>
        /// <param name="tag">tag 参数。</param>
        /// <returns>执行结果。</returns>
        public LateUpdateTimerHandle OnLateUpdate(Action callback, object owner = null, string tag = null)
        {
            return Register(new LateUpdateTimerHandle(callback), owner, tag);
        }

        /// <summary>
        /// 处理 Late Update 回调。
        /// </summary>
        /// <param name="callback">callback 参数。</param>
        /// <param name="fps">fps 参数。</param>
        /// <param name="owner">owner 参数。</param>
        /// <param name="tag">tag 参数。</param>
        /// <returns>执行结果。</returns>
        public LateUpdateTimerHandle OnLateUpdate(Action callback, float fps, object owner = null, string tag = null)
        {
            return Register(new LateUpdateTimerHandle(callback, fps), owner, tag);
        }

        /// <summary>
        /// 处理 Late Update 回调。
        /// </summary>
        /// <param name="callback">callback 参数。</param>
        /// <param name="owner">owner 参数。</param>
        /// <param name="tag">tag 参数。</param>
        /// <returns>执行结果。</returns>
        public LateUpdateTimerHandle OnLateUpdate(Action<TimerUpdateContext> callback, object owner = null, string tag = null)
        {
            return Register(new LateUpdateTimerHandle(callback), owner, tag);
        }

        /// <summary>
        /// 处理 Late Update 回调。
        /// </summary>
        /// <param name="callback">callback 参数。</param>
        /// <param name="fps">fps 参数。</param>
        /// <param name="owner">owner 参数。</param>
        /// <param name="tag">tag 参数。</param>
        /// <returns>执行结果。</returns>
        public LateUpdateTimerHandle OnLateUpdate(Action<TimerUpdateContext> callback, float fps, object owner = null, string tag = null)
        {
            return Register(new LateUpdateTimerHandle(callback, fps), owner, tag);
        }

        /// <summary>
        /// 处理 Fixed Update 回调。
        /// </summary>
        /// <param name="callback">callback 参数。</param>
        /// <param name="owner">owner 参数。</param>
        /// <param name="tag">tag 参数。</param>
        /// <returns>执行结果。</returns>
        public FixedUpdateTimerHandle OnFixedUpdate(Action callback, object owner = null, string tag = null)
        {
            return Register(new FixedUpdateTimerHandle(callback), owner, tag);
        }

        /// <summary>
        /// 处理 Fixed Update 回调。
        /// </summary>
        /// <param name="callback">callback 参数。</param>
        /// <param name="fps">fps 参数。</param>
        /// <param name="owner">owner 参数。</param>
        /// <param name="tag">tag 参数。</param>
        /// <returns>执行结果。</returns>
        public FixedUpdateTimerHandle OnFixedUpdate(Action callback, float fps, object owner = null, string tag = null)
        {
            return Register(new FixedUpdateTimerHandle(callback, fps), owner, tag);
        }

        /// <summary>
        /// 处理 Fixed Update 回调。
        /// </summary>
        /// <param name="callback">callback 参数。</param>
        /// <param name="owner">owner 参数。</param>
        /// <param name="tag">tag 参数。</param>
        /// <returns>执行结果。</returns>
        public FixedUpdateTimerHandle OnFixedUpdate(Action<TimerUpdateContext> callback, object owner = null, string tag = null)
        {
            return Register(new FixedUpdateTimerHandle(callback), owner, tag);
        }

        /// <summary>
        /// 处理 Fixed Update 回调。
        /// </summary>
        /// <param name="callback">callback 参数。</param>
        /// <param name="fps">fps 参数。</param>
        /// <param name="owner">owner 参数。</param>
        /// <param name="tag">tag 参数。</param>
        /// <returns>执行结果。</returns>
        public FixedUpdateTimerHandle OnFixedUpdate(Action<TimerUpdateContext> callback, float fps, object owner = null, string tag = null)
        {
            return Register(new FixedUpdateTimerHandle(callback, fps), owner, tag);
        }

        /// <summary>
        /// 注册 member。
        /// </summary>
        /// <param name="handle">handle 参数。</param>
        /// <param name="owner">owner 参数。</param>
        /// <param name="tag">tag 参数。</param>
        /// <returns>执行结果。</returns>
        public TimerHandle Register(TimerHandle handle, object owner = null, string tag = null)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            ValidateTickKind(handle.TickKind, nameof(handle));
            if (handle.Module == this)
            {
                var existingIndex = _handles.IndexOf(handle);
                if (existingIndex >= 0)
                {
                    if (!handle.IsCancelled && !handle.IsCompleted)
                    {
                        return handle;
                    }

                    RemoveCallbackHandle(handle);
                    handle.Detach();
                    _handles.RemoveAt(existingIndex);
                }
            }

            if (handle.Module != null)
            {
                handle.Module.Unregister(handle);
            }

            handle.Attach(this, owner, tag);
            _handles.Add(handle);
            return handle;
        }

        /// <summary>
        /// 注册 member。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        /// <param name="handle">handle 参数。</param>
        /// <param name="owner">owner 参数。</param>
        /// <param name="tag">tag 参数。</param>
        /// <returns>执行结果。</returns>
        public T Register<T>(T handle, object owner = null, string tag = null) where T : TimerHandle
        {
            return (T)Register((TimerHandle)handle, owner, tag);
        }

        /// <summary>
        /// 注销 member。
        /// </summary>
        /// <param name="handle">handle 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        public bool Unregister(TimerHandle handle)
        {
            if (handle == null)
            {
                return false;
            }

            for (var i = 0; i < _handles.Count; i++)
            {
                if (!ReferenceEquals(_handles[i], handle))
                {
                    continue;
                }

                handle.MarkCancelled();
                RemoveCallbackHandle(handle);
                handle.Detach();
                _handles.RemoveAt(i);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 执行 Cancel。
        /// </summary>
        /// <param name="handle">handle 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        public bool Cancel(TimerHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (handle.Module != this && !_handles.Contains(handle))
            {
                return false;
            }

            return handle.MarkCancelled();
        }

        /// <summary>
        /// 执行 Cancel Owner。
        /// </summary>
        /// <param name="owner">owner 参数。</param>
        /// <returns>执行结果。</returns>
        public int CancelOwner(object owner)
        {
            if (owner == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var handle in _handles)
            {
                if (!ReferenceEquals(handle.Owner, owner))
                {
                    continue;
                }

                if (handle.MarkCancelled())
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 设置计时器回调。
        /// </summary>
        /// <param name="handle">计时器句柄。</param>
        /// <param name="delay">延迟时间。</param>
        /// <param name="repeat">是否重复。</param>
        public void SetTimer(TimerHandle handle, float delay, bool repeat = false)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            ValidateDuration(delay, nameof(delay));
            Register(handle, handle.Owner, handle.Tag);
        }

        /// <summary>
        /// 清除计时器回调。
        /// </summary>
        /// <param name="handle">计时器句柄。</param>
        public void ClearTimer(TimerHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            Cancel(handle);
        }

        /// <summary>
        /// 设置计时器回调。
        /// </summary>
        /// <param name="callback">计时器回调函数。</param>
        /// <param name="delay">延迟时间。</param>
        /// <param name="repeat">是否重复。</param>
        public void SetTimer(Action<float> callback, float delay, bool repeat = false)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            ValidateDuration(delay, nameof(delay));
            ClearTimer(callback);
            TimerHandle handle;
            if (repeat)
            {
                handle = new TimerIntervalHandle(delay, callback);
            }
            else
            {
                handle = new TimerDelayHandle(delay, callback);
            }

            Register(handle);
            _callbackHandles[callback] = handle;
        }

        /// <summary>
        /// 清除计时器回调。
        /// </summary>
        /// <param name="callback">计时器回调函数。</param>
        public void ClearTimer(Action<float> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            if (_callbackHandles.TryGetValue(callback, out var handle))
            {
                Cancel(handle);
                _callbackHandles.Remove(callback);
            }
        }

        /// <summary>
        /// 执行 Update Timers。
        /// </summary>
        /// <param name="tickKind">tick Kind 参数。</param>
        /// <param name="context">context 参数。</param>
        /// <param name="phaseUnscaledDeltaTime">phase Unscaled Delta Time 参数。</param>
        private void UpdateTimers(TimerTickKind tickKind, in TimerUpdateContext context, float phaseUnscaledDeltaTime)
        {
            _dispatchBuffer.Clear();
            for (var i = 0; i < _handles.Count; i++)
            {
                var handle = _handles[i];
                if (handle.TickKind != tickKind || handle.IsCancelled || handle.IsCompleted)
                {
                    continue;
                }

                _dispatchBuffer.Add(handle);
            }

            for (var i = 0; i < _dispatchBuffer.Count; i++)
            {
                var handle = _dispatchBuffer[i];
                if (handle.Module != this)
                {
                    continue;
                }

                handle.Advance(in context, phaseUnscaledDeltaTime);
            }

            _dispatchBuffer.Clear();
            RemoveFinishedTimers();
        }

        /// <summary>
        /// 获取 Clock Time。
        /// </summary>
        /// <param name="useUnscaledTime">use Unscaled Time 参数。</param>
        /// <returns>执行结果。</returns>
        internal double GetClockTime(bool useUnscaledTime)
        {
            return useUnscaledTime ? UnscaledTime : Time;
        }

        /// <summary>
        /// 获取 Clock Time。
        /// </summary>
        /// <param name="tickKind">tick Kind 参数。</param>
        /// <param name="useUnscaledTime">use Unscaled Time 参数。</param>
        /// <returns>执行结果。</returns>
        internal double GetClockTime(TimerTickKind tickKind, bool useUnscaledTime)
        {
            ValidateTickKind(tickKind, nameof(tickKind));
            return GetClockTime(useUnscaledTime);
        }

        /// <summary>
        /// 移除 Finished Timers。
        /// </summary>
        private void RemoveFinishedTimers()
        {
            for (var i = _handles.Count - 1; i >= 0; i--)
            {
                var handle = _handles[i];
                if (!handle.IsCancelled && !handle.IsCompleted)
                {
                    continue;
                }

                RemoveCallbackHandle(handle);
                handle.Detach();
                _handles.RemoveAt(i);
            }
        }

        /// <summary>
        /// 移除 Callback Handle。
        /// </summary>
        /// <param name="handle">handle 参数。</param>
        private void RemoveCallbackHandle(TimerHandle handle)
        {
            if (handle is TimerDelayHandle delay && delay.LegacyCallback != null)
            {
                _callbackHandles.Remove(delay.LegacyCallback);
            }
            else if (handle is TimerIntervalHandle interval)
            {
                _callbackHandles.Remove(interval.Callback);
            }
        }

        /// <summary>
        /// 清理 All Timers。
        /// </summary>
        private void ClearAllTimers()
        {
            foreach (var handle in _handles)
            {
                handle.MarkCancelled();
                handle.Detach();
            }

            _handles.Clear();
            _dispatchBuffer.Clear();
            _callbackHandles.Clear();
        }

        /// <summary>
        /// 校验 Duration。
        /// </summary>
        /// <param name="value">value 参数。</param>
        /// <param name="paramName">param Name 参数。</param>
        private static void ValidateDuration(float value, string paramName)
        {
            if (value < 0f)
            {
                throw new ArgumentException("Timer duration cannot be negative.", paramName);
            }
        }

        /// <summary>
        /// 重置 Clock State。
        /// </summary>
        private void ResetClockState()
        {
            Tick = 0L;
            Time = 0d;
            UnscaledTime = 0d;
            DeltaTime = 0f;
            UnscaledDeltaTime = 0f;
        }

        /// <summary>
        /// 校验 Tick Kind。
        /// </summary>
        /// <param name="value">value 参数。</param>
        /// <param name="paramName">param Name 参数。</param>
        private static void ValidateTickKind(TimerTickKind value, string paramName)
        {
            switch (value)
            {
                case TimerTickKind.Update:
                case TimerTickKind.LateUpdate:
                case TimerTickKind.FixedUpdate:
                    return;
                default:
                    throw new ArgumentException("Timer tick kind is not supported.", paramName);
            }
        }
    }
}
