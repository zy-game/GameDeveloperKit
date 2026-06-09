---
doc_type: audit-finding
audit: 2026-06-07-combat-module
finding_id: "bug-03"
nature: bug
severity: P2
confidence: medium
suggested_action: cs-issue
status: fixed
---

# Finding 03：缺失组件读取/反射查询错误透出 massive 断言异常

## 速答

Combat 公开 API 已经用 `GameException` 处理 dead entity、foreign world、query 冲突等错误，但 `GetComponent<T>()` 读取未添加组件、`HasComponent(Entity, Type)` 使用未知或非数据 set 类型时，错误继续由 massive 的条件断言或底层反射路径决定，发布配置下还可能因为 `MASSIVE_ASSERT` 关闭而变成更底层的数组/空引用异常。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs:179` 到 `Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs:182` — `GetComponent<TComponent>()` 只校验 entity alive，然后直接返回 `m_MassiveWorld.Get<TComponent>(entity.Id)`。
- `Assets/GameDeveloperKit/Plugins/massive/Runtime/World/Extensions/WorldIdExtensions.cs:210` 到 `Assets/GameDeveloperKit/Plugins/massive/Runtime/World/Extensions/WorldIdExtensions.cs:228` — massive 的 `Get<T>()` 依赖 `InvalidGetOperationException.ThrowIf*` 和 `DataSet<T>.Get(id)`。
- `Assets/GameDeveloperKit/Plugins/massive/Runtime/Exceptions/MassiveException.cs:9` — massive 异常守卫使用条件符号 `MASSIVE_ASSERT`。
- `Assets/GameDeveloperKit/Plugins/massive/Runtime/DataSet/DataSet.cs:36` 到 `Assets/GameDeveloperKit/Plugins/massive/Runtime/DataSet/DataSet.cs:39` — 数据未添加时同样依赖条件 guard 后直接索引 `PagedData`。
- `Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs:191` 到 `Assets/GameDeveloperKit/Runtime/Combat/EntityManager.cs:199` — 反射版 `HasComponent(Entity, Type)` 没有校验 `componentType` 是否继承 `ComponentBase`，也没有处理 `GetReflected(componentType)` 返回非 `IDataSet` 的情况。

## 影响

调用方在写战斗系统时很容易先 `GetComponent<T>()` 后补判断；当前错误口径不稳定，既不统一成 GameDeveloperKit 的 `GameException`，也没有 `TryGetComponent` 这类安全入口。影响主要是调试体验和发布环境异常可读性，严重度低于生命周期崩溃。

## 修复方向

在 Combat 层显式检查组件类型、组件是否存在和 data set 类型；缺失时抛 `GameException`，并可补 `TryGetComponent<T>()` 降低业务误用成本。

## 建议动作

`cs-issue`，因为这是公开 API 错误语义不稳定，需要补 guard 和测试。
