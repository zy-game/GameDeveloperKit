# procedure-bootstrap-flow 验收报告

> 阶段：阶段 3（验收闭环）  
> 验收日期：2026-06-17  
> 关联方案 doc：`.codestable/features/2026-06-17-procedure-bootstrap-flow/procedure-bootstrap-flow-design.md`

## 1. 接口契约核对

**接口示例逐项核对**

- [x] `ProcedureModule.RequestChange<TProcedure>(object userData = null)`：已公开，委托到 `RequestChange(Type, object)`。
- [x] `ProcedureModule.RequestChange(Type, object)`：已公开，校验 procedure type，且只允许在 `IsChanging == true` 时调用。
- [x] `ProcedureModule.ClearPendingChange()`：已公开，用于清空 pending request。
- [x] `ProcedureModule.HasPendingChange` / `PendingChangeType`：已公开，用于观察 pending request。
- [x] `ProcedureChangeRequest`：已作为 `ProcedureModule` 内部 readonly struct 落地，保存目标 type 与 userData。

**名词层变化核对**

- [x] 后续流程请求：由 `m_PendingChange` 表达，`RequestChange` 写入，`DrainPendingChangeAsync` 消费。
- [x] Resource ready：测试 bootstrap procedure 在 `OnEnterAsync` 中显式 `await App.Resource.InitializeAsync(options)`。
- [x] Config / Tag 准备：测试 bootstrap procedure 只访问 `App.Config.Tags`，没有新增 `LoadTagCatalogAsync()`。

**流程图核对**

- [x] `ChangeAsync<BootstrapProcedure>()` 进入 bootstrap。
- [x] bootstrap `OnEnterAsync` 完成 Resource 初始化并访问 Config。
- [x] bootstrap 调用 `RequestChange<NextProcedure>()`。
- [x] bootstrap enter 成功结束后，`DrainPendingChangeAsync()` 串行进入 next procedure。
- [x] Resource 初始化失败时异常向调用方传播，不进入 next procedure。

## 2. 行为与决策核对

**需求摘要逐项验证**

- [x] 启动 Procedure 可在 `OnEnterAsync` 中完成 `await App.Resource.InitializeAsync()`：`ResourceBootstrapProcedure` 测试覆盖。
- [x] `RequestChange<NextProcedure>()` 不触发 `ChangeAsync` 重入异常：`ChangeAsync_WhenProcedureRequestsChange_DrainsAfterEnter` 覆盖。
- [x] 当前 bootstrap enter 完成后串行进入 next procedure：同上和 `BootstrapProcedure_WhenResourceInitializes_EntersNextWithReadyModules` 覆盖。
- [x] next procedure 进入时 Resource 已初始化并可访问 Config.Tags：`ResourceReadyProcedure` 捕获状态。
- [x] 直接在 `OnEnterAsync` 中调用 `ChangeAsync<T>()` 仍抛：既有 `ChangeAsync_WhenProcedureReenters_ThrowsAndDoesNotStartSecondChange` 保持。
- [x] 非切换期间调用 `RequestChange()` 抛明确错误：`RequestChange_WhenCalledOutsideChange_ThrowsAndDoesNotLeavePending` 覆盖。

**明确不做逐项核对**

- [x] 未新增内建固定 `BootstrapProcedure` / `LoginProcedure` runtime 类型；grep 仅文档示例命中。
- [x] 未删除或修改 `Startup.cs`。
- [x] 未让 `App.Startup()` 自动进入任何 Procedure。
- [x] 未新增 `GetModuleAsync<T>()`，未在 `App.X` 属性里隐式等待 async ready。
- [x] 未新增 `ConfigModule.LoadTagCatalogAsync()`。
- [x] 未做 Procedure Timer driver 收敛。

**关键决策落地**

- [x] 新增 `RequestChange<TProcedure>()`，`ChangeAsync()` 的 `IsChanging` 重入保护仍保留。
- [x] pending request 只保留最后一次：`ChangeAsync_WhenProcedureRequestsMultipleChanges_LastRequestWins` 覆盖。
- [x] bootstrap 示例落在测试中，没有引入业务示例目录或场景资产。
- [x] Config / Tag 只验证同步可访问，不新增异步聚合 API。

**挂载点核对**

- [x] `ProcedureModule.RequestChange<TProcedure>()` / `RequestChange(Type, object)`：公开挂载点已落地。
- [x] `ProcedureModule` pending change 状态：`HasPendingChange` / `PendingChangeType` 已落地。
- [x] `ProcedureModule.ChangeAsync()` drain pending request：`ChangeAsync()` 后调用 `DrainPendingChangeAsync()`。
- [x] `ProcedureModuleTests` bootstrap 场景：已新增 Resource / Config / pending request 相关测试。
- [x] 反向 grep：新增 `RequestChange` / `PendingChange` 引用集中在 Procedure runtime、Procedure tests 和本 feature 文档。
- [x] 拔除推演：移除 RequestChange API、pending request 字段、drain 调用、测试和架构记录后，bootstrap 自请求后续流程能力会完整消失，现有直接 `ChangeAsync` 流程仍可保留。

