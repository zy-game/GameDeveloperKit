---
doc_type: audit-finding
finding_id: "10"
slug: framework-full
dimension: maintainability
severity: P2
confidence: low
action: cs-refactor
---

# Finding 10: App.Startup() 生命周期状态冗余检查

## 证据

`App.cs` `Startup()` 方法（行 ~135-158）：

```csharp
public static async UniTask Startup()
{
    if (_lifecycleState == LifecycleState.Started) return;           // 第1次

    if (_lifecycleState == LifecycleState.Starting && ...)           // 等待第1次
    { await _startupCompletion.Task; return; }

    if (_lifecycleState == LifecycleState.ShuttingDown && ...)       // 等待 shutdown
    { await _shutdownCompletion.Task; }

    if (_lifecycleState == LifecycleState.Started) return;           // 第2次（重复）

    if (_lifecycleState == LifecycleState.Starting && ...)           // 第3次（重复）
    { await _startupCompletion.Task; return; }

    await StartupInternal();
}
```

## 问题

等待 shutdown 完成后，重新检查 `Started` 和 `Starting` 状态是合理的（shutdown 期间可能有人调用了 startup）。但逻辑可以简化为循环或状态机模式，当前线性展开降低了可读性。这不是 bug（功能正确），但增加了维护者的心智负担。

## 建议动作

可简化为 `while` 循环模式或提取为 `WaitForStableState()` 辅助方法。优先级最低。走 `cs-refactor`。
