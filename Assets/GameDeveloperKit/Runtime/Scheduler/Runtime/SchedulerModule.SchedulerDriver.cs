using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class SchedulerModule
    {
        private sealed class SchedulerDriver : MonoBehaviour
        {
            private SchedulerModule _module;

            /// <summary>
            /// 初始化调度模块驱动组件。
            /// </summary>
            /// <param name="module">所属的调度模块实例。</param>
            public void Initialize(SchedulerModule module)
            {
                _module = module;
            }

            private void Update()
            {
                _module?.Update(Time.unscaledTimeAsDouble);
            }
        }
    }
}
