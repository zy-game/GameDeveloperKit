---
doc_type: feature-ff-note
feature: 2026-05-22-resource-playmodes
date: "2026-05-22"
updated: "2026-05-24"
status: outdated
superseded-by: .codestable/roadmap/resource-management/resource-management-roadmap.md
requirement:
tags: [resource, playmode, assetbundle, unity]
---

> 2026-05-24 更新：本文记录的是旧实现口径，已与当前源码不一致。当前资源模块实际类型为 `ModeBase` / `ProviderBase` / `BuiltinMode` / `StreamingAssetMode` / `BundleMode` / `WebGLMode` / `EditorSimulatorMode`，清单类型为 `ManifestInfo` / `PackageInfo` / `BundleInfo` / `AssetInfo`。后续以 `resource-management` roadmap 和 `bundlemode-design` 的 2026-05-24 修订版为准。

## 做了什么

实现资源模块剩余的 PlayMode 能力：`ResourcesPlayMode`、`OfflinePlayMode`、`EditorSimulatorPlayMode`、`HostingPlayMode`、`WebPlayMode`。现在业务侧可以把具体 PlayMode 挂到 `Super.Resource.SetPlayMode(...)`，再通过既有 `ResourceModule` API 加载资源。

## 改了哪些

- `Assets/GameDeveloperKit/Runtime/Resource/ResourcePlayModeBase.cs` / `ResourceProviderPlayMode.cs` — 新增 manifest 查询、label/type 批量加载、bundle provider 路由和依赖 bundle 初始化骨架。
- `Assets/GameDeveloperKit/Runtime/Resource/ResourcesPlayMode.cs` / `ResourcesResourceProvider.cs` — 新增 Unity `Resources` 最小加载闭环。
- `Assets/GameDeveloperKit/Runtime/Resource/OfflinePlayMode.cs` / `WebPlayMode.cs` / `HostingPlayMode.cs` 及对应 provider — 新增本地 AssetBundle、Web AssetBundle、远端缓存 AssetBundle 运行模式。
- `Assets/GameDeveloperKit/Editor/EditorSimulatorPlayMode.cs` / `EditorSimulatorResourceProvider.cs` — 新增 Editor-only `AssetDatabase` 模拟运行模式。
- `Assets/GameDeveloperKit/Runtime/Resource/AssetHandle.cs` / `SceneAssetHandle.cs` / `ResourceManifest.cs` — 支持 scene 成功句柄不携带 asset，并补 bundle 查询 API。

## 怎么验证的

已运行 `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`，通过。由于 Unity 生成的 csproj 尚未包含新增脚本，额外创建临时编译项目验证新增 Runtime + Editor 脚本，均通过；临时项目已删除。

## 顺手发现

- `Assets/Resources/ResourceSettings.asset` 指向的 `ResourceSettings` 脚本当前未在工作区代码中找到，像是旧版或半成品残留；本次没有依赖它。
- `GameDeveloperKit.Runtime.Tests.csproj` 引用 `Assets/GameDeveloperKit/Tests/...`，但当前工作区不存在该目录；本次未能跑 Unity Test Framework 测试。
