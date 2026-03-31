using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 框架启动配置（极简版）。
    /// </summary>
    [CreateAssetMenu(fileName = "GameFrameworkConfiguration", menuName = "GameDeveloperKit/Game Framework Configuration")]
    public sealed class GameFrameworkConfiguration : ScriptableObject
    {
        /// <summary>
        /// 资源运行模式。
        /// </summary>
        public ResourcePlayMode ResourcePlayMode = ResourcePlayMode.Offline;

        /// <summary>
        /// 默认资源包名。
        /// </summary>
        public string DefaultResourcePackageName = "Package1";

        /// <summary>
        /// 网关服务器地址。
        /// </summary>
        public string GatewayServerUrl;

        public static GameFrameworkConfiguration CreateRuntimeDefault()
        {
            return CreateInstance<GameFrameworkConfiguration>();
        }
    }
}
