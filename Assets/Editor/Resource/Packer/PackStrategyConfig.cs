using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 打包策略配置基类
    /// </summary>
    [Serializable]
    public abstract class PackStrategyConfig
    {
        /// <summary>
        /// 配置名称
        /// </summary>
        public string name;
        
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool enabled = true;
        
        /// <summary>
        /// 策略显示名称
        /// </summary>
        public abstract string DisplayName { get; }
        
        /// <summary>
        /// 策略描述
        /// </summary>
        public abstract string Description { get; }
        
        /// <summary>
        /// 创建打包策略实例
        /// </summary>
        public abstract IPackStrategy CreateStrategy();
        
        /// <summary>
        /// 验证配置
        /// </summary>
        public virtual bool Validate(out string error)
        {
            error = null;
            return true;
        }
    }
    
    /// <summary>
    /// 按文件打包配置
    /// </summary>
    [Serializable]
    public class FilePackConfig : PackStrategyConfig
    {
        public override string DisplayName => "按文件打包";
        public override string Description => "每个文件单独打包为一个 Bundle";
        
        public override IPackStrategy CreateStrategy()
        {
            return new PackByFileStrategy();
        }
    }
    
    /// <summary>
    /// 按目录打包配置
    /// </summary>
    [Serializable]
    public class DirectoryPackConfig : PackStrategyConfig
    {
        [Tooltip("目录深度（从 Assets 开始计算）")]
        public int directoryDepth = 1;
        
        [Tooltip("是否包含根目录名")]
        public bool includeRootName = true;
        
        public override string DisplayName => "按目录打包";
        public override string Description => "同一目录下的资源打包到同一 Bundle";
        
        public override IPackStrategy CreateStrategy()
        {
            return new PackByDirectoryWithDepthStrategy(directoryDepth, includeRootName);
        }
    }
    
    /// <summary>
    /// 按标签打包配置
    /// </summary>
    [Serializable]
    public class LabelPackConfig : PackStrategyConfig
    {
        [Tooltip("无标签资源的处理方式")]
        public UnlabeledAssetHandling unlabeledHandling = UnlabeledAssetHandling.SeparateBundle;
        
        [Tooltip("无标签资源的 Bundle 名称")]
        public string unlabeledBundleName = "unlabeled";
        
        public override string DisplayName => "按标签打包";
        public override string Description => "相同标签的资源打包到同一 Bundle";
        
        public override IPackStrategy CreateStrategy()
        {
            return new PackByLabelStrategy();
        }
    }
    
    /// <summary>
    /// 按类型打包配置
    /// </summary>
    [Serializable]
    public class TypePackConfig : PackStrategyConfig
    {
        public override string DisplayName => "按类型打包";
        public override string Description => "相同类型的资源打包到同一 Bundle";
        
        public override IPackStrategy CreateStrategy()
        {
            return new PackByTypeStrategy();
        }
    }
    
    /// <summary>
    /// 全部打包配置
    /// </summary>
    [Serializable]
    public class TogetherPackConfig : PackStrategyConfig
    {
        [Tooltip("Bundle 名称")]
        public string bundleName = "all_assets";
        
        public override string DisplayName => "全部打包";
        public override string Description => "所有资源打包到一个 Bundle";
        
        public override IPackStrategy CreateStrategy()
        {
            return new PackTogetherStrategy(bundleName);
        }
        
        public override bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(bundleName))
            {
                error = "Bundle 名称不能为空";
                return false;
            }
            error = null;
            return true;
        }
    }
    
    /// <summary>
    /// 大小限制打包配置
    /// </summary>
    [Serializable]
    public class SizeLimitPackConfig : PackStrategyConfig
    {
        [Tooltip("单个 Bundle 最大大小（MB）")]
        public float maxBundleSizeMB = 10f;
        
        [Tooltip("Bundle 名称前缀")]
        public string bundleNamePrefix = "chunk";
        
        public override string DisplayName => "大小限制打包";
        public override string Description => "按大小自动分包，避免单个 Bundle 过大";
        
        public override IPackStrategy CreateStrategy()
        {
            return new PackBySizeLimitStrategy((long)(maxBundleSizeMB * 1024 * 1024), bundleNamePrefix);
        }
        
        public override bool Validate(out string error)
        {
            if (maxBundleSizeMB <= 0)
            {
                error = "最大 Bundle 大小必须大于 0";
                return false;
            }
            error = null;
            return true;
        }
    }
    
    /// <summary>
    /// 共享资源打包配置
    /// </summary>
    [Serializable]
    public class SharedAssetPackConfig : PackStrategyConfig
    {
        [Tooltip("最小引用次数（被引用超过此次数的资源会被提取到共享 Bundle）")]
        public int minReferenceCount = 2;
        
        [Tooltip("共享 Bundle 名称")]
        public string sharedBundleName = "shared";
        
        public override string DisplayName => "共享资源提取";
        public override string Description => "自动提取被多次引用的共享资源";
        
        public override IPackStrategy CreateStrategy()
        {
            return new PackSharedAssetsStrategy(minReferenceCount, sharedBundleName);
        }
        
        public override bool Validate(out string error)
        {
            if (minReferenceCount < 2)
            {
                error = "最小引用次数至少为 2";
                return false;
            }
            error = null;
            return true;
        }
    }
    
    /// <summary>
    /// 自定义规则打包配置
    /// </summary>
    [Serializable]
    public class CustomRulePackConfig : PackStrategyConfig
    {
        public List<PackRule> rules = new List<PackRule>();
        
        [Tooltip("默认 Bundle 名称（不匹配任何规则的资源）")]
        public string defaultBundleName = "default";
        
        public override string DisplayName => "自定义规则打包";
        public override string Description => "使用自定义规则决定资源的打包分组";
        
        public override IPackStrategy CreateStrategy()
        {
            return new PackByCustomRulesStrategy(rules, defaultBundleName);
        }
        
        public override bool Validate(out string error)
        {
            if (rules == null || rules.Count == 0)
            {
                error = "至少需要一条打包规则";
                return false;
            }
            
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (string.IsNullOrEmpty(rule.bundleName))
                {
                    error = $"规则 [{i}] 的 Bundle 名称不能为空";
                    return false;
                }
                if (string.IsNullOrEmpty(rule.pattern))
                {
                    error = $"规则 [{i}] 的匹配模式不能为空";
                    return false;
                }
            }
            
            error = null;
            return true;
        }
    }
    
    /// <summary>
    /// 打包规则
    /// </summary>
    [Serializable]
    public class PackRule
    {
        [Tooltip("规则名称")]
        public string ruleName;
        
        [Tooltip("目标 Bundle 名称")]
        public string bundleName;
        
        [Tooltip("条件类型")]
        public ConditionType conditionType;
        
        [Tooltip("匹配模式")]
        public string pattern;
        
        [Tooltip("规则优先级（数字越大优先级越高）")]
        public int priority = 0;
        
        /// <summary>
        /// 检查资源是否匹配此规则
        /// </summary>
        public bool Match(CollectedAsset asset)
        {
            return conditionType switch
            {
                ConditionType.PathContains => asset.assetPath.Contains(pattern),
                ConditionType.PathStartsWith => asset.assetPath.StartsWith(pattern),
                ConditionType.PathEndsWith => asset.assetPath.EndsWith(pattern),
                ConditionType.PathRegex => Regex.IsMatch(asset.assetPath, pattern),
                ConditionType.HasLabel => asset.labels?.Contains(pattern) ?? false,
                ConditionType.AssetType => asset.assetType?.Name == pattern,
                ConditionType.FileExtension => Path.GetExtension(asset.assetPath).Equals(pattern, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
    }
    
    /// <summary>
    /// 条件类型
    /// </summary>
    public enum ConditionType
    {
        [Tooltip("路径包含指定字符串")]
        PathContains,
        
        [Tooltip("路径以指定字符串开头")]
        PathStartsWith,
        
        [Tooltip("路径以指定字符串结尾")]
        PathEndsWith,
        
        [Tooltip("路径匹配正则表达式")]
        PathRegex,
        
        [Tooltip("资源包含指定标签")]
        HasLabel,
        
        [Tooltip("资源类型匹配")]
        AssetType,
        
        [Tooltip("文件扩展名匹配")]
        FileExtension
    }
    
    /// <summary>
    /// 无标签资源处理方式
    /// </summary>
    public enum UnlabeledAssetHandling
    {
        [Tooltip("单独打包到一个 Bundle")]
        SeparateBundle,
        
        [Tooltip("每个资源单独打包")]
        IndividualBundles,
        
        [Tooltip("忽略（不打包）")]
        Ignore
    }
}
