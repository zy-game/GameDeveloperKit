## 1. Implementation

- [ ] 1.1 修改 `MCPServer.cs` 的 `HandleToolCall` 方法
  - 移除 `SaveTasks()` 调用，任务提交时不再同步写文件
- [ ] 1.2 确保 `ProcessTasks()` 中任务完成后调用 `SaveTasks()`
  - 已有此逻辑，确认无需修改
- [ ] 1.3 修改 `unity_mcp_proxy.py` 增加 HTTP 请求超时
  - `_request()` timeout: 10 → 30 秒（保险措施）
- [ ] 1.4 测试验证
  - 短时间内提交多个任务，验证不再超时
