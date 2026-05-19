---
doc_type: audit-finding
audit: 2026-05-18-filesystem
finding_id: "arch-drift-05"
nature: arch-drift
severity: P2
confidence: medium
suggested_action: cs-issue
status: open
---

# Finding 05：Manifest 记录 BundleName 但读取固定使用默认 bundle

## 速答

`VfsFileEntry` 保存了 `BundleName`，writer 也写入 bundle 文件名，但 `FileModule.ReadFileAsync` 始终从 `m_BundlePath` 读取，manifest 元数据与读取行为不一致。

## 关键证据

- `.codestable/architecture/ARCHITECTURE.md:13` — 记录 `VfsFileEntry` 是文件元数据，包含存储方式等索引信息。
- `Assets/GameDeveloperKit/Runtime/FileSystem/VfsBundleWriter.cs:57` — `BundleName = Path.GetFileName(bundlePath),` —— packed entry 保存了实际 bundle 文件名。
- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:90` — `data = await VfsBundleReader.ReadAsync(m_BundlePath, entry);` —— 读取时忽略 `entry.BundleName`，固定使用默认 `bundle_0.vfsb`。
- `Assets/GameDeveloperKit/Runtime/FileSystem/VfsFileEntry.cs:17` — `public string BundleName;` —— 字段存在但当前读取路径没有消费。

## 影响

当前只有默认单 bundle 时影响较小；一旦后续做多 bundle、分代、迁移或 manifest 导入，entry 中的 bundle 定位信息会失效，表现为读取错误文件或找不到数据。

## 修复方向

读取 packed entry 时基于 `entry.BundleName` 解析 bundle 物理路径，并对空值保留兼容默认 bundle 的迁移逻辑。

## 建议动作

`cs-issue`，因为这是实现与元数据模型不一致的问题，可作为兼容性缺陷定点修复。
