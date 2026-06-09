---
doc_type: audit-finding
audit: 2026-06-09-runtime-flow-modules
finding_id: "bug-03"
nature: bug
severity: P2
confidence: high
suggested_action: cs-issue
status: open
---

# Finding 03：ArgsBase 被 ReferencePool 复用后仍保持已消费状态

## 速答

`ArgsBase.Release()` 会调用 `Use()` 把事件标记为已消费，但没有任何 reset 路径；同一个事件参数实例经 `ReferencePool` 复用后，下一次 `Fire()` 会在第一个 listener 前直接停止派发。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Event/ArgsBase.cs:8` 到 `Assets/GameDeveloperKit/Runtime/Event/ArgsBase.cs:24`：`m_HasUse` 只会被 `Use()` 置为 `true`，`HasUse()` 直接返回该字段。
- `Assets/GameDeveloperKit/Runtime/Event/ArgsBase.cs:30` 到 `Assets/GameDeveloperKit/Runtime/Event/ArgsBase.cs:32`：`Release()` 的默认行为是 `Use()`，没有把 `m_HasUse` 清回 `false`。
- `Assets/GameDeveloperKit/Runtime/Core/ReferencePool.cs:298` 到 `Assets/GameDeveloperKit/Runtime/Core/ReferencePool.cs:310`：`ReferencePool.Release()` 调用 `reference.Release()` 后把同一实例重新入队。
- `Assets/GameDeveloperKit/Runtime/Core/ReferencePool.cs:263` 到 `Assets/GameDeveloperKit/Runtime/Core/ReferencePool.cs:265`：下一次 `Acquire<T>()` 会直接把队列中的同一实例取出。
- `Assets/GameDeveloperKit/Runtime/Event/EventModule.cs:197` 到 `Assets/GameDeveloperKit/Runtime/Event/EventModule.cs:200`：派发前检查 `eventData.HasUse()`，为 true 时直接 `break`。

## 影响

任何继承 `ArgsBase` 并通过 `ReferencePool` 管理的事件数据都会受影响。首次派发后如果调用方按 `IReference` 语义释放，再次 acquire 同一实例时事件已经处于 consumed 状态，所有 listener 都收不到事件。当前仓库内事件参数用例不多，所以定为 P2，但这个基类语义会误导后续事件类型。

## 修复方向

给 `ArgsBase` 增加明确 reset 语义，例如 `Release()` 清理所有基类状态，或新增受保护的 `Reset()` / `OnRelease()` 模板，确保池化复用时 `m_HasUse` 回到 false。

## 建议动作

`cs-issue`，因为这是事件基类和引用池组合下的确定性状态残留。
