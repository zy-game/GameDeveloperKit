# Change: 添加美术资源映射编辑器

## Why
美术和程序使用独立的工程进行开发，美术资源存放在单独的美术工程中。需要一个编辑器工具将美术工程的资源目录通过符号链接（mklink/ln）映射到程序工程中，避免美术直接在程序工程中编辑资源，同时保持资源的实时同步。

## What Changes
- 新增 `ArtResourceMappingWindow` 编辑器窗口，用于管理美术资源映射配置
- 使用 UI Toolkit 构建界面，复用项目通用样式 `EditorCommonStyle.uss`
- 支持配置美术工程根目录路径
- 支持添加/删除/编辑映射规则（源目录 -> 目标目录）
- 每个映射目录后提供"更新"按钮，支持单个目录的 SVN 更新
- 提供"全部更新"按钮，从美术工程根目录开始执行 SVN 更新
- 跨平台支持：Windows 使用 `mklink /D`，macOS/Linux 使用 `ln -s`
- 映射配置持久化存储到 `ProjectSettings/ArtResourceMappingSettings.json`

## Impact
- Affected specs: 新增 `art-resource-mapping` 能力规范
- Affected code: 
  - `Assets/Editor/ArtMapping/` - 新增编辑器模块
  - `Assets/Editor/ArtMapping/ArtResourceMappingWindow.cs` - 主窗口（使用 EditorCommonStyle.uss）
  - `Assets/Editor/ArtMapping/ArtResourceMappingSettings.cs` - 配置数据和持久化
  - `Assets/Editor/ArtMapping/SymlinkUtility.cs` - 符号链接工具类
  - `Assets/Editor/ArtMapping/SvnUtility.cs` - SVN 操作工具类
  - `ProjectSettings/ArtResourceMappingSettings.json` - 配置文件
