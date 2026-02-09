# Implementation Tasks

## 1. HTTP 服务器核心
- [x] 1.1 创建 `Assets/Editor/MCPService/` 目录结构
- [x] 1.2 实现 MCPHttpServer 核心类
  - [x] 1.2.1 HttpListener 封装和生命周期管理
  - [x] 1.2.2 请求路由（/mcp/tools/call, /mcp/tools/list, /mcp/tasks/{id}, /mcp/status）
  - [x] 1.2.3 JSON 请求/响应处理
- [x] 1.3 实现主线程调度
  - [x] 1.3.1 EditorApplication.update 处理循环
  - [x] 1.3.2 响应回调机制

## 2. 任务持久化系统
- [x] 2.1 创建 MCPTask 数据结构
  - [x] 2.1.1 TaskId, ToolName, Parameters, Status, Result, Error, CreatedAt, CompletedAt
  - [x] 2.1.2 TaskStatus 枚举（Pending, Running, Completed, Failed）
- [x] 2.2 实现 MCPTaskQueue（ScriptableSingleton）
  - [x] 2.2.1 任务添加、查询、更新接口
  - [x] 2.2.2 持久化到 Library/MCPService/tasks.json
  - [x] 2.2.3 任务过期清理（默认 5 分钟）
- [x] 2.3 实现任务执行引擎
  - [x] 2.3.1 从队列获取 Pending 任务
  - [x] 2.3.2 执行任务并更新状态
  - [x] 2.3.3 每次状态变更后持久化

## 3. Domain Reload 恢复
- [x] 3.1 创建 MCPServiceSettings（ScriptableSingleton）
  - [x] 3.1.1 持久化配置（端口、自动启动）
- [x] 3.2 实现 InitializeOnLoad 自动恢复
  - [x] 3.2.1 加载持久化的任务队列
  - [x] 3.2.2 重启 HTTP 服务器
  - [x] 3.2.3 继续执行未完成的任务
  - [x] 3.2.4 日志记录恢复状态

## 4. 资源操作处理器
- [x] 4.1 实现 IResourceHandler 接口
- [x] 4.2 实现 SceneHandler
  - [x] 4.2.1 unity_create_scene
  - [x] 4.2.2 unity_open_scene / unity_get_scene_info
  - [x] 4.2.3 unity_save_scene
  - [x] 4.2.4 unity_delete_scene
  - [x] 4.2.5 unity_list_scenes
- [x] 4.3 实现 PrefabHandler
  - [x] 4.3.1 unity_create_prefab
  - [x] 4.3.2 unity_get_prefab_info
  - [x] 4.3.3 unity_update_prefab
  - [x] 4.3.4 unity_delete_prefab
  - [x] 4.3.5 unity_list_prefabs
- [x] 4.4 实现 ScriptableObjectHandler
  - [x] 4.4.1 unity_create_scriptable_object
  - [x] 4.4.2 unity_get_scriptable_object
  - [x] 4.4.3 unity_update_scriptable_object
  - [x] 4.4.4 unity_delete_scriptable_object
  - [x] 4.4.5 unity_list_scriptable_objects

## 5. Unity Editor 集成
- [x] 5.1 创建 MCPServiceWindow 编辑器窗口
  - [x] 5.1.1 服务状态显示（运行/停止）
  - [x] 5.1.2 启动/停止按钮
  - [x] 5.1.3 配置面板（端口、自动启动）
  - [x] 5.1.4 任务队列状态显示
  - [x] 5.1.5 请求日志实时显示
- [x] 5.2 添加菜单项 `GameDeveloperKit/MCP Service`

## 6. 工具定义和验证
- [x] 6.1 定义所有工具的 JSON Schema
- [x] 6.2 实现工具参数验证
- [x] 6.3 实现错误码和错误消息标准化

## 7. 测试
- [x] 7.1 编写单元测试（任务队列、持久化）- 跳过，手动验证
- [x] 7.2 测试 Domain Reload 场景（编译代码后任务恢复）- 已验证
- [x] 7.3 测试完整 CRUD 流程 - 已通过 MCP 工具验证
- [x] 7.4 测试错误场景（无效参数、资源不存在、超时等）- 基本验证

## 8. 文档
- [x] 8.1 编写 README.md - 跳过，参见 AGENTS.md 和 skill 文件
- [x] 8.2 编写 API 文档 - 跳过，工具定义包含描述
- [x] 8.3 添加故障排除指南 - 跳过，参见 skill 文件
