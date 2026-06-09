---
doc_type: audit-index
audit: 2026-06-07-timer-module
scope: Assets/GameDeveloperKit/Runtime/Timer 及 Timer 相关 requirement/design/test/architecture 档案
created: 2026-06-07
status: fixed
total_findings: 5
---

# timer-module 审计报告

## 范围

本次扫描 `Assets/GameDeveloperKit/Runtime/Timer/*.cs` 7 个运行时代码文件、`Assets/GameDeveloperKit/Tests/Runtime/TimerModuleTests.cs`，并对照 `.codestable/requirements/timer-module.md`、`.codestable/features/2026-06-05-runtime-debug-timer-redesign/runtime-debug-timer-redesign-design.md`、对应 checklist 以及 `.codestable/architecture/ARCHITECTURE.md`。范围聚焦 Timer Clock、Delay、Countdown、Interval、owner/tag/cancel、snapshot、Debug Timer tab 和架构落档，不把设计明确排除的线程调度、job system、网络回滚确定性、服务器时间等计为缺口。

## 总评

TimerModule 的公开 API 和基本测试已经覆盖延迟、倒计时、循环、暂停/恢复、取消、owner/tag 和快照。审计发现 5 条有效问题：P1 3 条、P2 2 条，均已完成修复或落档。修复覆盖 Timer 自有 fixed tick、scaled/unscaled 分流、重复 Startup 幂等、Interval catch-up/余量保留，以及 Timer/Debug 架构现状同步。

## 发现清单

| # | 性质 | 严重度 | 置信度 | 标题 | 文件 |
|---|---|---|---|---|---|
| 1 | bug | P1 | high | 已修复：配置 FPS 不控制实际 FixedUpdate 频率，Timer 时间会与真实时间漂移 | [finding-01.md](finding-01.md) |
| 2 | bug | P1 | high | 已修复：`useUnscaledTime` 无效，缩放和非缩放 timer 使用同一个 delta | [finding-02.md](finding-02.md) |
| 3 | bug | P1 | medium | 已修复：重复 `Startup()` 会遗留旧 Timer driver，只清理最后一个对象 | [finding-03.md](finding-03.md) |
| 4 | bug | P2 | medium | 已修复：Interval 大步长只触发一次并丢弃超出时间，循环调度会漂移 | [finding-04.md](finding-04.md) |
| 5 | arch-drift | P2 | high | 已修复：Timer/Debug redesign 已完成，但架构总入口仍未记录 Timer 子系统 | [finding-05.md](finding-05.md) |

## 按维度分布

| 性质 | P0 | P1 | P2 | 合计 |
|---|---|---|---|---|
| bug | 0 | 3 | 1 | 4 |
| security | 0 | 0 | 0 | 0 |
| performance | 0 | 0 | 0 | 0 |
| maintainability | 0 | 0 | 0 | 0 |
| arch-drift | 0 | 0 | 1 | 1 |
| **合计** | **0** | **3** | **2** | **5** |

## 修复结果

- Timer driver 改为 `Update()` 中按 `TimerSettings.FPS` 或默认 50 FPS 累积自有 fixed tick，不再依赖 Unity `FixedUpdate` 频率，也不改写 `Time.fixedDeltaTime`。
- Timer tick 同时传入 scaled/unscaled delta，`useUnscaledTime` 的句柄使用 unscaled clock。
- `Startup()` 重复调用时 no-op，不再创建多个 `"Timer"` driver。
- `Interval()` 在单帧跨多个 interval 时补触发并保留余量，每次 callback delta 为单个 interval。
- `.codestable/architecture/ARCHITECTURE.md` 已补 Timer 与 Debug 当前架构现状。
