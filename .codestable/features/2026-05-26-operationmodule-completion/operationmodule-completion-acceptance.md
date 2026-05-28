# operationmodule-completion 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-05-26
> 关联方案 doc：`.codestable/features/2026-05-26-operationmodule-completion/operationmodule-completion-design.md`

## 1. 接口契约核对

**接口示例逐项核对**：
- [x] `Super.Operation.Execute<MyOperationHandle>(key, args)` + `SetResult(key, result)`：`OperationModule.SetResult()` 查找运行中 operation，调用 `OperationHandle.SetResultObject()` 写入泛型 `Value`；`OperationModuleTests.SetResult_WhenOperationIsRunning_CompletesWithValueAndRemovesOperation` 覆盖。
- [x] `Super.Operation.WaitCompletionAsync<MyOperationHandle>(key, args)`：`OperationModule.WaitCompletionAsync<T>()` 执行、等待并返回同一个 operation；失败等待异常被吞掉，调用方通过 `Status` / `Error` 观察结果。

**名词层"现状 → 变化"逐项核对**：
- [x] `OperationModule` 增加 running operation 索引：`m_Operations` 使用 `OperationKey(key, operation.GetType())`。
- [x] 公开入口契约收紧：`RegisterOperation()` 对 null key / null operation / 重复 running operation 抛明确异常。
- [x] `OperationStatus` 可观察：`OperationHandle.SetRunning()` 将执行中状态置为 `Running`，终态由 `SetResult` / `SetException` / `SetCanceled` 写入。
- [x] `OperationModule.Set*` 有效：三组外部回写 API 已落地。

**流程图核对**：
- [x] Validate：`RegisterOperation()`。
- [x] Register：`m_Operations.Add(operationKey, operation)`。
- [x] Execute：`operation.Execute(args ?? Array.Empty<object>())`。
- [x] Complete：`OperationHandle.SetResult/SetException/SetCanceled`。
- [x] Remove：`RemoveOperation()` 与 `CleanupWhenCompletedAsync()`。
- [x] Return：`WaitCompletionAsync<T>()` 返回 operation。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] key / operation 校验：测试覆盖 null key、null operation。
- [x] `SetResult(key, value)`：测试覆盖 `Status == Succeeded` 且 `Value == value`。
- [x] `SetException(key, ex)`：测试覆盖 `Status == Failed` 且 `Error` 为同一异常对象。
- [x] `SetCanceled(key)`：测试覆盖 `Status == Cancelled`。
- [x] operation 自己进入终态后索引清理：同步成功 / 同步抛异常场景均不残留；失败场景由重复 key 和 missing key 测试间接覆盖。
- [x] `Shutdown()` 清理：测试覆盖未完成 operation 被取消，之后 key 不再可回写。

**明确不做逐项核对**：
- [x] 不做队列 / 调度 / 优先级 / 重试：grep `queue|priority|retry|thread` 在 Runtime OperationModule 无命中。
- [x] 不改变资源 package / provider operation 归属：本次未修改 `Assets/GameDeveloperKit/Runtime/Resource/PlayMode` 或 `Provider` 的 operation owner 文件。
- [x] 不实现 Builtin / StreamingAssets / WebGL / EditorSimulator 加载语义：本次未改对应 loading operation。
- [x] 不引入线程安全承诺：代码未加锁或线程调度语义，架构文档已写明主线程假定。
- [x] 不接入日志 / 事件 / Profiler：grep `Debug.Log|Event|Profiler` 在 Runtime OperationModule 无命中。
- [x] 不修改公开继承模型 / 不新增依赖：`OperationHandle` 仍为基类 + 泛型派生；`Packages/manifest.json` 未变。

