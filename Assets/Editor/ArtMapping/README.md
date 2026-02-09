# 美术资源映射编辑器

## 功能说明

美术资源映射编辑器用于将美术工程的资源目录通过符号链接映射到程序工程中，避免美术直接在程序工程中编辑资源。

## 使用方法

1. 打开编辑器：`Tools > GameDeveloperKit > Art Resource Mapping`
2. 设置美术工程根目录
3. 添加映射规则（源目录 -> 目标目录）
4. 点击"创建链接"按钮创建符号链接
5. 使用"更新"按钮进行 SVN 更新

## 功能特性

- ✅ 跨平台支持（Windows mklink / macOS ln -s）
- ✅ SVN 更新集成（单个目录 + 全部更新）
- ✅ 符号链接状态检测
- ✅ 配置持久化到 ProjectSettings

## 故障排除

### ⚠️ 窗口打开失败 (NullReferenceException)

如果遇到 `NullReferenceException: Object reference not set to an instance of an object` 错误：

**这是 Unity 编辑器窗口系统的已知问题，不是代码错误。**

**快速解决方案**：
1. **重置 Unity 布局**：`Window > Layouts > Default`
2. **重启 Unity 编辑器**
3. 如果问题持续：
   - 关闭 Unity
   - 删除项目根目录下的 `Library` 文件夹
   - 重新打开项目

**原因**：
- Unity 编辑器布局文件损坏
- 窗口系统状态不一致
- 之前的窗口实例没有正确清理

### 符号链接创建失败

**Windows**:
- 需要管理员权限
- 或启用开发者模式（Windows 10/11）
  1. 设置 > 更新和安全 > 开发者选项
  2. 启用"开发人员模式"

**macOS/Linux**:
- 确保有目录写入权限
- 使用 `chmod` 命令调整权限

### SVN 更新失败

- 确保已安装 SVN 命令行工具
- 确保 SVN 在系统 PATH 中
- 检查源目录是否为有效的 SVN 工作副本
- Windows: 安装 TortoiseSVN 时勾选"command line client tools"

## 配置文件

配置保存在：`ProjectSettings/ArtResourceMappingSettings.json`

## 技术说明

### 资源加载系统

项目使用 `EditorAssetLoader` 统一加载编辑器资源，自动支持：
- 项目内开发模式：`Assets/Editor/...`
- Package 引用模式：`Packages/com.gamedeveloperkit.framework/Editor/...`

### 符号链接原理

- **Windows**: 使用 `mklink /D` 创建目录符号链接
- **macOS/Linux**: 使用 `ln -s` 创建符号链接
- 符号链接指向源目录，但在目标位置显示为普通目录
- 删除符号链接不会影响源目录内容

