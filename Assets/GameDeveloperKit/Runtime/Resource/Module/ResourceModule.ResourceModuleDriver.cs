using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class ResourceModule
    {
        private sealed class ResourceModuleDriver : MonoBehaviour
        {
            private ResourceModule _module;

            /// <summary>
            /// 初始化资源模块驱动组件。
            /// </summary>
            /// <param name="module">所属的资源模块实例。</param>
            public void Initialize(ResourceModule module)
            {
                _module = module;
            }

            private void Update()
            {
                _module?.CollectUnused();
            }
        }
    }
}
