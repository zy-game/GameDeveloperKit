---
doc_type: audit-finding
audit: 2026-06-26-resource-commercial-readiness
finding_id: "bug-03"
nature: bug
severity: P1
confidence: high
suggested_action: cs-issue
status: open
---

# Finding 03：场景句柄卸载只清引用，不会真正卸载场景

## 速答

`UnloadSceneAsset()` 和 `SceneAssetHandle.Release()` 只是在资源表里移除句柄、清空字段，没有真正调用 `SceneManager.UnloadSceneAsync()`。这意味着所谓“卸载场景”实际上只做了 bookkeeping，加载进来的场景仍然挂在 Unity 里。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs:441-448` — `UnloadSceneAsset()` 只调用 `RemoveAsset(handle)`，然后立刻返回完成任务。
- `Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs:394` — `UnloadUnusedAssetAsync()` 对待释放句柄只执行 `handle.Release()`。
- `Assets/GameDeveloperKit/Runtime/Resource/Handle/SceneAssetHandle.cs:41-44` — `Release()` 只调用 `base.Release()` 并把 `Asset` 设为默认值，没有场景卸载调用。

## 影响

场景会在“已卸载”后继续驻留，导致内存增长、场景对象重复存在、切场景后的状态污染，以及调试时看到的状态和资源模块内部状态不一致。

## 修复方向

把真实卸载动作放到场景句柄或 provider 的卸载路径里，按 Unity 的异步场景卸载语义完成后再清理句柄，并补上重复卸载和未加载场景的幂等保护。

## 建议动作

`cs-issue`，因为这是场景生命周期的基础行为错误。
