using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 按文件打包策略（每个文件一个 Bundle）
    /// </summary>
    public class PackByFileStrategy : IPackStrategy
    {
        public Dictionary<string, List<CollectedAsset>> Pack(List<CollectedAsset> assets)
        {
            var bundles = new Dictionary<string, List<CollectedAsset>>();
            
            foreach (var asset in assets)
            {
                var bundleName = SanitizeBundleName(asset.assetPath);
                bundles[bundleName] = new List<CollectedAsset> { asset };
            }
            
            return bundles;
        }
        
        private string SanitizeBundleName(string assetPath)
        {
            // 移除 Assets/ 前缀，替换特殊字符
            var name = assetPath.Replace("Assets/", "").Replace("/", "_").Replace("\\", "_");
            return name.ToLower();
        }
        
        public string GetDescription()
        {
            return "Pack each file into a separate bundle";
        }
    }
    
    /// <summary>
    /// 按目录打包策略（同一目录的资源打包到一个 Bundle）
    /// </summary>
    public class PackByDirectoryStrategy : IPackStrategy
    {
        public Dictionary<string, List<CollectedAsset>> Pack(List<CollectedAsset> assets)
        {
            var bundles = new Dictionary<string, List<CollectedAsset>>();
            
            foreach (var asset in assets)
            {
                var directory = Path.GetDirectoryName(asset.assetPath)?.Replace('\\', '/') ?? "";
                var bundleName = SanitizeBundleName(directory);
                
                if (!bundles.ContainsKey(bundleName))
                {
                    bundles[bundleName] = new List<CollectedAsset>();
                }
                
                bundles[bundleName].Add(asset);
            }
            
            return bundles;
        }
        
        private string SanitizeBundleName(string directory)
        {
            // 移除 Assets/ 前缀，替换特殊字符
            var name = directory.Replace("Assets/", "").Replace("/", "_").Replace("\\", "_");
            return string.IsNullOrEmpty(name) ? "root" : name.ToLower();
        }
        
        public string GetDescription()
        {
            return "Pack assets in the same directory into one bundle";
        }
    }
    
    /// <summary>
    /// 按标签打包策略（相同标签的资源打包到一个 Bundle）
    /// </summary>
    public class PackByLabelStrategy : IPackStrategy
    {
        public Dictionary<string, List<CollectedAsset>> Pack(List<CollectedAsset> assets)
        {
            var bundles = new Dictionary<string, List<CollectedAsset>>();
            
            foreach (var asset in assets)
            {
                if (asset.labels == null || asset.labels.Length == 0)
                {
                    // 没有标签的资源单独打包
                    var bundleName = $"unlabeled_{SanitizeName(asset.name)}";
                    bundles[bundleName] = new List<CollectedAsset> { asset };
                }
                else
                {
                    // 使用第一个标签作为 Bundle 名称
                    var label = asset.labels[0];
                    var bundleName = $"label_{SanitizeName(label)}";
                    
                    if (!bundles.ContainsKey(bundleName))
                    {
                        bundles[bundleName] = new List<CollectedAsset>();
                    }
                    
                    bundles[bundleName].Add(asset);
                }
            }
            
            return bundles;
        }
        
        private string SanitizeName(string name)
        {
            return name.Replace(" ", "_").Replace("/", "_").Replace("\\", "_").ToLower();
        }
        
        public string GetDescription()
        {
            return "Pack assets with the same label into one bundle";
        }
    }
    
    /// <summary>
    /// 按类型打包策略（相同类型的资源打包到一个 Bundle）
    /// </summary>
    public class PackByTypeStrategy : IPackStrategy
    {
        public Dictionary<string, List<CollectedAsset>> Pack(List<CollectedAsset> assets)
        {
            var bundles = new Dictionary<string, List<CollectedAsset>>();
            
            foreach (var asset in assets)
            {
                var typeName = asset.assetType?.Name ?? "unknown";
                var bundleName = $"type_{typeName.ToLower()}";
                
                if (!bundles.ContainsKey(bundleName))
                {
                    bundles[bundleName] = new List<CollectedAsset>();
                }
                
                bundles[bundleName].Add(asset);
            }
            
            return bundles;
        }
        
        public string GetDescription()
        {
            return "Pack assets of the same type into one bundle";
        }
    }
    
    /// <summary>
    /// 全部打包策略（所有资源打包到一个 Bundle）
    /// </summary>
    public class PackTogetherStrategy : IPackStrategy
    {
        private string _bundleName = "all_assets";
        
        public PackTogetherStrategy(string bundleName = "all_assets")
        {
            _bundleName = bundleName;
        }
        
        public Dictionary<string, List<CollectedAsset>> Pack(List<CollectedAsset> assets)
        {
            return new Dictionary<string, List<CollectedAsset>>
            {
                { _bundleName, assets.ToList() }
            };
        }
        
        public string GetDescription()
        {
            return $"Pack all assets into one bundle: {_bundleName}";
        }
    }
    
    /// <summary>
    /// 按分组标签打包策略（使用 CollectedAsset 的 groupTag）
    /// </summary>
    public class PackByGroupTagStrategy : IPackStrategy
    {
        public Dictionary<string, List<CollectedAsset>> Pack(List<CollectedAsset> assets)
        {
            var bundles = new Dictionary<string, List<CollectedAsset>>();
            
            foreach (var asset in assets)
            {
                var groupTag = string.IsNullOrEmpty(asset.groupTag) ? "default" : asset.groupTag;
                var bundleName = SanitizeName(groupTag);
                
                if (!bundles.ContainsKey(bundleName))
                {
                    bundles[bundleName] = new List<CollectedAsset>();
                }
                
                bundles[bundleName].Add(asset);
            }
            
            return bundles;
        }
        
        private string SanitizeName(string name)
        {
            return name.Replace(" ", "_").Replace("/", "_").Replace("\\", "_").ToLower();
        }
        
        public string GetDescription()
        {
            return "Pack assets by their group tag";
        }
    }
}
