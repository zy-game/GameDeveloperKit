---
doc_type: audit-finding
audit: 2026-05-19-filesystem-bugs
finding_id: "bug-01"
nature: bug
severity: P0
confidence: high
suggested_action: cs-issue
status: open
---

# Finding 01：WriteAsync 边界检查阻止写入新 Bundle 文件

## 速答

`VFSteaming.WriteAsync` 的边界检查 `offset + data.Length > m_Stream.Length` 会拒绝所有向新创建的 Bundle 文件写入数据，因为 `FileMode.OpenOrCreate` 新建文件长度为 0，导致首次写入必定抛 `ArgumentOutOfRangeException`。

## 关键证据

- `VFSteaming.cs:49` — `if (offset < 0 || data == null || offset + data.Length > m_Stream.Length)`
  - 构造函数 `VFSteaming.cs:17` 使用 `FileMode.OpenOrCreate` 打开流，新文件长度为 0
  - `VfsManifest.CreateBundle()` (VfsManifest.cs:86) 创建 offset=0 的条目
  - `FileModule.WriteAsync` (FileModule.cs:58-59) 创建新 `VFSteaming` 后调用 `steaming.WriteAsync(entry.Offset, data)`，其中 `entry.Offset = 0`，`data.Length > 0`
  - 代入检查：`0 + data.Length > 0` → `true` → 抛出 `ArgumentOutOfRangeException`
- 即使文件已有内容，后续写入位于较大 offset（如 4096、8192）时，`offset + data.Length` 也可能超过当前流长度，因为 `FileStream` 不会在未写入区域自动填充占位字节

## 影响

当前 VFS 写入路径完全不可用——任何向新 Bundle 写入的请求都会失败。`FileStream` 本身支持 Write 操作扩展文件长度，这个检查是不必要的限制。

## 修复方向

移除 `> m_Stream.Length` 条件，或改为检查是否超过 Bundle 文件的最大槽位边界（`VfsConstants.DefaultThreshold * VfsConstants.BundleFileCount`）。写入操作本身由 `FileStream.WriteAsync` 处理越界问题。

## 建议动作

`cs-issue`，因为这是明确的代码缺陷，需要定点修复并补充单元测试覆盖首次写入场景。
