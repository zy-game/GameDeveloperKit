using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 文件大小过滤器
    /// </summary>
    [Serializable]
    public class FileSizeFilter : IAssetFilter
    {
        [Tooltip("最小文件大小（字节）")]
        public long minSize = 0;
        
        [Tooltip("最大文件大小（字节），0 表示不限制")]
        public long maxSize = 0;
        
        public string Name => "文件大小过滤器";
        
        public bool Match(CollectedAsset asset)
        {
            var fileInfo = new FileInfo(asset.assetPath);
            if (!fileInfo.Exists)
                return false;
            
            var size = fileInfo.Length;
            
            if (size < minSize)
                return false;
            
            if (maxSize > 0 && size > maxSize)
                return false;
            
            return true;
        }
        
        public bool Validate(out string error)
        {
            if (minSize < 0)
            {
                error = "最小文件大小不能为负数";
                return false;
            }
            
            if (maxSize > 0 && maxSize < minSize)
            {
                error = "最大文件大小不能小于最小文件大小";
                return false;
            }
            
            error = null;
            return true;
        }
    }
    
    /// <summary>
    /// 扩展名过滤器
    /// </summary>
    [Serializable]
    public class ExtensionFilter : IAssetFilter
    {
        [Tooltip("包含的扩展名（如 .png, .jpg）")]
        public string[] includeExtensions;
        
        [Tooltip("排除的扩展名")]
        public string[] excludeExtensions;
        
        public string Name => "扩展名过滤器";
        
        public bool Match(CollectedAsset asset)
        {
            var ext = Path.GetExtension(asset.assetPath);
            
            // 检查排除列表
            if (excludeExtensions != null && excludeExtensions.Length > 0)
            {
                foreach (var excludeExt in excludeExtensions)
                {
                    if (string.Equals(ext, excludeExt, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            
            // 检查包含列表
            if (includeExtensions != null && includeExtensions.Length > 0)
            {
                foreach (var includeExt in includeExtensions)
                {
                    if (string.Equals(ext, includeExt, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
            
            return true;
        }
        
        public bool Validate(out string error)
        {
            error = null;
            return true;
        }
    }
    
    /// <summary>
    /// 路径模式过滤器
    /// </summary>
    [Serializable]
    public class PathPatternFilter : IAssetFilter
    {
        [Tooltip("路径模式（支持通配符 * 和 ?）")]
        public string pattern;
        
        [Tooltip("是否排除匹配的资源（true = 排除，false = 包含）")]
        public bool exclude = false;
        
        [Tooltip("使用正则表达式")]
        public bool useRegex = false;
        
        private Regex _compiledRegex;
        
        public string Name => "路径模式过滤器";
        
        public bool Match(CollectedAsset asset)
        {
            if (string.IsNullOrEmpty(pattern))
                return true;
            
            bool matches;
            
            if (useRegex)
            {
                _compiledRegex ??= new Regex(pattern, RegexOptions.IgnoreCase);
                matches = _compiledRegex.IsMatch(asset.assetPath);
            }
            else
            {
                matches = MatchWildcard(asset.assetPath, pattern);
            }
            
            return exclude ? !matches : matches;
        }
        
        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                error = "路径模式不能为空";
                return false;
            }
            
            if (useRegex)
            {
                try
                {
                    _ = new Regex(pattern);
                }
                catch (Exception ex)
                {
                    error = $"无效的正则表达式: {ex.Message}";
                    return false;
                }
            }
            
            error = null;
            return true;
        }
        
        private bool MatchWildcard(string input, string pattern)
        {
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
        }
    }
    
    /// <summary>
    /// 资源类型过滤器
    /// </summary>
    [Serializable]
    public class AssetTypeFilter : IAssetFilter
    {
        [Tooltip("包含的类型名称")]
        public string[] includeTypes;
        
        [Tooltip("排除的类型名称")]
        public string[] excludeTypes;
        
        public string Name => "资源类型过滤器";
        
        public bool Match(CollectedAsset asset)
        {
            if (asset.assetType == null)
                return false;
            
            var typeName = asset.assetType.Name;
            
            // 检查排除列表
            if (excludeTypes != null && excludeTypes.Length > 0)
            {
                if (excludeTypes.Any(t => string.Equals(t, typeName, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }
            
            // 检查包含列表
            if (includeTypes != null && includeTypes.Length > 0)
            {
                return includeTypes.Any(t => string.Equals(t, typeName, StringComparison.OrdinalIgnoreCase));
            }
            
            return true;
        }
        
        public bool Validate(out string error)
        {
            error = null;
            return true;
        }
    }
    
    /// <summary>
    /// 标签过滤器
    /// </summary>
    [Serializable]
    public class LabelFilter : IAssetFilter
    {
        [Tooltip("必须包含的标签")]
        public string[] requiredLabels;
        
        [Tooltip("必须排除的标签")]
        public string[] excludedLabels;
        
        public string Name => "标签过滤器";
        
        public bool Match(CollectedAsset asset)
        {
            var assetLabels = asset.labels ?? Array.Empty<string>();
            
            // 检查排除标签
            if (excludedLabels != null && excludedLabels.Length > 0)
            {
                foreach (var label in excludedLabels)
                {
                    if (assetLabels.Contains(label, StringComparer.OrdinalIgnoreCase))
                        return false;
                }
            }
            
            // 检查必须标签
            if (requiredLabels != null && requiredLabels.Length > 0)
            {
                foreach (var label in requiredLabels)
                {
                    if (!assetLabels.Contains(label, StringComparer.OrdinalIgnoreCase))
                        return false;
                }
            }
            
            return true;
        }
        
        public bool Validate(out string error)
        {
            error = null;
            return true;
        }
    }
}
