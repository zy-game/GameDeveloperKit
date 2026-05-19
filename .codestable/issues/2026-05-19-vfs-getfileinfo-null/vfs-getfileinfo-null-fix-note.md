---
doc_type: issue-fix
issue: 2026-05-19-vfs-getfileinfo-null
path: fast-track
fix_date: 2026-05-19
tags: [vfs, api, null-safety]
---

# FileModule.GetFileInfo 静默返回 null 修复记录

## 1. 问题描述

`FileModule.GetFileInfo` 在条目不存在时静默返回 null，调用方未做 null 检查时触发 NRE。来源 audit `2026-05-19-filesystem-bugs` finding-04。

## 2. 根因

`FileModule.cs:111-114` — `GetFileInfo` 调用 `TryGetEntry` 后直接返回 `out entry`（未找到时为 null），签名未体现可空语义，调用方容易忽略 null 检查。

## 3. 修复方案

改为 Try-pattern：`TryGetFileInfo(string path, out VFSMeta entry)` 返回 `bool`，直接委托 `m_Manifest.TryGetEntry`。调用方必须显式检查返回值才能使用 entry。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:111` — `GetFileInfo` 重命名为 `TryGetFileInfo`，签名和实现改为 Try-pattern

## 5. 验证结果

- 静态分析：API 强制调用方处理未找到情况，消除静默 null 陷阱
- 影响面：无外部调用方（grep 确认仅 FileModule.cs 自身定义），零破坏性变更
- 无法运行 Unity 测试（环境无 Unity CLI），代码级变更逻辑自洽

## 6. 遗留事项

无
