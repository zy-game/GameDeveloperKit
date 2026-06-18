# resource-explicit-initialize 验收报告

> 阶段：阶段 3（验收闭环）  
> 验收日期：2026-06-17  
> 关联方案 doc：`.codestable/features/2026-06-17-resource-explicit-initialize/resource-explicit-initialize-design.md`

## 1. 接口契约核对

**接口示例逐项核对**

- [x] `ResourceInitializeState`：已在 `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs` 落地 `NotInitialized / Initializing / Initialized / Failed`。
- [x] `ResourceInitializeOptions.Settings`：已作为可选 settings 覆盖入口，未传时走 `Resources.Load<ResourceSettings>("ResourceSettings")`。
- [x] `ResourceModule.IsInitialized` / `InitializeState` / `Settings` / `Manifest`：公开属性已落地。
- [x] `InitializeAsync(options)`：已作为资源 ready 的显式 async 入口，负责 settings、manifest、mode、builtin、default package 初始化。
- [x] `UninitializeAsync()`：已作为显式反初始化入口，释放 mode 并回到 `NotInitialized`。

**名词层变化核对**

- [x] Resource 同步外壳：`Startup()` 只清空状态、completion 和 modes，不读取 settings / manifest。
- [x] 显式初始化：`InitializeInternalAsync()` 通过 `InitializeOperationHandle` 加载 manifest，再创建 `BuiltinMode` 和 selected mode。
- [x] 初始化状态：`_initializeState` 是唯一状态来源，`IsInitialized` 由 `Initialized` 推导。

**流程图核对**

- [x] `Initialized` 直接返回：`InitializeAsync()` 开头判断 `_initializeState == Initialized`。
- [x] `Initializing` 等待同一任务：复用 `_initializeCompletion.Task`。
- [x] `NotInitialized / Failed` 重新执行：新建 `UniTaskCompletionSource` 并进入 `Initializing`。
- [x] manifest、mode、builtin、default packages 编排均有代码落点。
- [x] 失败路径调用 `ReleaseModes()` 并清空 `_setting` / `_manifest`。

## 2. 行为与决策核对

**需求摘要逐项验证**

- [x] `App.Resource` 可同步创建外壳但不 ready：测试 `Startup_WhenOperationModuleIsUnavailable_StartsShell` 和初始化相关测试覆盖。
- [x] `InitializeAsync()` 成功后 `Settings` / `Manifest` 非空且状态为 `Initialized`。
- [x] 重复初始化、并发初始化、失败后重试均有状态机分支和测试覆盖。
- [x] `UninitializeAsync()` 释放后恢复未初始化错误语义。

**明确不做逐项核对**

- [x] 未在 `App.Resource` / `GetModule<ResourceModule>()` 隐式调用或等待 `InitializeAsync()`。
- [x] 未新增 Procedure bootstrap 示例；继续由后续 `procedure-bootstrap-flow` 承接。
- [x] 未删除 `Startup.cs`。
- [x] 未修改 manifest schema。
- [x] 未改 UI / Sound / Config 的资源调用契约。

**关键决策落地**

- [x] 初始化状态放在 `ResourceModule` 内部。
- [x] 失败后允许重试：失败状态不阻断下一次 `InitializeAsync()`。
- [x] `ResourceInitializeOptions.Settings` 作为测试和业务 bootstrap 的覆盖入口。
- [x] `UninitializeAsync()` / `Shutdown()` 均释放 mode 并清空 ready 状态。

**挂载点核对**

- [x] `ResourceModule.InitializeAsync(options)`：公开入口已落地。
- [x] `ResourceInitializeState` / `IsInitialized`：状态观察入口已落地。
- [x] `ResourceInitializeOptions.Settings`：显式 settings 覆盖已落地。
- [x] `UninitializeAsync()`：显式释放入口已落地。
- [x] `EnsureReady()`：未初始化 API 统一抛 `Call InitializeAsync first`。
- [x] 反向 grep：本 feature 的新增引用集中在 `ResourceModule.cs`、`ResourceModuleTests.cs`、architecture 和 feature 文档内；无清单外 runtime 挂载点。
- [x] 拔除推演：移除 public API、状态 enum/options、`EnsureReady()`、测试和 architecture 记录即可撤销本 feature；无额外隐式 App 接线。

## 3. 验收场景核对

