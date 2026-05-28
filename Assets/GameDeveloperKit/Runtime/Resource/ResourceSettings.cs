using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源设置类，继承自ScriptableObject，用于存储和管理资源相关的设置和配置，包括资源加载模式、默认资源包列表和资源服务器URL等信息。这种设计模式有助于在Unity编辑器中方便地创建和编辑资源设置实例，并且可以通过脚本访问和使用这些设置来控制资源的加载和管理行为。通过使用ResourceSettings类，开发者可以更好地组织和优化资源的使用，提高游戏的性能和用户体验。 --- IGNORE ---
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceSettings", menuName = "GameDeveloperKit/ResourceSettings")]
    public sealed class ResourceSettings : ScriptableObject
    {
        private const string DefaultManifestName = "manifest.json";

        /// <summary>
        /// 资源模式，表示资源加载的模式，包括编辑器模拟、离线、在线和Web等模式。这些模式定义了资源加载和使用的不同方式，开发者可以根据需要选择适合的模式来控制资源的加载和管理行为，从而提高游戏的性能和用户体验。
        /// </summary>
        public ResourceMode Mode;

        /// <summary>
        /// 默认资源包列表，表示在资源加载过程中默认使用的资源包列表。这些资源包包含了游戏中需要加载和使用的各种资源，如纹理、模型、音频等。通过指定默认资源包列表，开发者可以确保在资源加载过程中能够正确地找到和加载所需的资源，从而提高游戏的性能和用户体验。
        /// </summary>
        public string[] DefaultPackages;

        /// <summary>
        /// 资源服务器URL。
        /// </summary>
        public string Url;

        /// <summary>
        /// 资源清单文件名或完整路径。
        /// </summary>
        public string ManifestName = DefaultManifestName;

        /// <summary>
        /// 资源缓存路径。
        /// </summary>
        public string CachePath;

        /// <summary>
        /// 旧版资源服务器URL字段。
        /// </summary>
        public string url;

        /// <summary>
        /// 获取当前有效的资源服务器URL。
        /// </summary>
        public string ServerUrl => string.IsNullOrWhiteSpace(url) ? Url : url;

        /// <summary>
        /// 获取资源清单加载位置。
        /// </summary>
        public string ManifestLocation
        {
            get
            {
                var manifestName = string.IsNullOrWhiteSpace(ManifestName) ? DefaultManifestName : ManifestName;
                if (string.IsNullOrWhiteSpace(ServerUrl))
                {
                    return manifestName;
                }

                return $"{ServerUrl.TrimEnd('/')}/{manifestName.TrimStart('/')}";
            }
        }
    }
}
