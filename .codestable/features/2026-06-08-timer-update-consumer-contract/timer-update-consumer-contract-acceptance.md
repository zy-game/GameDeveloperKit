# timer-update-consumer-contract 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-08
> 关联方案 doc：`.codestable/features/2026-06-08-timer-update-consumer-contract/timer-update-consumer-contract-design.md`

## 1. 接口契约核对

**接口示例逐项核对**：
- [x] `TimerModule.Register(TimerHandle)` / `Register<T>()`：已作为显式 handle 注册入口落地。
- [x] `TimerModule.OnUpdate` / `OnLateUpdate` / `OnFixedUpdate`：已返回对应 `UpdateTimerHandle`、`LateUpdateTimerHandle`、`FixedUpdateTimerHandle`。
- [x] `TimerSnapshot.Updates`：已暴露 active `TimerUpdateHandle` 列表。

**名词层逐项核对**：
- [x] `TimerHandle` 基类已瘦身，只保留生命周期、owner/tag、pause/cancel/complete 和 `Advance()` 模板。
- [x] `TimerDelayHandle` / `TimerCountdownHandle` / `TimerIntervalHandle` 各自保存 delay/duration/interval、elapsed、remaining、progress 和 next fire time。
- [x] `TimerUpdateHandle` 保存 enabled、last tick、last exception 和 error 状态。
- [x] 三个显式 update handles 已落地，并通过内部 tick kind 分流。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] 三类 Unity tick 均接入 Timer driver。
- [x] 业务可直接注册 `new LateUpdateTimerHandle(...)`，也可用 `OnLateUpdate(...)` 便捷 API。
- [x] update handle 异常隔离在 `TimerUpdateHandle.Advance()` 内完成，不阻断后续 handle。

**明确不做逐项核对**：
- [x] 未保留 `ITimerUpdateConsumer`、`TimerUpdatePhase`、`TimerUpdateSubscription`。
- [x] 未迁移 Debug/Procedure/Combat 到 Timer。
- [x] 未新增 Network、Debug transport 或 Debug profile 实现。
- [x] 未改写 Unity `Time.fixedDeltaTime`。

## 3. 验收场景核对

- [x] N1/N2/N3：Update、LateUpdate、FixedUpdate handles 只在匹配 tick 被调用；证据：`TimerModuleTests.UpdateHandles_WhenTickKindMatches_InvokeOnlyMatchingHandle`、`FixedUpdateHandle_WhenRegistered_AdvancesOnlyOnFixedUpdate`。
- [x] N4：同 tick update handles 按注册顺序调用；证据：`UpdateHandles_WhenRegistered_InvokeInRegistrationOrder`。
- [x] N5：异常 handle 不阻断后续 handle，并记录异常；证据：`UpdateHandle_WhenThrows_DoesNotBlockOthersAndSnapshotStoresException`。
- [x] N6/N7：重复注册只调用一次，Unregister 后不再调用；证据：`Register_WhenSameUpdateHandleRegisteredTwice_InvokesOnce`、`Unregister_WhenUpdateHandleRegistered_RemovesHandle`。
- [x] B1/B2：旧 Timer API 默认 Update，回调中 cancel 保持遍历稳定；证据：既有 delay/countdown/interval/cancel tests。
- [x] E1/E2：`Register(null)` 抛异常，shutdown 清理 handles；证据：`TimerArguments_WhenInvalid_Throw`、`Shutdown_WhenUpdateHandlesRegistered_ClearsHandles`。

## 4. 术语一致性

- `UpdateTimerHandle`、`LateUpdateTimerHandle`、`FixedUpdateTimerHandle`：设计、代码、测试一致。
- `TimerUpdateHandle`：作为 handle 基类，不再称为 consumer。
- `TimerTickKind`：保留为 internal 调度口径，未作为公开业务选择 API。
- 防冲突：Runtime Timer 代码未出现 `ITimerUpdateConsumer`、`TimerUpdatePhase`、`TimerUpdateSubscription`。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` 已更新 Timer 现状：三类 Unity tick、显式 update handles、`TimerHandle` 基类瘦身、`TimerSnapshot.Updates`。
- [x] 架构硬边界已更新：Timer 不再记录旧 Timer FPS 自有 fixed tick 口径，改为 Unity `Update` / `LateUpdate` / `FixedUpdate` 三类入口。

## 6. requirement 回写

- [x] design frontmatter 指向 `timer-module`。本次主要是 Runtime 内部调度契约收敛；当前未直接改 requirements 文档，Timer 能力现状已先归并到 architecture。

## 7. roadmap 回写

- [x] `.codestable/roadmap/runtime-scheduling-diagnostics/runtime-scheduling-diagnostics-items.yaml` 已将 `timer-update-consumer-contract` 标记为 `done`。
- [x] roadmap 主文档第 4/5 节已同步为显式 update handle 契约。
- [x] YAML 校验通过。

## 8. attention.md 候选盘点

- [x] 无新增候选。已有 Runtime 快速编译命令仍适用。

## 9. 遗留

- 后续 roadmap item：Debug/Procedure/Combat 仍需迁移到 Timer update handle。
- 已知限制：Delay/Countdown/Interval 当前默认走 Update；若未来需要它们选择 LateUpdate/FixedUpdate，应另起 feature 评估 API。
- 验证：`dotnet build GameDeveloperKit.Runtime.csproj --no-restore /p:UseSharedCompilation=false` 通过；`dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore /p:UseSharedCompilation=false` 通过。
