using System;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源包清单
    /// </summary>
    [Serializable]
    public class PackageManifest //: ScriptableObject
    {
        /// <summary>
        /// 包名
        /// </summary>
        public string name;
        /// <summary>
        /// 资源包版本
        /// </summary>
        public string version;
        
        /// <summary>
        /// AB包清单列表
        /// </summary>
        public BundleManifest[] bundles;
    }
}