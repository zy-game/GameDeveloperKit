## 1. 基础架构
- [x] 1.1 创建 `Assets/Editor/ArtMapping/` 目录结构
- [x] 1.2 创建 `ArtResourceMappingSettings.cs` 配置数据类（JSON 序列化，存储到 ProjectSettings）

## 2. 工具类实现
- [x] 2.1 创建 `SymlinkUtility.cs` 符号链接工具类
  - [x] 2.1.1 实现 Windows mklink /D 命令封装
  - [x] 2.1.2 实现 macOS/Linux ln -s 命令封装
  - [x] 2.1.3 实现符号链接检测和删除功能
- [x] 2.2 创建 `SvnUtility.cs` SVN 操作工具类
  - [x] 2.2.1 实现 SVN update 命令封装
  - [x] 2.2.2 实现异步执行和进度回调

## 3. 编辑器窗口实现
- [x] 3.1 创建 `ArtResourceMappingWindow.cs` 主窗口（UI Toolkit）
  - [x] 3.1.1 加载并应用 `EditorCommonStyle.uss` 通用样式
  - [x] 3.1.2 实现美术工程根目录配置 UI
  - [x] 3.1.3 实现映射规则列表 UI（源目录、目标目录、状态）
  - [x] 3.1.4 实现添加/删除映射规则功能
  - [x] 3.1.5 实现单个目录"更新"按钮（SVN update）
  - [x] 3.1.6 实现"全部更新"按钮（从根目录 SVN update）
  - [x] 3.1.7 实现"创建链接"/"删除链接"按钮
- [x] 3.2 添加菜单入口 `Tools/GameDeveloperKit/Art Resource Mapping`

## 4. 测试与验证
- [ ] 4.1 Windows 平台符号链接创建/删除测试
- [ ] 4.2 macOS 平台符号链接创建/删除测试
- [ ] 4.3 SVN 更新功能测试
- [ ] 4.4 配置持久化测试（ProjectSettings/ArtResourceMappingSettings.json）
