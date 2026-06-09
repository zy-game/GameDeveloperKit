---
doc_type: audit-finding
audit: 2026-06-09-runtime-flow-modules
finding_id: "arch-drift-02"
nature: arch-drift
severity: P1
confidence: high
suggested_action: cs-issue
status: fixed
---

# Finding 02：ProcedureChanged 从本地事件偏成 EventModule 硬依赖

## 速答

Procedure 设计明确要求状态变化通知不应强制注册 `EventModule`，但原实现切换成功后直接调用 `App.Event.Fire(...)`。当前已修复：Procedure 切换完成后只更新 `Current`，不再发送全局 `ProcedureChangedEventArgs`。

## 修复状态

- 状态：已修复。
- 修复记录：`.codestable/issues/2026-06-09-runtime-flow-modules-audit-fixes/runtime-flow-modules-audit-fixes-fix-note.md`
- 验证：Procedure 切换测试已移除全局 changed event 断言，并覆盖 leave/enter 顺序与 `Current` 更新。

## 关键证据

- `.codestable/features/2026-06-04-procedure-module/procedure-module-design.md:59`：设计边界写明“不替代 `EventModule` 的事件订阅 / 派发；首版状态变化通知使用本地 C# 事件”。
- `.codestable/features/2026-06-04-procedure-module/procedure-module-design.md:103` 到 `.codestable/features/2026-06-04-procedure-module/procedure-module-design.md:104`：设计要求 `event Action<ProcedureChangedEventArgs> ProcedureChanged`，并说明不强制注册 `EventModule`。
- `Assets/GameDeveloperKit/Runtime/Procedure/ProcedureModule.cs:190` 到 `Assets/GameDeveloperKit/Runtime/Procedure/ProcedureModule.cs:191`：实现为 `Current = next; App.Event.Fire(new ProcedureChangedEventArgs(previous, next, userData));`。
- `Assets/GameDeveloperKit/Runtime/App.cs:36` 和 `Assets/GameDeveloperKit/Runtime/App.cs:112`：`App.Event` 走 `Get<EventModule>()`，未注册时抛 `GameException("Module 'EventModule' is not registered.")`。
- `Assets/GameDeveloperKit/Tests/Runtime/ProcedureModuleTests.cs:67` 到 `Assets/GameDeveloperKit/Tests/Runtime/ProcedureModuleTests.cs:70`：测试覆盖了手动只注册 `ProcedureModule` 后可访问模块，但没有覆盖随后 `ChangeAsync` 的 `App.Event` 依赖。

## 影响

默认 `App.Startup()` 路径会先注册 `EventModule`，所以不一定立刻暴露。但设计和测试都支持手动注册 `ProcedureModule`；在该路径下，`ChangeAsync` 会先执行 leave/enter 并把 `Current` 设置为目标 procedure，然后在发送通知时因缺少 `EventModule` 抛错。调用方看到切换失败，但模块状态已经变化，调试和业务控制流都会变得混乱。

## 修复方向

恢复 ProcedureModule 自有 `ProcedureChanged` 本地事件；如仍需要全局事件转发，应做成可选桥接，并在 `EventModule` 未注册时不影响 procedure 切换结果。

## 建议动作

`cs-issue`，因为这是设计契约偏离并带有实际运行时失败路径。
