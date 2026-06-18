# runtime-module-profile-handles 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-18
> 关联方案 doc：`.codestable/features/2026-06-18-runtime-module-profile-handles/runtime-module-profile-handles-design.md`

## 1. 接口契约核对

**接口示例逐项核对**：
- [x] `ProfileHandle` 仍保持 Name-only + 派生类自绘契约，未新增 table/snapshot profile API。
- [x] `TimerModule` 已提供内部 `RegisterDebugProfile(DebugModule)` / `UnregisterDebugProfile(DebugModule)`，并持有 `TimerProfileHandle`。
- [x] `ProcedureModule` / `CombatModule` 已提供同样的内部 Debug profile 注册/注销方法。

**名词层逐项核对**：
- [x] `TimerProfileHandle`：Name 为 `Timer`，通过 `TimerSnapshot` 自绘 clock、delta、handle 数量和 update handle 摘要。
- [x] `ProcedureProfileHandle`：Name 为 `Procedure`，自绘 current/changing/pending/update handle 状态。
- [x] `CombatProfileHandle`：Name 为 `Combat`，自绘 default world tick/time/frame rate/fixed delta/update handle 状态。

**流程图核对**：
- [x] 模块 startup -> `App.TryGetRegistered<DebugModule>` -> `RegisterDebugProfile` -> `DebugProfileRegistry` 已落地。
- [x] `DebugModule.Startup()` -> built-ins -> `RegisterRuntimeModuleProfiles()` -> module profile registry 已落地。
- [x] 模块 shutdown -> `UnregisterDebugProfile` 已落地。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] Debug 默认内建 profile 仍只有 Memory 和 Device Info；Debug 状态 profile 未恢复。
- [x] Debug 先启动或后启动时，Timer profile 都会出现。
- [x] Debug 先启动时，Procedure startup 会注册 Procedure profile。
- [x] Combat 先启动时，Debug 后启动会回扫 Combat profile。
- [x] 模块 unregister 会移除对应 profile。

**明确不做逐项核对**：
- [x] 未新增 Debug sink / analytics / transport API。
- [x] 未把 DebugModule 声明为 Timer / Procedure / Combat 的 ModuleDependency。
- [x] 未删除 Debug Console Timers tab。
- [x] 未新增 Network debug log bridge 或 payload。
- [x] 未改变 Timer / Procedure / Combat 调度语义。

**关键决策落地**：
- [x] 模块 profile 由模块自身持有，Debug 只做 registry lifecycle 与启动回扫。
- [x] Debug 接入是可选能力；无 Debug 时 runtime modules 仍照常运行。
- [x] Profile 类保持模块内部实现，未扩散公开状态 DTO。

**挂载点反向核对**：
- [x] `DebugModule.Startup()`：注册 built-ins 后回扫 runtime module profiles。
- [x] `TimerModule.Startup()` / `Shutdown()`：软注册 / 注销 Timer profile。
- [x] `ProcedureModule.Startup()` / `Shutdown()`：软注册 / 注销 Procedure profile。
- [x] `CombatModule.Startup()` / `Shutdown()`：软注册 / 注销 Combat profile。

## 3. 验收场景核对

- [x] N1：只启动 DebugModule -> Profiles 存在 Memory、Device Info、Timer，不存在 Debug；证据：`Startup_RegistersBuiltInProfileHandles`。
- [x] N2：先启动 TimerModule，再启动 DebugModule -> Profiles 出现 Timer profile；证据：`Startup_WhenDebugStartsAfterTimer_RegistersTimerProfileHandle`。
- [x] N3：先启动 DebugModule，再启动 ProcedureModule -> Profiles 出现 Procedure profile；证据：`Startup_WhenDebugExistsAndProcedureStarts_RegistersProcedureProfileHandle`。
- [x] N4：先启动 CombatModule，再启动 DebugModule -> Profiles 出现 Combat profile；证据：`Startup_WhenDebugStartsAfterCombat_RegistersCombatProfileHandle`。
- [x] N5：注销 TimerModule -> Timer profile 被移除；证据：`Shutdown_WhenTimerModuleUnregistered_RemovesTimerProfileHandle`。Procedure 注销清理由 `Startup_WhenDebugExistsAndProcedureStarts_RegistersProcedureProfileHandle` 覆盖。
- [x] N6/N7/N8：Timer / Procedure / Combat profile draw 均只读取已有模块状态；证据：Runtime/Test 编译通过，draw 方法无外部依赖或新增 DTO。

## 4. 术语一致性

- `TimerProfileHandle` / `ProcedureProfileHandle` / `CombatProfileHandle` 均只作为模块内部嵌套类型出现。
- `optional debug profile registration` 落为 `TryRegisterDebugProfile()` / `TryUnregisterDebugProfile()` 和 `DebugModule.RegisterRuntimeModuleProfiles()`。
- 防冲突：未新增 `DebugProfile` 默认状态表、旧 sink/analytics/transport 类型或第二套 profile table 类型。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` Procedure 小节已记录 `ProcedureProfileHandle` 与 Debug 软接入。
- [x] `.codestable/architecture/ARCHITECTURE.md` Combat 小节已记录 `CombatProfileHandle` 与 Debug 软接入。
- [x] `.codestable/architecture/ARCHITECTURE.md` Timer 小节已记录 `TimerProfileHandle` 与 Debug 软接入。
- [x] `.codestable/architecture/ARCHITECTURE.md` Debug 小节已记录 startup 回扫 runtime module profiles、默认 profile 边界与 Timers tab 保留。
- [x] 已知约束已补充 Runtime module profile 软接入边界。

## 6. requirement 回写

- [x] design frontmatter 指向 `runtime-diagnostics`。本 feature 实现了其中的模块 ProfileHandle 诊断子能力，已把 `implemented_by` 补入 `runtime-diagnostics.md`。
- [x] `runtime-diagnostics` 仍保持 `draft`，因为实时网络日志 bridge 等能力尚未完成，不能把整份愿景提前标为 current。

## 7. roadmap 回写

- [x] `.codestable/roadmap/runtime-scheduling-diagnostics/runtime-scheduling-diagnostics-items.yaml` 已将 `runtime-module-profile-handles` 标记为 `done`。
- [x] roadmap 主文档第 5 节同步为 done。
- [x] roadmap 背景、Module Profile Handles、观察项和变更日志已同步当前现状。
- [x] YAML 校验通过。

## 8. attention.md 候选盘点

- [x] 无新增候选。已有 Runtime / Tests 快速编译命令仍适用。

## 9. 遗留

- 后续 roadmap item：`network-debug-log-transport-contract` 仍待定义 Network 侧 debug log bridge。
- 结构观察：`DebugGuiDriver` 的 Timers tab 与新的 `Timer` profile 存在部分信息重复；是否合并属于 Debug Console 信息架构调整，建议后续另起 feature/refactor。
- 验证：`dotnet build GameDeveloperKit.Runtime.csproj --no-restore` 通过。
- 验证：`dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore` 通过。
- 验证：`python .codestable/tools/validate-yaml.py --file .codestable/features/2026-06-18-runtime-module-profile-handles/runtime-module-profile-handles-checklist.yaml --yaml-only` 通过。
- 验证：`python .codestable/tools/validate-yaml.py --file .codestable/roadmap/runtime-scheduling-diagnostics/runtime-scheduling-diagnostics-items.yaml --yaml-only` 通过。
