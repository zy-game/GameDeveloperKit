using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class ProcedureModule
    {
        /// <summary>
        /// 流程驱动器类，负责在每一帧更新流程模块。
        /// </summary>
        private sealed class ProcedureDriver : MonoBehaviour
        {
            private ProcedureModule _module;

            /// <summary>
            /// 初始化流程驱动器。
            /// </summary>
            /// <param name="module">流程模块。</param>
            public void Initialize(ProcedureModule module)
            {
                _module = module;
            }

            /// <summary>
            /// 在每一帧更新流程模块。
            /// </summary>
            private void Update()
            {
                _module?.Update(Time.deltaTime);
            }
        }
    }
}
