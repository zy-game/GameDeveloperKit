using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 按目录深度打包策略
    /// </summary>
    public class PackByDirectoryWithDepthStrategy : IPackStrategy
    {
        private readonly int _depth;
        private readonly bool _includeRootName;
        
        public PackByDirectoryWithDepthStrategy(int depth = 1, bool includeRootName = true)
        {
            _depth = Math.Max(1, depth);
            _includeRootName = includeRootName;
        }
        
        public Dictionary<string, List<CollectedAsset>> Pack(List<CollectedAsset> assets)
        {
            var bundles = new Dictionary<string, List<CollectedAsset>>();
            
            foreach (var asset in assets)
            {
                var directory = Path.GetDirectoryName(asset.assetPath)?.Replace('\\', '/') ?? "";
                var bundleName = GetBundleNameFromPath(directory);
                
                if (!bundles.ContainsKey(bundleName))
                {
                    bundles[bundleName] = new List<CollectedAsset>();
                }
                
                bundles[bundleName].Add(asset);
            }
            
            return bundles;
        }
        
        private string GetBundleNameFromPath(string directory)
        {
            var parts = directory.Split('/');
            var startIndex = _includeRootName ? 0 : 1;
            var endIndex = Math.Min(startIndex + _depth, parts.Length);
            
            if (endIndex <= startIndex)
                return "root";
            
            var relevantParts = parts.Skip(startIndex).Take(endIndex - startIndex);
            var name = string.Join("_", relevantParts);
            
            return string.IsNullOrEmpty(name) ? "root" : SanitizeName(name);
        }
        
        private string SanitizeName(string name)
        {
            return name.Replace(" ", "_").Replace("/", "_").Replace("\\", "_").ToLower();
        }
        
        public string GetDescription()
        {
            return $"Pack by directory with depth {_depth}";
        }
    }
    
    /// <summary>
    /// 按大小限制打包策略
    /// </summary>
    public class PackBySizeLimitStrategy : IPackStrategy
    {
        private readonly long _maxSize;
        private readonly string _prefix;
        
        public PackBySizeLimitStrategy(long maxSize, string prefix = "chunk")
        {
            _maxSize = maxSize;
            _prefix = prefix;
        }
        
        public Dictionary<string, List<CollectedAsset>> Pack(List<CollectedAsset> assets)
        {
            var bundles = new Dictionary<string, List<CollectedAsset>>();
            var currentBundleIndex = 0;
            var currentBundleSize = 0L;
            var currentBundleName = $"{_prefix}_{currentBundleIndex}";
            
            bundles[currentBundleName] = new List<CollectedAsset>();
            
            foreach (var asset in assets)
            {
                var fileSize = GetFileSize(asset.assetPath);
                
                if (currentBundleSize + fileSize > _maxSize && bundles[currentBundleName].Count > 0)
                {
                    currentBundleIndex++;
                    currentBundleName = $"{_prefix}_{currentBundleIndex}";
                    currentBundleSize = 0;
                    bundles[currentBundleName] = new List<CollectedAsset>();
                }
                
                bundles[currentBundleName].Add(asset);
                currentBundleSize += fileSize;
            }
            
            return bundles;
        }
        
        private long GetFileSize(string assetPath)
        {
            try
            {
                var fileInfo = new FileInfo(assetPath);
                return fileInfo.Exists ? fileInfo.Length : 0;
            }
            catch
            {
                return 0;
            }
        }
        
        public string GetDescription()
        {
            return $"Pack by size limit ({_maxSize / 1024 / 1024}MB)";
        }
    }
    
    /// <summary>
    /// 共享资源打包策略
    /// </summary>
    public class PackSharedAssetsStrategy : IPackStrategy
    {
        private readonly int _minRefCount;
        private readonly string _sharedBundleName;
        
        public PackSharedAssetsStrategy(int minRefCount = 2, string sharedBundleName = "shared")
        {
            _minRefCount = minRefCount;
            _sharedBundleName = sharedBundleName;
        }
        
        public Dictionary<string, List<CollectedAsset>> Pack(List<CollectedAsset> assets)
        {
            var bundles = new Dictionary<string, List<CollectedAsset>>();
            var refCounts = new Dictionary<string, int>();
            
            // 统计每个资源被引用的次数
            foreach (var asset in assets)
            {
                var deps = AssetDatabase.GetDependencies(asset.assetPath, false);
                foreach (var dep in deps)
                {
                    if (!refCounts.ContainsKey(dep))
                        refCounts[dep] = 0;
                    refCounts[dep]++;
                }
            }
            
            // 分离共享资源
            var sharedAssets = new List<CollectedAsset>();
            var normalAssets = new List<CollectedAsset>();
            
            foreach (var asset in assets)
            {
                if (refCounts.TryGetValue(asset.assetPath, out var count) && count >= _minRefCount)
                {
                    sharedAssets.Add(asset);
                }
                else
                {
                    normalAssets.Add(asset);
                }
            }
            
            // 共享资源打包到一个 Bundle
            if (sharedAssets.Count > 0)
            {
                bundles[_sharedBundleName] = sharedAssets;
            }
            
            // 普通资源按目录打包
            var directoryStrategy = new PackByDirectoryStrategy();
            var normalBundles = directoryStrategy.Pack(normalAssets);
            
            foreach (var kvp in normalBundles)
            {
                bundles[kvp.Key] = kvp.Value;
            }
            
            return bundles;
        }
        
        public string GetDescription()
        {
            return $"Extract shared assets (ref >= {_minRefCount}) to '{_sharedBundleName}'";
        }
    }
    
    /// <summary>
    /// 自定义规则打包策略
    /// </summary>
    public class PackByCustomRulesStrategy : IPackStrategy
    {
        private readonly List<PackRule> _rules;
        private readonly string _defaultBundleName;
        
        public PackByCustomRulesStrategy(List<PackRule> rules, string defaultBundleName = "default")
        {
            _rules = rules?.OrderByDescending(r => r.priority).ToList() ?? new List<PackRule>();
            _defaultBundleName = defaultBundleName;
        }
        
        public Dictionary<string, List<CollectedAsset>> Pack(List<CollectedAsset> assets)
        {
            var bundles = new Dictionary<string, List<CollectedAsset>>();
            
            foreach (var asset in assets)
            {
                var bundleName = GetBundleNameForAsset(asset);
                
                if (!bundles.ContainsKey(bundleName))
                {
                    bundles[bundleName] = new List<CollectedAsset>();
                }
                
                bundles[bundleName].Add(asset);
            }
            
            return bundles;
        }
        
        private string GetBundleNameForAsset(CollectedAsset asset)
        {
            foreach (var rule in _rules)
            {
                if (rule.Match(asset))
                {
                    return SanitizeName(rule.bundleName);
                }
            }
            
            return _defaultBundleName;
        }
        
        private string SanitizeName(string name)
        {
            return name.Replace(" ", "_").Replace("/", "_").Replace("\\", "_").ToLower();
        }
        
        public string GetDescription()
        {
            return $"Pack by custom rules ({_rules.Count} rules)";
        }
    }
}
