---
doc_type: issue-fix
issue: 2026-05-28-vfs-delete-cache-bloat
path: fast-track
fix_date: 2026-05-28
tags: [vfs, filesystem, cache]
---

# VFS 删除缓存膨胀修复记录

## 1. 问题描述

`FileModule.DeleteAsync` 删除虚拟文件后，磁盘上的 VFS 包文件仍保留，`vfs_manifest.json` 中空闲条目的 `BundlePath` 也会继续指向旧包，导致缓存目录随下载/写入/删除持续膨胀。

## 2. 根因

`FileModule.DeleteAsync` 只调用 `VFSMeta.Unused()` 并保存清单，没有删除不再被使用的包文件。`VfsManifest.Release` 在写入同一路径前释放旧条目时也有相同问题。旧版 `Unused()` 没有返回释放前的包路径，调用方无法判断要删除哪个物理文件。

## 3. 修复方案

释放条目时返回原 `BundlePath`，`FileModule` 在删除或覆盖写入后判断该包是否仍被有效条目引用；如果没有引用，则清理该包下所有空闲条目的 `BundlePath`、保存 manifest、关闭对应 `VFSteaming`，并删除磁盘包文件。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Runtime/FileSystem/VFSMeta.cs`
- `Assets/GameDeveloperKit/Runtime/FileSystem/VfsManifest.cs`
- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs`
- `Assets/GameDeveloperKit/Tests/Runtime/FileModuleTests.cs`
- `.codestable/issues/2026-05-28-vfs-delete-cache-bloat/vfs-delete-cache-bloat-report.md`

## 5. 验证结果

- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过
- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`：通过
- 新增运行时测试覆盖：
  - 删除最后一个引用该包的虚拟文件后，包文件被删除。
  - 删除后 manifest 不再残留旧 `BundlePath`。
  - 覆盖写入同一路径时，旧包文件和旧 `BundlePath` 被清理，新内容仍可读回。

## 6. 遗留事项

未执行 Unity Test Runner PlayMode 全量运行；当前验证覆盖编译和新增测试源码。
