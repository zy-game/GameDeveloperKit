# Change: 修复 MCP ScriptableObject 更新超时问题

## Why

调用 `unity_update_scriptable_object` MCP 工具时超时，**且任务没有出现在 MCP 服务器的任务列表中**。

这说明问题发生在 **任务提交阶段**，而不是任务处理阶段。

## Root Cause Analysis

**核心问题：短时间内提交多个任务时，锁竞争和同步文件 I/O 导致请求阻塞超时**

调用链：
```
Proxy call_tool() 
  → HTTP POST /mcp/tools/call (timeout=10s)
    → MCPServer.HandleToolCall() [ThreadPool 线程]
      → lock(_taskLock) { AddOrUpdateTask() }  ← 等待锁
      → _pendingTasks.Enqueue()
      → SaveTasks()  ← 同步文件 I/O + 再次获取锁
      → return taskId
```

当多个请求并发时：
```
Request 1: lock → AddOrUpdateTask → unlock → SaveTasks(lock → 写文件 → unlock)
Request 2: 等待 lock... (被 Request 1 阻塞)
Request 3: 等待 lock... (被 Request 1, 2 阻塞)
...
```

**问题点**：
1. 每个 `HandleToolCall` 都同步调用 `SaveTasks()` 写文件
2. `SaveTasks()` 内部也需要获取 `_taskLock`
3. 多个并发请求串行等待，累积延迟超过 HTTP timeout (10s)

## What Changes

1. **移除 `HandleToolCall` 中的 `SaveTasks()` 调用** - 任务提交时不再同步写文件
2. **改为定期保存或任务完成后保存** - 在 `ProcessTasks()` 完成任务后保存
3. **增加 HTTP 请求超时** (10s → 30s) - 作为保险措施

## Impact

- Affected specs: mcp-service
- Affected code:
  - `Assets/Editor/MCPService/unity_mcp_proxy.py`
  - `Assets/Editor/MCPService/MCPServer.cs`
