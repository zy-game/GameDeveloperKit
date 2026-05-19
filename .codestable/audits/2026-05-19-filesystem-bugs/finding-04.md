---
doc_type: audit-finding
audit: 2026-05-19-filesystem-bugs
finding_id: "bug-04"
nature: bug
severity: P1
confidence: medium
suggested_action: cs-issue
status: open
---

# Finding 04：GetFileInfo 未找到时静默返回 null

## 速答

`FileModule.GetFileInfo(string path)` 在条目不存在时静默返回 `null`，与同模块其他方法（`ReadAsync`、`DeleteAsync`）的行为不一致。调用方若未做 null 检查会触发 `NullReferenceException`。

## 关键证据

- `FileModule.cs:103-106` —
  ```csharp
  public VFSMeta GetFileInfo(string path)
  {
      m_Manifest.TryGetEntry(path, out var entry);
      return entry;
  }
  ```
  - `TryGetEntry` 在未找到时将 `entry` 设为 `null`（VfsManifest.cs:81），`GetFileInfo` 直接返回
  - 对比 `ReadAsync`（FileModule.cs:67）：未找到时返回 `null` 但返回类型是 `UniTask<byte[]>`，调用方通常在异步上下文中处理 null
  - 对比 `DeleteAsync`（FileModule.cs:96）：未找到时直接 `return`，不抛异常
  - `GetFileInfo` 的签名暗示它总是返回有效对象，静默 null 是调用方的陷阱

## 影响

调用方访问返回值的属性（如 `entry.FilePath`、`entry.Usegd`）时触发 NRE。由于编译器不会对引用类型返回值给出警告，此问题容易被遗漏，仅在运行时暴露。

## 修复方向

两种方向：(1) 改为 `TryGetFileInfo(string path, out VFSMeta entry)` 模式，让调用方显式处理未找到；(2) 未找到时抛 `KeyNotFoundException` 或 `GameException`。

## 建议动作

`cs-issue`，因为涉及公开 API 语义变更，需要评估调用方影响后定点修复。
