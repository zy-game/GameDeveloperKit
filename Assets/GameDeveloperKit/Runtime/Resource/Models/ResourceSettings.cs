using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源设置，定义资源加载的配置。
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceSettings", menuName = "GameDeveloperKit/Resource Settings")]
    public sealed class ResourceSettings : ScriptableObject
    {
        /// <summary>
        /// 资源播放模式。
        /// </summary>
        public ResourcePlayMode PlayMode;

        /// <summary>
        /// 资源包定义列表。
        /// </summary>
        public List<ResourcePackageDefinition> Packages = new();
    }
}
