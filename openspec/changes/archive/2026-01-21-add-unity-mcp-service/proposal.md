# Change: 添加 Unity Editor MCP Service

## Why
当前缺少一个标准化的接口让 AI 助手（如 Claude Desktop、Cursor 等）能够直接操作 Unity 编辑器中的资源。开发者需要手动在 Unity 中创建、修改场景、Prefab、ScriptableObject 等资源，效率较低。

通过实现 MCP (Model Context Protocol) 服务器，可以让 AI 助手通过标准协议直接与 Unity Editor 交互，实现资源的自动化创建、查询、更新和删除，大幅提升开发效率。

## What Changes
- 在 `Assets/Editor/MCPService/` 下创建 MCP 服务器实现
- 支持场景（Scene）、预制体（Prefab）、ScriptableObject 的 CRUD 操作
- 实现 MCP 标准协议（基于 stdio 通信）
- 提供资源搜索、列表、属性读写等功能
- 支持 Unity 编辑器生命周期管理（启动时自动启动服务）
- 提供配置界面用于管理 MCP 服务设置

## Impact
- 新增能力：`editor-mcp-service` - Unity Editor MCP 服务器
- 影响文件：
  - 新增：`Assets/Editor/MCPService/` 目录及相关文件
  - 新增：`Assets/Editor/MCPService/MCPServer.cs` - MCP 服务器核心
  - 新增：`Assets/Editor/MCPService/Handlers/` - 资源操作处理器
  - 新增：`Assets/Editor/MCPService/Protocol/` - MCP 协议实现
  - 新增：`Assets/Editor/MCPService/Window/MCPServiceWindow.cs` - 配置窗口
- 依赖：需要 .NET 进程间通信（stdio）
- 兼容性：仅在 Unity Editor 环境下运行，不影响运行时
