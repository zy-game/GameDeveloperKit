# Design: Unity Editor MCP Service

## Context
Model Context Protocol (MCP) 是 Anthropic 推出的开放协议，用于让 AI 应用与外部数据源和工具集成。通过实现 MCP 服务器，Unity Editor 可以暴露其资源管理能力给 AI 助手，实现自动化的资源操作。

### 约束
- Unity Editor 运行在主线程，需要考虑线程安全
- **Unity 编译时会触发 Domain Reload，所有托管代码状态丢失**
- 需要支持 Unity 的 AssetDatabase API
- 必须在 Editor 环境下运行，不能影响运行时构建

### 相关方
- AI 助手用户（开发者）
- Unity Editor
- MCP 客户端（Claude Desktop、Cursor 等需要通过配置 HTTP endpoint）

## Goals / Non-Goals

### Goals
- 实现基于 HTTP 的 MCP 服务器
- 支持场景、Prefab、ScriptableObject 的完整 CRUD 操作
- **通过任务持久化机制，在 Domain Reload 后恢复未完成的任务**
- 提供友好的编辑器窗口用于管理服务
- 确保操作的原子性和错误处理

### Non-Goals
- 不支持运行时资源操作（仅 Editor）
- 不使用 stdio 通信（Unity Editor 不适合）
- 不处理复杂的资源依赖关系

## Decisions

### 1. 通信方式：HTTP Server
**决策**：Unity Editor 直接启动 HTTP 服务器

**理由**：
- 简单直接，无需外部进程
- MCP 协议支持 HTTP+SSE 传输
- 易于调试和测试
- C# HttpListener 原生支持

**实现**：
```csharp
public class MCPHttpServer
{
    private HttpListener _listener;
    private const int DefaultPort = 27182;
    
    public void Start()
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{DefaultPort}/");
        _listener.Start();
        BeginAcceptRequest();
    }
}
```

### 2. Domain Reload 处理：任务持久化
**决策**：将进行中的任务持久化到磁盘，Domain Reload 后恢复执行

**理由**：
- 无需外部进程，架构简单
- 任务状态可追溯，便于调试
- 客户端通过轮询获取结果，对 Reload 无感知

**架构**：
```
┌─────────────────┐                    ┌─────────────────┐
│  MCP Client     │  HTTP Request      │  Unity Editor   │
│  (AI Assistant) │ ──────────────────►│  HTTP Server    │
└─────────────────┘                    └────────┬────────┘
        ▲                                       │
        │                                       ▼
        │                              ┌─────────────────┐
        │  Poll for result             │  Task Queue     │
        │◄─────────────────────────────│  (Persistent)   │
        │                              └────────┬────────┘
        │                                       │
        │                              Domain Reload
        │                                       │
        │                              ┌────────▼────────┐
        │                              │  Task Recovery  │
        │◄─────────────────────────────│  & Execution    │
                                       └─────────────────┘
```

**任务生命周期**：
1. 客户端发送请求 → 创建任务（状态：Pending）→ 返回 taskId
2. 任务执行 → 更新状态（Running → Completed/Failed）
3. 客户端轮询 `/tasks/{taskId}` 获取结果
4. 如果 Domain Reload 发生：
   - Reload 前：任务状态已持久化
   - Reload 后：恢复任务队列，继续执行未完成任务

**持久化存储**：
```csharp
// 任务存储在 Library/MCPService/tasks.json
[Serializable]
public class MCPTask
{
    public string TaskId;
    public string ToolName;
    public string Parameters;  // JSON
    public TaskStatus Status;  // Pending, Running, Completed, Failed
    public string Result;      // JSON
    public string Error;
    public long CreatedAt;
    public long CompletedAt;
}

// 使用 ScriptableSingleton 管理任务队列
public class MCPTaskQueue : ScriptableSingleton<MCPTaskQueue>
{
    public List<MCPTask> Tasks = new();
    
    public void Save() => 
        File.WriteAllText(TaskFilePath, JsonUtility.ToJson(this));
    
    public void Load() => 
        JsonUtility.FromJsonOverwrite(File.ReadAllText(TaskFilePath), this);
}
```

