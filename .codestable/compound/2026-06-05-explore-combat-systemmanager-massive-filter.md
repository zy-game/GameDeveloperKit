---
doc_type: explore
type: question
date: 2026-06-05
slug: combat-systemmanager-massive-filter
topic: Combat SystemManager 的 include/exclude 匹配是否应该使用 Massive Query/Filter
scope: Assets/GameDeveloperKit/Runtime/Combat/SystemManager.cs 与 Assets/GameDeveloperKit/Plugins/massive/Runtime Query/Filter API
keywords:
  - combat
  - systemmanager
  - massive
  - query
  - filter
status: active
confidence: high
updated: 2026-06-05
---

## 问题与范围

问题：`SystemRegistration.Matches` 当前用 `foreach` 调用 `EntityManager.Has` 判断 include/exclude，是否应该改用 Massive 自带的 `Query` / `Filter` / `QueryCache`。

范围：只探索现有代码和 Massive 公开运行时 API，不拍板实现方案。

## 速答

Massive 确实有原生过滤体系，而且可以从运行时 `Type` 映射到 `BitSet` 后构造 `Filter`。当前实现已经改为：`SystemRegistration` 只缓存 include / exclude 对应的 Massive `BitSet[]` / `Filter`，不持有 entity 集合。

生命周期的边界由 Combat 包装层负责：实体创建 / 销毁、组件 Set / Add / Remove、rollback 前后会使用临时匹配快照比较“是否刚进入/离开匹配条件”；固定步 `OnUpdate` 则通过 Massive query 现查当前匹配实体。

```mermaid
flowchart LR
    SystemBase[SystemBase Include/Exclude] --> Registration[SystemRegistration]
    Registration --> Filter[Massive BitSet[] / Filter]
    Filter --> Query[Massive Query current matches]
    Query --> OnUpdate[OnUpdate matching entity]
    EntityChange[Create/Destroy/Set/Add/Remove/Rollback] --> Snapshot[Temporary before/after match snapshot]
    Snapshot --> Lifecycle[OnCreate/OnDestroy]
```

## 关键证据

- `SystemManager.Add` 先规范化 include / exclude，校验冲突，再创建 `SystemRegistration` 并对当前匹配实体调用 `OnCreate`：`Assets/GameDeveloperKit/Runtime/Combat/SystemManager.cs:33`
- `SystemRegistration` 构造时把 include / exclude 解析成 Massive `BitSet[]` 并创建 `Filter`，没有 `HashSet<Entity>` 或其他 entity 集合字段：`Assets/GameDeveloperKit/Runtime/Combat/SystemManager.cs:348`
- `SystemManager.Update` 按注册顺序调用 `InvokeOnUpdateForMatches`，后者通过 `new Query(m_World.MassiveWorld, registration.Filter)` 查询当前匹配实体：`Assets/GameDeveloperKit/Runtime/Combat/SystemManager.cs:85`
- `EntityManager.Set/Add/Remove` 在变更前捕获 `SystemManager.Capture(entity)`，成功变更后调用 `NotifyChanged` 比较前后匹配差异：`Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs:82`
- `World.Rollback` 在回滚前捕获 world 级匹配快照，回滚后重建实体句柄并用快照差异触发生命周期回调：`Assets/GameDeveloperKit/Runtime/Combat/World.cs:123`
- Massive `Filter` 本身就是 `BitSet[] Included` / `BitSet[] Excluded`，并会检查 include/exclude 冲突：`Assets/GameDeveloperKit/Plugins/massive/Runtime/Filter/Filter.cs:12`
- Massive `QueryExtensions` 暴露了 `Include(this Query, BitSet[] all)` / `Exclude(this Query, BitSet[] none)`，不只有泛型便利方法，因此运行时构造 `BitSet[]` 后可以接入 query：`Assets/GameDeveloperKit/Plugins/massive/Runtime/Query/QueryExtensions.cs:10`
- Massive `Query` 应用过滤时会把 include/exclude 的 `BitSet` 加入 `QueryCache`，无 include 时会默认 include `World.Entities`：`Assets/GameDeveloperKit/Plugins/massive/Runtime/Query/Query.cs:539`

## 细节展开

Massive 的泛型 `Include<T>()` / `Exclude<T>()` 更像高性能便利 API，但这次 Combat 的 `SystemBase.Include` / `Exclude` 是 `ComponentType[]`，本质上是运行时 `Type` 描述。Massive 的 `Sets.GetReflected(Type)` 提供了动态类型到 `BitSet` 的桥，现在 `SystemRegistration` 在注册时使用这条路径预编译过滤条件。

真正的边界在生命周期语义：Massive query 能给“当前满足过滤条件的实体集合”，但 Combat 还需要知道“这个实体是不是刚进入匹配集合”“是不是刚离开匹配集合”。现在这部分不由 system 持有 entity，而是由实体变更/rollback 时的一次性快照负责。

## 未决问题

- 如果组件变化很频繁，实体级快照会逐系统检查匹配状态；是否需要更细粒度的 component-to-system 索引，是后续性能优化问题。
- `Remove(system)` / `Clear()` 在无常驻 entity 集合的约束下对当前匹配实体触发 `OnDestroy`；它不再保存历史匹配状态。

## 后续建议

后续验收时重点检查：`SystemRegistration` 不持有 entity，`OnUpdate` 通过 Massive query 现查当前匹配实体，组件变更和 rollback 仍能正确触发 `OnCreate` / `OnDestroy`。

## 相关文档

- `.codestable/features/2026-06-04-combat-module/combat-module-design.md`
