# procedure-timer-consumer 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-18
> 关联方案 doc：`.codestable/features/2026-06-18-procedure-timer-consumer/procedure-timer-consumer-design.md`

## 1. 接口契约核对

**接口示例逐项核对**：
- [x] `ProcedureModule` 已声明 `[ModuleDependency(typeof(TimerModule))]`，`App.Register<ProcedureModule>()` 会通过 App resolver 先启动 Timer。
- [x] `ProcedureUpdateHandle` 已作为 `ProcedureModule` 内部 `UpdateTimerHandle` 派生类型落地，构造参数为 `ProcedureModule module`。
- [x] `ProcedureModule.Startup()` 已注册 tag 为 `ProcedureModule.Update`、owner 为 ProcedureModule 的 Timer update handle。

**名词层逐项核对**：
- [x] `ProcedureModule` 保存 update handle 引用，startup 注册，shutdown 取消。
- [x] `ProcedureBase.OnUpdate(deltaTime, unscaledDeltaTime)` 仍是业务流程公开 update 契约。
- [x] `ProcedureRuntimeDriver` 已从 `ProcedureModule` 中移除。

**流程图核对**：
- [x] `App.Procedure -> TimerModule -> ProcedureModule -> ProcedureUpdateHandle -> Timer Update -> Current.OnUpdate` 均有代码落点。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] Procedure current update 已由 Timer `UpdateTimerHandle` 驱动。
- [x] `App.Procedure` 按需访问会先启动 Timer。
- [x] Procedure shutdown / unregister 会取消 update handle。
- [x] Procedure 不再创建独立 root/driver。

**明确不做逐项核对**：
- [x] 未迁移 Combat driver。
- [x] 未改变 Procedure enter / leave / pending request 异步切换语义。
- [x] 未新增 Procedure LateUpdate / FixedUpdate 配置。
- [x] 未把 Resource / Config / UI / Command / Event 职责并入 Procedure。
- [x] 未新增内建 BootstrapProcedure / LoginProcedure runtime 类型。
- [x] 未恢复 Runtime `Startup.cs` 或默认模块预加载。
- [x] 未新增第二套 update consumer 接口。

**关键决策落地**：
- [x] ProcedureModule 声明 TimerModule 依赖，适配按需 resolver。
- [x] Procedure update 使用内部 handle，不新增公开 Procedure update API。
- [x] callback 使用 `TimerUpdateContext.DeltaTime` / `UnscaledDeltaTime`，保持旧 driver delta 口径。

**挂载点反向核对**：
- [x] `ProcedureModule` 类型声明：新增 Timer dependency。
- [x] `ProcedureModule.Startup()`：注册 `ProcedureUpdateHandle`。
- [x] `ProcedureModule.Shutdown()`：取消 update handle。
- [x] `ProcedureRuntimeDriver` / `GameDeveloperKit.ProcedureRoot`：runtime 挂入点已移除。

## 3. 验收场景核对

- [x] N1：`App.Register<ProcedureModule>()` 后 Timer 已注册；证据：`Register_WhenProcedureModuleIsRegistered_ReturnsProcedure`。
- [x] N2：Procedure startup 后 Timer snapshot 存在 `ProcedureModule.Update` handle；证据：`Startup_RegistersTimerUpdateHandle`。
- [x] N3：Timer Update 推进后当前 procedure 收到相同 delta；证据：`Update_WhenCurrentChanges_OnlyUpdatesCurrentProcedure`。
- [x] N4：切到 B 后 A 不再更新，B 更新；证据：`Update_WhenCurrentChanges_OnlyUpdatesCurrentProcedure`。
- [x] N5：切换正在等待初始化时 Timer Update 不继续调用旧 procedure；证据：`Update_WhenChangeIsInitializingProcedure_SkipsCurrentUpdate`。
- [x] N6：Procedure unregister 后 Timer snapshot 不保留 Procedure handle；证据：`Shutdown_UnregistersTimerUpdateHandle`。
- [x] N7：Procedure startup 后不存在 `GameDeveloperKit.ProcedureRoot`；证据：`Register_WhenProcedureModuleIsRegistered_ReturnsProcedure` 和 `Startup_WhenCompleted_CurrentIsEmpty`。
- [x] E1：procedure `OnUpdate` 抛异常由 Timer handle 记录；证据：`TimerUpdate_WhenProcedureUpdateThrows_StoresExceptionOnHandle`。

## 4. 术语一致性

- `ProcedureUpdateHandle`：仅作为 Procedure 内部 Timer adapter。
- `ProcedureRuntimeDriver`：runtime 类型已移除，仅测试反射字符串用于确认不存在。
- `ModuleDependency(typeof(TimerModule))`：用于 App resolver 按需启动 Procedure 的依赖。
- 防冲突：未新增 `ITimerUpdateConsumer`、Procedure Late/Fixed 配置、Combat 迁移类型或 runtime BootstrapProcedure。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` Procedure 小节已记录 ProcedureModule 声明 TimerModule 依赖。
- [x] `.codestable/architecture/ARCHITECTURE.md` Procedure 小节已记录 `ProcedureUpdateHandle` 通过 Timer Update 驱动 current procedure。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已记录 Procedure 不再创建 `ProcedureRuntimeDriver` / `ProcedureRoot`。
- [x] 已知约束已同步 Procedure Timer Update 驱动边界。

## 6. requirement 回写

- [x] design frontmatter 指向 `procedure-module`。本次是 roadmap 内部调度接线推进，未改变 requirement 用户故事和边界；无需更新 requirement 正文。

## 7. roadmap 回写

- [x] `.codestable/roadmap/runtime-scheduling-diagnostics/runtime-scheduling-diagnostics-items.yaml` 已将 `procedure-timer-consumer` 标记为 `done`。
- [x] roadmap 主文档第 5 节同步为 done。
- [x] roadmap 背景、Procedure adapter 契约和变更日志已同步当前现状。
- [x] YAML 校验通过。

## 8. attention.md 候选盘点

- [x] 无新增候选。已有 Runtime / Tests 快速编译命令仍适用。

## 9. 遗留

- 后续 roadmap item：`combat-timer-consumer` 仍需把 Combat runtime driver 迁移到 Timer update handle。
- 后续 roadmap item：`runtime-module-profile-handles` 依赖 Procedure / Combat adapter 完成后再统一补 runtime module profiles。
- 结构观察：`ProcedureModuleTests.cs` 已超过 700 行，后续继续增加 diagnostics/profile 测试时建议走 `cs-refactor` 按行为域拆测试文件。
- 验证：`dotnet build GameDeveloperKit.Runtime.csproj --no-restore` 通过；`dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore` 通过。Runtime build 保留 massive 插件既有 nullable warning。
