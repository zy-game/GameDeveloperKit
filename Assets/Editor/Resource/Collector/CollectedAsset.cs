using System;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 资源地址生成模式
    /// </summary>
    public enum AddressMode
    {
        /// <summary>
        /// 使用完整路径作为地址
        /// </summary>
        FullPath,
        
        /// <summary>
        /// 使用相对于目录的路径作为地址
        /// </summary>
        RelativeToDirectory,
        
        /// <summary>
        /// 使用文件名（不含扩展名）作为地址
        /// </summary>
        FileName,
        
        /// <summary>
        /// 使用文件名（含扩展名）作为地址
        /// </summary>
        FileNameWithExtension
    }
    
    /// <summary>
    /// 收集到的资源信息
    /// </summary>
    [Serializable]
    public class CollectedAsset
    {
        /// <summary>
        /// 资源路径（相对于 Assets/ 的完整路径）
        /// </summary>
        public string assetPath;
        
        /// <summary>
        /// 资源 GUID
        /// </summary>
        public string guid;
        
        /// <summary>
        /// 资源寻址地址（用于加载）
        /// </summary>
        public string address;
        
        /// <summary>
        /// 资源名称
        /// </summary>
        public string name;
        
        /// <summary>
        /// 资源标签
        /// </summary>
        public string[] labels;
        
        /// <summary>
        /// 资源类型
        /// </summary>
        public Type assetType;
        
        /// <summary>
        /// 分组标识（用于打包策略）
        /// </summary>
        public string groupTag;
        
        public CollectedAsset() { }
        
        public CollectedAsset(string assetPath, string guid, string address, string name, string[] labels, Type assetType, string groupTag = "")
        {
            this.assetPath = assetPath;
            this.guid = guid;
            this.address = address;
            this.name = name;
            this.labels = labels ?? Array.Empty<string>();
            this.assetType = assetType;
            this.groupTag = groupTag;
        }
    }
}
