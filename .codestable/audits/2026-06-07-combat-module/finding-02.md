---
doc_type: audit-finding
audit: 2026-06-07-combat-module
finding_id: "bug-02"
nature: bug
severity: P1
confidence: high
suggested_action: cs-issue
status: fixed
---

# Finding 02：系统回调内加载/卸载系统会修改正在枚举的注册表

## 速答

`World.Step()`、`SystemManager.NotifyChanged()`、`NotifyDestroyed()`、`Clear()` 等路径直接 `foreach` 遍历 `m_Registrations`；系统的 `OnCreate` / `OnDestroy` / `OnUpdate` 回调可以拿到 `World` 并调用 `LoadSystem()` / `UnloadSystem()`，从而在枚举中修改同一个 `List<Registration>`，导致运行时 `InvalidOperationException` 或生命周期漏发。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Combat/World.cs:224` — `Step()` 对 `m_Systems.Registrations` 做 `foreach`。
- `Assets/GameDeveloperKit/Runtime/Combat/SystemManager.cs:17` — `Registrations` 直接暴露底层 `m_Registrations` 枚举。
- `Assets/GameDeveloperKit/Runtime/Combat/World.cs:226` 到 `Assets/GameDeveloperKit/Runtime/Combat/World.cs:228` — 枚举期间调用用户可覆写的 `registration.System.OnUpdate(entity)`。
- `Assets/GameDeveloperKit/Runtime/Combat/SystemBase.cs:17` — `Initialize(World world)` 把 world 提供给系统，系统可以保存该引用。
- `Assets/GameDeveloperKit/Runtime/Combat/World.cs:100` 和 `Assets/GameDeveloperKit/Runtime/Combat/World.cs:110` — `LoadSystem()` / `UnloadSystem()` 会委托 `SystemManager.Add/Remove` 修改注册表。
- `Assets/GameDeveloperKit/Runtime/Combat/SystemManager.cs:141` 和 `Assets/GameDeveloperKit/Runtime/Combat/SystemManager.cs:162` — 组件变化和实体销毁通知也在遍历 `m_Registrations` 时调用用户回调。

## 影响

系统在回调中触发系统切换并不罕见，例如进入战斗阶段加载某个系统、某个实体死亡后卸载临时系统。当前实现没有“延迟注册队列”或“快照枚举”语义，业务一旦这样做就可能在固定步或组件变化路径中崩溃，属于框架对可重入生命周期缺少封口。

## 修复方向

定义系统注册表的可重入策略：要么在回调期间禁止加载/卸载并抛明确异常，要么把 Add/Remove 排入队列并在当前通知/step 结束后统一应用。

## 建议动作

`cs-issue`，因为这是明确可复现的运行时崩溃风险。
