using System;
using UnityEngine;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 构建配置文件 (已废弃，保留用于兼容性)
    /// 请使用 PackageSettings 代替
    /// </summary>
    [Obsolete("Use PackageSettings instead")]
    [CreateAssetMenu(fileName = "BuildProfile", menuName = "GameFramework/Resource/Build Profile")]
    public class BuildProfile : ScriptableObject
    {
        /// <summary>
        /// 资源包名称
        /// </summary>
        public string packageName = "GameContent";
        
        /// <summary>
        /// 资源包版本
        /// </summary>
        public string version = "1.0.0";
        
        /// <summary>
        /// 构建设置
        /// </summary>
        public BuildSettings buildSettings = new BuildSettings();
        
        /// <summary>
        /// 构建说明
        /// </summary>
        [TextArea(3, 5)]
        public string description;
    }
}
