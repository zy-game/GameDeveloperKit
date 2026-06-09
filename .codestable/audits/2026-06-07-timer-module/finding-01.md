---
doc_type: audit-finding
audit: 2026-06-07-timer-module
finding_id: "bug-01"
nature: bug
severity: P1
confidence: high
suggested_action: cs-issue
status: fixed
---

# Finding 01：配置 FPS 不控制实际 FixedUpdate 频率，Timer 时间会与真实时间漂移

## 速答

`TimerSettings.FPS` 只被换算成内部 `_fixedDeltaTime`，但 Timer driver 仍由 Unity `FixedUpdate()` 以项目 `Time.fixedDeltaTime` 节奏调用；当配置 FPS 与 Unity 固定步频率不一致时，`TimerModule.Time`、Delay、Countdown 和 Interval 都会按错误速度推进。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Timer/TimerSettings.cs:14` — `TimerSettings` 暴露 `FPS` 作为“计时器目标帧率”配置。
- `Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs:56` 到 `Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs:57` — `Startup()` 把 `FPS` 换算成 `_fixedDeltaTime = 1f / fps`，但没有同步或校验 Unity 的固定更新频率。
- `Assets/GameDeveloperKit/Runtime/Timer/TimerModule.Timer.cs:21` 到 `Assets/GameDeveloperKit/Runtime/Timer/TimerModule.Timer.cs:23` — driver 使用 `FixedUpdate()` 调用 `onUpdate`，实际调用频率由 Unity `Time.fixedDeltaTime` 决定。
- `Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs:80` 到 `Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs:84` — 每次 driver tick 都直接用 `_fixedDeltaTime` 累加 `Time` 并推进 timer。
- `.codestable/features/2026-06-05-runtime-debug-timer-redesign/runtime-debug-timer-redesign-design.md:326` 到 `.codestable/features/2026-06-05-runtime-debug-timer-redesign/runtime-debug-timer-redesign-design.md:327` — 设计要求 Timer Clock 表达 seconds、tick、delta 语义。

## 影响

默认配置 50 FPS 与 Unity 默认 fixed timestep 0.02 秒一致时问题不明显；一旦 `TimerSettings.FPS` 改成 30、60 或 100，而 Unity 固定步仍是 50Hz，Timer 会按配置秒长累加但按 Unity 频率触发。例如 FPS=100 时每个 `FixedUpdate` 累加 0.01 秒，真实 1 秒只推进约 0.5 秒；FPS=25 时真实 1 秒会推进约 2 秒。业务 delay/countdown/interval、Debug Timer tab 和依赖 Timer tick 的 profile 刷新都会失真。

## 修复方向

明确 Timer Clock 的驱动策略：要么用 Unity 实际 `Time.fixedDeltaTime`/`Time.deltaTime` 推进，要么在 Startup 时同步 `Time.fixedDeltaTime` 并记录这是全局物理步设置；不要只改内部 `_fixedDeltaTime`。

## 建议动作

`cs-issue`，因为这是运行时计时正确性问题，需要补不同 FPS 配置下的可复现实验和回归测试。
