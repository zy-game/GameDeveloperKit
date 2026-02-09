using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 资源引用信息
    /// </summary>
    public class AssetReferenceInfo
    {
        public string AssetPath { get; set; }
        public string AssetName { get; set; }
        public string AssetType { get; set; }
        public long FileSize { get; set; }
        public List<string> Dependencies { get; set; } = new List<string>();
        public List<string> ReferencedBy { get; set; } = new List<string>();
        public bool IsInPackage { get; set; }
        public string BundleName { get; set; }
    }
    
    /// <summary>
    /// 资源验证结果
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<ValidationError> Errors { get; } = new List<ValidationError>();
        public List<ValidationWarning> Warnings { get; } = new List<ValidationWarning>();
        public List<string> Info { get; } = new List<string>();
        
        public int TotalAssets { get; set; }
        public int ValidAssets { get; set; }
        public int InvalidAssets { get; set; }
        
        /// <summary>
        /// 资源引用关系图
        /// </summary>
        public Dictionary<string, AssetReferenceInfo> ReferenceGraph { get; } = new Dictionary<string, AssetReferenceInfo>();
        
        /// <summary>
        /// 外部依赖（不在 Package 中的依赖）
        /// </summary>
        public List<string> ExternalDependencies { get; } = new List<string>();
    }
    
    /// <summary>
    /// 验证错误
    /// </summary>
    public class ValidationError
    {
        public string AssetPath { get; set; }
        public string Message { get; set; }
        public ValidationErrorType Type { get; set; }
    }
    
    /// <summary>
    /// 验证警告
    /// </summary>
    public class ValidationWarning
    {
        public string AssetPath { get; set; }
        public string Message { get; set; }
        public ValidationWarningType Type { get; set; }
    }
    
    public enum ValidationErrorType
    {
        MissingAsset,
        InvalidReference,
        DuplicateAddress,
        ConfigurationError,
        MissingDependency
    }
    
    public enum ValidationWarningType
    {
        LargeAsset,
        UnusedAsset,
        CircularDependency,
        MixedCase,
        SpecialCharacters
    }
    
    /// <summary>
    /// 资源验证器
    /// </summary>
    public static class AssetValidator
    {
        private const long LargeAssetThreshold = 10 * 1024 * 1024; // 10MB
        
        /// <summary>
        /// 验证 Package 配置
        /// </summary>
        public static ValidationResult ValidatePackage(PackageSettings package)
        {
            var result = new ValidationResult();
            
            if (package == null)
            {
                result.Errors.Add(new ValidationError
                {
                    Message = "Package 配置为空",
                    Type = ValidationErrorType.ConfigurationError
                });
                return result;
            }
            
            // 1. 验证基本配置
            ValidateBasicConfig(package, result);
            
            // 2. 验证收集器配置
            ValidateCollector(package, result);
            
            // 3. 验证打包策略配置
            ValidatePackStrategy(package, result);
            
            // 4. 收集并验证资源
            if (package.collector != null)
            {
                ValidateCollectedAssets(package, result);
            }
            
            return result;
        }
        
        /// <summary>
        /// 验证基本配置
        /// </summary>
        private static void ValidateBasicConfig(PackageSettings package, ValidationResult result)
        {
            if (string.IsNullOrEmpty(package.packageName))
            {
                result.Errors.Add(new ValidationError
                {
                    Message = "Package 名称不能为空",
                    Type = ValidationErrorType.ConfigurationError
                });
            }
            else
            {
                // 检查名称是否包含特殊字符
                if (package.packageName.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '-'))
                {
                    result.Warnings.Add(new ValidationWarning
                    {
                        Message = $"Package 名称 '{package.packageName}' 包含特殊字符，可能导致兼容性问题",
                        Type = ValidationWarningType.SpecialCharacters
                    });
                }
            }
            
            if (string.IsNullOrEmpty(package.version))
            {
                result.Errors.Add(new ValidationError
                {
                    Message = "Package 版本不能为空",
                    Type = ValidationErrorType.ConfigurationError
                });
            }
            
            result.Info.Add($"Package: {package.packageName} v{package.version}");
            result.Info.Add($"类型: {package.packageType}");
        }
        
        /// <summary>
        /// 验证收集器配置
        /// </summary>
        private static void ValidateCollector(PackageSettings package, ValidationResult result)
        {
            if (package.collector == null)
            {
                result.Errors.Add(new ValidationError
                {
                    Message = "未配置资源收集器",
                    Type = ValidationErrorType.ConfigurationError
                });
                return;
            }
            
            if (!package.collector.Validate(out var error))
            {
                result.Errors.Add(new ValidationError
                {
                    Message = $"收集器配置无效: {error}",
                    Type = ValidationErrorType.ConfigurationError
                });
            }
            else
            {
                result.Info.Add($"收集器: {package.collector.Name}");
            }
        }
        
        /// <summary>
        /// 验证打包策略配置
        /// </summary>
        private static void ValidatePackStrategy(PackageSettings package, ValidationResult result)
        {
            if (package.packStrategyConfig == null)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = "未配置打包策略，将使用默认策略",
                    Type = ValidationWarningType.UnusedAsset
                });
                return;
            }
            
            if (!package.packStrategyConfig.Validate(out var error))
            {
                result.Errors.Add(new ValidationError
                {
                    Message = $"打包策略配置无效: {error}",
                    Type = ValidationErrorType.ConfigurationError
                });
            }
            else
            {
                result.Info.Add($"打包策略: {package.packStrategyConfig.DisplayName}");
            }
        }
        
        /// <summary>
        /// 验证收集到的资源
        /// </summary>
        private static void ValidateCollectedAssets(PackageSettings package, ValidationResult result)
        {
            var context = new CollectorContext(package.packageName, package.addressMode);
            List<CollectedAsset> assets;
            
            try
            {
                assets = package.CollectAssets().ToList();
            }
            catch (Exception ex)
            {
                result.Errors.Add(new ValidationError
                {
                    Message = $"收集资源时发生错误: {ex.Message}",
                    Type = ValidationErrorType.ConfigurationError
                });
                return;
            }
            
            result.TotalAssets = assets.Count;
            result.Info.Add($"收集到 {assets.Count} 个资源");
            
            if (assets.Count == 0)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = "收集器未收集到任何资源",
                    Type = ValidationWarningType.UnusedAsset
                });
                return;
            }
            
            // 检查地址重复
            var addressGroups = assets.GroupBy(a => a.address).Where(g => g.Count() > 1).ToList();
            foreach (var group in addressGroups)
            {
                result.Errors.Add(new ValidationError
                {
                    Message = $"地址重复: '{group.Key}' 被 {group.Count()} 个资源使用",
                    AssetPath = group.First().assetPath,
                    Type = ValidationErrorType.DuplicateAddress
                });
            }
            
            // 验证每个资源
            foreach (var asset in assets)
            {
                ValidateAsset(asset, result);
            }
            
            result.ValidAssets = result.TotalAssets - result.InvalidAssets;
            
            // 验证依赖
            ValidateDependencies(assets, result);
        }
        
        /// <summary>
        /// 验证单个资源
        /// </summary>
        private static void ValidateAsset(CollectedAsset asset, ValidationResult result)
        {
            // 检查资源是否存在
            if (!File.Exists(asset.assetPath))
            {
                result.Errors.Add(new ValidationError
                {
                    AssetPath = asset.assetPath,
                    Message = "资源文件不存在",
                    Type = ValidationErrorType.MissingAsset
                });
                result.InvalidAssets++;
                return;
            }
            
            // 检查资源是否可加载
            var assetType = AssetDatabase.GetMainAssetTypeAtPath(asset.assetPath);
            if (assetType == null)
            {
                result.Errors.Add(new ValidationError
                {
                    AssetPath = asset.assetPath,
                    Message = "无法识别资源类型",
                    Type = ValidationErrorType.InvalidReference
                });
                result.InvalidAssets++;
                return;
            }
            
            // 检查大文件
            var fileInfo = new FileInfo(asset.assetPath);
            if (fileInfo.Length > LargeAssetThreshold)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    AssetPath = asset.assetPath,
                    Message = $"大文件警告: {FormatBytes(fileInfo.Length)}",
                    Type = ValidationWarningType.LargeAsset
                });
            }
            
            // 检查路径大小写问题（Windows 不区分大小写可能导致跨平台问题）
            var actualPath = GetActualPath(asset.assetPath);
            if (actualPath != null && actualPath != asset.assetPath)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    AssetPath = asset.assetPath,
                    Message = $"路径大小写不一致: 实际路径为 '{actualPath}'",
                    Type = ValidationWarningType.MixedCase
                });
            }
        }
        
        /// <summary>
        /// 验证依赖关系并构建引用图
        /// </summary>
        private static void ValidateDependencies(List<CollectedAsset> assets, ValidationResult result)
        {
            var assetPaths = new HashSet<string>(assets.Select(a => a.assetPath));
            var missingDeps = new HashSet<string>();
            
            // 第一步：为所有 Package 内的资源创建引用信息
            foreach (var asset in assets)
            {
                if (!result.ReferenceGraph.ContainsKey(asset.assetPath))
                {
                    var assetType = AssetDatabase.GetMainAssetTypeAtPath(asset.assetPath);
                    var fileInfo = File.Exists(asset.assetPath) ? new FileInfo(asset.assetPath) : null;
                    
                    result.ReferenceGraph[asset.assetPath] = new AssetReferenceInfo
                    {
                        AssetPath = asset.assetPath,
                        AssetName = Path.GetFileNameWithoutExtension(asset.assetPath),
                        AssetType = assetType?.Name ?? "Unknown",
                        FileSize = fileInfo?.Length ?? 0,
                        IsInPackage = true,
                        BundleName = asset.groupTag
                    };
                }
            }
            
            // 第二步：收集所有依赖关系
            foreach (var asset in assets)
            {
                var deps = AssetDatabase.GetDependencies(asset.assetPath, false);
                var refInfo = result.ReferenceGraph[asset.assetPath];
                
                foreach (var dep in deps)
                {
                    // 跳过自身
                    if (dep == asset.assetPath)
                        continue;
                    
                    // 跳过脚本
                    if (dep.EndsWith(".cs"))
                        continue;
                    
                    // 跳过内置资源
                    if (dep.StartsWith("Packages/") || dep.StartsWith("Library/") || dep.StartsWith("Resources/unity_builtin"))
                        continue;
                    
                    // 添加到依赖列表
                    refInfo.Dependencies.Add(dep);
                    
                    // 确保依赖资源也在引用图中
                    if (!result.ReferenceGraph.ContainsKey(dep))
                    {
                        var depType = AssetDatabase.GetMainAssetTypeAtPath(dep);
                        var depFileInfo = File.Exists(dep) ? new FileInfo(dep) : null;
                        
                        result.ReferenceGraph[dep] = new AssetReferenceInfo
                        {
                            AssetPath = dep,
                            AssetName = Path.GetFileNameWithoutExtension(dep),
                            AssetType = depType?.Name ?? "Unknown",
                            FileSize = depFileInfo?.Length ?? 0,
                            IsInPackage = assetPaths.Contains(dep)
                        };
                        
                        // 如果不在 Package 中，添加到外部依赖列表
                        if (!assetPaths.Contains(dep))
                        {
                            result.ExternalDependencies.Add(dep);
                        }
                    }
                    
                    // 添加反向引用
                    result.ReferenceGraph[dep].ReferencedBy.Add(asset.assetPath);
                    
                    // 检查依赖是否存在
                    if (!File.Exists(dep))
                    {
                        if (!missingDeps.Contains(dep))
                        {
                            missingDeps.Add(dep);
                            result.Errors.Add(new ValidationError
                            {
                                AssetPath = dep,
                                Message = $"缺失依赖: 被 '{asset.assetPath}' 引用",
                                Type = ValidationErrorType.MissingDependency
                            });
                        }
                    }
                }
            }
            
            // 统计信息
            if (missingDeps.Count > 0)
            {
                result.Info.Add($"发现 {missingDeps.Count} 个缺失的依赖");
            }
            
            if (result.ExternalDependencies.Count > 0)
            {
                result.Info.Add($"外部依赖: {result.ExternalDependencies.Count} 个资源");
            }
            
            result.Info.Add($"引用关系: {result.ReferenceGraph.Count} 个节点");
        }
        
        /// <summary>
        /// 获取实际路径（处理大小写）
        /// </summary>
        private static string GetActualPath(string path)
        {
            try
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                    return null;
                
                var directory = Path.GetDirectoryName(path);
                var fileName = Path.GetFileName(path);
                
                if (string.IsNullOrEmpty(directory))
                    return path;
                
                var files = Directory.GetFiles(directory, fileName);
                if (files.Length > 0)
                {
                    return files[0].Replace('\\', '/');
                }
                
                return path;
            }
            catch
            {
                return path;
            }
        }
        
        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F2} KB";
            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / 1024.0 / 1024.0:F2} MB";
            return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
        }
    }
}
