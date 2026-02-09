using System;
using UnityEditor;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 构建设置
    /// </summary>
    [Serializable]
    public class BuildSettings
    {
        /// <summary>
        /// 构建目标平台
        /// </summary>
        public BuildTarget buildTarget = BuildTarget.StandaloneWindows64;
        
        /// <summary>
        /// 输出路径（相对于项目根目录）
        /// </summary>
        public string outputPath = "Build/AssetBundles";
        
        /// <summary>
        /// 压缩方式
        /// </summary>
        public BuildAssetBundleOptions compression = BuildAssetBundleOptions.ChunkBasedCompression;
        
        /// <summary>
        /// 是否生成 Bundle 哈希值
        /// </summary>
        public bool generateHashForBundles = true;
        
        /// <summary>
        /// 是否复制到 StreamingAssets
        /// </summary>
        public bool copyToStreamingAssets = false;
        
        /// <summary>
        /// 是否启用增量构建
        /// </summary>
        public bool enableIncrementalBuild = true;
        
        /// <summary>
        /// 是否强制重新构建所有 Bundle
        /// </summary>
        public bool forceRebuild = false;
        
        /// <summary>
        /// Bundle 文件扩展名
        /// </summary>
        public string bundleExtension = ".bundle";
        
        /// <summary>
        /// 清单文件名
        /// </summary>
        public string manifestFileName = "manifest.json";
    }
}
