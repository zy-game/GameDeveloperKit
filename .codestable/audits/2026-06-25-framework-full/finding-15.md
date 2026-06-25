---
doc_type: audit-finding
finding_id: "15"
slug: framework-full
dimension: bug
severity: P2
confidence: low
action: cs-refactor
---

# Finding 15: TimerModule.SetTimer/ClearTimer 遗留 API 与主流 API 语义不一致

## 证据

`TimerModule.cs` 中存在两套定时器 API：

**主流 API**（公开，推荐使用）:
```csharp
public TimerDelayHandle Delay(float delay, Action callback, ...)
public TimerCountdownHandle Countdown(float duration, ...)
public TimerIntervalHandle Interval(float interval, Action<float> callback, ...)
```

**遗留 API**:
```csharp
public void SetTimer(TimerHandle handle, float delay, bool repeat = false)
public void SetTimer(Action<float> callback, float delay, bool repeat = false)
public void ClearTimer(TimerHandle handle)
public void ClearTimer(Action<float> callback)
```

## 问题

- `SetTimer(Action<float> callback, ...)` 创建 `TimerDelayHandle` 或 `TimerIntervalHandle`，但回调签名是 `Action<float>`（带 deltaTime 参数），而 `Delay(Action callback, ...)` 的回调是无参的。行为不一致：相同语义的 delay 在不同 API 下有不同的回调签名
- `SetTimer` 内部维护独立的 `_callbackHandles` 字典，与 `_handles` 列表形成两套追踪机制
- 两套 API 共存增加了模块表面积和理解成本

## 建议动作

明确 `SetTimer`/`ClearTimer` 是否为废弃 API。如果是，标记 `[Obsolete]` 并引导到 `Delay`/`Interval`/`Countdown`。走 `cs-refactor`。
