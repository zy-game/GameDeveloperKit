---
doc_type: issue-fix
issue: 2026-05-19-vfs-write-boundary-rejects-new-bundle
path: fast-track
fix_date: 2026-05-19
tags: [vfs, boundary-check, file-stream]
---

# VFSteaming.WriteAsync 边界检查拒绝写入新 Bundle 修复记录

## 1. 问题描述

`VFSteaming.WriteAsync` 向新创建的 Bundle 文件写入数据时，始终抛出 `ArgumentOutOfRangeException`。来源 audit `2026-05-19-filesystem-bugs` finding-01。

## 2. 根因

`VFSteaming.cs:49` — 条件 `offset + data.Length > m_Stream.Length`。构造函数以 `FileMode.OpenOrCreate` 打开流，新文件长度为 0。`FileModule.WriteAsync` 调用 `WriteAsync(entry.Offset, data)` 时（entry.Offset=0, data.Length>0），检查 `0 + data.Length > 0` 恒为 true，每次首次写入都抛异常。

## 3. 修复方案

移除 `offset + data.Length > m_Stream.Length` 条件，仅保留 `offset < 0 || data == null` 校验。`FileStream` 自身支持 Write 自动扩展文件长度，无需在此层做流长度检查。上层 `FileModule.WriteAsync` 已通过 `DefaultThreshold` 控制 entry 大小边界。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Runtime/FileSystem/VFSteaming.cs:49` — 移除 `|| offset + data.Length > m_Stream.Length` 条件，异常消息同步更新

## 5. 验证结果

- 静态分析：修改后 WriteAsync 对 `offset=0, data.Length>0, m_Stream.Length=0` 不再抛异常，`FileStream.WriteAsync` 正常扩展文件
- 影响面：`FileModule.WriteAsync` 写入通路不再被此检查阻塞；`ReadAsync` 边界检查未改动
- 无法运行 Unity 测试（环境无 Unity CLI），代码级变更逻辑自洽

## 6. 遗留事项

无
