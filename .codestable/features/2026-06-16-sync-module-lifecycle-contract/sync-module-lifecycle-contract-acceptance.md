---
doc_type: feature-acceptance
feature: 2026-06-16-sync-module-lifecycle-contract
status: accepted
summary: 模块生命周期同步化实现已通过验收，架构与 roadmap 已回写。
tags: [runtime, module, lifecycle, startup, shutdown]
---

# sync-module-lifecycle-contract 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-16
> 关联方案 doc：.codestable/features/2026-06-16-sync-module-lifecycle-contract/sync-module-lifecycle-contract-design.md

## 1. 接口契约核对

**接口示例逐项核对**：
- [x] `IGameModule`：`Startup()` / `Shutdown()` 均为 `void`，`IReference.Release()` 同步调用 `Shutdown()`。代码落点：`Assets/GameDeveloperKit/Runtime/Core/IGameModule.cs`。
- [x] `GameModuleBase`：抽象生命周期签名为 `public abstract void Startup()` / `Shutdown()`。代码落点：`Assets/GameDeveloperKit/Runtime/Core/IGameModule.cs`。
- [x] `EventModule` 示例：`Startup()` 同步注册绑定和 Timer update 派发，`Shutdown()` 同步取消 dispatch handle 并清理。代码落点：`Assets/GameDeveloperKit/Runtime/Event/EventModule.cs`。
- [x] `App.Register<T>()` 过渡示例：App 级 API 仍返回 `UniTask`，内部同步调用 `module.Startup()` 并记录顺序。代码落点：`Assets/GameDeveloperKit/Runtime/App.cs`。

**名词层“现状 → 变化”逐项核对**：
- [x] Core 生命周期契约已从 `UniTask` 改为 `void`。
- [x] App 框架级启动/注册保留 `UniTask` 外壳，但不再 await 模块生命周期。
- [x] Runtime 模块实现已全部迁移为 `override void Startup()` / `override void Shutdown()`。
- [x] 测试中直接模块生命周期调用已改为同步调用，App 级 await 调用保留兼容验证。

**流程图核对**：
- [x] 创建模块 → 同步 `Startup()` → 记录 `_moduleOrder` → 可访问模块外壳，已在 `App.Register<T>()` 落地。
- [x] 关闭模块 → 反序同步 `Shutdown()` → 清空 `_modules` / `_moduleOrder`，已在 `ShutdownRegisteredModules()` 落地。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] `IGameModule` / `GameModuleBase` 生命周期为同步契约。
- [x] 所有 Runtime 模块实现同步生命周期签名。
- [x] `App.Startup()` / `Register<T>()` / `Shutdown()` / `Unregister<T>()` API 仍可被 await。
- [x] 模块 `Startup()` 不再 await 文件、网络、资源 manifest/package 或 procedure enter/leave。
- [x] Runtime 与 Runtime.Tests csproj 均可编译。

**明确不做逐项核对**：
- [x] 未新增 `ModuleDependencyAttribute` / `DependencyAttribute`。
- [x] 未实现 `App.GetModule<T>()` 或按需依赖 resolver。
- [x] 未删除 App 默认预加载列表。
- [x] 未删除 Runtime `Startup.cs`。
- [x] 未改变 `App.Event` / `App.Resource` 未注册时报错语义。
- [x] 未把 Resource `InitializeAsync()` 完整状态机作为本 feature 主要产物。

**关键决策落地**：
- [x] Core 生命周期一次性同步化，`IReference.Release()` 不再 fire-and-forget。
- [x] App 聚合生命周期保留 `UniTask` 外壳以降低调用侧冲击。
- [x] 模块 `Startup()` 只保留同步轻量外壳；Resource/File/UI/Sound/Network 等异步边界已收口到显式业务 API 或同步 guard。

**流程级约束核对**：
- [x] 同步 `Startup()` 抛异常时，App 移除本次模块并透传原异常。
- [x] 同步 `Shutdown()` 抛异常时，App 继续关闭剩余模块并最终抛首个异常。
- [x] 默认模块列表和反序 shutdown 策略未改变。
- [x] `ResourceModule` / `FileModule` 在未 ready 状态下抛明确 `GameException`，避免空引用崩溃。

**挂载点反向核对**：
- [x] Core 契约挂载点：`IGameModule` / `GameModuleBase`。
- [x] Runtime 模块挂载点：所有 `GameModuleBase` 派生模块的同步 lifecycle override。
- [x] App 编排挂载点：注册、默认启动、卸载、反序关闭。
- [x] 测试调用挂载点：Runtime tests 中直接生命周期调用已迁移。
- [x] 拔除沙盘：若回退 Core 契约、模块签名、App 同步调用和测试调用，本 feature 在系统视角即消失；后续 resolver/attribute 不属于本 feature。

