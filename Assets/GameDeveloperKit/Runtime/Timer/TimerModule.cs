using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Timer
{
    public sealed partial class TimerModule : GameModuleBase
    {
        private Timer _timer;
        private float _detailsTime;
        private TimerSettings _timerSettings;

        public long Tick { get; private set; }

        public float Time { get; private set; }

        public override UniTask Startup()
        {
            this._timerSettings = Resources.Load<TimerSettings>("TimerSettings");
            _timer = new GameObject("Timer").AddComponent<Timer>();
            _timer.onUpdate = Update;
            Object.DontDestroyOnLoad(_timer.gameObject);
            Time = 0;
            this._detailsTime = 1000f / this._timerSettings.FPS;
            return UniTask.CompletedTask;
        }

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
    }
}