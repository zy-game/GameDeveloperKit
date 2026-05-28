---
doc_type: audit-finding
audit: 2026-05-27-resource-module
finding_id: "maintainability-07"
nature: maintainability
severity: P2
confidence: medium
suggested_action: cs-refactor
status: fixed
---

# Finding 07：Provider 加载编排复制成多份

## 速答

`BuiltinAssetProvider`、`BundleAssetProvider`、`WebAssetProvider`、`EditorAssetProvider` 都复制了按标签/类型筛选、缓存命中、operation 等待、`AddAsset()` 的编排逻辑；已有 Web provider 接线遗漏和空 `Assets` 判空遗漏都说明复制成本正在显现。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Resource/Provider/BundleAssetProvider.cs:119` — `LoadAssetsByLabelAsync(string label)` —— bundle provider 自己实现一套批量加载编排。
- `Assets/GameDeveloperKit/Runtime/Resource/Provider/WebAssetProvider.cs:115` — 同名方法在 Web provider 再实现一套。
- `Assets/GameDeveloperKit/Runtime/Resource/Provider/EditorAssetProvider.cs:142` — 同名方法在 editor provider 再实现一套。
- `Assets/GameDeveloperKit/Runtime/Resource/Provider/BuiltinAssetProvider.cs:79` — 同名方法在 builtin provider 再实现一套。
- `Assets/GameDeveloperKit/Runtime/Resource/Provider/WebAssetProvider.cs:10` — Web provider 注释仍写“编辑器资源提供者”，显示复制后语义未同步。
- `Assets/GameDeveloperKit/Runtime/Resource/Provider/WebAssetProvider.InitializeBundleOperationHandle.cs:79`、`BundleAssetProvider.InitializeBundleOperationHandle.cs:83` — 两个 `LoadFromUriAsync` 私有方法存在但未被调用。

## 影响

新增 provider 或修复加载策略时需要改多份代码，容易出现某个 provider 漏判空、漏错误处理、漏接线。当前审计中的 Finding 03 和 Finding 04 都与这种分叉实现有关。

## 修复方向

把“查询 AssetInfo、复用已加载句柄、执行具体 load operation、登记 handle”的通用编排上移到 `ProviderBase`，各 provider 只保留最小的具体加载差异。

## 修复状态

已修复：加载查询、缓存复用、批量遍历、句柄登记和 pending unload 管理已上移到 `ProviderBase`，各 provider 只保留具体 loading operation 差异。

## 建议动作

`cs-refactor`，因为这是降低重复和后续缺陷率的结构优化。
