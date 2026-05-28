---
doc_type: audit-finding
audit: 2026-05-27-resource-module
finding_id: "performance-06"
nature: performance
severity: P2
confidence: medium
suggested_action: cs-refactor
status: fixed
---

# Finding 06：未使用资源卸载会按 provider 多次触发全局扫描

## 速答

`ResourceModule.UnloadUnusedAssetAsync()` 会对每个 mode 并发调用卸载；每个 mode 又对每个 provider 调用 `ProviderBase.UnloadUnusedAssetAsync()`，而 provider 内部每次都会执行一次 `Resources.UnloadUnusedAssets()`。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs:291` — `List<UniTask> unloadTasks = new List<UniTask>();` —— 门面聚合所有 mode 的卸载任务。
- `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs:294` — `unloadTasks.Add(playMode.UnloadUnusedAssetAsync());` —— 每个 mode 都会被调用。
- `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/BundleMode.cs:210` — `this._providers.Select(provider => provider.UnloadUnusedAssetAsync())` —— 一个 mode 内继续按 provider 分发。
- `Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs:193` — `await UnityEngine.Resources.UnloadUnusedAssets();` —— 每个 provider 都触发 Unity 全局未使用资源扫描。

## 影响

当初始化多个 package/bundle 时，一次 `UnloadUnusedAssetAsync()` 可能触发多次全局卸载扫描。该 API 本身成本较高，且多 provider 并发调用没有明显收益，容易造成帧卡顿或卸载时长放大。

## 修复方向

先让 provider 只释放自己的 pending handle，再由 mode 或 module 统一调用一次全局 `Resources.UnloadUnusedAssets()`。

## 修复状态

已修复：`ProviderBase.UnloadUnusedAssetAsync()` 只释放 pending handle，`ResourceModule.UnloadUnusedAssetAsync()` 在所有 mode 完成后统一调用一次 `UnityEngine.Resources.UnloadUnusedAssets()`。

## 建议动作

`cs-refactor`，因为这是卸载调度结构优化，不需要改变公开功能语义。
