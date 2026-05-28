---
doc_type: issue-report
issue: 2026-05-28-vfs-delete-cache-bloat
status: confirmed
severity: P1
summary: VFS 删除虚拟文件后未清理 bundlepath 和对应物理文件，导致缓存持续膨胀
tags: [vfs, filesystem, cache]
---

# VFS 删除缓存膨胀 Issue Report

## 1. 问题现象

删除 VFS 文件后，`vfs_manifest.json` 中对应条目的 `BundlePath` 仍然保留，并且磁盘上的对应包文件没有删除。多次下载、写入、删除后，持久化目录里的 VFS 缓存会持续膨胀。

## 2. 复现步骤

1. 运行会通过 `FileModule.WriteAsync` 写入 VFS 的流程，例如下载模块测试把远程文件写入 VFS。
2. 调用 `FileModule.DeleteAsync(path)` 删除该虚拟文件。
3. 打开 `Application.persistentDataPath/vfs/vfs_manifest.json`。
4. 观察到：对应条目已变为空闲，但 `BundlePath` 仍保留；对应的物理包文件仍留在 VFS 目录。

复现频率：稳定

## 3. 期望 vs 实际

**期望行为**：删除 VFS 虚拟文件后，应清理该条目的路径元数据，并删除不再被任何有效条目引用的对应物理包文件，避免缓存无限增长。

**实际行为**：删除只把条目标为空闲，`BundlePath` 和磁盘文件仍保留，缓存空间不会随删除释放。

## 4. 环境信息

- 涉及模块 / 功能：GameDeveloperKit FileSystem / VFS 删除流程
- 相关文件 / 函数：`Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs` 的 `DeleteAsync`；`Assets/GameDeveloperKit/Runtime/FileSystem/VfsManifest.cs` 的 `Release`；`Assets/GameDeveloperKit/Runtime/FileSystem/VFSMeta.cs` 的 `Unused`
- 运行环境：Unity PlayMode / 本地持久化目录
- 其他上下文：观察文件 `C:\Users\15849\AppData\LocalLow\DefaultCompany\Black Rain\vfs\vfs_manifest.json`

## 5. 严重程度

**P1** — 删除缓存文件无法释放磁盘空间，下载/缓存流程频繁执行时会持续增长并影响用户设备存储。

## 备注

当前代码位置已足够明确，适合走快速通道：确认后直接修复删除清理逻辑，并补运行时测试覆盖。
