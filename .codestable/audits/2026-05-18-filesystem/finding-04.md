---
doc_type: audit-finding
audit: 2026-05-18-filesystem
finding_id: "performance-04"
nature: performance
severity: P2
confidence: high
suggested_action: cs-refactor
status: open
---

# Finding 04：Packed 文件覆盖写入只追加不回收，bundle 会持续膨胀

## 速答

小文件每次写入都 append 到同一个 `.vfsb`，manifest 只指向最新 entry，旧记录无法回收，频繁覆盖会让 bundle 持续增大。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:43` — `if (data.Length < m_Threshold)` —— 小文件进入 packed 存储。
- `Assets/GameDeveloperKit/Runtime/FileSystem/VfsBundleWriter.cs:39` — `stream.Seek(0, SeekOrigin.End);` —— 每次写入都追加到文件末尾。
- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:70` — `m_Manifest.AddOrUpdateEntry(entry);` —— manifest 只保留最新 entry，旧 packed 数据不再可达但仍占用磁盘。
- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:140` — `entry.Flags |= FileFlags.Deleted;` —— 删除 packed 文件也只是软删除 manifest，不回收 bundle 空间。

## 影响

配置、存档、缓存等小文件如果频繁更新，`bundle_0.vfsb` 会随着写入次数线性增长，长期运行后浪费持久化存储并增加备份/迁移成本。

## 修复方向

设计 packed bundle compaction、分代 bundle 或空洞复用策略；至少暴露维护入口用于清理 deleted/obsolete packed records。

## 建议动作

`cs-refactor`，因为它是行为不变的存储结构优化，需要先设计回收策略和兼容性。
