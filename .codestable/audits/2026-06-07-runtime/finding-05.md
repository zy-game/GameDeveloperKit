---
doc_type: audit-finding
audit: 2026-06-07-runtime
finding_id: "performance-05"
nature: performance
severity: P1
confidence: medium
suggested_action: cs-issue
status: fixed
---

# Finding 05：Raw/Scene 资源句柄会被缓存但没有公开卸载路径

## 速答

`ProviderBase` 会把 `RawAssetHandle` 和 `SceneAssetHandle` 放进 `_assets` 缓存，但公开 unload API 只接受 `AssetHandle`，raw bytes 和 scene handle 没有对应的移入 pending unload 路径。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs:169` 到 `Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs:173` — raw asset 加载成功后调用 `AddAsset(handle)`，进入 `_assets`。
- `Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs:225` 到 `Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs:229` — scene asset 加载成功后同样调用 `AddAsset(handle)`。
- `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs:336` — 公开卸载入口只有 `UnloadAsset(AssetHandle handle)`。
- `Assets/GameDeveloperKit/Runtime/Resource/ModeBase.cs:113` — mode 抽象也只有 `UnloadAsset(AssetHandle handle)`。
- `Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs:391` 到 `Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs:399` — provider 只能移除 `AssetHandle`。
- `Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs:374` 到 `Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs:382` — `UnloadUnusedAssetAsync()` 只释放 `_pendingUnloadAssets`，不会释放仍在 `_assets` 的 raw/scene handle。

## 影响

Raw 资源的 `byte[] Data` 和 scene 句柄会在 provider 生命周期内持续缓存。长时间运行、频繁加载配置/二进制资源或切换场景时，调用方即使“用完”也没有 API 把这些句柄从 active 缓存移出，只能等整个 package/provider 释放。

## 修复方向

补齐 `ResourceHandle` 或 `RawAssetHandle` / `SceneAssetHandle` 的卸载入口，并让 provider 能按 `ResourceHandle` 统一移入 pending unload；同时明确 scene unload 是否只释放句柄还是卸载 Unity scene。

## 建议动作

`cs-issue`，因为这是资源生命周期 API 缺口，会造成实际内存驻留。
