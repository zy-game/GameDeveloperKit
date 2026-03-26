using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class PoolModule
    {
        /// <summary>
        /// 标记 GameObject 为对象池实例的组件。
        /// </summary>
        /// <remarks>
        /// 此组件自动添加到从对象池生成的所有 GameObject 上，用于跟踪对象来源。
        /// 通过此标记可以识别哪些对象是从池中生成的，以及它们对应的预制体。
        /// </remarks>
        private sealed class PooledInstanceMarker : MonoBehaviour
        {
            /// <summary>
            /// 获取或设置此实例对应的预制体。
            /// </summary>
            /// <remarks>
            /// 此字段记录创建此实例时所使用的预制体引用，可用于调试和验证目的。
            /// 系统在实例化时自动设置此值。
            /// </remarks>
            public GameObject Prefab;
        }
    }
}
