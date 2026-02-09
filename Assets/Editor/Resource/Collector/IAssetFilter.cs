using System;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 资源过滤器接口
    /// </summary>
    public interface IAssetFilter
    {
        /// <summary>
        /// 过滤器名称
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// 判断资源是否匹配过滤条件
        /// </summary>
        /// <param name="asset">待检查的资源</param>
        /// <returns>true 表示保留，false 表示过滤掉</returns>
        bool Match(CollectedAsset asset);
        
        /// <summary>
        /// 验证过滤器配置是否有效
        /// </summary>
        bool Validate(out string error);
    }
}
