---
doc_type: audit-finding
audit: 2026-06-08-debug-module
finding_id: "bug-01"
nature: bug
severity: P1
confidence: medium
suggested_action: cs-issue
status: open
---

# Finding 01：异步 log transport fire-and-forget，失败状态与 shutdown 存在竞态

## 速答

`IDebugLogTransport` 的发送被 fire-and-forget 掉，真正异步的 transport 可能在日志调用返回后才失败，也可能在 `Shutdown()` 清空状态后继续回写 `LastTransportException`。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:407` — `Send()` 对 transport 做快照后逐个调用 `SendAsync(transport, record).Forget()`，调用方无法等待发送完成。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:416` — `SendAsync()` 在异步 catch 中写入 `LastTransportException`，但这个写入时间不受 `WriteLog()` 或 `Shutdown()` 约束。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:98` — `Shutdown()` 清空 `m_Transports` 和 `LastTransportException`，没有等待或取消已经发出的 transport 任务。
- `Assets/GameDeveloperKit/Tests/Runtime/DebugModuleTests.cs:224` — 现有 transport 测试使用立即抛出/立即完成的实现，断言同步可见的 `LastTransportException`，没有覆盖延迟失败路径。

## 影响

Debug 文档承诺 transport 失败会记录在 Debug 状态且不阻断业务。当前实现对同步 transport 成立，但对网络发送、队列发送等真实异步 transport，错误状态会延迟出现，甚至在模块关闭后重新污染状态。排查线上实时日志丢失时，这类竞态会让 Console 的 Last Transport Error 不可靠。

## 修复方向

为 transport 发送建立可观察的生命周期：记录 pending task、在 shutdown 时取消或等待收束，并补充延迟失败 transport 的测试。

## 建议动作

`cs-issue`，因为这是可复现的异步状态竞态，应该定点修行为和测试。
