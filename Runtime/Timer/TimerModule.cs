using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Debugger;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Timer
{
    /// <summary>
    /// 计时器模块，按 Unity 生命周期驱动注册的计时器回调。
    /// </summary>
    public sealed partial class TimerModule : GameModuleBase
    {
        private Timer _timer;
        private readonly List<TimerHandle> _handles = new List<TimerHandle>();
        private readonly List<TimerHandle> _dispatchBuffer = new List<TimerHandle>();
        private readonly TimerProfileHandle m_ProfileHandle;

        /// <summary>
        /// 初始化 Timer Module。
        /// </summary>
        public TimerModule()
        {
            m_ProfileHandle = new TimerProfileHandle(this);
        }

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
        public override void Startup()
        {
            if (_timer != null)
            {
                return;
            }

            ResetClockState();
            _handles.Clear();
            _dispatchBuffer.Clear();
            _timer = new GameObject("Timer").AddComponent<Timer>();
            _timer.onUpdate = Update;
            Object.DontDestroyOnLoad(_timer.gameObject);
            TryRegisterDebugProfile();
        }

        /// <summary>
        /// 关闭计时器模块。
        /// </summary>
        public override void Shutdown()
        {
            if (this._timer != null)
            {
                this._timer.onUpdate = null;
                Object.DestroyImmediate(this._timer.gameObject);
                this._timer = null;
            }

            TryUnregisterDebugProfile();
            ClearAllTimers();
        }

        /// <summary>
        /// Unity Update 回调。
        /// </summary>
        internal void Update(float deltaTime, float unscaledDeltaTime)
        {
            Update(TimerTickKind.Update, deltaTime, unscaledDeltaTime);
        }

        /// <summary>
        /// Unity Update 回调。
        /// </summary>
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
        public TimerCountdownHandle Countdown(float duration, Action<float> onTick = null, Action onComplete = null, bool useUnscaledTime = false, object owner = null, string tag = null)
        {
            return Register(new TimerCountdownHandle(duration, onTick, onComplete, useUnscaledTime), owner, tag);
        }

        /// <summary>
        /// 执行 Interval。
        /// </summary>
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
        public UpdateTimerHandle OnUpdate(Action callback, object owner = null, string tag = null)
        {
            return Register(new UpdateTimerHandle(callback), owner, tag);
        }

        /// <summary>
        /// 处理 Update 回调。
        /// </summary>
        public UpdateTimerHandle OnUpdate(Action<TimerUpdateContext> callback, object owner = null, string tag = null)
        {
            return Register(new UpdateTimerHandle(callback), owner, tag);
        }

        /// <summary>
        /// 处理 Late Update 回调。
        /// </summary>
        public LateUpdateTimerHandle OnLateUpdate(Action callback, object owner = null, string tag = null)
        {
            return Register(new LateUpdateTimerHandle(callback), owner, tag);
        }

        /// <summary>
        /// 处理 Late Update 回调。
        /// </summary>
        public LateUpdateTimerHandle OnLateUpdate(Action callback, float fps, object owner = null, string tag = null)
        {
            return Register(new LateUpdateTimerHandle(callback, fps), owner, tag);
        }

        /// <summary>
        /// 处理 Late Update 回调。
        /// </summary>
        public LateUpdateTimerHandle OnLateUpdate(Action<TimerUpdateContext> callback, object owner = null, string tag = null)
        {
            return Register(new LateUpdateTimerHandle(callback), owner, tag);
        }

        /// <summary>
        /// 处理 Late Update 回调。
        /// </summary>
        public LateUpdateTimerHandle OnLateUpdate(Action<TimerUpdateContext> callback, float fps, object owner = null, string tag = null)
        {
            return Register(new LateUpdateTimerHandle(callback, fps), owner, tag);
        }

        /// <summary>
        /// 处理 Fixed Update 回调。
        /// </summary>
        public FixedUpdateTimerHandle OnFixedUpdate(Action callback, object owner = null, string tag = null)
        {
            return Register(new FixedUpdateTimerHandle(callback), owner, tag);
        }

        /// <summary>
        /// 处理 Fixed Update 回调。
        /// </summary>
        public FixedUpdateTimerHandle OnFixedUpdate(Action callback, float fps, object owner = null, string tag = null)
        {
            return Register(new FixedUpdateTimerHandle(callback, fps), owner, tag);
        }

        /// <summary>
        /// 处理 Fixed Update 回调。
        /// </summary>
        public FixedUpdateTimerHandle OnFixedUpdate(Action<TimerUpdateContext> callback, object owner = null, string tag = null)
        {
            return Register(new FixedUpdateTimerHandle(callback), owner, tag);
        }

        /// <summary>
        /// 处理 Fixed Update 回调。
        /// </summary>
        public FixedUpdateTimerHandle OnFixedUpdate(Action<TimerUpdateContext> callback, float fps, object owner = null, string tag = null)
        {
            return Register(new FixedUpdateTimerHandle(callback, fps), owner, tag);
        }

        /// <summary>
        /// 注册 member。
        /// </summary>
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
        public T Register<T>(T handle, object owner = null, string tag = null) where T : TimerHandle
        {
            return (T)Register((TimerHandle)handle, owner, tag);
        }

        /// <summary>
        /// 注销 member。
        /// </summary>
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
                handle.Detach();
                _handles.RemoveAt(i);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 执行 Cancel。
        /// </summary>
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
        /// 注册 Debug Profile。
        /// </summary>
        internal void RegisterDebugProfile(DebugModule debug)
        {
            if (debug == null)
            {
                throw new ArgumentNullException(nameof(debug));
            }

            debug.RegisterProfile(m_ProfileHandle);
        }

        /// <summary>
        /// 注销 Debug Profile。
        /// </summary>
        internal void UnregisterDebugProfile(DebugModule debug)
        {
            if (debug == null)
            {
                throw new ArgumentNullException(nameof(debug));
            }

            debug.UnregisterProfile(m_ProfileHandle);
        }

        /// <summary>
        /// 执行 Cancel Owner。
        /// </summary>
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
        internal double GetClockTime(bool useUnscaledTime)
        {
            return useUnscaledTime ? UnscaledTime : Time;
        }

        /// <summary>
        /// 获取 Clock Time。
        /// </summary>
        internal double GetClockTime(TimerTickKind tickKind, bool useUnscaledTime)
        {
            ValidateTickKind(tickKind, nameof(tickKind));
            return GetClockTime(useUnscaledTime);
        }

        private void RemoveFinishedTimers()
        {
            for (var i = _handles.Count - 1; i >= 0; i--)
            {
                var handle = _handles[i];
                if (!handle.IsCancelled && !handle.IsCompleted)
                {
                    continue;
                }

                handle.Detach();
                _handles.RemoveAt(i);
            }
        }

        private void ClearAllTimers()
        {
            foreach (var handle in _handles)
            {
                handle.MarkCancelled();
                handle.Detach();
            }

            _handles.Clear();
            _dispatchBuffer.Clear();
        }

        private void TryRegisterDebugProfile()
        {
            base.TryRegisterDebugProfile(m_ProfileHandle);
        }

        private void TryUnregisterDebugProfile()
        {
            base.TryUnregisterDebugProfile(m_ProfileHandle);
        }

        private static void ValidateDuration(float value, string paramName)
        {
            if (value < 0f)
            {
                throw new ArgumentException("Timer duration cannot be negative.", paramName);
            }
        }

        private void ResetClockState()
        {
            Tick = 0L;
            Time = 0d;
            UnscaledTime = 0d;
            DeltaTime = 0f;
            UnscaledDeltaTime = 0f;
        }

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