## 3. 验收场景核对

- [x] **N1**：`IGameModule` 契约为 `void Startup()` / `void Shutdown()`，`Release()` 同步调用 `Shutdown()`。证据：源码核对 + Runtime 编译。
- [x] **N2**：Runtime 模块实现中不存在 `override UniTask Startup/Shutdown`。证据：grep 无命中。
- [x] **N3**：`await App.Register<TimerModule>()` 仍可编译并可访问 `App.Timer`。证据：Runtime.Tests 编译与现有测试。
- [x] **N4**：直接创建 `TimerModule` 并同步调用生命周期可编译，重复启动/关闭不抛。证据：`TimerModuleTests`。
- [x] **N5**：`EventModule.Startup()` 在 Timer 已注册时仍能注册 Timer update 派发。证据：`EventModule` 源码与测试编译。
- [x] **N6**：`ResourceModule.Startup()` 不 await manifest/default package 初始化；未初始化资源 API 抛明确错误。证据：`ResourceModuleTests`。
- [x] **N7**：`FileModule.Startup()` 不阻塞等待 `VfsManifest.LoadAsync()`，公开 API 有未启动 guard。证据：源码核对 + Runtime 编译。
- [x] **N8**：`ProcedureModule.Shutdown()` 不阻塞等待 `OnLeaveAsync()`，同步释放当前 procedure 和 driver。证据：源码核对。
- [x] **N9**：App shutdown 反序关闭且保留首个异常。证据：`App.ShutdownRegisteredModules()` 源码核对。
- [x] **B1**：测试源码中不再直接 await module lifecycle。证据：grep 无命中。
- [x] **B2**：App 内部不存在 `await module.Startup()` / `await module.Shutdown()`。证据：grep 无命中。
- [x] **E1**：未新增用 `.GetAwaiter().GetResult()` / `.Wait()` 适配同步生命周期。证据：grep；既有 `ProcedureModule.RegisterProcedure()` 阻塞点为本 feature 前已存在且非 lifecycle 适配。
- [x] **E2**：未新增依赖 attribute 或 resolver。证据：grep 无新增命中。

验证命令：

```powershell
dotnet build GameDeveloperKit.Runtime.csproj --no-restore
dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore
```

两条命令均通过。

## 4. 术语一致性

- 模块生命周期：代码契约统一为 `Startup()` / `Shutdown()`。
- 同步外壳：Resource、File、UI、Sound、Network、Procedure 的生命周期只保留同步状态复位 / driver / 清理边界。
- 异步 ready：仍由 Resource 显式初始化、配置加载、Procedure bootstrap 等后续 roadmap 条目承接。
- 防冲突：本 feature 未引入 `ModuleDependency`、`GetModule<T>`、resolver 等后续术语。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` 已新增 Module Lifecycle 核心概念，记录同步 `IGameModule` / `GameModuleBase` 契约和 App 级 `UniTask` 过渡外壳。
- [x] Resource 架构段已更新：`ResourceModule.Startup()` 只保留同步外壳，不再描述为读取 settings/manifest/default package。
- [x] 已知约束已新增“模块 Startup/Shutdown 同步轻量”边界。
- [x] 变更日志已追加 2026-06-16 记录。

## 6. requirement 回写

- [x] 本 feature 为 runtime 架构契约迁移，frontmatter `requirement` 为空；无用户可见新能力需要 backfill，跳过 requirement 回写。

## 7. roadmap 回写

- [x] `.codestable/roadmap/module-dependency-loading/module-dependency-loading-items.yaml` 中 `sync-module-lifecycle-contract` 已从 `in-progress` 改为 `done`。
- [x] `.codestable/roadmap/module-dependency-loading/module-dependency-loading-roadmap.md` 第 5 节对应子 feature 已同步为 `done`，并填入 feature 目录名。
- [x] YAML 校验通过。

## 8. attention.md 候选盘点

- 候选 1：Runtime tests 快速编译验证可用 `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`。当前未直接写入 attention.md，等待用户确认是否沉淀。

## 9. 遗留

- 后续 roadmap：`core-dependency-attributes`、`app-sync-module-resolver`、`module-dependency-annotations`、`resource-explicit-initialize`、`procedure-bootstrap-flow`、`remove-default-preload-startup`。
- 已知限制：App 默认预加载和 Runtime `Startup.cs` 暂时保留；Resource 显式初始化状态机尚未落地。
- 顺手发现：`Assets/GameDeveloperKit/Runtime/Procedure/ProcedureModule.cs` 的 `RegisterProcedure()` 仍同步等待 `OnInitializeAsync().GetAwaiter().GetResult()`，建议后续在 Procedure bootstrap 或单独 issue 中处理。
