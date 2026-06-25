---
doc_type: audit-finding
finding_id: "03"
slug: framework-full
dimension: arch-drift
severity: P2
confidence: high
action: cs-refactor
---

# Finding 03: 字段命名约定碎片化

## 现状

AGENTS.md 规定："Private fields commonly use the `m_` prefix, for example `m_Handlers` and `m_Handles`."

实际代码中的分布：

| 模块 | 前缀 | 示例 |
|---|---|---|
| EventModule | `m_` | `m_Listeners`, `m_QueuedEvents`, `m_DispatchCache` |
| CommandModule | `m_` | `m_UndoStack`, `m_RedoStack` |
| ProcedureModule | `m_` | `m_Procedures`, `m_UpdateHandle`, `m_ProfileHandle` |
| OperationModule | `m_` | `m_Operations` |
| UIModule | `m_` | `m_Layers`, `m_Records`, `m_Root`, `m_Canvas` |
| CombatModule | `m_` | `m_UpdateHandle`, `m_ProfileHandle` |
| ConfigModule | `m_` | `m_Tables`, `m_PendingLoads` |
| DataModule | `m_` | `m_Entries`, `m_Serializer` |
| TimerModule | `_` | `_handles`, `_dispatchBuffer`, `_timer`, `_callbackHandles` |
| App | `_` | `_modules`, `_moduleOrder`, `_lifecycleState` |
| ResourceModule | 混合 | `_manifest`, `_setting`, `modes`（无前缀！） |
| ReferencePool | `s_` | `s_ReferenceCollections`（static，符合规范） |

## 问题

- AGENTS.md 说用 `m_`，但 TimerModule 和 App 都用 `_`，且 App 用的是 `private static` 字段，`_` 通常用于实例字段
- ResourceModule 的 `modes` 没有任何前缀，与同类内 `_manifest`/`_setting` 不一致
- 两个约定并存造成认知负担：开发者不知道在新模块中该用哪个前缀

## 建议动作

统一为一种约定。建议统一为 `_` 前缀（与 App、TimerModule 对齐，且更简洁），或统一为 `m_` 并修复 App/TimerModule。同时更新 AGENTS.md。走 `cs-refactor`。
