---
doc_type: audit-finding
audit: 2026-06-07-combat-module
finding_id: "performance-04"
nature: performance
severity: P1
confidence: medium
suggested_action: cs-refactor
status: fixed
---

# Finding 04：组件变更按全部系统逐一匹配，频繁变更会形成热路径放大

## 速答

每次实体创建、添加组件、移除组件、销毁前都会对所有系统捕获匹配状态，变更后再对所有系统重新匹配；每个匹配又逐个检查 include/exclude 类型。系统数和组件变化数增大后，这条路径会变成 `变更次数 × 系统数 × 查询组件数` 的热路径。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs:99` 到 `Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs:101` — `AddComponent<TComponent>()` 在变更前捕获所有系统匹配，变更后通知所有系统。
- `Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs:125` 到 `Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs:133` — 反射添加组件路径同样捕获和通知。
- `Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs:151` 到 `Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs:155` — `RemoveComponent<TComponent>()` 同样走全系统捕获和通知。
- `Assets/GameDeveloperKit/Runtime/Combat/SystemManager.cs:119` 到 `Assets/GameDeveloperKit/Runtime/Combat/SystemManager.cs:127` — `Capture(entity)` 遍历全部 `m_Registrations` 并调用 `registration.Matches(entity)`。
- `Assets/GameDeveloperKit/Runtime/Combat/SystemManager.cs:141` 到 `Assets/GameDeveloperKit/Runtime/Combat/SystemManager.cs:159` — `NotifyChanged()` 再遍历全部注册并重新匹配。
- `.codestable/compound/2026-06-05-explore-combat-systemmanager-massive-filter.md:58` 到 `.codestable/compound/2026-06-05-explore-combat-systemmanager-massive-filter.md:61` — 既有 explore 已记录“如果组件变化很频繁，实体级快照会逐系统检查匹配状态”是后续性能优化问题。

## 影响

Combat 首版的功能契约没有错，但“投射物、Buff、状态切换”这类战斗场景常见高频组件变化。一旦业务系统数量增加，单帧大量 Add/Remove 会放大匹配成本，影响固定步预算。当前还没有性能测试或索引来证明在目标规模内足够稳。

## 修复方向

建立组件类型到受影响系统的索引，只对 include/exclude 涉及变更组件的系统做生命周期差异检查；同时补一个大规模组件 churn 的性能基准。

## 建议动作

`cs-refactor`，因为这是行为不变的热路径优化和数据结构调整。
