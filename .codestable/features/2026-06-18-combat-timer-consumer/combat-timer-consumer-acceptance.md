# combat-timer-consumer 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-18
> 关联方案 doc：`.codestable/features/2026-06-18-combat-timer-consumer/combat-timer-consumer-design.md`

## 1. 接口契约核对

**接口示例逐项核对**：
- [x] `CombatModule` 已声明 `[ModuleDependency(typeof(TimerModule))]`，`App.Register<CombatModule>()` 会通过 App resolver 先启动 Timer。
- [x] `CombatModule` 保存 `FixedUpdateTimerHandle m_UpdateHandle`，startup 注册 tag 为 `CombatModule.Update`、owner 为 CombatModule 的 Timer fixed update handle。
- [x] fixed update callback 已调用默认 `World.Update(context.DeltaTime)`。

**名词层逐项核对**：
- [x] `CombatModule` 保存 update handle 引用，startup 注册，shutdown 取消。
- [x] `World.Update(float deltaTime)` 的固定步累积语义未改变。
- [x] `CombatRuntimeDriver` 已从 `CombatModule` 中移除。

**流程图核对**：
- [x] `App.Combat -> TimerModule -> CombatModule -> FixedUpdateTimerHandle -> Timer FixedUpdate -> World.Update` 均有代码落点。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] Combat default world update 已由 Timer `FixedUpdateTimerHandle` 驱动。
- [x] `App.Combat` 按需访问会先启动 Timer。
- [x] Combat shutdown / unregister 会取消 update handle。
- [x] Combat 不再创建独立 root/driver。

**明确不做逐项核对**：
- [x] 未迁移 Procedure 或 Debug driver。
- [x] 未新增 Combat Update / LateUpdate / FixedUpdate 配置开关。
- [x] 未新增多 world 全局调度、系统分组、网络锁步、回滚同步或表现同步。
- [x] 未改变 `World.Update(float)` 的固定步累积语义。
- [x] 未把 CombatModule 纳入默认启动计划。
- [x] 未新增第二套 update consumer 接口。

**关键决策落地**：
- [x] CombatModule 声明 TimerModule 依赖，适配按需 resolver。
- [x] Combat update 使用 Timer `FixedUpdateTimerHandle`，不新增公开 Combat update API。
- [x] callback 使用 `TimerUpdateContext.DeltaTime`，符合 roadmap 约定的全局 clock context。
- [x] 直接启动 CombatModule 且 Timer 未注册时明确抛 `GameException`，不会创建 fallback driver 或半初始化 world。

**挂载点反向核对**：
- [x] `CombatModule` 类型声明：新增 Timer dependency。
- [x] `CombatModule.Startup()`：注册 Timer fixed update handle。
- [x] `CombatModule.Shutdown()`：取消 update handle。
- [x] `CombatRuntimeDriver` / `GameDeveloperKit.CombatRoot`：runtime 挂入点已移除。

## 3. 验收场景核对

- [x] N1：`App.Register<CombatModule>()` 后 Timer 已注册；证据：`Register_WhenCombatModuleIsRegistered_ReturnsWorld`。
- [x] N2：Combat startup 后 Timer snapshot 存在 `CombatModule.Update` fixed handle；证据：`Startup_RegistersTimerFixedUpdateHandle`。
- [x] N3：Timer FixedUpdate 推进后默认 world 收到 update；证据：`TimerFixedUpdate_WhenTriggered_UpdatesDefaultWorld`。
- [x] N4：Timer Update / LateUpdate 不驱动 Combat world；证据：`TimerUpdateAndLateUpdate_WhenTriggered_DoNotUpdateDefaultWorld`。
- [x] N5：Combat unregister 后 Timer snapshot 不保留 Combat handle；证据：`Shutdown_UnregistersTimerUpdateHandle`。
- [x] N6：Combat startup 后不存在 `GameDeveloperKit.CombatRoot`；证据：`Register_WhenCombatModuleIsRegistered_ReturnsWorld` 和 `Shutdown_WhenCalledRepeatedly_IsSafe`。
- [x] E1：直接 startup 且 Timer 未注册会抛 `GameException` 且不创建 world；证据：`Startup_WhenTimerIsMissing_ThrowsWithoutCreatingWorld`。
- [x] E2：default world update 抛异常由 Timer handle 记录；证据：`TimerFixedUpdate_WhenWorldUpdateThrows_StoresExceptionOnHandle`。

## 4. 术语一致性

- `CombatUpdateHandle`：实现为 `FixedUpdateTimerHandle m_UpdateHandle` 字段，没有新增公开类型。
- `CombatRuntimeDriver`：runtime 类型已移除，仅测试反射字符串用于确认不存在。
- `ModuleDependency(typeof(TimerModule))`：用于 App resolver 按需启动 Combat 的依赖。
- 防冲突：未新增 `ITimerUpdateConsumer`、Combat phase 配置、多 world 调度或 network sync 类型。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` Combat 小节已记录 CombatModule 声明 TimerModule 依赖。
- [x] `.codestable/architecture/ARCHITECTURE.md` Combat 小节已记录 Timer `FixedUpdateTimerHandle` 通过 FixedUpdate phase 驱动默认 world。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已记录 Combat 不再创建 `CombatRuntimeDriver` / `CombatRoot`。
- [x] 已知约束已同步 Combat Timer FixedUpdate 驱动边界。

## 6. requirement 回写

- [x] design frontmatter 指向 `combat-module`。本次是 roadmap 内部调度接线推进，未改变 requirement 用户故事和边界；无需更新 requirement 正文。

## 7. roadmap 回写

- [x] `.codestable/roadmap/runtime-scheduling-diagnostics/runtime-scheduling-diagnostics-items.yaml` 已将 `combat-timer-consumer` 标记为 `done`。
- [x] roadmap 主文档第 5 节同步为 done。
- [x] roadmap 背景、Module Update Adapters、Combat adapter 备注和变更日志已同步当前现状。
- [x] YAML 校验通过。

## 8. attention.md 候选盘点

- [x] 无新增候选。并行执行两个 `dotnet build` 可能争用 `Temp/obj` 输出锁，这次已通过顺序重跑解决，不属于每个 feature 必踩的硬约束。

## 9. 遗留

- 后续 roadmap item：`runtime-module-profile-handles` 依赖 Procedure / Combat adapter 完成后再统一补 runtime module profiles。
- 后续 roadmap item：`network-debug-log-transport-contract` 仍待定义 Network 侧 debug log bridge。
- 验证：`python .codestable/tools/validate-yaml.py --file .codestable/roadmap/runtime-scheduling-diagnostics/runtime-scheduling-diagnostics-items.yaml --yaml-only` 通过。
- 验证：`python .codestable/tools/validate-yaml.py --file .codestable/features/2026-06-18-combat-timer-consumer/combat-timer-consumer-checklist.yaml --yaml-only` 通过。
- 验证：`dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore` 通过。
- 验证：首次并行 `dotnet build GameDeveloperKit.Runtime.csproj --no-restore` 遇到输出 DLL 文件锁，顺序重跑后通过。
