using System;
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
        private float _detailsTime;
        private TimerSettings _timerSettings;

        /// <summary>
        /// 当前计时器帧计数。
        /// </summary>
        public long Tick { get; private set; }

        /// <summary>
        /// 当前计时器累计时间。
        /// </summary>
        public float Time { get; private set; }

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
            Time = 0;
            var fps = this._timerSettings != null && this._timerSettings.FPS > 0 ? this._timerSettings.FPS : 50;
            this._detailsTime = 1000f / fps;
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 关闭计时器模块。
        /// </summary>
        /// <returns>模块关闭任务。</returns>
        public override UniTask Shutdown()
        {
            Object.DestroyImmediate(this._timer.gameObject);
            return UniTask.CompletedTask;
        }

        private void Update()
        {
            Tick++;
            Time += this._detailsTime;
        }

        /// <summary>
        /// 设置计时器回调。
        /// </summary>
        /// <param name="handle">计时器句柄。</param>
        /// <param name="delay">延迟时间。</param>
        /// <param name="repeat">是否重复。</param>
        public void SetTimer(TimerHandle handle, float delay, bool repeat = false)
        {

        }

        /// <summary>
        /// 清除计时器回调。
        /// </summary>
        /// <param name="handle">计时器句柄。</param>
        public void ClearTimer(TimerHandle handle)
        {

        }

        /// <summary>
        /// 设置计时器回调。
        /// </summary>
        /// <param name="callback">计时器回调函数。</param>
        /// <param name="delay">延迟时间。</param>
        /// <param name="repeat">是否重复。</param>
        public void SetTimer(Action<float> callback, float delay, bool repeat = false)
        {

        }

        /// <summary>
        /// 清除计时器回调。
        /// </summary>
        /// <param name="callback">计时器回调函数。</param>
        public void ClearTimer(Action<float> callback)
        {

        }
    }
}
