# Design: 资源收集与打包策略架构设计

## Context

当前资源编辑器存在以下问题：

**收集方面**：
- `AssetGroup` 层级增加了不必要的复杂度
- 收集策略与数据结构耦合，难以扩展
- 无法组合多种收集策略

**打包策略方面**：
- `IPackStrategy` 接口已存在，但只能选择预设策略类型
- 缺乏可视化的打包预览和自定义规则

**配置存储**：
- 需要将配置存储在 ProjectSettings，避免污染项目 Assets 结构

## Goals / Non-Goals

### Goals
- 移除 AssetGroup 层级，简化结构
- 每个 Package 直接配置收集器和打包策略
- 提供灵活、可扩展的资源收集架构
- 提供可组合的打包策略系统
- 所有配置存储在 ProjectSettings 目录
- 保持向后兼容，平滑迁移

### Non-Goals
- 不改变运行时资源加载逻辑
- 不引入外部依赖
- 不在 Assets 目录创建配置文件

## Decisions

### 1. 简化后的数据结构

```
Package (资源包)
├── packageName
├── version
├── packageType (BasePackage / HotfixPackage)
├── collector: IAssetCollector        # 资源收集器（组合多种收集策略）
├── packStrategy: PackStrategyConfig  # 打包策略配置
└── addressMode: AddressMode          # 地址生成模式
```

移除 `AssetGroup` 层级，Package 直接包含收集器配置。如需收集多个来源，使用 `CompositeCollector` 组合。

### 2. 收集器接口设计

```csharp
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
public class CollectorContext
{
    public string PackageName { get; set; }
    public AddressMode AddressMode { get; set; }
    public string[] DefaultLabels { get; set; }
}
```

### 3. 内置收集器实现

```csharp
// 目录收集器
[Serializable]
public class DirectoryCollector : IAssetCollector
{
    public string directoryPath;
    public string[] searchPatterns = { "*.*" };
    public bool recursive = true;
    public string[] excludePatterns = { "*.meta", "*.cs" };
}

// 标签收集器（按 Unity Asset Label）
[Serializable]
public class LabelCollector : IAssetCollector
{
    public string[] labels;
    public string searchPath = "Assets";
}

// 类型收集器
[Serializable]
public class TypeCollector : IAssetCollector
{
    public string typeName;  // e.g., "Texture2D", "AudioClip"
    public string searchPath = "Assets";
}

// GUID 列表收集器（精确指定资源）
[Serializable]
public class GuidListCollector : IAssetCollector
{
    public List<string> guids = new();
}

// 依赖收集器
[Serializable]
public class DependencyCollector : IAssetCollector
{
    public string rootAssetPath;
    public bool recursive = true;
    public string[] excludePatterns;
}

// 查询收集器（使用 AssetDatabase.FindAssets 语法）
[Serializable]
public class QueryCollector : IAssetCollector
{
    public string query;
    public string[] searchPaths = { "Assets" };
}

// 组合收集器（并集）
[Serializable]
public class CompositeCollector : IAssetCollector
{
    [SerializeReference]
    public List<IAssetCollector> collectors = new();
    
    public IEnumerable<CollectedAsset> Collect(CollectorContext context)
    {
        var seen = new HashSet<string>();
        foreach (var collector in collectors)
        {
            foreach (var asset in collector.Collect(context))
            {
                if (seen.Add(asset.guid))
                    yield return asset;
            }
        }
    }
}

// 过滤收集器
[Serializable]
public class FilteredCollector : IAssetCollector
{
    [SerializeReference]
    public IAssetCollector source;
    
    [SerializeReference]
    public List<IAssetFilter> filters = new();
}
```

### 4. 过滤器系统

```csharp
public interface IAssetFilter
{
    string Name { get; }
    bool Match(CollectedAsset asset);
}

[Serializable]
public class FileSizeFilter : IAssetFilter
{
    public long minSize = 0;
    public long maxSize = long.MaxValue;
}

[Serializable]
public class ExtensionFilter : IAssetFilter
{
    public string[] includeExtensions;  // e.g., ".png", ".jpg"
    public string[] excludeExtensions;
}

[Serializable]
public class PathPatternFilter : IAssetFilter
{
    public string pattern;  // 支持通配符
    public bool exclude = false;
}
```

### 5. 打包策略系统

