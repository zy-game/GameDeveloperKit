---
doc_type: audit-finding
audit: 2026-05-19-filesystem-bugs
finding_id: "bug-02"
nature: bug
severity: P0
confidence: high
suggested_action: cs-issue
status: open
---

# Finding 02：WriteAsync 重复创建 VFSteaming 导致 FileShare.None 冲突

## 速答

`FileModule.WriteAsync` 每次写入都新建 `VFSteaming` 实例并加入 `m_Steamings`，但从不检查是否已存在同一 Bundle 文件的 steaming。`VFSteaming` 构造函数以 `FileShare.None` 打开文件，导致同一 Bundle 被写第二次时抛出 `IOException`（文件已被占用）。

## 关键证据

- `FileModule.cs:58-59` — `VFSteaming steaming = new VFSteaming(Path.Combine(this.m_RootPath, entry.BundlePath)); m_Steamings.Add(steaming);`
  - 每次 `WriteAsync` 调用无条件创建新 steaming，不检查 `m_Steamings` 是否已有同路径 steaming
- `VFSteaming.cs:17` — `m_Stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);`
  - `FileShare.None` 禁止任何其他进程或同一进程内其他 `FileStream` 打开同一文件
- 对比 `FileModule.ReadAsync` (FileModule.cs:76-80)，读取路径正确地先 `Find` 现有 steaming，不存在才新建。写入路径缺少同等逻辑。
- 同一 Bundle 中有多个槽位（`VfsConstants.BundleFileCount = 20`）时，多次写入不同虚拟路径可能分配到同一 Bundle 的不同 offset，触发此问题。

## 影响

任何对同一 Bundle 文件的第二次写入都会失败。在 VFS 实际使用中，一个 Bundle 可容纳最多 20 个文件条目，写入第 2 个文件就会触发此 bug。整个 VFS 写入通路实际上不可用。

## 修复方向

`FileModule.WriteAsync` 中复用 `ReadAsync` 的查找逻辑：先在 `m_Steamings` 中 `Find` 同路径 steaming，找到则复用，未找到才新建。同时考虑 `VFSteaming` 的 `FileShare` 是否应从 `None` 改为 `Read` 以允许同进程内共享访问。

## 建议动作

`cs-issue`，因为这是资源管理和并发访问的代码缺陷，需要修改 `FileModule.WriteAsync` 的 steaming 获取逻辑。
