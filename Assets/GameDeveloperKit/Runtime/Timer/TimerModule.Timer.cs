using System;
using UnityEngine;

namespace GameDeveloperKit.Timer
{
    /// <summary>
    /// 计时器模块分部定义。
    /// </summary>
    public sealed partial class TimerModule
    {
        /// <summary>
        /// 计时器MonoBehaviour桥接组件，用于接收Unity固定更新回调。
        /// </summary>
        private class Timer : MonoBehaviour
        {
            /// <summary>
            /// 计时器更新回调。
            /// </summary>
            public Action onUpdate;

            private void FixedUpdate()
            {
                this.onUpdate?.Invoke();
            }
        }
    }
}