```csharp
/// <summary>
/// 可配置的打包策略
/// </summary>
[Serializable]
public abstract class PackStrategyConfig
{
    public string name;
    public bool enabled = true;
    
    public abstract IPackStrategy CreateStrategy();
}

/// <summary>
/// 目录打包策略配置
/// </summary>
[Serializable]
public class DirectoryPackConfig : PackStrategyConfig
{
    public int directoryDepth = 1;
    public bool includeRootName = true;
    
    public override IPackStrategy CreateStrategy() 
        => new PackByDirectoryStrategy(directoryDepth, includeRootName);
}

/// <summary>
/// 大小限制打包策略配置
/// </summary>
[Serializable]
public class SizeLimitPackConfig : PackStrategyConfig
{
    public long maxBundleSize = 10 * 1024 * 1024;  // 10MB
    public string bundleNamePrefix = "chunk";
    
    public override IPackStrategy CreateStrategy() 
        => new PackBySizeLimitStrategy(maxBundleSize, bundleNamePrefix);
}

/// <summary>
/// 共享资源打包策略配置
/// </summary>
[Serializable]
public class SharedAssetPackConfig : PackStrategyConfig
{
    public int minReferenceCount = 2;
    public string sharedBundleName = "shared";
    
    public override IPackStrategy CreateStrategy() 
        => new PackSharedAssetsStrategy(minReferenceCount, sharedBundleName);
}

/// <summary>
/// 自定义规则打包策略
/// </summary>
[Serializable]
public class CustomRulePackConfig : PackStrategyConfig
{
    public List<PackRule> rules = new();
    
    public override IPackStrategy CreateStrategy() 
        => new PackByCustomRulesStrategy(rules);
}

/// <summary>
/// 打包规则
/// </summary>
[Serializable]
public class PackRule
{
    public string ruleName;
    public string bundleName;
    public ConditionType conditionType;
    public string pattern;
}

public enum ConditionType
{
    PathContains,
    PathStartsWith,
    PathEndsWith,
    PathRegex,
    HasLabel,
    AssetType,
    FileExtension
}
```

### 6. PackageSettings 结构调整

```csharp
[Serializable]
public class PackageSettings
{
    public string packageName;
    public string version;
    public PackageType packageType = PackageType.HotfixPackage;
    public AddressMode addressMode = AddressMode.FullPath;
    public CompressionType compression;
    
    // 新增：资源收集器（替代 AssetGroup 列表）
    [SerializeReference]
    public IAssetCollector collector;
    
    // 新增：打包策略配置
    [SerializeReference]
    public PackStrategyConfig packStrategyConfig;
    
    // 保留用于向后兼容（迁移后置空）
    [Obsolete("Use collector instead")]
    public List<AssetGroup> assetGroups;
    
    /// <summary>
    /// 从旧的 AssetGroup 列表迁移到新的收集器
    /// </summary>
    public void MigrateFromAssetGroups()
    {
        if (collector != null || assetGroups == null || assetGroups.Count == 0)
            return;
        
        var composite = new CompositeCollector();
        
        foreach (var group in assetGroups)
        {
            // 将每个 AssetGroup 转换为对应的收集器
            if (!string.IsNullOrEmpty(group.folderPath))
            {
                composite.collectors.Add(new DirectoryCollector
                {
                    directoryPath = group.folderPath,
                    searchPatterns = group.GetSearchPatterns(),
                    recursive = group.recursive,
                    excludePatterns = group.excludePatterns
                });
            }
            
            if (group.assetPaths?.Count > 0)
            {
                composite.collectors.Add(new GuidListCollector
                {
                    guids = group.assetPaths
                        .Select(p => AssetDatabase.AssetPathToGUID(p))
                        .Where(g => !string.IsNullOrEmpty(g))
                        .ToList()
                });
            }
        }
        
        collector = composite.collectors.Count == 1 
            ? composite.collectors[0] 
            : composite;
        
        // 迁移打包策略
        packStrategyConfig = packStrategy switch
        {
            PackStrategyType.PackByFile => new FilePackConfig(),
            PackStrategyType.PackByDirectory => new DirectoryPackConfig(),
            PackStrategyType.PackByLabel => new LabelPackConfig(),
            PackStrategyType.PackByType => new TypePackConfig(),
            PackStrategyType.PackTogether => new TogetherPackConfig { bundleName = packageName },
            _ => new DirectoryPackConfig()
        };
    }
    
    /// <summary>
    /// 收集此 Package 的所有资源
    /// </summary>
    public List<CollectedAsset> CollectAssets()
    {
        if (collector == null)
            return new List<CollectedAsset>();
        
        var context = new CollectorContext
        {
            PackageName = packageName,
            AddressMode = addressMode
        };
        
        return collector.Collect(context).ToList();
    }
    
    /// <summary>
    /// 获取打包分组
    /// </summary>
    public Dictionary<string, List<CollectedAsset>> GetBundleGroups()
    {
        var assets = CollectAssets();
        var strategy = packStrategyConfig?.CreateStrategy() 
            ?? new PackByDirectoryStrategy();
        return strategy.Pack(assets);
    }
}
```