**关键决策落地**：
- [x] D1 只管理运行中 operation：`OperationModule` 只登记、等待、回写、清理，不感知资源业务。
- [x] D2 key 不做去重复用：`Execute<T>()` 每次创建新 operation；同 key + type running 时抛异常。
- [x] D3 `(key, operation type)` 索引：`OperationKey` 同时比较 key 和 operation type。
- [x] D4 终态移除：同步终态、等待终态、外部 Set* 终态都会调用 `RemoveOperation()`。
- [x] D5 result 类型匹配：`OperationHandle<T>.SetResultObject()` 检查 null / 类型不匹配并抛 `GameException`。

**编排层变化核对**：
- [x] 执行前登记：`Execute()` 第一行调用 `RegisterOperation()`。
- [x] 执行后清理：同步完成立即移除；异步完成由 `CleanupWhenCompletedAsync()` 移除；等待路径 finally 再兜底。
- [x] 外部终态回写：`SetResult` / `SetException` / `SetCanceled` 都按 key 查找并移除。
- [x] 关闭清理：`Shutdown()` 取消未完成 operation 并清空索引。

**流程级约束核对**：
- [x] 错误语义：`WaitCompletionAsync<T>()` 保持返回 operation，失败通过 `Status` / `Error` 观察。
- [x] 幂等性：`OperationHandle` 终态后再次 Set* 直接返回，不改写既有结果。
- [x] 顺序约束：同一 key + operation type 同时运行会抛 `GameException`。
- [x] 并发约束：无线程安全承诺；架构文档已归并。
- [x] 扩展点：仍通过继承 `OperationHandle` 扩展具体 operation。
- [x] 可观测点：`Status`、`Error`、`Value` 保持为观察面。

**挂载点反向核对（可卸载性）**：
- [x] `Super.Operation`：入口未变，`Super.cs` 仍只暴露 `OperationModule`。
- [x] `OperationModule` public API：`Execute` / `WaitCompletionAsync` / `SetResult` / `SetException` / `SetCanceled` 为生命周期入口。
- [x] `OperationHandle` 状态契约：`Status` / `Error` / `WaitCompletionAsync()` 保持调用方观察面。
- [x] 反向 grep：本 feature 新增运行时代码只落在 OperationModule 目录；测试代码落在 Tests/Runtime。
- [x] 拔除沙盘推演：移除测试目录 + 恢复 `OperationModule` / `OperationHandle` 本次改动即可拔除本 feature；资源 owner 布局无残留变更。

## 3. 验收场景核对

- [x] **N1**：`Execute<MyOperation>(key, args)`，operation 同步 `SetResult(value)` → 返回 handle 状态为 `Succeeded`，泛型 `Value` 为 value。
  - 证据来源：`OperationModuleTests.Execute_WhenOperationSetsResult_CompletesWithValue`；Runtime.Tests 编译通过。
- [x] **N2**：`WaitCompletionAsync<MyOperation>(key, args)`，operation 异步完成 → await 返回同一个 operation，状态为 `Succeeded`。
  - 证据来源：`OperationModuleTests.WaitCompletionAsync_WhenOperationIsCompletedExternally_ReturnsOperation`；Runtime.Tests 编译通过。
- [x] **N3**：`SetResult(key, value)` 完成运行中 `OperationHandle<T>`。
  - 证据来源：`OperationModuleTests.SetResult_WhenOperationIsRunning_CompletesWithValueAndRemovesOperation`。
- [x] **N4**：`SetException(key, ex)` 完成运行中 operation。
  - 证据来源：`OperationModuleTests.SetException_WhenOperationIsRunning_CompletesWithError`。
- [x] **N5**：`SetCanceled(key)` 完成运行中 operation。
  - 证据来源：`OperationModuleTests.SetCanceled_WhenOperationIsRunning_CompletesWithCanceled`。
- [x] **B1**：`Execute(null, operation, args)` 抛 `ArgumentNullException`。
  - 证据来源：`OperationModuleTests.Execute_WhenKeyIsNull_Throws`。
- [x] **B2**：`Execute(key, null, args)` 抛 `ArgumentNullException`。
  - 证据来源：`OperationModuleTests.Execute_WhenOperationIsNull_Throws`。
