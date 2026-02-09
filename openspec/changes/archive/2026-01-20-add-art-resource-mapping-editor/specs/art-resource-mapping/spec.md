## ADDED Requirements

### Requirement: Art Resource Mapping Editor Window
系统 SHALL 提供一个 Unity 编辑器窗口，用于管理美术资源工程到程序工程的目录映射。

#### Scenario: 打开编辑器窗口
- **WHEN** 用户点击菜单 `Tools/GameDeveloperKit/Art Resource Mapping`
- **THEN** 系统显示美术资源映射编辑器窗口

#### Scenario: 配置美术工程根目录
- **WHEN** 用户在编辑器窗口中设置美术工程根目录路径
- **THEN** 系统保存该路径配置并验证路径有效性

### Requirement: Directory Mapping Configuration
系统 SHALL 支持配置多个目录映射规则，每个规则包含源目录（美术工程）和目标目录（程序工程）。

#### Scenario: 添加映射规则
- **WHEN** 用户点击"添加映射"按钮并指定源目录和目标目录
- **THEN** 系统创建新的映射规则并显示在列表中

#### Scenario: 删除映射规则
- **WHEN** 用户选择一个映射规则并点击"删除"按钮
- **THEN** 系统移除该映射规则（如存在符号链接则同时删除）

#### Scenario: 编辑映射规则
- **WHEN** 用户修改映射规则的源目录或目标目录
- **THEN** 系统更新配置并提示用户重新创建符号链接

### Requirement: Cross-Platform Symbolic Link Support
系统 SHALL 支持跨平台创建符号链接，Windows 使用 mklink，macOS/Linux 使用 ln -s。

#### Scenario: Windows 平台创建符号链接
- **WHEN** 用户在 Windows 平台点击"创建链接"按钮
- **THEN** 系统使用 `mklink /D` 命令创建目录符号链接

#### Scenario: macOS/Linux 平台创建符号链接
- **WHEN** 用户在 macOS 或 Linux 平台点击"创建链接"按钮
- **THEN** 系统使用 `ln -s` 命令创建目录符号链接

#### Scenario: 删除符号链接
- **WHEN** 用户点击"删除链接"按钮
- **THEN** 系统安全删除符号链接而不影响源目录内容

#### Scenario: 检测符号链接状态
- **WHEN** 编辑器窗口加载或刷新时
- **THEN** 系统检测每个映射的符号链接状态并显示（已链接/未链接/错误）

### Requirement: SVN Update Integration
系统 SHALL 支持通过 SVN 更新美术工程资源。

#### Scenario: 单个目录 SVN 更新
- **WHEN** 用户点击某个映射目录后的"更新"按钮
- **THEN** 系统对该映射的源目录执行 `svn update` 命令并显示进度

#### Scenario: 全部更新
- **WHEN** 用户点击"全部更新"按钮
- **THEN** 系统从美术工程根目录执行 `svn update` 命令更新所有资源

#### Scenario: 更新进度显示
- **WHEN** SVN 更新正在执行时
- **THEN** 系统显示更新进度和状态信息

### Requirement: Configuration Persistence
系统 SHALL 持久化保存映射配置到 ProjectSettings 目录。

#### Scenario: 保存配置
- **WHEN** 用户修改映射配置后
- **THEN** 系统自动保存配置到 `ProjectSettings/ArtResourceMappingSettings.json`

#### Scenario: 加载配置
- **WHEN** 编辑器窗口打开时
- **THEN** 系统从 `ProjectSettings/ArtResourceMappingSettings.json` 加载之前保存的映射配置

### Requirement: UI Style Consistency
系统 SHALL 使用项目通用的编辑器样式。

#### Scenario: 加载通用样式
- **WHEN** 编辑器窗口初始化时
- **THEN** 系统加载并应用 `EditorCommonStyle.uss` 通用样式表
