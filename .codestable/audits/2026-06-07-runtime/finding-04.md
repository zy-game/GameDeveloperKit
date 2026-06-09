---
doc_type: audit-finding
audit: 2026-06-07-runtime
finding_id: "bug-04"
nature: bug
severity: P1
confidence: high
suggested_action: cs-issue
status: fixed
---

# Finding 04：`SceneAssetHandle.Active()` 在场景已加载时直接返回

## 速答

`SceneAssetHandle.Active()` 的条件写反了：场景已加载时直接 `return`，未加载时才调用 `SceneManager.SetActiveScene(Asset)`。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Resource/Handle/SceneAssetHandle.cs:25` — 公开方法 `Active()` 用于激活场景。
- `Assets/GameDeveloperKit/Runtime/Resource/Handle/SceneAssetHandle.cs:27` 到 `Assets/GameDeveloperKit/Runtime/Resource/Handle/SceneAssetHandle.cs:30` — `if (Asset.isLoaded) return;`。
- `Assets/GameDeveloperKit/Runtime/Resource/Handle/SceneAssetHandle.cs:32` — 只有未加载场景才会执行 `SceneManager.SetActiveScene(Asset)`。

## 影响

正常加载完成的场景无法通过该句柄设为 active scene；反而未加载或无效场景会进入 `SetActiveScene`，Unity 会返回 false 或触发后续流程异常。场景资源加载成功后的主流程会被这个 helper 误导。

## 修复方向

改为在 `!Asset.isLoaded` 时返回或抛错，在已加载时调用 `SceneManager.SetActiveScene(Asset)` 并检查返回值。

## 建议动作

`cs-issue`，因为这是明确的条件反转 bug。
