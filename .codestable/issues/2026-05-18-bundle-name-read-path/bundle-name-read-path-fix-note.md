---
doc_type: issue-fix
issue: 2026-05-18-bundle-name-read-path
path: fast-track
fix_date: 2026-05-18
tags: [filesystem, vfs, bundle]
---

# BundleName 读取路径修复记录

## 1. 问题描述

FileSystem audit finding-05 指出 packed 文件的 manifest entry 记录了 `BundleName`，但读取时固定使用默认 `bundle_0.vfsb`，导致 manifest 元数据与实际读取行为不一致。

## 2. 根因

`Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs:90` 在 `entry.Storage == StorageType.Packed` 时直接把 `m_BundlePath` 传给 `VfsBundleReader.ReadAsync`，没有消费 `entry.BundleName`。

## 3. 修复方案

新增 `ResolveBundlePath(VfsFileEntry entry)`：当 `entry.BundleName` 为空时保留旧版默认 bundle 兼容；否则基于 `m_RootPath` 和 `entry.BundleName` 解析实际 bundle 路径。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs`

## 5. 验证结果

- IDE diagnostics：`FileModule.cs` 无诊断错误。
- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：未通过，失败原因是当前项目已有 `Super.cs` 引用的 `EventManager`、`ResourceManager`、`DownloadManager` 类型在该 csproj 编译上下文中不可解析，非本次改动引入。
- `dotnet build "Black Rain.sln" --no-restore`：未通过，失败原因是 Unity 生成项目缺少 `Temp/obj/.../project.assets.json` restore 产物，非本次改动引入。

## 6. 遗留事项

- 本次仅修复 finding-05；audit 中 finding-01 到 finding-04 未在本 issue 范围内处理。
- 如需完全命令行验证，需要先恢复 Unity/.NET 生成项目的 restore/build 环境。
