# debug-timer-refresh 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-18
> 关联方案 doc：`.codestable/features/2026-06-18-debug-timer-refresh/debug-timer-refresh-design.md`

## 1. 接口契约核对

**接口示例逐项核对**：
- [x] `DebugModule` 已声明 `[ModuleDependency(typeof(TimerModule))]`，`App.Register<DebugModule>()` 会通过 App resolver 先启动 Timer。
- [x] `DebugRefreshHandle` 已作为 `DebugModule` 内部 `UpdateTimerHandle` 派生类型落地，构造参数为 `DebugModule module`。
- [x] `UpdateTimerHandle` 已从 sealed 放开为可派生类型，满足 Debug 内部 adapter 契约。

**名词层逐项核对**：
- [x] `DebugModule` 保存 refresh handle 引用，startup 注册，shutdown 取消。
- [x] `MemoryProfileHandle.Sample()` 仍是 metrics 计算节点，未改采样算法。
- [x] `DebugGuiDriver` 不再实现 Unity `Update()`，只保留 `OnGUI()` 绘制桥接。

**流程图核对**：
- [x] `App.Debug -> TimerModule -> DebugModule -> DebugRefreshHandle -> Timer Update -> MemoryProfileHandle.Sample` 均有代码落点。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] Debug metrics sampling 已由 Timer `UpdateTimerHandle` 驱动。
- [x] `DebugGuiDriver` 不再通过 Unity `Update()` 采样 metrics。
- [x] Debug shutdown / unregister 会取消 refresh handle。

**明确不做逐项核对**：
- [x] 未把 IMGUI `OnGUI()` 改成 Timer 调用。
- [x] 未迁移 Procedure / Combat driver。
- [x] 未新增 NetworkModule、transport、sender、retry 或 batch upload。
- [x] 未重命名 `GameDeveloperKit.Logger` namespace。
- [x] 未新增第二套 profile 接口。

**关键决策落地**：
- [x] DebugModule 声明 TimerModule 依赖，适配按需 resolver。
- [x] Debug refresh 使用内部 handle，不新增公开 Debug refresh API。
- [x] refresh callback 使用 `TimerUpdateContext.UnscaledDeltaTime`，保持旧 `Time.unscaledDeltaTime` 采样口径。

**挂载点反向核对**：
- [x] `DebugModule` 类型声明：新增 Timer dependency。
- [x] `DebugModule.Startup()`：注册 `DebugRefreshHandle`。
- [x] `DebugModule.Shutdown()`：取消 refresh handle。
- [x] `DebugGuiDriver.Update()`：已移除，`OnGUI()` 保留。

## 3. 验收场景核对

- [x] N1：`App.Register<DebugModule>()` 后 Timer 已注册；证据：`Register_WhenDebugModuleIsRegistered_StartsTimerDependency`。
- [x] N2：Debug startup 后 Timer snapshot 存在 `DebugModule.Refresh` handle；证据：`Startup_RegistersTimerRefreshHandle`。
- [x] N3：Timer Update 推进后 metrics 更新；证据：`TimerUpdate_WhenDebugRefreshHandleRegistered_StoresMemoryProfileSample`。
- [x] N4：Debug disabled 后 Timer refresh 不继续采样；证据：`TimerUpdate_WhenDebugDisabled_DoesNotSampleMemoryProfile`。
- [x] N5：Debug unregister 后 Timer snapshot 不保留 refresh handle；证据：`Shutdown_UnregistersTimerRefreshHandle`。
- [x] B1：直接调用 `UpdateMetrics(0.2f)` 仍能采样；证据：既有 `UpdateMetrics_WhenSampleIntervalReached_StoresMemoryProfileSample`。
- [x] E1：refresh callback 异常隔离由 Timer update handle 既有测试覆盖；本 feature 未新增额外异常传播路径。

## 4. 术语一致性

- `DebugRefreshHandle`：仅作为 Debug 内部 Timer adapter。
- `DebugGuiDriver`：只保留 IMGUI `OnGUI` 绘制桥接。
- `ModuleDependency(typeof(TimerModule))`：用于 App resolver 按需启动 Debug 的依赖。
- 防冲突：未新增 `ITimerUpdateConsumer`、Network transport、Procedure / Combat 迁移类型。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` Debug 小节已记录 DebugModule 声明 TimerModule 依赖。
- [x] `.codestable/architecture/ARCHITECTURE.md` Debug 小节已记录 `DebugRefreshHandle` 通过 Timer Update 驱动 metrics 采样。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已记录 `DebugGuiDriver` 只保留 `OnGUI` 绘制桥接。
- [x] 已知约束已同步 Debug 运行时边界。

## 6. requirement 回写

- [x] design frontmatter 指向 `runtime-diagnostics`。本次是 roadmap 内部接线推进，未改变 requirement 用户故事和边界；无需更新 requirement 正文。

## 7. roadmap 回写

- [x] `.codestable/roadmap/runtime-scheduling-diagnostics/runtime-scheduling-diagnostics-items.yaml` 已将 `debug-timer-refresh` 标记为 `done`。
- [x] roadmap 主文档第 5 节同步为 done。
- [x] roadmap 背景、Debug refresh 契约和变更日志已同步当前现状。
- [x] YAML 校验通过。

## 8. attention.md 候选盘点

- [x] 无新增候选。已有 Runtime / Tests 快速编译命令仍适用。

## 9. 遗留

- 后续 roadmap item：`procedure-timer-consumer` 仍需把 Procedure runtime driver 迁移到 Timer update handle。
- 后续 roadmap item：`combat-timer-consumer` 仍需把 Combat runtime driver 迁移到 Timer update handle。
- 结构观察：`DebugModule.cs` 和 `DebugGuiDriver.cs` 均接近或超过 500 行，后续继续扩 Debug 子域时建议走 `cs-refactor` 拆 lifecycle / GUI drawing 协作者。
- 验证：`dotnet build GameDeveloperKit.Runtime.csproj --no-restore` 通过；`dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore` 通过。两者均保留 massive 插件既有 nullable warning。
