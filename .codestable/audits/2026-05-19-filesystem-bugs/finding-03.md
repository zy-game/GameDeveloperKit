---
doc_type: audit-finding
audit: 2026-05-19-filesystem-bugs
finding_id: "bug-03"
nature: bug
severity: P1
confidence: medium
suggested_action: cs-issue
status: open
---

# Finding 03：ReadAsync 边界检查可能拒绝稀疏 Bundle 中的合法读取

## 速答

`VFSteaming.ReadAsync` 使用 `offset + size > m_Stream.Length` 做边界检查。Bundle 文件中条目按固定 offset 分布（0、4096、8192...），但文件长度仅增长到已写入的最高字节位置。当读取位于较高 offset 的条目时，若该位置之前有空隙（未被写入的槽位），流长度可能短于 `offset + size`，导致合法读取被拒绝。

## 关键证据

- `VFSteaming.cs:31` — `if (offset < 0 || size < 0 || offset + size > m_Stream.Length)`
  - Bundle 条目 offset 为 `i * VfsConstants.DefaultThreshold`（VfsManifest.cs:89），即 0, 4096, 8192...
  - 若槽位 0 写入 100 字节、槽位 1 (offset=4096) 未写入、槽位 2 (offset=8192) 写入 200 字节：流长度 ≈ 8392，读取 offset=4096 的条目时 `4096 + size > 8392` 为 false（假设 size 不大），但读取 offset=8192 时 `8192 + 200 = 8392`，刚好等于长度，不会触发。
  - 更坏的情况：若先写入高 offset 槽位（如 offset=8192），流长度约 8392，此时读取低 offset 未写入槽位（如 offset=0，即便该槽位后来被写入）——但 `m_Stream.Length` 是动态的，读取后写入的槽位时流长度已包含其数据。
  - 实际情况中触发概率较低，因为 VFS 按顺序分配槽位，且每个槽位写入后会扩展流长度。但若 Bundle 复用（条目被 `Unused` 后重新 `Used`），写入顺序可能与 offset 顺序不一致。

## 影响

在特定条件下（Bundle 条目复用且写入顺序与 offset 顺序不一致时），合法读取会被错误拒绝。触发概率低，但会导致数据读取失败。

## 修复方向

对于读取操作，`FileStream.ReadAsync` 本身会在 offset 超出流长度时返回 0 字节，不会崩溃。可以移除 `> m_Stream.Length` 检查，或改为检查 `offset` 是否超出 Bundle 最大槽位范围。

## 建议动作

`cs-issue`，因为属于边界条件缺陷，需要修改检查逻辑并补充稀疏 Bundle 的读取测试。
