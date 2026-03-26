using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class UIModule
    {
        /// <summary>
        /// UI 模块运行驱动组件，用于转发 Unity 生命周期更新。
        /// </summary>
        private sealed class UIModuleDriver : MonoBehaviour
        {
            private UIModule _module;

            /// <summary>
            /// 初始化驱动组件。
            /// </summary>
            /// <param name="module">所属的 UI 模块实例。</param>
            public void Initialize(UIModule module)
            {
                _module = module;
            }

            private void Update()
            {
                _module?.UpdateSafeAreaIfNeeded();
            }
        }
    }
}
