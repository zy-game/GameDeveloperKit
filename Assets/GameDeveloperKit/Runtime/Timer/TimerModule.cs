using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Timer
{
    /// <summary>
    /// 计时器模块，按固定帧率驱动注册的计时器回调。
    /// </summary>
    public sealed partial class TimerModule : GameModuleBase
    {
        private Timer _timer;
        private float _fixedDeltaTime;
        private TimerSettings _timerSettings;
        private readonly List<TimerHandle> _handles = new List<TimerHandle>();
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
            this._timerSettings = Resources.Load<TimerSettings>("TimerSettings");
            _timer = new GameObject("Timer").AddComponent<Timer>();
            _timer.onUpdate = Update;
            Object.DontDestroyOnLoad(_timer.gameObject);
            Tick = 0;
            Time = 0d;
            DeltaTime = 0f;
            UnscaledDeltaTime = 0f;
            _handles.Clear();
            _callbackHandles.Clear();
            var fps = this._timerSettings != null && this._timerSettings.FPS > 0 ? this._timerSettings.FPS : 50;
            this._fixedDeltaTime = 1f / fps;
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

        private void Update()
        {
            DeltaTime = this._fixedDeltaTime;
            UnscaledDeltaTime = this._fixedDeltaTime;
            Tick++;
            Time += DeltaTime;
            UpdateTimers();
        }

        public TimerSnapshot Snapshot()
        {
            var delays = new List<TimerDelayHandle>();
            var countdowns = new List<TimerCountdownHandle>();
            var intervals = new List<TimerIntervalHandle>();
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
            }

            return new TimerSnapshot(Tick, Time, DeltaTime, UnscaledDeltaTime, delays, countdowns, intervals);
        }

        public TimerDelayHandle Delay(float delay, Action callback, bool useUnscaledTime = false, object owner = null, string tag = null)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            ValidateDuration(delay, nameof(delay));
            var handle = new TimerDelayHandle(callback);
            AddHandle(handle, delay, false, useUnscaledTime, owner, tag);
            return handle;
        }

        public TimerCountdownHandle Countdown(float duration, Action<float> onTick = null, Action onComplete = null, bool useUnscaledTime = false, object owner = null, string tag = null)
        {
            ValidateDuration(duration, nameof(duration));
            var handle = new TimerCountdownHandle(onTick, onComplete);
            AddHandle(handle, duration, false, useUnscaledTime, owner, tag);
            return handle;
        }

        public TimerIntervalHandle Interval(float interval, Action<float> callback, bool useUnscaledTime = false, object owner = null, string tag = null)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            ValidateDuration(interval, nameof(interval));
            var handle = new TimerIntervalHandle(callback);
            AddHandle(handle, interval, true, useUnscaledTime, owner, tag);
            return handle;
        }

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
            AddHandle(handle, delay, repeat, false, handle.Owner, handle.Tag);
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
                handle = new TimerIntervalHandle(callback);
            }
            else
            {
                handle = new TimerDelayHandle(callback);
            }

            AddHandle(handle, delay, repeat, false, null, null);
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

        private void AddHandle(TimerHandle handle, float delay, bool repeat, bool useUnscaledTime, object owner, string tag)
        {
            if (handle.Module != null)
            {
                handle.Module.Cancel(handle);
            }

            handle.Schedule(this, delay, repeat, useUnscaledTime, owner, tag);
            if (!_handles.Contains(handle))
            {
                _handles.Add(handle);
            }
        }

        private void UpdateTimers()
        {
            var count = _handles.Count;
            for (var i = 0; i < count; i++)
            {
                var handle = _handles[i];
                var deltaTime = handle.UseUnscaledTime ? UnscaledDeltaTime : DeltaTime;
                handle.Advance(deltaTime, Time);
            }

            RemoveFinishedTimers();
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

                RemoveCallbackHandle(handle);
                handle.Detach();
                _handles.RemoveAt(i);
            }
        }

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

        private void ClearAllTimers()
        {
            foreach (var handle in _handles)
            {
                handle.MarkCancelled();
                handle.Detach();
            }

            _handles.Clear();
            _callbackHandles.Clear();
        }

        private static void ValidateDuration(float value, string paramName)
        {
            if (value < 0f)
            {
                throw new ArgumentException("Timer duration cannot be negative.", paramName);
            }
        }
    }
}
