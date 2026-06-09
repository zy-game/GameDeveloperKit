---
doc_type: audit-finding
audit: 2026-06-07-combat-module
finding_id: "bug-01"
nature: bug
severity: P1
confidence: high
suggested_action: cs-issue
status: fixed
---

# Finding 01：`World.Dispose()` 后公开 API 仍可继续使用已释放 world

## 速答

`World.Dispose()` 只调用 `Clear()` 并设置 `m_Disposed = true`，但 `Create()`、`LoadSystem()`、`Update()`、`Step()`、组件 API、`SaveFrame()` 和 `Rollback()` 都没有检查 disposed 状态；调用方持有 disposed world 引用时仍能重新创建实体和系统，破坏释放后的生命周期边界。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Combat/World.cs:269` — `Dispose()` 中只在首次释放时 `Clear()` 并把 `m_Disposed` 设为 true。
- `Assets/GameDeveloperKit/Runtime/Combat/World.cs:85` — `Create()` 直接委托 `m_Entities.Create()`，没有检查 `m_Disposed`。
- `Assets/GameDeveloperKit/Runtime/Combat/World.cs:100` — `LoadSystem<TSystem>()` 可以在 disposed 后继续注册系统。
- `Assets/GameDeveloperKit/Runtime/Combat/World.cs:202` — `Update(deltaTime)` 仍会累积时间并进入 `Step()`。
- `Assets/GameDeveloperKit/Runtime/Combat/World.cs:236` 和 `Assets/GameDeveloperKit/Runtime/Combat/World.cs:245` — `SaveFrame()` / `Rollback()` 仍直接委托底层 `MassiveWorld`。

## 影响

`CombatModule.Shutdown()` 会 dispose 默认 world 并把 `World` 置空，但外部如果缓存了旧 `World`，仍可在 shutdown 后继续驱动底层 ECS。单机短流程里不一定马上触发，但在测试、场景切换、重进战斗或业务保留引用时，会出现“已释放对象复活”的状态污染。

## 修复方向

给所有公开 mutating/query API 增加统一 disposed guard；disposed 后抛明确异常，或定义只读查询返回失败的统一语义。

## 建议动作

`cs-issue`，因为这是生命周期契约漏洞，需要明确复现、修复和补测试。
