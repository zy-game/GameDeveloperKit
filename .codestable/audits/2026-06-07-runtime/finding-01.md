---
doc_type: audit-finding
audit: 2026-06-07-runtime
finding_id: "arch-drift-01"
nature: arch-drift
severity: P1
confidence: high
suggested_action: cs-issue
status: fixed
---

# Finding 01：VFS 存储策略与 CRC32 完整性契约没有落到代码

## 速答

架构文档要求小文件打包、大文件独立存储，并在读取后强制 CRC32 校验；当前 `FileModule` 阈值方向相反、始终写入 bundle，读取也没有校验 CRC32。

## 关键证据

- `.codestable/architecture/ARCHITECTURE.md:23` — 记录“小文件（< 阈值 4096 字节）合并写入单一 Bundle 文件，大文件（>= 阈值）独立文件存储”。
- `.codestable/architecture/ARCHITECTURE.md:119` — 记录 `FileModule.ReadFileAsync` 读取后强制 CRC32 校验。
- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:101` — 写入时计算 `crc32`，但后续读取路径没有使用该值。
- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:109` — `data.Length > VfsConstants.DefaultThreshold ? StorageType.Packed : StorageType.Standalone`，与文档的小文件打包、大文件独立相反。
- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:111` 到 `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:119` — 不论 `StorageType` 是什么，都取 `entry.BundlePath` 并写入 `VFSteaming`。
- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:128` 到 `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:142` — 读取只按 offset/size 返回字节，没有重新计算并比对 `entry.Crc32`。

## 影响

大文件会被写入预分配 bundle offset，可能覆盖后续 slot 或膨胀 bundle；小文件会被标记为 `Standalone` 但仍放在 bundle 中，后续维护者按元数据实现独立文件路径时会读不到数据。读取缺少 CRC32 校验会让损坏文件、短读或错位读取静默进入上层 Data/Resource/Config 流程。

## 修复方向

把 VFS storage 策略和实际读写路径对齐：小文件走 bundle slot，大文件走独立文件；读取后强制计算 CRC32 并在不匹配时抛 `GameException`。

## 建议动作

`cs-issue`，因为这是持久化数据正确性问题，不只是结构整理。
