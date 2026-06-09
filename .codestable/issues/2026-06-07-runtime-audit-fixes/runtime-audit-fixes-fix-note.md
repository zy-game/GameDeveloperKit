---
doc_type: issue-fix
issue: 2026-06-07-runtime-audit-fixes
path: fast-track
fix_date: 2026-06-07
status: fixed
tags:
  - runtime
  - audit
  - vfs
  - resource
  - debug
---

# Runtime Audit Fixes 修复记录

## 1. 问题描述

来源：`.codestable/audits/2026-06-07-runtime/` 的 9 条有效 finding。用户确认“全部修复”后，按审计清单一次性处理全部 runtime 问题。

## 2. 根因

- VFS 写入策略与架构记录反向，读取没有校验 CRC，`ReadAllStringAsync` 未实现。
- 非 Bundle 资源模式把 provider 的 `BundleInfo.Name` 当作 package 名判断，导致 package 卸载入口找不到已初始化模式。
- Scene/Raw 资源句柄被 Provider 缓存，但公开 API 只有 `AssetHandle` 卸载路径。
- 场景句柄激活条件写反；下载临时文件只按尾部文件名命名；Debug 外发记录只脱敏了 category/message；Combat 销毁实体后未移除 wrapper；Raw label 加载只取第一个命中模式。

## 3. 修复方案

- VFS：小文件按 packed slot 写入，大文件创建 standalone 条目；读取时按清单 CRC32 校验；`ReadAllStringAsync` 读取 UTF-8 文本。
- Resource：`HasPackage` 按 manifest package 的直接 bundle 集合匹配已初始化 provider；新增 Raw/Scene 卸载 API 并接入 Provider pending-unload；Raw label 加载聚合所有命中模式。
- Scene/Download/Debug/Combat：修正场景激活条件，下载临时文件名加入 URL CRC32 前缀，Debug record 创建前脱敏 exception/context/tags，实体销毁成功后清理 wrapper 缓存。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs`
- `Assets/GameDeveloperKit/Runtime/FileSystem/VfsManifest.cs`
- `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs`
- `Assets/GameDeveloperKit/Runtime/Resource/ModeBase.cs`
- `Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs`
- `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/BuiltinMode.cs`
- `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/BundleMode.cs`
- `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/EditorSimulatorMode.cs`
- `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/StreamingAssetMode.cs`
- `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/WebGLMode.cs`
- `Assets/GameDeveloperKit/Runtime/Resource/Handle/SceneAssetHandle.cs`
- `Assets/GameDeveloperKit/Runtime/Download/DownloadHandler.cs`
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs`
- `Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs`
- `.codestable/audits/2026-06-07-runtime/`

## 5. 验证结果

- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过，0 warning，0 error。
- 静态复核：
  - VFS packed/standalone 选择与架构记录一致，读取会比较清单 CRC32。
  - Resource Raw/Scene 卸载入口完整覆盖 `ResourceModule`、`ModeBase`、各 PlayMode 和 `ProviderBase`。
  - 非 Bundle `HasPackage` 不再比较 provider bundle 名和 package 名。
  - Debug transport 收到的 `DebugLogRecord` 不再持有原始 exception/context/tags。

## 6. 遗留事项

未做 Unity PlayMode 运行验证；本次只执行了 runtime C# 构建验证。
