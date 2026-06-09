---
doc_type: audit-finding
audit: 2026-06-08-debug-module
finding_id: "bug-03"
nature: bug
severity: P1
confidence: medium
suggested_action: cs-issue
status: open
---

# Finding 03：redaction 调用外部对象 `ToString()`，日志 API 可能反向抛异常

## 速答

日志写入在进入 buffer/sink/transport 前会做 redaction，其中 exception 和 context 会直接调用外部对象 `ToString()`；如果这些对象的 `ToString()` 抛异常，`App.Debug.Info/Error` 本身会失败。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:180` — 所有日志 API 最终进入 `WriteLog()`。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:195` — `DebugLogRecord` 构造发生在 sink/transport 之前。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:203` — record 构造时先调用 `RedactException(exception)`。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:204` — record 构造时先调用 `RedactContext(context)`。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:817` — `RedactException()` 直接执行 `exception.ToString()`。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:827` — `RedactContext()` 直接执行 `context.ToString()`。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:391` — sink 异常隔离只包住 `sink.Write(record)`，覆盖不到 record 构造前的 redaction 异常。

## 影响

Debug 是基础设施，通常会在错误现场被调用。context 可能是业务对象、Unity 对象包装、懒加载对象或第三方类型；自定义 exception 也可以覆写 `ToString()`。当前 redaction 发生在写入前且没有兜底，导致“记录错误”这件事本身可能抛出新异常，掩盖原始问题并打断调用栈。

## 修复方向

把 exception/context 的字符串化放进安全 helper：捕获 `ToString()` 异常后写入 fallback 文本，并保留 Unity object context 的可用性。

## 建议动作

`cs-issue`，因为这是日志 API 的稳定性问题，需要补覆盖异常 `ToString()` 的单元测试。
