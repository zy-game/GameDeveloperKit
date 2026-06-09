---
doc_type: audit-finding
audit: 2026-06-09-runtime-flow-modules
finding_id: "maintainability-04"
nature: maintainability
severity: P2
confidence: medium
suggested_action: cs-refactor
status: open
---

# Finding 04：Command registry 创建的非历史命令释放所有权不清晰

## 速答

`CommandModule.ExecuteAsync(string, args)` 通过 registry factory 创建命令实例，但命令失败或以 `Transient` 模式成功时不会进入历史栈，也没有统一释放策略，调用方很难判断谁负责 `ICommand.Release()`。

## 关键证据

- `Assets/GameDeveloperKit/Runtime/Command/CommandModule.Registry.cs:112` 到 `Assets/GameDeveloperKit/Runtime/Command/CommandModule.Registry.cs:119`：registry 路径创建 `command`，执行成功后把 `command` 塞进 `CommandInvokeResult.Success(...)`。
- `Assets/GameDeveloperKit/Runtime/Command/CommandModule.Registry.cs:121` 到 `Assets/GameDeveloperKit/Runtime/Command/CommandModule.Registry.cs:123`：执行过程抛异常时只返回失败结果，局部创建的 `command` 不会被释放，也不会返回给调用方。
- `Assets/GameDeveloperKit/Runtime/Command/CommandModule.cs:96` 到 `Assets/GameDeveloperKit/Runtime/Command/CommandModule.cs:108`：`Undoable` 命令进入 undo 栈，`Barrier` 命令执行后 `command.Release()`；`Transient` 分支只是 `break`，没有释放，也没有所有权标记。
- `Assets/GameDeveloperKit/Runtime/Debug/Console/DebugGuiDriver.cs:263` 到 `Assets/GameDeveloperKit/Runtime/Debug/Console/DebugGuiDriver.cs:265`：Debug command tab 只使用 `result.Message` 并写日志，不处理 `result.Command` 的释放。

## 影响

历史命令的所有权比较清楚：undo/redo 栈持有并在清理时释放。registry-created transient 命令和执行失败命令则处在灰区：它们可能来自 `ReferencePool` 或持有临时资源，但模块不会释放，Debug GUI 等调用方也没有释放习惯。短期表现是资源泄漏或池计数不回落，长期会让命令作者对 `HistoryMode.Transient` 的生命周期产生分歧。

## 修复方向

明确 registry-created 命令的所有权：例如非历史命令由 `CommandModule` 在执行结束后释放，或让 `CommandInvokeResult` 显式实现可释放结果并要求调用方消费；失败路径也需要和成功路径一致。

## 建议动作

`cs-refactor`，因为这里更像生命周期契约和 API 所有权整理，不是单个业务分支的局部修补。
