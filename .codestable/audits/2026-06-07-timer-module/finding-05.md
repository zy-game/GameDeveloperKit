---
doc_type: audit-finding
audit: 2026-06-07-timer-module
finding_id: "arch-drift-05"
nature: arch-drift
severity: P2
confidence: high
suggested_action: cs-refactor
status: fixed
---

# Finding 05：Timer/Debug redesign 已完成，但架构总入口仍未记录 Timer 子系统

## 速答

Timer/Debug redesign checklist 已把 Timer 和 Debug 相关步骤标为 done，当前代码也有 `TimerModule`、`App.Timer`、Debug Timers tab 和默认启动顺序，但 `.codestable/architecture/ARCHITECTURE.md` 仍只记录 FileSystem、Download、Event、Resource、Combat，缺少 Timer 子系统和 Debug redesign 的架构现状。

## 关键证据

- `.codestable/features/2026-06-05-runtime-debug-timer-redesign/runtime-debug-timer-redesign-checklist.yaml:11` 到 `.codestable/features/2026-06-05-runtime-debug-timer-redesign/runtime-debug-timer-redesign-checklist.yaml:16` — Timer Clock 骨架与 Delay/Countdown/Interval 两个实现步骤均标记为 `done`。
- `.codestable/features/2026-06-05-runtime-debug-timer-redesign/runtime-debug-timer-redesign-design.md:443` 到 `.codestable/features/2026-06-05-runtime-debug-timer-redesign/runtime-debug-timer-redesign-design.md:451` — 设计明确要求验收后更新 `ARCHITECTURE.md`，记录 Timer 子系统、Debug 子系统和默认启动顺序。
- `Assets/GameDeveloperKit/Runtime/App.cs:277` 到 `Assets/GameDeveloperKit/Runtime/App.cs:278` — 默认启动顺序已经注册 Timer 早于 Debug。
- `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:562` 到 `Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs:578` — Debug Console 已有 Timers tab，从 `TimerModule.Snapshot()` 绘制 active delay/countdown/interval。
- `.codestable/architecture/ARCHITECTURE.md:13` 到 `.codestable/architecture/ARCHITECTURE.md:138` — 架构总入口当前只列出 FileSystem、Download、Event、Resource、Combat，未记录 Timer 或 Debug 子系统。

## 影响

CodeStable 后续审计和设计会把 `.codestable/architecture/ARCHITECTURE.md` 当“当前现状”对照源。Timer/Debug 已经是 Runtime 默认启动链的一部分，如果架构入口不记录，后续 feature 容易重复设计 Timer 语义、误判 Debug/Logger 边界，或遗漏 Timer 早于 Debug 的启动约束。

## 修复方向

走 `cs-arch update` 同步架构现状：补 Timer 入口 `TimerModule` / `App.Timer`、Clock/Delay/Countdown/Interval、seconds/tick/delta、owner/tag/cancel/snapshot、Debug Timers tab，以及 Timer 早于 Debug 的默认启动顺序。

## 建议动作

`cs-refactor`，因为这不是代码 bug，而是架构文档和已落地实现之间的同步债务。
