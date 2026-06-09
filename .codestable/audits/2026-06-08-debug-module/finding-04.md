---
doc_type: audit-finding
audit: 2026-06-08-debug-module
finding_id: "arch-drift-04"
nature: arch-drift
severity: P2
confidence: medium
suggested_action: cs-refactor
status: open
---

# Finding 04：Debug profile/metrics 仍由 Unity delta 驱动，未接入 Timer 统一口径

## 速答

Runtime redesign 要求 Debug 的 profile/console 刷新使用 Timer 口径，但当前 `DebugGuiDriver.Update()` 仍直接把 `Time.unscaledDeltaTime` 传给 Debug metrics/profile 刷新。

## 关键证据

- `.codestable/architecture/ARCHITECTURE.md:140` — Timer 位于 Debug 之前，给 Debug 相关诊断提供统一时间口径。
- `.codestable/features/2026-06-05-runtime-debug-timer-redesign/runtime-debug-timer-redesign-design.md:110` — 设计要求 Debug 的 profile 刷新和面板刷新使用 Timer 口径。
- `.codestable/features/2026-06-05-runtime-debug-timer-redesign/runtime-debug-timer-redesign-design.md:338` — 流程约束写明 Debug profile 和 Console refresh 作为 Timer 消费者。
- `Assets/GameDeveloperKit/Runtime/Debug/Console/DebugGuiDriver.cs:16` — 当前 driver 直接调用 `m_Module?.UpdateMetrics(Time.unscaledDeltaTime)`。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:435` — `UpdateMetrics(deltaTime)` 用传入 delta 刷新 `Profiles`。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:442` — 同一 delta 还用于累计 metric sample interval。

## 影响

Debug 日志记录已经能附带 Timer tick，但 profile refresh 和 metrics sample 仍绕过 TimerModule。这样 Timer FPS、scaled/unscaled 语义和 Debug 面板刷新节奏之间会出现两个口径：Timer tab 展示的是 Timer snapshot，Profiles/Metrics 用的是 Unity Update delta。短期影响是诊断数据不一致，长期会让 Timer/Debug redesign 的验收边界变模糊。

## 修复方向

把 Debug profile/metrics 刷新注册为 Timer 消费者，或明确记录“metrics 使用 Unity unscaled frame delta”的架构例外并补测试。

## 建议动作

`cs-refactor`，因为这更像模块编排和时钟口径的对齐工作，行为变更需要小步改。
