# Change: 增强 MCP 服务 - 扩展工具集与包模式支持

## Why
当前 MCP 服务存在两个主要问题：
1. **工具集不足** - 仅支持 Scene、Prefab、ScriptableObject、GameObject 四类基础操作，缺少 Material、Texture、Animation、Audio、Project Settings 等常用资源操作，以及代码执行、控制台日志等开发辅助功能
2. **包模式资源加载问题** - 当 GameDeveloperKit 作为 UPM 包在其他项目中使用时，编辑器资源（如 `unity_mcp_proxy.py`）位于 `Packages/` 目录而非 `Assets/`，当前使用 `Application.dataPath` 的硬编码路径无法正确定位这些资源

## What Changes

### 1. 扩展 MCP 工具集

**新增资源操作工具：**
- Material Handler: 创建/读取/更新/删除材质，设置 Shader 属性
- Texture Handler: 导入/读取纹理信息，修改导入设置
- Animation Handler: 列出/读取动画剪辑信息
- Audio Handler: 列出/读取音频剪辑信息，修改导入设置
- Asset Handler: 通用资源操作（复制、移动、重命名、查找引用）

**新增编辑器辅助工具：**
- Console Handler: 读取/清除控制台日志，执行 Debug.Log
- Editor Handler: 获取/设置编辑器状态（播放模式、暂停、选中对象）
- Project Handler: 获取项目设置，刷新 AssetDatabase
- Code Handler: 执行 C# 代码片段（受限沙箱环境）

**增强现有工具：**
- GameObject: 支持获取/设置组件属性
- Prefab: 支持实例化到场景、应用/还原修改
- ScriptableObject: 支持嵌套对象和数组字段更新

### 2. 修复包模式资源加载

**BREAKING** - 资源路径解析逻辑变更

- 使用 `PackageInfo.FindForAssembly()` 或 `AssetDatabase.GetAssetPath()` 动态定位包路径
- 支持 Assets 模式（开发）和 Packages 模式（发布）两种部署方式
- 更新 `MCPServer.InstallProxy()` 使用正确的源文件路径

## Impact
- Affected specs: `editor-mcp-service`
- Affected code:
  - `Assets/Editor/MCPService/MCPServer.cs` - 路径解析逻辑
  - `Assets/Editor/MCPService/Handlers/` - 新增 Handler 文件
  - `Assets/Editor/MCPService/Window/MCPServiceWindow.cs` - 可能需要更新 UI