### 3. 自动恢复机制
**决策**：使用 InitializeOnLoad 在 Domain Reload 后自动恢复

**实现**：
```csharp
[InitializeOnLoad]
public static class MCPServiceAutoStart
{
    static MCPServiceAutoStart()
    {
        EditorApplication.delayCall += () =>
        {
            // 1. 恢复任务队列
            MCPTaskQueue.Instance.Load();
            
            // 2. 重启 HTTP 服务器
            if (MCPServiceSettings.Instance.AutoStart)
            {
                MCPHttpServer.Instance.Start();
            }
            
            // 3. 继续执行未完成的任务
            MCPTaskQueue.Instance.ProcessPendingTasks();
        };
    }
}
```

### 4. API 设计
**决策**：RESTful 风格 + 异步任务模式

**端点**：
```
POST /mcp/tools/call
  请求: { "name": "unity_create_scene", "arguments": {...} }
  响应: { "taskId": "xxx" }

GET /mcp/tasks/{taskId}
  响应: { "status": "completed", "result": {...} }
  或:   { "status": "running" }
  或:   { "status": "failed", "error": "..." }

GET /mcp/tools/list
  响应: { "tools": [...] }

GET /mcp/status
  响应: { "running": true, "pendingTasks": 3 }
```

### 5. 线程模型
**决策**：HTTP 请求在后台线程接收，Unity 操作在主线程执行

**实现**：
```csharp
// HTTP 线程接收请求
private void HandleRequest(HttpListenerContext context)
{
    var task = CreateTask(context.Request);
    MCPTaskQueue.Instance.AddTask(task);
    
    // 立即返回 taskId
    SendResponse(context, new { taskId = task.TaskId });
}

// 主线程处理任务（EditorApplication.update）
private void ProcessTasks()
{
    var task = MCPTaskQueue.Instance.GetNextPendingTask();
    if (task != null)
    {
        task.Status = TaskStatus.Running;
        try
        {
            task.Result = ExecuteTask(task);
            task.Status = TaskStatus.Completed;
        }
        catch (Exception e)
        {
            task.Error = e.Message;
            task.Status = TaskStatus.Failed;
        }
        MCPTaskQueue.Instance.Save();
    }
}
```

### 6. 工具定义
**工具列表**：
- `unity_create_scene` - 创建新场景
- `unity_open_scene` - 打开场景
- `unity_save_scene` - 保存场景
- `unity_delete_scene` - 删除场景
- `unity_list_scenes` - 列出所有场景
- `unity_create_prefab` - 创建 Prefab
- `unity_get_prefab` - 获取 Prefab 信息
- `unity_update_prefab` - 更新 Prefab
- `unity_delete_prefab` - 删除 Prefab
- `unity_list_prefabs` - 列出所有 Prefab
- `unity_create_scriptable_object` - 创建 ScriptableObject
- `unity_get_scriptable_object` - 获取 ScriptableObject
- `unity_update_scriptable_object` - 更新 ScriptableObject
- `unity_delete_scriptable_object` - 删除 ScriptableObject
- `unity_list_scriptable_objects` - 列出所有 ScriptableObject

## Risks / Trade-offs

### 风险 1：任务堆积
**风险**：大量任务可能导致队列过长

**缓解措施**：
- 设置任务过期时间（默认 5 分钟）
- 定期清理已完成/过期任务
- 限制最大并发任务数

### 风险 2：客户端轮询开销
**风险**：频繁轮询增加负载

**缓解措施**：
- 简单任务同步返回（不创建任务）
- 建议客户端使用指数退避轮询
- 后续可考虑 SSE 推送

### Trade-off：同步 vs 异步
**选择**：默认异步（任务模式），简单操作可同步

**理由**：
- 异步模式对 Domain Reload 友好
- 简单查询（如 list_scenes）可同步返回

## Open Questions
1. **任务过期时间设置多少合适？** - 建议 5 分钟
2. **是否需要支持任务取消？** - 第一版暂不支持
3. **是否需要 SSE 推送？** - 后续版本考虑
4. **是否需要支持材质、纹理等其他资源类型？** - 先实现核心三种，后续根据反馈扩展
