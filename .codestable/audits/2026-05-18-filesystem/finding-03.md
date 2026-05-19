---
doc_type: audit-finding
audit: 2026-05-18-filesystem
finding_id: "bug-03"
nature: bug
severity: P1
confidence: medium
suggested_action: cs-issue
status: open
---

# Finding 03：Bundle 读取信任 manifest offset/size，损坏清单可读错或异常

## 速答

`VfsBundleReader` 根据 manifest 中的 offset 直接 seek 并读取长度字段，未校验 bundle header、offset 范围、记录路径、记录 CRC 或实际读取长度。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/FileSystem/VfsBundleReader.cs:26` — `stream.Seek(entry.Offset, SeekOrigin.Begin);` —— 没有检查 offset 是否落在有效记录边界内。
- `Assets/GameDeveloperKit/Runtime/FileSystem/VfsBundleReader.cs:28` — `var dataSize = reader.ReadInt64();` —— 读取到的长度完全来自文件内容，未与 `entry.Size` 比较，也未检查负数/超大值。
- `Assets/GameDeveloperKit/Runtime/FileSystem/VfsBundleReader.cs:29` — `var storedCrc32 = reader.ReadUInt32();` —— 读取了记录内 CRC 但未使用；实际校验只依赖 manifest 的 `entry.Crc32`。
- `Assets/GameDeveloperKit/Runtime/FileSystem/VfsBundleReader.cs:31` — `var storedVersion = ...` 和 `VfsBundleReader.cs:32` — `var storedTimestamp = ...` —— 读取后未使用，说明记录元数据没有参与一致性校验。

## 影响

manifest 或 bundle 局部损坏时，读取可能抛底层 IO/格式异常、分配异常大小数组，或从错误 offset 返回错误内容；触发条件依赖文件损坏或 manifest 被篡改。

## 修复方向

读取前校验 bundle header/format，校验 offset、size、path、record CRC 与 manifest 一致，并把格式错误包装为明确的框架异常。

## 建议动作

`cs-issue`，因为这是数据损坏路径下的正确性问题，需要明确异常语义和损坏用例。
