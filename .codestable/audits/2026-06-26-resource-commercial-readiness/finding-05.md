---
doc_type: audit-finding
audit: 2026-06-26-resource-commercial-readiness
finding_id: "bug-05"
nature: bug
severity: P1
confidence: medium
suggested_action: cs-issue
status: open
---

# Finding 05：解除 package 初始化会误删其他 package 仍共享的 bundle

## 速答

`UninitializePackageOperationHandle` 会递归把某个 package 的 bundle 依赖全收进集合，然后直接从全局 `providers` 里删掉这些 bundle provider。这里没有任何引用计数或“别的 package 还在用”的检查，所以只要两个 package 共享同一个 bundle，卸载其中一个就可能把另一个正在用的资源一起拆掉。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/BundleMode.UninitializePackageOperationHandle.cs:51-80` — 先展开 `packageBundleNames`，再找 `providers.Where(x => x.Info != null && packageBundleNames.Contains(x.Info.Name))` 并逐个 `Release()` / `Remove()`。
- `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/BundleMode.UninitializePackageOperationHandle.cs:70-80` — 一旦命中就直接释放并从 `_providers` 删除，没有检查其他 package 的依赖关系。
- `Assets/GameDeveloperKit/Runtime/Resource/PlayMode/StreamingAssetMode.UninitializePackageOperationHandle.cs:52-69`、`WebGLMode.UninitializePackageOperationHandle.cs:52-69`、`EditorSimulatorMode.UninitializePackageOperationHandle.cs:52-69` — 同样的卸载逻辑在其他 play mode 里复制存在。

## 影响

共享 bundle 被错误拆掉后，仍然初始化着的另一个 package 会在下一次加载或读取资源时失败。这个问题通常要等到项目里出现共享依赖后才暴露，因此置信度是 medium，但代码路径本身很明确。

## 修复方向

给 bundle/provider 加引用计数或 package 归属追踪，卸载时只移除“最后一个依赖它的 package”对应的 provider；不能按单个 package 的依赖闭包直接删全局资源。

## 建议动作

`cs-issue`，因为这是多 package 共享资源的生命周期 bug。
