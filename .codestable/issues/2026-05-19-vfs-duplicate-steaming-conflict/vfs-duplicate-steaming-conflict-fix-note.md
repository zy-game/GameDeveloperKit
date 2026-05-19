---
doc_type: issue-fix
issue: 2026-05-19-vfs-duplicate-steaming-conflict
path: fast-track
fix_date: 2026-05-19
tags: [vfs, file-share, resource-leak]
---

# FileModule.WriteAsync 重复创建 VFSteaming 导致 FileShare.None 冲突 修复记录

## 1. 问题描述

`FileModule.WriteAsync` 每次写入同一 Bundle 文件时都创建新的 `VFSteaming` 实例，配合 `FileShare.None` 导致第二次写入抛 `IOException`。来源 audit `2026-05-19-filesystem-bugs` finding-02。

## 2. 根因

`FileModule.cs:58-59` — 每次 `WriteAsync` 无条件 `new VFSteaming(path)` + `m_Steamings.Add()`，不检查是否已存在同路径 steaming。`VFSteaming` 构造函数以 `FileShare.None` 打开文件，同进程内第二个 `FileStream` 打开同一文件失败。

对比 `ReadAsync`（同文件 76-80 行）已正确实现 `Find` 复用逻辑，写入路径遗漏了同等处理。

## 3. 修复方案

`WriteAsync` 中复用 `ReadAsync` 的 steaming 查找模式：先 `m_Steamings.Find(s => s.Path == bundlePath)`，找到则复用，未找到才新建并加入列表。提取 `bundlePath` 为局部变量避免重复 `Path.Combine`。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:57-61` — `new VFSteaming` + `Add` 替换为 `Find` 复用逻辑

## 5. 验证结果

- 静态分析：写入路径现在与读取路径对齐，同一 Bundle 的 steaming 复用而非重复创建
- 影响面：仅 `FileModule.WriteAsync`，`ReadAsync` 逻辑未改动
- 无法运行 Unity 测试（环境无 Unity CLI），代码级变更逻辑自洽

## 6. 遗留事项

无