## 3. 验收场景核对

- [x] **N1**：`OnEnterAsync` 中 `RequestChange` 后 pending 状态可观察。证据：`ChangeAsync_WhenProcedureRequestsChange_DrainsAfterEnter`。
- [x] **N2**：BootstrapProcedure 请求 Next 后最终 `Current` 为 Next。证据：`ChangeAsync_WhenProcedureRequestsChange_DrainsAfterEnter`。
- [x] **N3**：NextProcedure 进入时 Resource 已 initialized。证据：`BootstrapProcedure_WhenResourceInitializes_EntersNextWithReadyModules`。
- [x] **N4**：BootstrapProcedure 可访问 `App.Config.Tags` 且无需 `LoadTagCatalogAsync()`。证据：`BootstrapProcedure_WhenResourceInitializes_EntersNextWithReadyModules` + grep。
- [x] **N5**：同一轮多次 `RequestChange()` 最后一次生效。证据：`ChangeAsync_WhenProcedureRequestsMultipleChanges_LastRequestWins`。
- [x] **N6**：`OnEnterAsync` 中直接 `ChangeAsync` 仍抛。证据：`ChangeAsync_WhenProcedureReenters_ThrowsAndDoesNotStartSecondChange`。
- [x] **N7**：Bootstrap resource 初始化失败不进入 NextProcedure。证据：`BootstrapProcedure_WhenResourceInitializeFails_DoesNotEnterNext`。
- [x] **N8**：非切换期间调用 `RequestChange` 抛错且不留下 pending。证据：`RequestChange_WhenCalledOutsideChange_ThrowsAndDoesNotLeavePending`。
- [x] **B1 / B2**：grep 确认 runtime 未新增 `LoadTagCatalogAsync`，未改 `Startup.cs`。
- [x] **B3**：Runtime 与 Runtime.Tests 编译通过。

反向核对：

- [x] 不新增内建固定 `BootstrapProcedure` / `LoginProcedure` runtime 类型。
- [x] 不让 `App.Startup()` 自动切换 Procedure。
- [x] 不在 `App.X` 属性里隐式等待 Resource 初始化。
- [x] 不放宽 `ChangeAsync()` 的重入保护。

## 4. 术语一致性

- `RequestChange`、`PendingChange`、`Resource ready`、`Config / Tag 准备` 与 design 第 0 节一致。
- runtime 代码没有引入新的 bootstrap / startup 名词类型；`BootstrapProcedure` / `LoginProcedure` 仅在文档示例中出现。
- 禁用词核对：runtime 无 `LoadTagCatalogAsync`。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md`：Procedure 小节已记录 pending change request、`RequestChange<TProcedure>()` 和串行 drain 行为。
- [x] `.codestable/architecture/ARCHITECTURE.md`：已知约束已记录启动 Procedure 显式完成 Resource / Config / Data ready，直接重入 `ChangeAsync()` 仍禁止。
- [x] `.codestable/architecture/ARCHITECTURE.md`：变更日志已追加 2026-06-17 Procedure bootstrap flow 现状。

## 6. requirement 回写

- [x] design frontmatter 指向 `procedure-module`；本次增强的是已有 Procedure 能力边界，不改变用户故事主体。`procedure-module` 仍为 draft，暂不升级为 current；验收记录本次已由 roadmap / architecture 落档。

## 7. roadmap 回写

- [x] `.codestable/roadmap/module-dependency-loading/module-dependency-loading-items.yaml`：`procedure-bootstrap-flow` 已从 `in-progress` 改为 `done`。
- [x] `.codestable/roadmap/module-dependency-loading/module-dependency-loading-roadmap.md`：子 feature 清单中 `procedure-bootstrap-flow` 状态已同步为 `done`。
- [x] YAML 校验通过。

## 8. attention.md 候选盘点

- [x] 无新增候选。既有 Runtime 快速编译命令已在 `.codestable/attention.md`。

## 9. 遗留

- `remove-default-preload-startup` 仍未启动；现在其前置 `module-dependency-annotations` 与 `procedure-bootstrap-flow` 均为 done，可作为下一条 roadmap 推进。
- `runtime-scheduling-diagnostics` roadmap 仍包含旧边界“不删除 Startup bootstrap”的观察项，需要后续 roadmap update 或对应 feature 验收时同步。