- [x] **N1**：`App.Resource` 首次访问后未调用 `InitializeAsync()` 时为 `NotInitialized`。证据：`Startup_WhenOperationModuleIsUnavailable_StartsShell`。
- [x] **N2**：未初始化调用资源加载抛明确错误。证据：`LoadMethods_WhenNotInitialized_ThrowGameException`。
- [x] **N3**：有效 local manifest settings 可完成初始化。证据：`InitializeAsync_WhenManifestIsValid_EntersInitializedState`。
- [x] **N4**：初始化后再次调用直接保持 ready。证据：`InitializeAsync_WhenCalledAgain_ReturnsReadyState`。
- [x] **N5**：并发初始化复用同一次结果。证据：`InitializeAsync_WhenCalledConcurrently_ReusesInFlightInitialization`。
- [x] **N6**：manifest 读取失败后状态为 `Failed` 且不 ready。证据：`InitializeAsync_WhenManifestFails_AllowsRetry` 前半段。
- [x] **N7**：失败后修正 settings 可重试成功。证据：`InitializeAsync_WhenManifestFails_AllowsRetry` 后半段。
- [x] **N8**：`UninitializeAsync()` 后回到 `NotInitialized`。证据：`UninitializeAsync_WhenInitialized_ReturnsToNotInitialized`。
- [x] **N9**：manifest 包含 `BUILTIN` bundle 时初始化 BuiltinMode 并可加载内置资源。证据：`InitializeAsync_WhenManifestContainsBuiltin_InitializesBuiltinMode`。
- [x] **N10**：manifest 不包含 `BUILTIN` bundle 时不失败。证据：多个空 package manifest 初始化测试通过。
- [x] **N11**：默认 package 缺失时异常包含 package 名。证据：`InitializeAsync_WhenDefaultPackageIsMissing_FailsWithPackageName`。
- [x] **B1**：grep `App.Resource` / `GetModule<ResourceModule>()`，不存在隐式调用 `InitializeAsync()`。
- [x] **B2**：Runtime 与 Runtime.Tests 编译通过。

反向核对：

- [x] 不在 `Startup()` 中 await manifest 或 package 初始化。
- [x] 不删除 `Startup.cs`。
- [x] 不修改 manifest schema。
- [x] 不改 UI / Sound / Config 的资源调用契约。

## 4. 术语一致性

- `ResourceInitializeState`、`ResourceInitializeOptions`、`InitializeAsync`、`UninitializeAsync` 与 design 术语一致。
- 代码中资源 ready 表达统一为显式 initialization，不再把 `_setting == null || _manifest == null` 当成对外状态名。
- 禁用方向核对：runtime 中没有新增 `GetModuleAsync<TModule>()` 或 App 隐式 Resource ready 入口。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md`：Resource 小节已记录 `InitializeAsync(options)` / `UninitializeAsync()` 是显式 ready 入口。
- [x] `.codestable/architecture/ARCHITECTURE.md`：已记录 `App.Resource` 只创建同步外壳，不隐式初始化资源。
- [x] `.codestable/architecture/ARCHITECTURE.md`：已知约束已记录业务 Procedure/bootstrap 必须显式 `await App.Resource.InitializeAsync()`。
- [x] `.codestable/architecture/ARCHITECTURE.md`：变更日志已追加 2026-06-17 Resource 显式 ready 现状。

## 6. requirement 回写

- [x] design frontmatter `requirement` 为空；本 feature 属于 runtime 架构迁移，不新增单独用户愿景文档。本次跳过 requirement 回写。

## 7. roadmap 回写

- [x] `.codestable/roadmap/module-dependency-loading/module-dependency-loading-items.yaml`：`resource-explicit-initialize` 已从 `in-progress` 改为 `done`。
- [x] `.codestable/roadmap/module-dependency-loading/module-dependency-loading-roadmap.md`：子 feature 清单中 `resource-explicit-initialize` 状态已同步为 `done`。
- [x] YAML 校验通过。

## 8. attention.md 候选盘点

- [x] 无新增候选。既有 Runtime 快速编译命令已在 `.codestable/attention.md`。

## 9. 遗留

- `procedure-bootstrap-flow` 仍未启动；下一步应建立 Procedure 承担 Resource 初始化、Config / Tag 准备和进入业务 Procedure 的约定与示例。
- `remove-default-preload-startup` 仍需等待 bootstrap flow 完成后再删除 Runtime `Startup.cs`。
