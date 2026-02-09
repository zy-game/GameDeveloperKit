# Tasks: 资源收集与打包策略系统重构

## 1. 核心接口与基础设施

- [ ] 1.1 创建 `IAssetCollector` 接口
- [ ] 1.2 创建 `CollectorContext` 上下文类
- [ ] 1.3 创建 `IAssetFilter` 过滤器接口
- [ ] 1.4 更新 `CollectedAsset` 类，添加必要字段

## 2. 内置收集器实现

- [ ] 2.1 实现 `DirectoryCollector`（按目录收集）
- [ ] 2.2 实现 `LabelCollector`（按 Unity Asset Label 收集）
- [ ] 2.3 实现 `TypeCollector`（按资源类型收集）
- [ ] 2.4 实现 `GuidListCollector`（按 GUID 列表收集）
- [ ] 2.5 实现 `DependencyCollector`（收集依赖资源）
- [ ] 2.6 实现 `QueryCollector`（使用 FindAssets 查询）
- [ ] 2.7 实现 `CompositeCollector`（组合多个收集器）
- [ ] 2.8 实现 `FilteredCollector`（带过滤的收集器）

## 3. 过滤器实现

- [ ] 3.1 实现 `FileSizeFilter`（文件大小过滤）
- [ ] 3.2 实现 `ExtensionFilter`（扩展名过滤）
- [ ] 3.3 实现 `PathPatternFilter`（路径模式过滤）

## 4. 打包策略系统

- [ ] 4.1 创建 `PackStrategyConfig` 抽象基类
- [ ] 4.2 实现 `DirectoryPackConfig`（目录打包配置）
- [ ] 4.3 实现 `FilePackConfig`（单文件打包配置）
- [ ] 4.4 实现 `SizeLimitPackConfig`（大小限制分包配置）
- [ ] 4.5 实现 `SharedAssetPackConfig`（共享资源提取配置）
- [ ] 4.6 实现 `CustomRulePackConfig`（自定义规则打包配置）
- [ ] 4.7 实现 `PackRule` 规则系统

## 5. 数据结构与存储

- [ ] 5.1 更新 `PackageSettings`，添加 `collector` 和 `packStrategyConfig` 字段
- [ ] 5.2 实现 `PackageSettings.MigrateFromAssetGroups()` 迁移方法
- [ ] 5.3 实现 `PackageSettings.CollectAssets()` 方法
- [ ] 5.4 实现 `PackageSettings.GetBundleGroups()` 方法
- [ ] 5.5 创建 `CollectorPresetsData`（存储在 ProjectSettings/CollectorPresets.json）
- [ ] 5.6 创建 `PackStrategyPresetsData`（存储在 ProjectSettings/PackStrategyPresets.json）
- [ ] 5.7 更新 `ResourcePackagesData` 序列化逻辑支持 SerializeReference

## 6. 编辑器 UI

- [ ] 6.1 创建收集器类型选择下拉菜单
- [ ] 6.2 为每种收集器创建自定义编辑器 UI
- [ ] 6.3 创建 CompositeCollector 的可视化编辑器（支持添加/删除/排序）
- [ ] 6.4 创建打包策略配置 UI
- [ ] 6.5 实现收集结果预览面板
- [ ] 6.6 实现打包分组预览面板
- [ ] 6.7 支持预设配置的保存和加载
- [ ] 6.8 添加依赖分析功能

## 7. 迁移工具

- [ ] 7.1 创建自动迁移检测逻辑（检测旧的 AssetGroup 配置）
- [ ] 7.2 实现批量迁移功能
- [ ] 7.3 添加配置备份功能

## 8. 测试与验证

- [ ] 8.1 为每种收集器编写单元测试
- [ ] 8.2 为打包策略编写单元测试
- [ ] 8.3 测试向后兼容性和迁移流程
- [ ] 8.4 更新编辑器工具提示和帮助文本
