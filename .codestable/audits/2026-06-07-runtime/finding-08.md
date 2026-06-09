---
doc_type: audit-finding
audit: 2026-06-07-runtime
finding_id: "performance-08"
nature: performance
severity: P2
confidence: medium
suggested_action: cs-refactor
status: fixed
---

# Finding 08：Combat 销毁实体后 wrapper 长期留在缓存中

## 速答

`EntityManager.Destroy()` 成功销毁 Massive entity 后没有从 `m_Entities` 移除对应 wrapper；除 rollback `Rebuild()` 或 `Clear()` 外，实体缓存会随实体 churn 增长。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs:15` — `m_Entities` 用 `Dictionary<long, Entity>` 缓存 wrapper。
- `Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs:66` 到 `Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs:81` — `Destroy(Entity entity)` 通知系统并调用 `m_MassiveWorld.Destroy(entity.Id)`，成功后直接返回 true，没有移除 `m_Entities` 中的 key。
- `Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs:209` 到 `Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs:225` — 清理 stale wrapper 的逻辑只在 `Rebuild()` 中存在。
- `Assets/GameDeveloperKit/Runtime/Combat/World.cs:245` 到 `Assets/GameDeveloperKit/Runtime/Combat/World.cs:250` — `Rebuild()` 只在 rollback 后调用。

## 影响

战斗中大量创建/销毁实体时，已经死亡的 entity wrapper 会持续占用 dictionary entry 和对象引用。单局短生命周期影响较小，但长战斗、编辑器模拟、服务器式长期运行或大量投射物/临时实体会出现无意义缓存增长。

## 修复方向

销毁成功后按 `GetKey(entity.Id, entity.Version)` 移除 wrapper；若需要保留历史 wrapper，应明确容量/生命周期策略。

## 建议动作

`cs-refactor`，因为这是缓存生命周期优化，行为修正范围较小。
