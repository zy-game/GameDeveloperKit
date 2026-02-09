using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 资源快照信息 - 用于追踪资源内部变动
    /// </summary>
    [Serializable]
    public class AssetSnapshot
    {
        public string assetPath;              // 资源路径
        public string assetGuid;              // GUID
        public string contentHash;            // 内容哈希 (AssetDatabase.GetAssetDependencyHash)
        public long fileSize;                 // 文件大小
        public string lastModified;           // 最后修改时间 (序列化为字符串)
        public string assetType;              // 资源类型 (Prefab, Texture2D, etc.)
        public string bundleName;             // AssetBundle 分配
    }
    
    /// <summary>
    /// 文件差异行
    /// </summary>
    [Serializable]
    public class FileDiffLine
    {
        public string type;           // "added", "removed", "modified"
        public string line;           // 完整的行内容
        public int lineNumber;        // 行号
    }

    /// <summary>
    /// Bundle快照信息
    /// </summary>
    [Serializable]
    public class BundleSnapshot
    {
        public string bundleName;
        public long size;
        public string bundleHash;             // Bundle文件哈希
        public List<string> assetPaths;       // 包含的资源路径列表

        public BundleSnapshot()
        {
            assetPaths = new List<string>();
        }
    }

    /// <summary>
    /// 单次构建历史记录
    /// </summary>
    [Serializable]
    public class BuildHistoryRecord
    {
        public string buildId;                // 构建唯一ID (GUID)
        public string buildTime;              // 构建时间 (序列化为字符串)
        public string packageName;            // 包名
        public string packageVersion;         // 包版本
        public bool success;                  // 是否成功
        public float buildDuration;           // 构建耗时(秒)
        public string errorMessage;           // 错误信息（失败时记录）
        public string failedTask;             // 失败的任务名（失败时记录）
        
        // Bundle信息
        public int totalBundles;              
        public long totalSize;
        public List<BundleSnapshot> bundles;  // Bundle快照列表 (用List便于序列化)
        
        // 资源快照 (核心 - 用于对比)
        public List<AssetSnapshot> assetSnapshots;  // 资源快照列表

        public BuildHistoryRecord()
        {
            bundles = new List<BundleSnapshot>();
            assetSnapshots = new List<AssetSnapshot>();
        }
    }

    /// <summary>
    /// 构建历史记录列表 (用于JSON序列化)
    /// </summary>
    [Serializable]
    public class BuildHistoryList
    {
        public List<BuildHistoryRecord> records;

        public BuildHistoryList()
        {
            records = new List<BuildHistoryRecord>();
        }
    }

    /// <summary>
    /// 资源变动类型
    /// </summary>
    public enum AssetChangeType
    {
        Added,              // 新增资源
        Removed,            // 删除资源
        ContentModified,    // 内容修改 (Prefab组件变化、Texture设置变化等)
        BundleMoved,        // 移动到其他Bundle
        SizeChanged         // 仅大小变化
    }

    /// <summary>
    /// 资源变动详情
    /// </summary>
    public class AssetChangeDetail
    {
        public AssetChangeType type;
        public string assetPath;
        public string assetType;
        public string fromBundle;             // 原Bundle
        public string toBundle;               // 新Bundle
        public string oldHash;                // 旧哈希
        public string newHash;                // 新哈希
        public long oldSize;
        public long newSize;
        public long sizeDiff;
        public List<FileDiffLine> fileDifferences;  // 文件差异（Git diff 风格）
        
        /// <summary>
        /// 获取详细变动描述
        /// </summary>
        public string GetDetailedDescription()
        {
            switch(type)
            {
                case AssetChangeType.ContentModified:
                    var oldHashShort = oldHash?.Length > 8 ? oldHash.Substring(0, 8) : oldHash;
                    var newHashShort = newHash?.Length > 8 ? newHash.Substring(0, 8) : newHash;
                    return $"资源内容已修改 (hash: {oldHashShort} → {newHashShort})";
                case AssetChangeType.Added:
                    return $"新增资源 ({FormatBytes(newSize)})";
                case AssetChangeType.Removed:
                    return $"删除资源 ({FormatBytes(oldSize)})";
                case AssetChangeType.BundleMoved:
                    return $"从 {fromBundle} 移动到 {toBundle}";
                case AssetChangeType.SizeChanged:
                    return $"大小变化 ({FormatBytes(oldSize)} → {FormatBytes(newSize)})";
                default:
                    return type.ToString();
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F2} KB";
            else if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / 1024.0 / 1024.0:F2} MB";
            else
                return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
        }
    }

    /// <summary>
    /// 构建对比结果
    /// </summary>
    public class BuildCompareResult
    {
        public BuildHistoryRecord current;
        public BuildHistoryRecord previous;
        
        // Bundle级变动
        public List<string> bundlesAdded;         // 新增的Bundle
        public List<string> bundlesRemoved;       // 删除的Bundle
        public Dictionary<string, long> bundleSizeChanges;  // Bundle大小变化
        
        // 资源级变动
        public List<AssetChangeDetail> assetChanges;  // 所有资源变动
        public int assetsAdded;
        public int assetsRemoved;
        public int assetsModified;                // 内容修改的资源数
        public int assetsMoved;
        
        // 统计信息
        public long totalSizeDiff;
        public float buildTimeDiff;

        public BuildCompareResult()
        {
            bundlesAdded = new List<string>();
            bundlesRemoved = new List<string>();
            bundleSizeChanges = new Dictionary<string, long>();
            assetChanges = new List<AssetChangeDetail>();
        }
    }
}
