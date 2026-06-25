---
doc_type: audit-finding
finding_id: "01"
slug: framework-full
dimension: arch-drift
severity: P1
confidence: high
action: cs-refactor
---

# Finding 01: Debug 模块目录与命名空间不一致

## 现状

- **目录**: `Runtime/Debug/`（含 `DebugModule.cs`、`Profiles/`、`Console/`、`Metrics/` 等 20 个文件）
- **命名空间**: 全部使用 `namespace GameDeveloperKit.Logger`
- **证据**: Grep 确认 Debug 目录下所有 20 个 .cs 文件命名空间均为 `GameDeveloperKit.Logger`

## 问题

架构文档已记录此问题（"当前源码命名空间仍为 `GameDeveloperKit.Logger`，但目录和默认入口已经归入 Runtime Debug 子系统"），但未修复。目录/asmdef 入口叫 Debug，对外引用 `App.Debug` 返回 `DebugModule`，但内部类型全在 `Logger` 命名空间下。

这导致：
- 新开发者按目录找 `using GameDeveloperKit.Debug` 编译失败
- 对外公开的 `DebugModule` 类和内部 `ProfileHandle` 等类型分属两个不同的概念命名空间
- 架构文档明确模块在 Debug 子系统，但代码残留 Logger 旧名

## 建议动作

将 `Runtime/Debug/` 下所有文件的命名空间从 `GameDeveloperKit.Logger` 迁移到 `GameDeveloperKit.Debug`，同步更新所有 `using GameDeveloperKit.Logger` 引用（TimerModule、ProcedureModule、CombatModule、App.cs 等处）。走 `cs-refactor`。
