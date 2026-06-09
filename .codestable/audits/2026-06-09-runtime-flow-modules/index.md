---
doc_type: audit-index
audit: 2026-06-09-runtime-flow-modules
scope: "Assets/GameDeveloperKit/Runtime 下 Command / Procedure / Operation / Event 流程编排模块"
created: 2026-06-09
status: partially-fixed
total_findings: 4
fixed_findings: 2
open_findings: 2
---

# runtime-flow-modules 审计报告

## 范围

本次继续 Runtime 审计，专项扫描 `Assets/GameDeveloperKit/Runtime/Command/**/*.cs`、`Assets/GameDeveloperKit/Runtime/Procedure/**/*.cs`、`Assets/GameDeveloperKit/Runtime/Operation/**/*.cs`、`Assets/GameDeveloperKit/Runtime/Event/**/*.cs`，并交叉读取对应测试与设计档：`CommandModuleTests`、`ProcedureModuleTests`、`OperationModuleTests`、`EventModuleTests`、`command-module-design`、`procedure-module-design`、`operationmodule-*`。范围覆盖同步事件派发、命令历史、命令注册执行、procedure 切换、operation running registry、生命周期和设计契约偏离。`OperationModule` 本轮未发现高价值新增问题。

## 总评

共发现 4 条有效问题：P1 2 条、P2 2 条。P1 已按用户确认方案修复：Event 的普通 `Fire` 改为 Timer Update 队列派发，并提供不安全即时 `FireNow`；Procedure 切换不再发送全局切换事件。剩余两条开放项分别是 pooled `ArgsBase` 复用后保持 consumed 状态，以及 Command registry 创建的非历史命令缺少清晰释放所有权。

## 发现清单

| # | 性质 | 严重度 | 置信度 | 状态 | 标题 | 文件 |
|---|---|---|---|---|---|---|
| 1 | bug | P1 | high | fixed | EventModule 嵌套派发复用同一个 dispatch cache，会破坏外层枚举 | [finding-01.md](finding-01.md) |
| 2 | arch-drift | P1 | high | fixed | ProcedureChanged 从本地事件偏成 EventModule 硬依赖 | [finding-02.md](finding-02.md) |
| 3 | bug | P2 | high | open | ArgsBase 被 ReferencePool 复用后仍保持已消费状态 | [finding-03.md](finding-03.md) |
| 4 | maintainability | P2 | medium | open | Command registry 创建的非历史命令释放所有权不清晰 | [finding-04.md](finding-04.md) |

## 按维度分布

| 性质 | P0 | P1 | P2 | 合计 |
|---|---|---|---|---|
| bug | 0 | 1 | 1 | 2 |
| security | 0 | 0 | 0 | 0 |
| performance | 0 | 0 | 0 | 0 |
| maintainability | 0 | 0 | 1 | 1 |
| arch-drift | 0 | 1 | 0 | 1 |
| **合计** | **0** | **2** | **2** | **4** |

## 下一步建议

- **P1 已修复**：finding-01 / finding-02 已进入 `.codestable/issues/2026-06-09-runtime-flow-modules-audit-fixes/` 修复记录。
- **P2 待处理**：finding-03 建议开 `cs-issue` 补事件参数 reset 语义；finding-04 建议开 `cs-refactor` 明确 registry-created command 的释放所有权。