- [x] **B3**：同一 `(key, operationType)` 重复运行有明确失败语义。
  - 证据来源：`OperationModuleTests.Execute_WhenSameKeyAndTypeIsAlreadyRunning_Throws`。
- [x] **B4**：同 key 不同 operation type 不互相覆盖；只按 key 外部 Set* 时抛歧义异常。
  - 证据来源：`OperationModuleTests.Execute_WhenSameKeyHasDifferentOperationTypes_AllowsBothUntilExternalSetIsAmbiguous`。
- [x] **E1**：`operation.Execute(args)` 同步抛异常。
  - 证据来源：`OperationModuleTests.Execute_WhenOperationThrows_SetsFailedStatus`。
- [x] **E2**：`SetResult` 使用不匹配 value 类型。
  - 证据来源：`OperationModuleTests.SetResult_WhenValueTypeDoesNotMatch_ThrowsAndKeepsOperationRunning`。
- [x] **E3**：`SetResult` / `SetException` / `SetCanceled` 找不到 key。
  - 证据来源：`OperationModuleTests.SetMethods_WhenKeyIsMissing_Throw`。
- [x] **E4**：`Shutdown()` 时仍有未完成 operation。
  - 证据来源：`OperationModuleTests.Shutdown_WhenOperationIsRunning_CancelsAndClearsOperation`。

验证命令：
- [x] `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过，0 warnings / 0 errors。
- [x] `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`：通过，0 warnings / 0 errors。
- [ ] Unity Test Runner：未完成。原因：batchmode 日志显示当前项目已有 Unity 实例打开，Unity 不允许同一项目多实例打开，因此未生成 XML 结果。

## 4. 术语一致性

- `operation`：Runtime operation 类型仍全部继承 `OperationHandle`。
- `operation key`：代码中以 `OperationKey` 表达 key + type 索引，未引入别名。
- `running operation`：实现为 `m_Operations`，只保存未完成 operation。
- `terminal status`：实现为 `OperationHandle.IsDone`。
- `result value`：实现为 `OperationHandle<T>.Value` + `SetResultObject` 类型校验。
- 防冲突：未引入 `OperationQueue`、`OperationScheduler`、`OperationRetry` 等方案外概念。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md`：`OperationModule` 描述已从“最小执行 / 等待入口”更新为执行、等待、按 key 终态回写和关闭清理入口。
- [x] `.codestable/architecture/ARCHITECTURE.md`：Resource 关键行为已写入 `(operation key, operation type)` running operation 索引、同 key 不同 type 并存、同 key + type 不允许同时运行、外部 Set* 歧义异常。
- [x] `.codestable/architecture/ARCHITECTURE.md`：已知约束新增 `OperationModule` 不提供队列、优先级、重试、调度线程或线程安全承诺。

## 6. requirement 回写

- [x] `requirement` 为空，本 feature 是运行时框架内部能力补齐，不新增用户可见能力愿景；无 requirement 回写。

## 7. roadmap 回写

- [x] design frontmatter 中 `roadmap` / `roadmap_item` 均为空；非 roadmap 起头，无 roadmap 回写。

## 8. attention.md 候选盘点

- [x] 候选 1：Unity Test Runner batchmode 在项目已有 Unity Editor 实例打开时无法运行，会报“Multiple Unity instances cannot open the same project”。建议加入 `.codestable/attention.md` 的测试或命令陷阱分节。

## 9. 遗留

- 后续优化点：`OperationHandle.SetProgress(float)` 当前只写 `_progress`，不触发 `_progressHandle`，如需进度可观察性另起 feature / issue。
- 后续优化点：`ReferencePool` 尚未接入 operation 创建 / 回收，是否池化需另起性能评估。
- 已知限制：Unity Test Runner 未实际执行本次测试，原因是项目已有 Unity 实例打开；测试程序集 dotnet 编译已通过。
