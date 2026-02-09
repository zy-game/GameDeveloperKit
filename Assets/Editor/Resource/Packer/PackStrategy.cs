using System.Collections.Generic;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 打包策略接口
    /// </summary>
    public interface IPackStrategy
    {
        /// <summary>
        /// 将收集到的资源分组到 Bundle
        /// </summary>
        /// <param name="assets">收集到的资源列表</param>
        /// <returns>Bundle 分组结果（Bundle名称 -> 资源列表）</returns>
        Dictionary<string, List<CollectedAsset>> Pack(List<CollectedAsset> assets);
        
        /// <summary>
        /// 获取策略描述
        /// </summary>
        string GetDescription();
    }
    
    /// <summary>
    /// Bundle 分组信息
    /// </summary>
    public class BundleGroup
    {
        /// <summary>
        /// Bundle 名称
        /// </summary>
        public string bundleName;
        
        /// <summary>
        /// Bundle 中的资源列表
        /// </summary>
        public List<CollectedAsset> assets;
        
        public BundleGroup(string bundleName)
        {
            this.bundleName = bundleName;
            this.assets = new List<CollectedAsset>();
        }
    }
}
