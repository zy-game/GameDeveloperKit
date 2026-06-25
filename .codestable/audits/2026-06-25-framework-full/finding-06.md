---
doc_type: audit-finding
finding_id: "06"
slug: framework-full
dimension: maintainability
severity: P1
confidence: high
action: cs-refactor
---

# Finding 06: Debug Profile 注册/注销模式三处逐字复制

## 证据

以下 4 个方法在三个模块中以完全相同逻辑出现：

**TimerModule.cs** (行 ~477-526):
```csharp
internal void RegisterDebugProfile(DebugModule debug)
{
    if (debug == null) throw new ArgumentNullException(nameof(debug));
    debug.RegisterProfile(m_ProfileHandle);
}
internal void UnregisterDebugProfile(DebugModule debug) { ... }
private void TryRegisterDebugProfile()
{
    if (App.TryGetRegistered<DebugModule>(out var debug))
        RegisterDebugProfile(debug);
}
private void TryUnregisterDebugProfile() { ... }
```

**ProcedureModule.cs** — 完全相同的 4 个方法，仅 `m_ProfileHandle` 类型不同。
**CombatModule.cs** — 完全相同的 4 个方法，仅 `m_ProfileHandle` 类型不同。

## 问题

- 任何对 Debug Profile 注册逻辑的修改需要三处同步
- 如果未来新增模块需要 Debug Profile，仍会复制这一模式
- 这三处代码的存在理由完全一致：模块有内部 ProfileHandle，需要在 DebugModule 可用时软注册/注销

## 建议动作

将 `TryRegisterDebugProfile`/`TryUnregisterDebugProfile` 逻辑提取到一个工具方法或基类能力中。例如在 `GameModuleBase` 添加 `protected void TryRegisterProfile(ProfileHandle handle)` / `TryUnregisterProfile`。走 `cs-refactor`。
