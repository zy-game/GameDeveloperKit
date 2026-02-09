# psd-to-ugui Specification Delta

## ADDED Requirements

### Requirement: 菜单导入入口
系统 SHALL 提供菜单入口，允许用户一键导入 PSD 文件。

#### Scenario: 通过菜单导入 PSD
- **WHEN** 用户点击 `GameDeveloperKit/导入 PSD` 菜单
- **THEN** 系统打开文件选择对话框
- **AND** 用户选择 PSD 文件后，系统使用全局设置自动导入

#### Scenario: 使用全局设置
- **WHEN** 导入 PSD 文件时
- **THEN** 系统使用 `PsdToUguiSettings` 中的配置（导出路径、字体、纹理设置等）
- **AND** Prefab 保存到 `ExportRootPath/PsdName/PsdName.prefab`
- **AND** 纹理保存到 `ExportRootPath/PsdName/Textures/`

---

### Requirement: PSD 文档绑定组件
系统 SHALL 提供 `PsdDocumentBinding` 组件，用于记录 PSD 图层与 Prefab 节点的映射关系。

#### Scenario: 首次导入时自动添加绑定组件
- **WHEN** 用户首次将 PSD 文件导入为 Prefab
- **THEN** 系统自动在 Prefab 根节点添加 `PsdDocumentBinding` 组件
- **AND** 记录 PSD 文件路径和哈希值
- **AND** 记录所有图层的绑定信息（图层 ID、名称、GameObject 路径）

#### Scenario: 查看绑定信息
- **WHEN** 用户在 Inspector 中选中带有 `PsdDocumentBinding` 的 Prefab
- **THEN** 系统显示 PSD 文件信息（路径、尺寸、图层数）
- **AND** 显示图层绑定列表

#### Scenario: 手动重新绑定图层
- **WHEN** 用户在 Inspector 中点击某个图层绑定的"重新绑定"按钮
- **THEN** 系统允许用户选择 Prefab 内的其他 GameObject
- **AND** 更新绑定信息

#### Scenario: 从 Inspector 重新导入
- **WHEN** 用户在 Inspector 中点击"重新导入"按钮
- **THEN** 系统重新解析 PSD 文件并执行增量导入

---

### Requirement: 增量导入
系统 SHALL 支持增量导入 PSD 文件，保留用户对 Prefab 的修改。

#### Scenario: 检测已有绑定
- **WHEN** 用户导入 PSD 文件
- **AND** 目标 Prefab 已存在且包含 `PsdDocumentBinding` 组件
- **THEN** 系统进入增量导入模式

#### Scenario: 更新匹配的图层
- **WHEN** 增量导入时 PSD 图层 ID 与绑定记录匹配
- **THEN** 系统只更新图层内容（纹理、文本内容）
- **AND** 保留 GameObject 上用户添加的其他组件和脚本

#### Scenario: 添加新增的图层
- **WHEN** 增量导入时 PSD 包含新的图层（绑定记录中不存在）
- **THEN** 系统创建新的 GameObject
- **AND** 添加到正确的父节点下
- **AND** 更新 `PsdDocumentBinding` 添加新绑定

#### Scenario: 处理删除的图层
- **WHEN** 增量导入时绑定记录中的图层在 PSD 中不存在
- **THEN** 系统将对应的 GameObject 标记为"孤立节点"（添加 Tag 或组件标记）
- **AND** 在导入完成后显示孤立节点列表
- **AND** 由用户决定是否删除这些节点

#### Scenario: 覆盖无绑定的 Prefab
- **WHEN** 用户导入 PSD 文件到已存在但没有 `PsdDocumentBinding` 的 Prefab
- **THEN** 系统显示警告对话框
- **AND** 询问用户是否覆盖现有 Prefab

---

## REMOVED Requirements

### Requirement: 编辑器窗口布局
**Reason**: 简化工作流，不再需要复杂的编辑器窗口。图层配置直接在 Prefab 的 Inspector 中进行。
**Migration**: 用户可以直接在 Prefab 中编辑 RectTransform、Image、Text 等组件属性。

### Requirement: 图层树视图
**Reason**: 随编辑器窗口一起移除。
**Migration**: 用户可以在 Hierarchy 中查看 Prefab 的层级结构。

### Requirement: 预览窗口
**Reason**: 随编辑器窗口一起移除。
**Migration**: 用户可以直接在 Scene 视图中预览 Prefab。

### Requirement: 属性面板
**Reason**: 随编辑器窗口一起移除。
**Migration**: 用户可以在 Unity 原生 Inspector 中编辑组件属性。

### Requirement: 编辑状态文件管理
**Reason**: 编辑状态现在保存到 `PsdDocumentBinding` 组件中，不再需要外部状态文件。
**Migration**: 旧的 `Library/PsdToUgui/*.json` 文件可以删除，重新导入 PSD 即可。
