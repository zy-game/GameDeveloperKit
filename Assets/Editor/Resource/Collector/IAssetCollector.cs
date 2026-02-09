using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 资源收集器接口
    /// </summary>
    public interface IAssetCollector
    {
        /// <summary>
        /// 收集器名称（用于 UI 显示）
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// 收集资源
        /// </summary>
        IEnumerable<CollectedAsset> Collect(CollectorContext context);
        
        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        bool Validate(out string error);
    }
    
    /// <summary>
    /// 收集器上下文
    /// </summary>
    [Serializable]
    public class CollectorContext
    {
        /// <summary>
        /// Package 名称
        /// </summary>
        public string PackageName { get; set; }
        
        /// <summary>
        /// 地址生成模式
        /// </summary>
        public AddressMode AddressMode { get; set; }
        
        /// <summary>
        /// 默认标签
        /// </summary>
        public string[] DefaultLabels { get; set; }
        
        /// <summary>
        /// 基础目录（用于相对路径计算）
        /// </summary>
        public string BaseDirectory { get; set; }
        
        public CollectorContext()
        {
            PackageName = string.Empty;
            AddressMode = AddressMode.FullPath;
            DefaultLabels = Array.Empty<string>();
            BaseDirectory = "Assets";
        }
        
        public CollectorContext(string packageName, AddressMode addressMode)
        {
            PackageName = packageName;
            AddressMode = addressMode;
            DefaultLabels = Array.Empty<string>();
            BaseDirectory = "Assets";
        }
    }
}
