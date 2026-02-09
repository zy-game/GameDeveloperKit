# Resource Collector and Pack Strategy Specification

## ADDED Requirements

### Requirement: Asset Collector Interface
系统 SHALL 提供 `IAssetCollector` 接口作为所有资源收集器的统一抽象。

#### Scenario: 实现自定义收集器
- **WHEN** 开发者实现 `IAssetCollector` 接口
- **THEN** 该收集器可被 Package 使用
- **AND** 收集器可在编辑器 UI 中选择

#### Scenario: 收集器返回资源列表
- **WHEN** 调用 `IAssetCollector.Collect(context)`
- **THEN** 返回 `IEnumerable<CollectedAsset>` 包含所有匹配的资源
- **AND** 每个 `CollectedAsset` 包含 assetPath、guid、address、labels 等信息

### Requirement: Directory Collector
系统 SHALL 提供 `DirectoryCollector` 用于按目录路径收集资源。

#### Scenario: 收集目录下所有资源
- **WHEN** 配置 `directoryPath = "Assets/Art/Characters"`
- **AND** `recursive = true`
- **THEN** 收集该目录及子目录下所有资源

#### Scenario: 按扩展名过滤
- **WHEN** 配置 `searchPatterns = ["*.prefab", "*.mat"]`
- **THEN** 仅收集匹配扩展名的资源

#### Scenario: 排除特定文件
- **WHEN** 配置 `excludePatterns = ["*.meta", "*_backup.*"]`
- **THEN** 排除匹配模式的文件

### Requirement: Label Collector
系统 SHALL 提供 `LabelCollector` 用于按 Unity Asset Label 收集资源。

#### Scenario: 按单个标签收集
- **WHEN** 配置 `labels = ["character"]`
- **THEN** 收集所有带有 "character" 标签的资源

#### Scenario: 按多个标签收集（并集）
- **WHEN** 配置 `labels = ["character", "enemy"]`
- **THEN** 收集带有任一标签的资源

### Requirement: Type Collector
系统 SHALL 提供 `TypeCollector` 用于按资源类型收集。

#### Scenario: 收集特定类型资源
- **WHEN** 配置 `typeName = "Texture2D"`
- **AND** `searchPath = "Assets/Art"`
- **THEN** 收集该路径下所有 Texture2D 资源

### Requirement: GUID List Collector
系统 SHALL 提供 `GuidListCollector` 用于按 GUID 列表精确收集资源。

#### Scenario: 按 GUID 列表收集
- **WHEN** 配置 `guids = ["abc123...", "def456..."]`
- **THEN** 精确收集这些 GUID 对应的资源
- **AND** 忽略不存在的 GUID

### Requirement: Dependency Collector
系统 SHALL 提供 `DependencyCollector` 用于收集资源的依赖项。

#### Scenario: 收集直接依赖
- **WHEN** 配置 `rootAssetPath = "Assets/Prefabs/Player.prefab"`
- **AND** `recursive = false`
- **THEN** 收集 Player.prefab 的直接依赖资源

#### Scenario: 收集递归依赖
- **WHEN** 配置 `recursive = true`
- **THEN** 收集所有直接和间接依赖资源

### Requirement: Query Collector
系统 SHALL 提供 `QueryCollector` 支持 AssetDatabase.FindAssets 查询语法。

#### Scenario: 使用查询语法收集
- **WHEN** 配置 `query = "t:Prefab player"`
- **THEN** 收集名称包含 "player" 的所有 Prefab

### Requirement: Composite Collector
系统 SHALL 提供 `CompositeCollector` 用于组合多个收集器。

#### Scenario: 组合多个收集器
- **WHEN** Package 配置 CompositeCollector 包含多个子收集器
- **THEN** 返回所有子收集器结果的并集
- **AND** 自动去重（按 GUID）

### Requirement: Filtered Collector
系统 SHALL 提供 `FilteredCollector` 用于在收集结果上应用过滤条件。

#### Scenario: 应用文件大小过滤
- **WHEN** 配置 `FileSizeFilter` 且 `maxSize = 1MB`
- **THEN** 仅保留小于 1MB 的资源

#### Scenario: 应用多个过滤器
- **WHEN** 配置多个 `IAssetFilter`
- **THEN** 资源必须通过所有过滤器才被保留

### Requirement: Package Collector Integration
`PackageSettings` SHALL 直接配置 `IAssetCollector` 作为资源收集策略。

#### Scenario: 使用收集器配置
- **WHEN** Package 配置了 `collector` 字段
- **THEN** 使用该收集器收集资源

