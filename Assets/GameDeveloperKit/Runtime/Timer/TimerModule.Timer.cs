using System;
using UnityEngine;

namespace GameDeveloperKit.Timer
{
    public sealed partial class TimerModule
    {
        private class Timer : MonoBehaviour
        {
            public Action onUpdate;

            private void FixedUpdate()
            {
                this.onUpdate?.Invoke();
            }
        }
    }
}