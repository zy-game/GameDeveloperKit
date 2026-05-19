---
doc_type: issue-fix
issue: 2026-05-19-vfs-read-boundary-sparse
path: fast-track
fix_date: 2026-05-19
tags: [vfs, boundary-check, read]
---

# VFSteaming.ReadAsync 边界检查拒绝稀疏 Bundle 合法读取 修复记录

## 1. 问题描述

`VFSteaming.ReadAsync` 的 `offset + size > m_Stream.Length` 检查在 Bundle 条目分布稀疏或写入顺序与 offset 不一致时可能拒绝合法读取。来源 audit `2026-05-19-filesystem-bugs` finding-03。

## 2. 根因

`VFSteaming.cs:31` — 与 WriteAsync 同款的流长度边界检查。`FileStream.ReadAsync` 本身在 offset 超出流长度时安全返回 0 字节而非抛异常，此层拦截不必要。

## 3. 修复方案

移除 `offset + size > m_Stream.Length` 条件，仅保留 `offset < 0 || size < 0`。对齐 WriteAsync 的修复风格，由 `FileStream.ReadAsync` 自身处理越界。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Runtime/FileSystem/VFSteaming.cs:31` — 移除 `|| offset + size > m_Stream.Length` 条件

## 5. 验证结果

- 静态分析：ReadAsync 边界检查不再拦截合法偏移读取
- 影响面：仅 `VFSteaming.ReadAsync`，`FileModule.ReadAsync` 无需修改
- 无法运行 Unity 测试（环境无 Unity CLI），代码级变更逻辑自洽

## 6. 遗留事项

无
