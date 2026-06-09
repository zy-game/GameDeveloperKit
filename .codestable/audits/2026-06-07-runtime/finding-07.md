---
doc_type: audit-finding
audit: 2026-06-07-runtime
finding_id: "security-07"
nature: security
severity: P2
confidence: medium
suggested_action: cs-issue
status: fixed
---

# Finding 07：Debug 日志脱敏没有覆盖 exception/context/tags 及外发 transport

## 速答

Debug redaction 当前只处理 category/message 和 analytics properties；`DebugLogRecord` 仍携带原始 `Exception`、`Context`、`Tags`，并会被 sink 或 transport 外发。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:195` 到 `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:205` — `DebugLogRecord` 中 category/message 调用 `RedactLogText`，但 `exception`、`context`、`tags` 原样写入。
- `Assets/GameDeveloperKit/Runtime/Debug/Logs/LogEntry.cs:46` 到 `Assets/GameDeveloperKit/Runtime/Debug/Logs/LogEntry.cs:50` — log record 暴露原始 `Exception`、`Context`、`Tags`。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:407` 到 `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:420` — 每条 record 会传给注册的 `IDebugLogTransport.SendAsync(record)`。
- `Assets/GameDeveloperKit/Runtime/Debug/Logs/UnityConsoleLogSink.cs:44` 到 `Assets/GameDeveloperKit/Runtime/Debug/Logs/UnityConsoleLogSink.cs:49` — Unity sink 格式化时直接拼接 `entry.Exception`。
- `Assets/GameDeveloperKit/Runtime/Debug/Logs/DebugRedactionUtility.cs:15` 到 `Assets/GameDeveloperKit/Runtime/Debug/Logs/DebugRedactionUtility.cs:31` — 脱敏工具只处理字符串值。

## 影响

如果异常消息、异常数据、context 对象或 tags 中包含 token、key、账号等敏感信息，即使 `Settings.RedactionEnabled` 为 true，也可能进入 Unity Console、内存日志缓存或自定义远端 transport。因为 transport 是公开扩展点，泄露边界不只在本地调试窗口。

## 修复方向

在生成 `DebugLogRecord` 前对 exception 展示文本、context、tags 做统一的可配置脱敏；或者把 record 分为 raw/local 与 redacted/export 两种视图，transport 默认只能拿 redacted record。

## 建议动作

`cs-issue`，因为这涉及敏感信息外发边界。
