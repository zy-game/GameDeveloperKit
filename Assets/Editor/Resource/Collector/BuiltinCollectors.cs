using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 目录收集器 - 按文件夹路径收集资源
    /// </summary>
    [Serializable]
    public class DirectoryCollector : IAssetCollector
    {
        [Tooltip("要收集的目录路径")]
        public string directoryPath = "Assets";
        
        [Tooltip("搜索模式（如 *.prefab, *.png）")]
        public string[] searchPatterns = { "*.*" };
        
        [Tooltip("是否递归搜索子目录")]
        public bool recursive = true;
        
        [Tooltip("排除的文件模式")]
        public string[] excludePatterns = { "*.meta", "*.cs" };
        
        public string Name => "目录收集器";
        
        public IEnumerable<CollectedAsset> Collect(CollectorContext context)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
                yield break;
            
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var patterns = searchPatterns != null && searchPatterns.Length > 0 ? searchPatterns : new[] { "*.*" };
            var processedPaths = new HashSet<string>();
            
            foreach (var pattern in patterns)
            {
                string[] files;
                try
                {
                    files = Directory.GetFiles(directoryPath, pattern, searchOption);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DirectoryCollector] Error scanning pattern '{pattern}': {ex.Message}");
                    continue;
                }
                
                foreach (var file in files)
                {
                    var assetPath = file.Replace('\\', '/');
                    
                    if (processedPaths.Contains(assetPath))
                        continue;
                    
                    if (IsExcluded(assetPath))
                        continue;
                    
                    var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                    if (assetType == null)
                        continue;
                    
                    processedPaths.Add(assetPath);
                    
                    var guid = AssetDatabase.AssetPathToGUID(assetPath);
                    var address = GenerateAddress(assetPath, context);
                    var name = Path.GetFileNameWithoutExtension(assetPath);
                    var groupTag = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "";
                    
                    yield return new CollectedAsset(
                        assetPath,
                        guid,
                        address,
                        name,
                        context.DefaultLabels,
                        assetType,
                        groupTag
                    );
                }
            }
        }
        
        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                error = "目录路径不能为空";
                return false;
            }
            
            if (!Directory.Exists(directoryPath))
            {
                error = $"目录不存在: {directoryPath}";
                return false;
            }
            
            error = null;
            return true;
        }
        
        private bool IsExcluded(string assetPath)
        {
            if (excludePatterns == null)
                return false;
            
            var fileName = Path.GetFileName(assetPath);
            foreach (var pattern in excludePatterns)
            {
                if (MatchWildcard(fileName, pattern))
                    return true;
            }
            return false;
        }
        
        private bool MatchWildcard(string input, string pattern)
        {
            if (pattern.StartsWith("*."))
            {
                var ext = pattern.Substring(1);
                return input.EndsWith(ext, StringComparison.OrdinalIgnoreCase);
            }
            return string.Equals(input, pattern, StringComparison.OrdinalIgnoreCase);
        }
        
        private string GenerateAddress(string assetPath, CollectorContext context)
        {
            return context.AddressMode switch
            {
                AddressMode.FullPath => assetPath,
                AddressMode.RelativeToDirectory => assetPath.Replace(directoryPath + "/", ""),
                AddressMode.FileName => Path.GetFileNameWithoutExtension(assetPath),
                AddressMode.FileNameWithExtension => Path.GetFileName(assetPath),
                _ => assetPath
            };
        }
    }
    
    /// <summary>
    /// 标签收集器 - 按 Unity Asset Label 收集资源
    /// </summary>
    [Serializable]
    public class LabelCollector : IAssetCollector
    {
        [Tooltip("要收集的标签列表")]
        public string[] labels = Array.Empty<string>();
        
        [Tooltip("搜索路径")]
        public string searchPath = "Assets";
        
        public string Name => "标签收集器";
        
        public IEnumerable<CollectedAsset> Collect(CollectorContext context)
        {
            if (labels == null || labels.Length == 0)
                yield break;
            
            var processedGuids = new HashSet<string>();
            
            foreach (var label in labels)
            {
                if (string.IsNullOrEmpty(label))
                    continue;
                
                var guids = AssetDatabase.FindAssets($"l:{label}", new[] { searchPath });
                
                foreach (var guid in guids)
                {
                    if (processedGuids.Contains(guid))
                        continue;
                    
                    processedGuids.Add(guid);
                    
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                    
                    if (assetType == null)
                        continue;
                    
                    var address = GenerateAddress(assetPath, context);
                    var name = Path.GetFileNameWithoutExtension(assetPath);
                    var assetLabels = AssetDatabase.GetLabels(AssetDatabase.LoadMainAssetAtPath(assetPath));
                    
                    yield return new CollectedAsset(
                        assetPath,
                        guid,
                        address,
                        name,
                        assetLabels,
                        assetType,
                        string.Join(",", labels)
                    );
                }
            }
        }
        
        public bool Validate(out string error)
        {
            if (labels == null || labels.Length == 0)
            {
                error = "至少需要指定一个标签";
                return false;
            }
            
            error = null;
            return true;
        }
        
        private string GenerateAddress(string assetPath, CollectorContext context)
        {
            return context.AddressMode switch
            {
                AddressMode.FullPath => assetPath,
                AddressMode.RelativeToDirectory => assetPath.Replace(searchPath + "/", ""),
                AddressMode.FileName => Path.GetFileNameWithoutExtension(assetPath),
                AddressMode.FileNameWithExtension => Path.GetFileName(assetPath),
                _ => assetPath
            };
        }
    }
    
    /// <summary>
    /// 类型收集器 - 按资源类型收集
    /// </summary>
    [Serializable]
    public class TypeCollector : IAssetCollector
    {
        [Tooltip("资源类型名称（如 Texture2D, AudioClip, GameObject）")]
        public string typeName = "GameObject";
        
        [Tooltip("搜索路径")]
        public string searchPath = "Assets";
        
        public string Name => "类型收集器";
        
        public IEnumerable<CollectedAsset> Collect(CollectorContext context)
        {
            if (string.IsNullOrEmpty(typeName))
                yield break;
            
            var guids = AssetDatabase.FindAssets($"t:{typeName}", new[] { searchPath });
            
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                
                if (assetType == null)
                    continue;
                
                var address = GenerateAddress(assetPath, context);
                var name = Path.GetFileNameWithoutExtension(assetPath);
                
                yield return new CollectedAsset(
                    assetPath,
                    guid,
                    address,
                    name,
                    context.DefaultLabels,
                    assetType,
                    typeName
                );
            }
        }
        
        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                error = "类型名称不能为空";
                return false;
            }
            
            error = null;
            return true;
        }
        
        private string GenerateAddress(string assetPath, CollectorContext context)
        {
            return context.AddressMode switch
            {
                AddressMode.FullPath => assetPath,
                AddressMode.RelativeToDirectory => assetPath.Replace(searchPath + "/", ""),
                AddressMode.FileName => Path.GetFileNameWithoutExtension(assetPath),
                AddressMode.FileNameWithExtension => Path.GetFileName(assetPath),
                _ => assetPath
            };
        }
    }
    
    /// <summary>
    /// GUID 列表收集器 - 精确指定资源
    /// </summary>
    [Serializable]
    public class GuidListCollector : IAssetCollector
    {
        [Tooltip("资源 GUID 列表")]
        public List<string> guids = new List<string>();
        
        public string Name => "GUID列表收集器";
        
        public IEnumerable<CollectedAsset> Collect(CollectorContext context)
        {
            if (guids == null || guids.Count == 0)
                yield break;
            
            foreach (var guid in guids)
            {
                if (string.IsNullOrEmpty(guid))
                    continue;
                
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                    continue;
                
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                if (assetType == null)
                    continue;
                
                var address = GenerateAddress(assetPath, context);
                var name = Path.GetFileNameWithoutExtension(assetPath);
                
                yield return new CollectedAsset(
                    assetPath,
                    guid,
                    address,
                    name,
                    context.DefaultLabels,
                    assetType,
                    "GuidList"
                );
            }
        }
        
        public bool Validate(out string error)
        {
            if (guids == null || guids.Count == 0)
            {
                error = "GUID 列表为空";
                return false;
            }
            
            error = null;
            return true;
        }
        
        private string GenerateAddress(string assetPath, CollectorContext context)
        {
            return context.AddressMode switch
            {
                AddressMode.FullPath => assetPath,
                AddressMode.RelativeToDirectory => assetPath.Replace(context.BaseDirectory + "/", ""),
                AddressMode.FileName => Path.GetFileNameWithoutExtension(assetPath),
                AddressMode.FileNameWithExtension => Path.GetFileName(assetPath),
                _ => assetPath
            };
        }
        
        /// <summary>
        /// 从资源路径添加 GUID
        /// </summary>
        public void AddFromPath(string assetPath)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!string.IsNullOrEmpty(guid) && !guids.Contains(guid))
            {
                guids.Add(guid);
            }
        }
    }
    
    /// <summary>
    /// 依赖收集器 - 收集指定资源的依赖
    /// </summary>
    [Serializable]
    public class DependencyCollector : IAssetCollector
    {
        [Tooltip("根资源路径")]
        public string rootAssetPath;
        
        [Tooltip("是否递归收集依赖")]
        public bool recursive = true;
        
        [Tooltip("排除的路径模式")]
        public string[] excludePatterns = { "Packages/", "Library/" };
        
        public string Name => "依赖收集器";
        
        public IEnumerable<CollectedAsset> Collect(CollectorContext context)
        {
            if (string.IsNullOrEmpty(rootAssetPath))
                yield break;
            
            var dependencies = AssetDatabase.GetDependencies(rootAssetPath, recursive);
            
            foreach (var depPath in dependencies)
            {
                if (IsExcluded(depPath))
                    continue;
                
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(depPath);
                if (assetType == null)
                    continue;
                
                // 排除脚本
                if (assetType == typeof(MonoScript))
                    continue;
                
                var guid = AssetDatabase.AssetPathToGUID(depPath);
                var address = GenerateAddress(depPath, context);
                var name = Path.GetFileNameWithoutExtension(depPath);
                
                yield return new CollectedAsset(
                    depPath,
                    guid,
                    address,
                    name,
                    context.DefaultLabels,
                    assetType,
                    "Dependency"
                );
            }
        }
        
        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(rootAssetPath))
            {
                error = "根资源路径不能为空";
                return false;
            }
            
            if (!File.Exists(rootAssetPath))
            {
                error = $"根资源不存在: {rootAssetPath}";
                return false;
            }
            
            error = null;
            return true;
        }
        
        private bool IsExcluded(string path)
        {
            if (excludePatterns == null)
                return false;
            
            foreach (var pattern in excludePatterns)
            {
                if (path.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        
        private string GenerateAddress(string assetPath, CollectorContext context)
        {
            return context.AddressMode switch
            {
                AddressMode.FullPath => assetPath,
                AddressMode.RelativeToDirectory => assetPath.Replace(context.BaseDirectory + "/", ""),
                AddressMode.FileName => Path.GetFileNameWithoutExtension(assetPath),
                AddressMode.FileNameWithExtension => Path.GetFileName(assetPath),
                _ => assetPath
            };
        }
    }
    
    /// <summary>
    /// 查询收集器 - 使用 AssetDatabase.FindAssets 语法
    /// </summary>
    [Serializable]
    public class QueryCollector : IAssetCollector
    {
        [Tooltip("查询字符串（支持 t:, l:, ref: 等语法）")]
        public string query = "";
        
        [Tooltip("搜索路径列表")]
        public string[] searchPaths = { "Assets" };
        
        public string Name => "查询收集器";
        
        public IEnumerable<CollectedAsset> Collect(CollectorContext context)
        {
            if (string.IsNullOrEmpty(query))
                yield break;
            
            var paths = searchPaths != null && searchPaths.Length > 0 ? searchPaths : new[] { "Assets" };
            var guids = AssetDatabase.FindAssets(query, paths);
            
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                
                if (assetType == null)
                    continue;
                
                var address = GenerateAddress(assetPath, context);
                var name = Path.GetFileNameWithoutExtension(assetPath);
                
                yield return new CollectedAsset(
                    assetPath,
                    guid,
                    address,
                    name,
                    context.DefaultLabels,
                    assetType,
                    "Query"
                );
            }
        }
        
        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(query))
            {
                error = "查询字符串不能为空";
                return false;
            }
            
            error = null;
            return true;
        }
        
        private string GenerateAddress(string assetPath, CollectorContext context)
        {
            return context.AddressMode switch
            {
                AddressMode.FullPath => assetPath,
                AddressMode.RelativeToDirectory => assetPath.Replace(context.BaseDirectory + "/", ""),
                AddressMode.FileName => Path.GetFileNameWithoutExtension(assetPath),
                AddressMode.FileNameWithExtension => Path.GetFileName(assetPath),
                _ => assetPath
            };
        }
    }
}
