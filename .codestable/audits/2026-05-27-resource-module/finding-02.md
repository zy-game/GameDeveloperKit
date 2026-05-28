---
doc_type: audit-finding
audit: 2026-05-27-resource-module
finding_id: "bug-02"
nature: bug
severity: P1
confidence: high
suggested_action: cs-issue
status: fixed
---

# Finding 02：`UnloadAsset()` 会解引用无效句柄的 `Info`

## 速答

`ResourceModule.UnloadAsset()` 只检查 handle 本身是否为 null，没有检查 `handle.Info`；失败句柄或已释放句柄都会让这里抛 `NullReferenceException`。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs:307` — `public UniTask UnloadAsset(AssetHandle handle)` —— 对外暴露的卸载入口。
- `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs:319` — `x.HasAsset(handle.Info.Location)` —— 未检查 `handle.Info` 就读取 `Location`。
- `Assets/GameDeveloperKit/Runtime/Resource/Handle/AssetHandle.cs:57` — `AssetHandle.Failure(Exception error)` —— 失败句柄是公开构造路径。
- `Assets/GameDeveloperKit/Runtime/Resource/Handle/AssetHandle.cs:61` — `Info = null` —— 失败句柄的 `Info` 确认为 null。
- `Assets/GameDeveloperKit/Runtime/Resource/Handle/ResourceHandle.cs:38` — `Info = null` —— `Release()` 后句柄也会清空 `Info`。

## 影响

业务层如果把加载失败的 `AssetHandle` 或已释放句柄传入 `UnloadAsset()`，资源模块不会返回可诊断的 `GameException`，而是触发空引用异常。该路径很容易出现在清理兜底、失败重试或重复释放场景。

## 修复方向

在门面入口先判定 handle 状态和 `Info`，失败/已释放句柄走明确错误或幂等返回，再按有效 `Location` 分发给 mode。

## 修复状态

已修复：`ResourceModule.UnloadAsset()` 对 `handle.Info == null` 的失败或已释放句柄幂等返回，不再解引用空 `Info`。

## 建议动作

`cs-issue`，因为这是公开 API 的边界条件 bug。
