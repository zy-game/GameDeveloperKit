# Change: 重构资源收集与打包策略系统

## Why

当前资源编辑器存在以下问题：

**收集方面**：
1. `AssetGroup` 层级增加了不必要的复杂度
2. 收集策略与数据结构耦合，难以扩展
3. 无法基于资源属性（标签、类型、依赖关系）进行智能收集
4. 缺乏条件过滤（大小、时间、引用关系等）

**打包策略方面**：
1. 只能选择预设策略类型（`PackStrategyType` 枚举）
2. 缺乏可视化的打包预览和自定义规则
3. 无法配置高级策略（如按大小分包、共享资源提取等）

## What Changes

### 1. 移除 AssetGroup，简化结构

**之前**：
```
Package
└── AssetGroup[]
    ├── folderPath
    ├── assetPaths
    └── ...
```

**之后**：
```
Package
├── collector: IAssetCollector      # 资源收集器
├── packStrategyConfig              # 打包策略配置
└── addressMode                     # 地址生成模式
```

每个 Package 直接配置收集器，如需收集多个来源，使用 `CompositeCollector` 组合。

### 2. 可组合的收集器架构

引入 `IAssetCollector` 接口，支持多种收集策略的灵活组合：

| 收集器 | 描述 |
|--------|------|
| `DirectoryCollector` | 按目录收集 |
| `LabelCollector` | 按 Unity Asset Label 收集 |
| `TypeCollector` | 按资源类型收集 |
| `GuidListCollector` | 按 GUID 列表精确收集 |
| `DependencyCollector` | 收集指定资源的所有依赖 |
| `QueryCollector` | 使用 AssetDatabase.FindAssets 查询语法 |
| `CompositeCollector` | 组合多个收集器（并集） |
| `FilteredCollector` | 在收集结果上应用过滤条件 |

### 3. 可配置的打包策略系统

引入 `PackStrategyConfig` 抽象类：

| 策略配置 | 描述 |
|----------|------|
| `DirectoryPackConfig` | 按目录打包，可配置目录深度 |
| `SizeLimitPackConfig` | 按大小限制分包 |
| `SharedAssetPackConfig` | 自动提取共享资源 |
| `CustomRulePackConfig` | 自定义规则打包（路径匹配、类型、标签等） |

### 4. 配置存储在 ProjectSettings

```
ProjectSettings/
├── ResourcePackages.json          # Package 配置（包含收集器和打包策略）
├── ResourceBuilderSettings.json   # 构建器全局设置
├── CollectorPresets.json          # 收集器预设（可复用）
└── PackStrategyPresets.json       # 打包策略预设
```

## Impact

- **Affected specs**: 无现有 spec（新功能）
- **Affected code**:
  - `Editor/Resource/Collector/` - 新增收集器系统
  - `Editor/Resource/Packer/` - 增强打包策略系统
  - `Editor/Resource/Data/ResourcePackagesData.cs` - PackageSettings 结构调整
  - `Editor/Resource/Window/Packages/` - UI 更新

## 向后兼容

- 现有的 `AssetGroup` 列表将自动迁移为 `CompositeCollector`
- 每个 AssetGroup 的 `folderPath` 转换为 `DirectoryCollector`
- 每个 AssetGroup 的 `assetPaths` 转换为 `GuidListCollector`
- 现有的 `PackStrategyType` 枚举转换为对应的 `PackStrategyConfig`
- 不影响运行时资源加载逻辑
