---
doc_type: issue-fix
issue: 2026-06-09-runtime-flow-modules-audit-fixes
path: fast-track
fix_date: 2026-06-09
status: fixed
tags:
  - runtime
  - audit
  - event
  - procedure
---

# Runtime Flow Modules Audit Fixes 修复记录

## 1. 问题描述

来源：`.codestable/audits/2026-06-09-runtime-flow-modules/` 的 P1 finding-01 和 finding-02。用户确认修复方向：Event 普通 `Fire` 不直接触发，改为保存到队列并由 Timer Update 处理；保留不安全即时 `FireNow`；Procedure 切换事件移除。

## 2. 根因

- `EventModule.Fire()` 原本直接同步派发，且复用模块级 `m_DispatchCache`。listener 内嵌套调用 `Fire()` 时会清空并改写外层正在枚举的 List，导致确定性枚举异常。
- `ProcedureModule.ChangeAsync()` 切换成功后调用 `App.Event.Fire(new ProcedureChangedEventArgs(...))`，让 Procedure 手动注册路径隐式依赖 EventModule，并偏离当前用户确认的“移除切换事件”方向。

## 3. 修复方案

- Event：新增事件队列，`Fire<TEvent>()` 只入队；`Startup()` / `Fire()` 尝试通过已注册的 `TimerModule` 注册 `EventModule.Dispatch` Update 回调；没有 Timer 时 `Fire()` 明确抛 `GameException`，避免事件静默留在无人消费的队列中；Update 中只处理本轮开始前已经排队的事件，listener 中再次 `Fire()` 的事件留到下一次 Update。
- Event：新增 `FireNow<TEvent>()` 保留即时同步派发能力；委托 listener 增加非泛型 invoker，让队列可以按保存的运行时事件类型派发。
- Procedure：移除 `ChangeAsync()` 中的全局 `ProcedureChangedEventArgs` 派发，测试只验证 leave/enter 顺序和 `Current` 更新。
- 默认启动顺序：`App.Startup()` 中把 `TimerModule` 提前到 `EventModule` 之前，确保默认路径下 Event 队列有 Timer Update 驱动。

## 4. 改动文件清单

- `Assets/GameDeveloperKit/Runtime/App.cs`
- `Assets/GameDeveloperKit/Runtime/Event/EventModule.cs`
- `Assets/GameDeveloperKit/Runtime/Event/Listener.cs`
- `Assets/GameDeveloperKit/Runtime/Procedure/ProcedureModule.cs`
- `Assets/GameDeveloperKit/Tests/Runtime/EventModuleTests.cs`
- `Assets/GameDeveloperKit/Tests/Runtime/ProcedureModuleTests.cs`
- `.codestable/architecture/ARCHITECTURE.md`
- `.codestable/audits/2026-06-09-runtime-flow-modules/`

## 5. 验证结果

- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过，0 warning，0 error。
- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`：通过，0 warning，0 error。
- 新增/调整测试覆盖：
  - `Fire_WhenQueued_DispatchesOnTimerUpdate`
  - `Fire_WhenTimerModuleIsMissing_Throws`
  - `Fire_WhenListenerQueuesEvent_DispatchesNestedEventOnNextTimerUpdate`
  - 原即时派发相关测试改为 `FireNow`
  - Procedure 切换测试不再期待全局 changed event

## 6. 遗留事项

- finding-03 / finding-04 仍为 open，分别建议后续走 `cs-issue` 和 `cs-refactor`。
- `ProcedureChangedEventArgs.cs` 当前已无运行时引用，但生成的 Unity project file 仍列出该源码；本次不手动删除该文件，等待 Unity 刷新项目文件后可单独清理。
- 未运行 Unity Test Runner；本次完成 runtime 与 runtime tests C# 构建验证。