#### Scenario: 向后兼容迁移
- **WHEN** Package 仅有旧的 `assetGroups` 列表
- **AND** `collector` 为 null
- **THEN** 调用 `MigrateFromAssetGroups()` 自动迁移
- **AND** 将多个 AssetGroup 合并为 CompositeCollector

### Requirement: Pack Strategy Config
系统 SHALL 提供 `PackStrategyConfig` 抽象类作为可配置打包策略的基类。

#### Scenario: 创建打包策略配置
- **WHEN** 开发者继承 `PackStrategyConfig`
- **THEN** 可通过 `CreateStrategy()` 方法创建对应的 `IPackStrategy` 实例
- **AND** 配置可在编辑器 UI 中可视化编辑

### Requirement: Directory Pack Config
系统 SHALL 提供 `DirectoryPackConfig` 用于按目录打包，支持目录深度配置。

#### Scenario: 按直接父目录打包
- **WHEN** 配置 `directoryDepth = 1`
- **THEN** 同一直接父目录下的资源打包到同一 Bundle

#### Scenario: 按上两级目录打包
- **WHEN** 配置 `directoryDepth = 2`
- **THEN** 按上两级目录分组打包

### Requirement: Size Limit Pack Config
系统 SHALL 提供 `SizeLimitPackConfig` 用于按大小限制分包。

#### Scenario: 限制 Bundle 大小
- **WHEN** 配置 `maxBundleSize = 10MB`
- **THEN** 单个 Bundle 大小不超过 10MB
- **AND** 超出部分自动分割到新 Bundle

### Requirement: Shared Asset Pack Config
系统 SHALL 提供 `SharedAssetPackConfig` 用于自动提取共享资源。

#### Scenario: 提取共享资源
- **WHEN** 配置 `minReferenceCount = 2`
- **THEN** 被 2 个以上 Bundle 引用的资源提取到共享 Bundle
- **AND** 减少资源重复打包

### Requirement: Custom Rule Pack Config
系统 SHALL 提供 `CustomRulePackConfig` 用于自定义规则打包。

#### Scenario: 按路径规则打包
- **WHEN** 配置规则 `PathContains("UI")` -> `bundleName = "ui_assets"`
- **THEN** 路径包含 "UI" 的资源打包到 ui_assets Bundle

#### Scenario: 按类型规则打包
- **WHEN** 配置规则 `AssetType("AudioClip")` -> `bundleName = "audio"`
- **THEN** 所有 AudioClip 资源打包到 audio Bundle

#### Scenario: 多规则优先级
- **WHEN** 配置多条规则
- **THEN** 按规则顺序匹配，首个匹配的规则生效

### Requirement: Configuration Storage in ProjectSettings
所有配置 SHALL 存储在 `ProjectSettings/` 目录。

#### Scenario: Package 配置存储
- **WHEN** 用户保存 Package 配置
- **THEN** 配置保存到 `ProjectSettings/ResourcePackages.json`
- **AND** 包含收集器和打包策略的完整配置

#### Scenario: 收集器预设存储
- **WHEN** 用户保存收集器预设
- **THEN** 配置保存到 `ProjectSettings/CollectorPresets.json`

#### Scenario: 打包策略预设存储
- **WHEN** 用户保存打包策略预设
- **THEN** 配置保存到 `ProjectSettings/PackStrategyPresets.json`

### Requirement: Editor UI for Package Configuration
编辑器 SHALL 提供可视化界面配置 Package 的收集器和打包策略。

#### Scenario: 选择收集器类型
- **WHEN** 用户在 Package 编辑器中点击收集器类型下拉菜单
- **THEN** 显示所有可用的收集器类型
- **AND** 选择后显示对应的配置字段

#### Scenario: 编辑 CompositeCollector
- **WHEN** 用户选择 CompositeCollector 类型
- **THEN** 显示子收集器列表
- **AND** 支持添加、删除、排序子收集器

#### Scenario: 预览收集结果
- **WHEN** 用户点击 "Preview Assets" 按钮
- **THEN** 显示当前配置会收集到的资源列表
- **AND** 显示资源数量和总大小

#### Scenario: 预览打包分组
- **WHEN** 用户点击 "Preview Bundles" 按钮
- **THEN** 显示当前配置会生成的 Bundle 列表
- **AND** 显示每个 Bundle 包含的资源和预估大小

## REMOVED Requirements

### Requirement: AssetGroup
移除 `AssetGroup` 层级，由 Package 直接配置收集器。

**Reason**: AssetGroup 增加了不必要的复杂度，Package 直接使用 CompositeCollector 可以实现相同功能且更灵活。

**Migration**: 现有 AssetGroup 列表自动迁移为 CompositeCollector，每个 AssetGroup 转换为对应的子收集器。
