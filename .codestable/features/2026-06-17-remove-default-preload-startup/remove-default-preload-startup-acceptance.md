# remove-default-preload-startup 验收报告

> 阶段：阶段 3（验收闭环）  
> 验收日期：2026-06-17  
> 关联方案 doc：`.codestable/features/2026-06-17-remove-default-preload-startup/remove-default-preload-startup-design.md`

## 1. 接口契约核对

**接口示例逐项核对**

- [x] `Assets/GameDeveloperKit/Runtime/Startup.cs`：已删除。
- [x] `Assets/GameDeveloperKit/Runtime/Startup.cs.meta`：已删除，未留下孤儿 meta。
- [x] `GameDeveloperKit.Runtime.csproj`：已移除 `Startup.cs` compile include。
- [x] `App.Startup()` / `App.Shutdown()`：API 保留，未删除或改名。
- [x] `App.TryGetValue<T>()`：仍委托 `TryGetRegistered(out module)`，未注册时不创建模块。

**名词层变化核对**

- [x] Runtime Startup 脚本：已从 runtime 源码与工程引用中移除。
- [x] App.Startup：仍是 lifecycle 状态入口，不承担默认模块 ready。
- [x] 默认预加载：grep `RegisterDefault` / `Preload` 无 runtime 命中。
- [x] TryGetValue 裸创建：`AppModuleResolverTests.TryGetValue_WhenModuleIsNotRegistered_DoesNotCreateModule` 继续覆盖。

**流程图核对**

- [x] 旧场景 MonoBehaviour 入口已删除。
- [x] App 按需 resolver 仍由 `App.GetModule<T>()` / `App.X` 驱动。
- [x] Procedure bootstrap 仍由业务显式 `App.Procedure.ChangeAsync<BootstrapProcedure>()` 驱动。

## 2. 行为与决策核对

**需求摘要逐项验证**

- [x] Runtime `Startup.cs` MonoBehaviour 删除：`Test-Path` 与 grep 均确认。
- [x] 工程引用删除：`GameDeveloperKit.Runtime.csproj` 无 `Startup.cs` compile include。
- [x] `App.Startup()` 保留且不注册默认模块：现有测试覆盖并编译通过。
- [x] `TryGetValue<T>()` 未注册时不创建模块：现有测试覆盖并编译通过。
- [x] architecture 不再描述 Runtime `Startup.cs` 仍存在。

**明确不做逐项核对**

- [x] 未删除或改名 `App.Startup()` / `App.Shutdown()`。
- [x] 未新增 `BootstrapBehaviour` / `StartupProcedureRunner` / 其他 MonoBehaviour 替代品。
- [x] 未修改 scene / prefab / ProjectSettings。
- [x] 未把业务 BootstrapProcedure 固化到框架。
- [x] 未清理历史 feature / audit 文档的原始记录。

**关键决策落地**

- [x] 删除 Runtime MonoBehaviour，不替换。
- [x] 保留 `App.Startup()` 兼容手动 lifecycle 调用。
- [x] 不改 Unity serialized 场景 / prefab。

**挂载点核对**

- [x] `Startup.cs` 删除。
- [x] `Startup.cs.meta` 删除。
- [x] `GameDeveloperKit.Runtime.csproj` 编译项删除。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已更新系统现状。
- [x] 反向 grep：runtime 中无 `class Startup : MonoBehaviour`，无 `RegisterDefault` / `Preload`。
- [x] 拔除推演：恢复这三个挂载点即可恢复旧场景入口；当前删除后框架不再提供默认 MonoBehaviour bootstrap。

## 3. 验收场景核对

- [x] **N1**：`Assets/GameDeveloperKit/Runtime/Startup.cs` 不存在。
- [x] **N2**：`Assets/GameDeveloperKit/Runtime/Startup.cs.meta` 不存在。
- [x] **N3**：`GameDeveloperKit.Runtime.csproj` 不再引用 `Startup.cs`。
- [x] **N4**：`App.Startup()` 后不注册 Event / Timer 等默认模块。证据：`Startup_WhenCalled_DoesNotPreloadDefaultModules`。
- [x] **N5**：`TryGetValue<TimerModule>` 未注册时返回 false 且不创建模块。证据：`TryGetValue_WhenModuleIsNotRegistered_DoesNotCreateModule`。
- [x] **B1**：grep `RegisterDefault` / 默认预加载列表无 runtime 命中。
- [x] **B2**：grep `class Startup : MonoBehaviour` 无 runtime 命中。
- [x] **B3**：Runtime 与 Runtime.Tests 编译通过。

反向核对：

- [x] 不删除 `App.Startup()` / `App.Shutdown()`。
- [x] 不新增替代 MonoBehaviour bootstrap。
- [x] 不修改 scene / prefab / ProjectSettings。
- [x] 不把业务 BootstrapProcedure 固化到框架。

## 4. 术语一致性

- `Runtime Startup 脚本` 在 architecture 中已改为“已删除”现状。
- `App.Startup` 仍指 lifecycle API，不再与场景 MonoBehaviour 混用。
- 禁用方向核对：runtime 无 `Startup : MonoBehaviour` / `RegisterDefault` / `Preload`。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md`：Module Lifecycle 段已记录 Runtime `Startup.cs` 场景脚本已删除。
- [x] `.codestable/architecture/ARCHITECTURE.md`：已知约束新增“无默认 Runtime Startup 脚本”。
- [x] `.codestable/architecture/ARCHITECTURE.md`：变更日志追加 2026-06-17 Startup removal 现状。

## 6. requirement 回写

- [x] design frontmatter 指向 `framework-startup`；本 roadmap 已把该旧愿景方向替换为“按需 resolver + Procedure bootstrap”。本次未直接改 requirement 文档，后续建议单独用 `cs-req update` 将 `framework-startup` 标记为 outdated 或改写为新启动模型。

## 7. roadmap 回写

- [x] `.codestable/roadmap/module-dependency-loading/module-dependency-loading-items.yaml`：`remove-default-preload-startup` 已从 `in-progress` 改为 `done`。
- [x] `.codestable/roadmap/module-dependency-loading/module-dependency-loading-roadmap.md`：子 feature 清单状态已同步为 `done`。
- [x] `.codestable/roadmap/module-dependency-loading/module-dependency-loading-roadmap.md`：frontmatter `status` 已改为 `completed`。
- [x] YAML 校验通过。

## 8. attention.md 候选盘点

- [x] 无新增候选。既有 Runtime 快速编译命令已在 `.codestable/attention.md`。

## 9. 遗留

- `framework-startup` requirement 仍是旧“一键按依赖启动和关闭”愿景，建议后续用 `cs-req update` 标记为 outdated 或改写为按需 resolver + Procedure bootstrap。
- `runtime-scheduling-diagnostics` roadmap 仍写“不删除 Startup bootstrap”，建议后续 roadmap update 同步这一边界变化。
