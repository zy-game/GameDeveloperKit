---
doc_type: audit-finding
finding_id: "08"
slug: framework-full
dimension: maintainability
severity: P2
confidence: medium
action: cs-refactor
---

# Finding 08: TimerModule 公开 API 重载爆炸

## 证据

`TimerModule.cs` 中 OnUpdate/OnLateUpdate/OnFixedUpdate 各有 4 个重载：

```csharp
OnUpdate(Action callback, ...)
OnUpdate(Action<TimerUpdateContext> callback, ...)
OnLateUpdate(Action callback, ...)
OnLateUpdate(Action callback, float fps, ...)
OnLateUpdate(Action<TimerUpdateContext> callback, ...)
OnLateUpdate(Action<TimerUpdateContext> callback, float fps, ...)
OnFixedUpdate(Action callback, ...)
OnFixedUpdate(Action callback, float fps, ...)
OnFixedUpdate(Action<TimerUpdateContext> callback, ...)
OnFixedUpdate(Action<TimerUpdateContext> callback, float fps, ...)
```

共 **12 个**公开重载方法，全部只做一件事：`return Register(new XxxTimerHandle(...), owner, tag)`。

## 问题

- OnUpdate 只有 2 个重载（无 fps 参数），OnLateUpdate/OnFixedUpdate 各有 4 个（有 fps 参数）。API 不对称——为什么 Update 不支持 fps 门控？
- 每次新增 phase 或参数组合都需要新增重载（维度爆炸）
- 内部实现完全相同，仅 new 的类型和可选 fps 参数不同

## 建议动作

考虑用 Builder 模式或配置对象替代重载洪水。至少让 Update 也支持 fps 参数以保持一致性。走 `cs-refactor`。
