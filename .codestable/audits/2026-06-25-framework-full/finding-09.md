---
doc_type: audit-finding
finding_id: "09"
slug: framework-full
dimension: maintainability
severity: P2
confidence: high
action: cs-refactor
---

# Finding 09: ResourceModule.UnloadAsset/UnloadRawAsset/UnloadSceneAsset 三方法结构完全相同

## 证据

`ResourceModule.cs` 中三个卸载方法（行 ~335-420）具有相同的控制流：

```csharp
public UniTask UnloadAsset(AssetHandle handle)
{
    if (handle == null) throw new ArgumentNullException(...);
    EnsureReady();
    if (modes.Count == 0) throw ...;
    if (handle.Info == null) return UniTask.CompletedTask;
    var playmode = modes.FirstOrDefault(x => x.HasAsset(handle.Info.Location));
    if (playmode == null) throw ...;
    return playmode.UnloadAsset(handle);
}
```

`UnloadRawAsset(RawAssetHandle)` 和 `UnloadSceneAsset(SceneAssetHandle)` 结构完全一致，仅 handle 类型和方法后缀不同。

## 问题

- 三份相同逻辑，bug 修复需要三处同步
- 违反 DRY 原则
- 如果未来新增 Handle 类型，需要再复制一份

## 建议动作

提取为 `private UniTask UnloadHandle<T>(T handle, Func<ModeBase, T, UniTask> unloader, string assetType)` 泛型方法。走 `cs-refactor`。
