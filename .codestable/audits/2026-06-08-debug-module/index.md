---
doc_type: audit-index
audit: 2026-06-08-debug-module
scope: "Assets/GameDeveloperKit/Runtime/Debug runtime debug subsystem"
created: 2026-06-08
status: active
total_findings: 5
---

# debug-module 审计报告

## 范围

本次继续 Runtime 审计，专项扫描 `Assets/GameDeveloperKit/Runtime/Debug/**/*.cs`、`Assets/GameDeveloperKit/Tests/Runtime/DebugModuleTests.cs`，并对照 `.codestable/architecture/ARCHITECTURE.md` 与 `runtime-debug-timer-redesign` 设计档。范围覆盖 Debug 日志 pipeline、sink、realtime transport、analytics、profile registry、IMGUI console、Unity log capture、Timer 口径和测试缺口；不重复记录 2026-06-07 runtime 审计中已经修复的 Debug 外发脱敏问题。

## 总评

共发现 5 条有效问题：P1 3 条、P2 2 条。Debug 的主干能力已经具备，但 extension point 和生命周期边界仍偏脆：异步 transport 用 fire-and-forget 导致失败状态和关闭语义竞态，ProfileHandle 的异常隔离没有覆盖 `Name`/`Category`，redaction 会直接调用外部对象 `ToString()`，都有机会让调试基础设施反向影响业务调用。另外，Debug profile/metrics 仍由 Unity `Update()` 直接推进，和 Timer 统一口径设计不完全一致；`DebugModule` 也承担了过多子域职责，后续维护成本会继续升高。

## 发现清单

| # | 性质 | 严重度 | 置信度 | 标题 | 文件 |
|---|---|---|---|---|---|
| 1 | bug | P1 | medium | 异步 log transport fire-and-forget，失败状态与 shutdown 存在竞态 | [finding-01.md](finding-01.md) |
| 2 | bug | P1 | high | ProfileHandle 的 `Name`/`Category` 抛异常会击穿隔离 | [finding-02.md](finding-02.md) |
| 3 | bug | P1 | medium | redaction 调用外部对象 `ToString()`，日志 API 可能反向抛异常 | [finding-03.md](finding-03.md) |
| 4 | arch-drift | P2 | medium | Debug profile/metrics 仍由 Unity delta 驱动，未接入 Timer 统一口径 | [finding-04.md](finding-04.md) |
| 5 | maintainability | P2 | high | `DebugModule` 聚合过多子域，后续改动风险偏高 | [finding-05.md](finding-05.md) |

## 按维度分布

| 性质 | P0 | P1 | P2 | 合计 |
|---|---|---|---|---|
| bug | 0 | 3 | 0 | 3 |
| security | 0 | 0 | 0 | 0 |
| performance | 0 | 0 | 0 | 0 |
| maintainability | 0 | 0 | 1 | 1 |
| arch-drift | 0 | 0 | 1 | 1 |
| **合计** | **0** | **3** | **2** | **5** |

## 下一步建议

- **P1 本迭代修**：优先开 `cs-issue` 修 finding-01、finding-02、finding-03，它们都可能让 Debug 扩展点影响业务调用或运行时状态。
- **P2 有空再看**：finding-04 建议通过 `cs-refactor` 把 Debug 刷新口径接入 Timer；finding-05 建议等 P1 稳定后再拆分 log/profile/console/transport 子服务。
