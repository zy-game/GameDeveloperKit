---
doc_type: issue-fix
issue: 2026-05-19-vfs-release-silent-noop
path: fast-track
fix_date: 2026-05-19
tags: [vfs, input-validation, io-waste]
---

# VfsManifest.Release 静默无操作 + 浪费 SaveAsync 修复记录

## 1. 问题描述

`VfsManifest.Release(string path)` 在 path 不匹配任何条目时静默跳过 `Unused()` 但仍调用 `SaveAsync()`，无效输入被吞没且浪费文件 I/O。path 为 null 时因 `e.FilePath == null` 匹配到已释放条目，语义错误。来源 audit `2026-05-19-filesystem-bugs` finding-05。

## 2. 根因

`VfsManifest.cs:39-46` — `Release` 未对 path 做 null 校验，未找到时未提前返回，无条件执行 `SaveAsync()`。

## 3. 修复方案

1. 对 null/empty path 抛出 `ArgumentException`，对齐项目编码约定
2. 未找到条目时返回 `UniTask.CompletedTask`，避免无效 I/O
3. 只有确实释放了条目才调用 `SaveAsync()`

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Runtime/FileSystem/VfsManifest.cs:39-46` — 加 null 校验 + 未找到时提前返回

## 5. 验证结果

- 静态分析：无效输入被显式拒绝，未找到路径不触发 I/O
- 影响面：仅 `VfsManifest.Release`，调用方 `FileModule.WriteAsync` 和 `DeleteAsync` 行为不变
- 无法运行 Unity 测试（环境无 Unity CLI），代码级变更逻辑自洽

## 6. 遗留事项

无
