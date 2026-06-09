---
doc_type: audit-finding
audit: 2026-06-07-timer-module
finding_id: "bug-02"
nature: bug
severity: P1
confidence: high
suggested_action: cs-issue
status: fixed
---

# Finding 02：`useUnscaledTime` 无效，缩放和非缩放 timer 使用同一个 delta

## 速答

Timer API 提供 `useUnscaledTime`，`UpdateTimers()` 也按该标记选择 `DeltaTime` 或 `UnscaledDeltaTime`，但 `Update()` 每帧把两者都赋成同一个 `_fixedDeltaTime`，所以缩放 timer 和非缩放 timer 实际没有区别。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs:116`、`Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs:129`、`Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs:137` — Delay、Countdown、Interval 都暴露 `bool useUnscaledTime = false` 参数。
- `Assets/GameDeveloperKit/Runtime/Timer/TimerHandle.cs:17` 和 `Assets/GameDeveloperKit/Runtime/Timer/TimerHandle.cs:79` — `TimerHandle` 保存 `UseUnscaledTime`。
- `Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs:80` 到 `Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs:81` — `DeltaTime` 和 `UnscaledDeltaTime` 每次更新都被赋成同一个 `_fixedDeltaTime`。
- `Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs:287` 到 `Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs:288` — `UpdateTimers()` 根据 `UseUnscaledTime` 选择 delta，但两个候选值相等。
- `.codestable/features/2026-06-05-runtime-debug-timer-redesign/runtime-debug-timer-redesign-design.md:105` 到 `.codestable/features/2026-06-05-runtime-debug-timer-redesign/runtime-debug-timer-redesign-design.md:106` — 设计把 Timer Clock 的 scaled/unscaled 语义列为职责。

## 影响

调用方会以为 `useUnscaledTime: true` 可以在 `Time.timeScale = 0` 或慢动作下继续推进，但当前实现不读取 Unity 的 scaled/unscaled delta，暂停菜单、冷却、调试刷新、倒计时等场景会得到相同速度。更糟的是 Debug Timer tab 会同时显示 `Delta` 和 `Unscaled`，但这两个值无法反映真实缩放状态。

## 修复方向

Timer driver 应传入或读取真实的 scaled/unscaled delta，例如 `Time.fixedDeltaTime` 和 `Time.fixedUnscaledDeltaTime`，并在测试中覆盖 `Time.timeScale` 不为 1 的路径。

## 建议动作

`cs-issue`，因为公开参数的语义当前不可用，属于 API 行为 bug。
