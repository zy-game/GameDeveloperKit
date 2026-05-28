---
doc_type: issue-fix-note
issue: resource-module-audit
status: fixed
fixed_at: 2026-05-27
tags:
  - resource
  - audit
  - runtime
---

# Resource Module Audit 修复记录

## 修复范围

来源：`.codestable/audits/2026-05-27-resource-module/` 中除 Finding 05 外的所有有效 finding。

Finding 05 已按项目约定撤销：`#if UNITY_EDITOR` 保护下的 Editor API 可以留在 Runtime 目录。

## 修复内容

- Finding 01：`ResourceModule.Startup()` 改为直接通过 Unity `Resources.Load<ResourceSettings>("ResourceSettings")` 自举配置，再按 `ResourceSettings.ManifestLocation` 执行 manifest operation；`ManifestOperationHandle` 支持 HTTP/HTTPS 下载和 StreamingAssets / 本地文件读取。
- Finding 02：`ResourceModule.UnloadAsset()` 对 `handle.Info == null` 的失败或已释放句柄幂等返回，避免空引用。
- Finding 03：`BundleInfo.Assets` / `Dependencies` 默认初始化为空列表，Provider 批量加载统一走 `ProviderBase` 的安全枚举。
- Finding 04：`WebGLMode.InitializePackageOperationHandle` 创建 `WebAssetProvider`；`WebAssetProvider` 使用 `ResourceSettings.ServerUrl` 拼接远端 bundle URL，并检查 `UnityWebRequest` 结果。
- Finding 06：Provider 只释放自己的 pending handle；`ResourceModule.UnloadUnusedAssetAsync()` 在所有 mode 完成后统一调用一次 `UnityEngine.Resources.UnloadUnusedAssets()`。
- Finding 07：Provider 的查询、缓存复用、批量遍历、加载后登记句柄编排上移到 `ProviderBase`，具体 provider 只保留实际 loading operation 差异；删除 `BundleAssetProvider.InitializeBundleOperationHandle` 的未使用 URI helper。

## 验证

- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过，0 warning，0 error。
- 静态检查：
  - `WebGLMode.InitializePackageOperationHandle` 已命中 `new WebAssetProvider(bundle)`。
  - `UnityEngine.Resources.UnloadUnusedAssets()` 在资源模块内只保留模块级统一调用。
  - Provider 侧不再直接使用 `Info.Assets.Where(...)`。
  - `Super.Resource.Settings.url` 旧字段直读已替换为 `ResourceSettings.ServerUrl`。

## 剩余风险

当前仓库 `Assets/StreamingAssets/manifest.json` 仍是旧 JSON 形态，不匹配当前 `ManifestInfo` / `PackageInfo` / `BundleInfo` / `AssetInfo` 清单模型。代码修复已让启动链路不再自举失败，但真实运行仍需要资源构建链路输出当前清单格式。