### 7. 配置存储（ProjectSettings）

```
ProjectSettings/
├── ResourcePackages.json          # Package 配置（包含收集器和打包策略）
├── ResourceBuilderSettings.json   # 构建器全局设置
├── CollectorPresets.json          # 收集器预设（可复用的收集器配置）
└── PackStrategyPresets.json       # 打包策略预设
```

```csharp
/// <summary>
/// 收集器预设配置
/// </summary>
[Serializable]
public class CollectorPresetsData
{
    private const string SETTINGS_PATH = "ProjectSettings/CollectorPresets.json";
    
    public List<CollectorPreset> presets = new();
    
    // 单例访问、Load/Save 方法...
}

[Serializable]
public class CollectorPreset
{
    public string presetId;
    public string presetName;
    public string description;
    
    [SerializeReference]
    public IAssetCollector collector;
}
```

### 8. 编辑器 UI 设计

基于现有的 `ResourcePackagesWindow` 左右分栏布局进行改造：

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ 资源包管理                                                        [≡ 菜单] │
├────────────────────────┬────────────────────────────────────────────────────┤
│ ┌────────────────────┐ │ ┌────────────────────────────────────────────────┐ │
│ │ Package 列表       │ │ │ Package 详情                                   │ │
│ ├────────────────────┤ │ ├────────────────────────────────────────────────┤ │
│ │                    │ │ │                                                │ │
│ │ ┌────────────────┐ │ │ │ ┌─ 基本信息 ─────────────────────────────────┐ │ │
│ │ │ [首] MainPkg   │ │ │ │ │ 名称: [MainPackage        ]               │ │ │
│ │ │     v1.0.0     │◀│ │ │ │ 版本: [1.0.0] 类型: [首包资源 ▼]          │ │ │
│ │ └────────────────┘ │ │ │ │ 地址模式: [完整路径 ▼]                     │ │ │
│ │                    │ │ │ └────────────────────────────────────────────┘ │ │
│ │ ┌────────────────┐ │ │ │                                                │ │
│ │ │ [热] DLCPkg    │ │ │ │ ┌─ 资源收集器 ──────────────────────────────┐ │ │
│ │ │     v1.0.0     │ │ │ │ │ 类型: [组合收集器 ▼]  [保存预设] [加载]   │ │ │
│ │ └────────────────┘ │ │ │ │                                            │ │ │
│ │                    │ │ │ │ ┌─ 子收集器列表 ────────────────────────┐ │ │ │
│ │                    │ │ │ │ │ ▼ [目录] Assets/Art/Characters    [×] │ │ │ │
│ │                    │ │ │ │ │     扩展名: *.prefab, *.mat           │ │ │ │
│ │                    │ │ │ │ │     递归: ☑  排除: *.meta, *.cs       │ │ │ │
│ │                    │ │ │ │ │                                        │ │ │ │
│ │                    │ │ │ │ │ ▼ [目录] Assets/Art/UI            [×] │ │ │ │
│ │                    │ │ │ │ │     扩展名: *.prefab                   │ │ │ │
│ │                    │ │ │ │ │     递归: ☑                           │ │ │ │
│ │                    │ │ │ │ │                                        │ │ │ │
│ │                    │ │ │ │ │ ▶ [标签] ui, common               [×] │ │ │ │
│ │                    │ │ │ │ │                                        │ │ │ │
│ │                    │ │ │ │ │ [+ 添加收集器]                         │ │ │ │
│ │                    │ │ │ │ └────────────────────────────────────────┘ │ │ │
│ │                    │ │ │ └────────────────────────────────────────────┘ │ │
│ │                    │ │ │                                                │ │
│ │                    │ │ │ ┌─ 打包策略 ────────────────────────────────┐ │ │
│ │                    │ │ │ │ 类型: [按目录打包 ▼]                      │ │ │
│ │                    │ │ │ │ 目录深度: [1]  包含根目录名: ☑            │ │ │
│ │                    │ │ │ │                                            │ │ │
│ │                    │ │ │ │ 提示: 同一目录下的资源打包到同一 Bundle   │ │ │
│ │                    │ │ │ └────────────────────────────────────────────┘ │ │
│ │                    │ │ │                                                │ │
│ │                    │ │ │ ┌─ 预览 ────────────────────────────────────┐ │ │
│ │                    │ │ │ │ 资源: 165  |  Bundle: 12  |  大小: 48MB   │ │ │
│ │                    │ │ │ │ [刷新] [查看资源] [查看Bundle] [依赖分析] │ │ │
│ │                    │ │ │ └────────────────────────────────────────────┘ │ │
│ │                    │ │ │                                                │ │
│ ├────────────────────┤ │ └────────────────────────────────────────────────┘ │
│ │ [+ 新建包]         │ │                                                    │
│ └────────────────────┘ │                                                    │
└────────────────────────┴────────────────────────────────────────────────────┘
```

**UI 改造要点**：

1. **移除 AssetGroup 区域**
   - 删除原有的 `groups-section` 和 `groups-container`
   - 用 `collector-section` 替代

2. **收集器配置区域**
   - 收集器类型下拉框（Directory/Label/Type/Query/Composite 等）
   - 当选择 `CompositeCollector` 时，显示子收集器列表
   - 每个子收集器可折叠展开，显示详细配置
   - 支持添加/删除/排序子收集器
   - 预设保存/加载按钮

3. **打包策略配置区域**
   - 策略类型下拉框
   - 根据选择的策略类型显示对应的配置项
   - 帮助文本说明当前策略的效果

4. **预览区域**
   - 显示收集到的资源数量、预估 Bundle 数量和大小
   - 提供查看详细资源列表、Bundle 分组、依赖分析的按钮

**收集器类型选择菜单**：

```
┌─────────────────────────┐
│ 目录收集器 (Directory)  │  按文件夹路径收集
│ 标签收集器 (Label)      │  按 Unity Asset Label 收集
│ 类型收集器 (Type)       │  按资源类型收集
│ GUID列表 (GuidList)     │  精确指定资源
│ 依赖收集器 (Dependency) │  收集指定资源的依赖
│ 查询收集器 (Query)      │  使用 FindAssets 语法
│ ─────────────────────── │
│ 组合收集器 (Composite)  │  组合多个收集器
│ 过滤收集器 (Filtered)   │  在结果上应用过滤
│ ─────────────────────── │
│ 从预设加载...           │
└─────────────────────────┘
```

**打包策略选择菜单**：

```
┌─────────────────────────┐
│ 按目录打包 (Directory)  │  同目录资源打同一 Bundle
│ 按文件打包 (File)       │  每个文件单独打包
│ 按标签打包 (Label)      │  相同标签打同一 Bundle
│ 按类型打包 (Type)       │  相同类型打同一 Bundle
│ 全部打包 (Together)     │  所有资源打一个 Bundle
│ ─────────────────────── │
│ 大小限制 (SizeLimit)    │  按大小自动分包
│ 共享提取 (SharedAsset)  │  自动提取共享资源
│ 自定义规则 (CustomRule) │  自定义打包规则
│ ─────────────────────── │
│ 从预设加载...           │
└─────────────────────────┘
```

## Risks / Trade-offs

| 风险 | 缓解措施 |
|------|----------|
| `SerializeReference` 在旧版 Unity 不支持 | 项目已使用 Unity 6+，支持良好 |
| 移除 AssetGroup 可能影响现有项目 | 提供自动迁移工具，将 AssetGroup 转换为 CompositeCollector |
| 复杂配置可能导致性能问题 | 添加收集结果缓存，支持增量更新 |

## Migration Plan

1. **Phase 1**: 添加新的收集器系统，保持 `assetGroups` 字段
2. **Phase 2**: 提供自动迁移工具 `MigrateFromAssetGroups()`
3. **Phase 3**: 标记 `assetGroups` 为 `[Obsolete]`，下个大版本移除

## Open Questions

1. 是否需要支持收集器的"交集"操作（IntersectCollector）？
2. 是否需要支持异步收集（大型项目可能需要）？
3. 收集器配置是否需要支持变量/参数化（如 `${ProjectRoot}/Art`）？
