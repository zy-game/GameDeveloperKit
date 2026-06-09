---
doc_type: audit-index
audit: 2026-06-07-combat-module
scope: Assets/GameDeveloperKit/Runtime/Combat 及 CombatModule requirement/design/test 档案
created: 2026-06-07
status: fixed
total_findings: 5
---

# combat-module 审计报告

## 范围

本次扫描 `Assets/GameDeveloperKit/Runtime/Combat/*.cs` 8 个运行时代码文件、`Assets/GameDeveloperKit/Tests/Runtime/CombatModuleTests.cs`，并对照 `.codestable/requirements/combat-module.md`、`.codestable/features/2026-06-04-combat-module/combat-module-design.md`、`.codestable/compound/2026-06-05-explore-combat-systemmanager-massive-filter.md` 和 `.codestable/architecture/ARCHITECTURE.md`。范围聚焦 Combat 首版承诺内的运行时契约、生命周期、查询性能和架构落档，不把设计已明确排除的战斗规则、技能、AI、GameObject 绑定、自动系统扫描、多线程、网络同步等计为缺口。

## 总评

CombatModule 的首版核心能力已经成型：默认 world、实体组件门面、include/exclude 系统匹配、固定步驱动、rollback 后匹配重建和基本测试都存在。审计发现 5 条有效问题：P1 3 条、P2 2 条，均已完成修复或落档。修复覆盖 world dispose 后生命周期封口、系统回调内加载/卸载系统的枚举安全、缺失组件错误口径、组件变更匹配索引，以及 Combat 架构文档同步。

## 发现清单

| # | 性质 | 严重度 | 置信度 | 标题 | 文件 |
|---|---|---|---|---|---|
| 1 | bug | P1 | high | 已修复：`World.Dispose()` 后公开 API 仍可继续使用已释放 world | [finding-01.md](finding-01.md) |
| 2 | bug | P1 | high | 已修复：系统回调内加载/卸载系统会修改正在枚举的注册表 | [finding-02.md](finding-02.md) |
| 3 | bug | P2 | medium | 已修复：缺失组件读取/反射查询错误透出 massive 断言异常 | [finding-03.md](finding-03.md) |
| 4 | performance | P1 | medium | 已修复：组件变更按全部系统逐一匹配，频繁变更会形成热路径放大 | [finding-04.md](finding-04.md) |
| 5 | arch-drift | P2 | high | 已修复：Combat 已实现但架构总入口仍未记录 Combat 子系统 | [finding-05.md](finding-05.md) |

## 按维度分布

| 性质 | P0 | P1 | P2 | 合计 |
|---|---|---|---|---|
| bug | 0 | 2 | 1 | 3 |
| security | 0 | 0 | 0 | 0 |
| performance | 0 | 1 | 0 | 1 |
| maintainability | 0 | 0 | 0 | 0 |
| arch-drift | 0 | 0 | 1 | 1 |
| **合计** | **0** | **3** | **2** | **5** |

## 修复结果

- `World` 增加 disposed guard；旧 world 公开 API 释放后抛 `GameException`，旧 entity `IsAlive == false`。
- `SystemManager` 的 step/通知路径使用注册表快照和 active 标记，允许回调内加载/卸载系统而不破坏当前枚举。
- `EntityManager` 在 Combat 层显式校验组件类型和缺失组件读取，统一抛 `GameException` / `ArgumentException`。
- 组件增删按变更组件类型只捕获和通知相关系统，降低高频组件 churn 的匹配放大。
- `.codestable/architecture/ARCHITECTURE.md` 已补 Combat 子系统现状。
