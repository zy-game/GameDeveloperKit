using UnityEngine;

namespace GameDeveloperKit.Timer
{
    /// <summary>
    /// 计时器模块分部定义。
    /// </summary>
    public sealed partial class TimerModule
    {
        /// <summary>
        /// 计时器MonoBehaviour桥接组件，用于按 Unity 生命周期推进计时器。
        /// </summary>
        private class Timer : MonoBehaviour
        {
            /// <summary>
            /// 计时器更新回调。
            /// </summary>
            public System.Action<TimerTickKind, float, float> onUpdate;

            /// <summary>
            /// Unity Update 回调。
            /// </summary>
            private void Update()
            {
                this.onUpdate?.Invoke(TimerTickKind.Update, UnityEngine.Time.deltaTime, UnityEngine.Time.unscaledDeltaTime);
            }

            /// <summary>
            /// Unity LateUpdate 回调。
            /// </summary>
            private void LateUpdate()
            {
                this.onUpdate?.Invoke(TimerTickKind.LateUpdate, UnityEngine.Time.deltaTime, UnityEngine.Time.unscaledDeltaTime);
            }

            /// <summary>
            /// Unity FixedUpdate 回调。
            /// </summary>
            private void FixedUpdate()
            {
                this.onUpdate?.Invoke(TimerTickKind.FixedUpdate, UnityEngine.Time.fixedDeltaTime, UnityEngine.Time.fixedUnscaledDeltaTime);
            }
        }
    }
}
