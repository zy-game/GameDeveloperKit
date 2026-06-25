---
doc_type: audit-finding
finding_id: "13"
slug: framework-full
dimension: bug
severity: P1
confidence: high
action: cs-issue
---

# Finding 13: ResourceModule.LoadAssetsByLabelAsync 无效 null 检查导致静默不报错

## 证据

`Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs` 第 260-279 行：

```csharp
public async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label)
{
    ValidateKey(label, nameof(label));
    EnsureReady();
    if (modes.Count == 0)
        throw new GameException("No resource play mode is available.");

    var playmode = this.modes.Where(pm => pm.HasAsset(label));
    if (playmode == null)          // <-- LINQ Where 永远不返回 null
    {
        throw new GameException($"No play mode contains assets with label: {label}");
    }
    // ...
}
```

## 问题

`Enumerable.Where()` 返回空 `IEnumerable<T>`，**永远不返回 null**。当没有 mode 包含指定 label 时，`playmode` 是一个空序列（非 null），代码跳过异常进入 `foreach` 循环，循环体不执行，最终返回空列表 `handles`。

对比同文件的 `LoadAssetAsync`，它对 `FirstOrDefault` 做了正确的 null 检查。此处行为不一致：label 查询静默返回空，而 location 查询正确抛异常。

同模式也出现在 `LoadRawAssetsByLabelAsync` 和 `LoadAssetsByTypeAsync`（后者还用了 `is null` 模式匹配，同样无效）。

## 建议动作

改为 `var playmode = this.modes.Where(...).ToArray(); if (playmode.Length == 0) throw ...;`，与其他加载方法的行为保持一致。走 `cs-issue`。
