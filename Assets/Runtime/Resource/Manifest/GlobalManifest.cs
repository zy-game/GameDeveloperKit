using System;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 全局资源清单
    /// 管理所有package的版本信息
    /// </summary>
    [Serializable]
    public class VersionManifest
    {
        /// <summary>
        /// 清单版本
        /// </summary>
        public string version = "1.0";
        
        /// <summary>
        /// 更新时间
        /// </summary>
        public string updateTime;
        
        /// <summary>
        /// 所有package的版本信息
        /// </summary>
        public PackageVersionInfo[] packages;
    }
    
    /// <summary>
    /// Package版本信息
    /// </summary>
    [Serializable]
    public class PackageVersionInfo
    {
        /// <summary>
        /// Package名称
        /// </summary>
        public string name;
        
        /// <summary>
        /// 当前版本（最新版本）
        /// </summary>
        public string currentVersion;
        
        /// <summary>
        /// Package类型：BasePackage/HotfixPackage
        /// </summary>
        public string packageType;
        
        /// <summary>
        /// 历史版本列表（按时间倒序）
        /// </summary>
        public VersionDetail[] versions;
    }
    
    /// <summary>
    /// 版本详情
    /// </summary>
    [Serializable]
    public class VersionDetail
    {
        /// <summary>
        /// 版本号
        /// </summary>
        public string version;
        
        /// <summary>
        /// 构建时间
        /// </summary>
        public string buildTime;
        
        /// <summary>
        /// 总大小（字节）
        /// </summary>
        public long size;
        
        /// <summary>
        /// Bundle数量
        /// </summary>
        public int bundleCount;
        
        /// <summary>
        /// Package清单相对路径
        /// 例如: NewPackage/1.0.0/newpackage.json
        /// </summary>
        public string manifestPath;
    }
}
