# app-sync-module-resolver 验收报告

> 阶段：阶段 3（验收闭环）  
> 验收日期：2026-06-17  
> 关联方案 doc：`.codestable/features/2026-06-17-app-sync-module-resolver/app-sync-module-resolver-design.md`

## 1. 接口契约核对

**接口示例逐项核对**

- [x] `App.Event => GetModule<EventModule>()` / `App.Resource => GetModule<ResourceModule>()` / `App.Timer => GetModule<TimerModule>()`：`Assets/GameDeveloperKit/Runtime/App.cs` 中所有同步模块属性均委托到 `GetModule<TModule>()`。
- [x] `public static TModule GetModule<TModule>() where TModule : class, IGameModule, new()`：已落地，内部调用 `ResolveModuleWithRollback(typeof(TModule))`。
- [x] `public static bool TryGetRegistered<TModule>(out TModule module)`：已公开，且复用类型查找逻辑。
- [x] `public static bool TryGetValue<TModule>(out TModule module)`：已收口为 `TryGetRegistered(out module)`，不再创建裸模块。

**名词层变化核对**

- [x] resolver：`ResolveModuleWithRollback` / `ResolveModule` / `GetModuleDependencyTypes` 已形成 App 内部同步解析逻辑。
- [x] 按需获取：`App.X` 属性第一次访问时会按需创建模块外壳；测试覆盖 `App.Event`、`App.Resource`、`App.Timer`。
- [x] 启动顺序：新模块启动成功后 `TrackModuleOrder(moduleType)`，`ShutdownRegisteredModules()` 反向遍历 `_moduleOrder`。

**流程图核对**

- [x] 已注册直接返回：`TryGetRegistered(moduleType, out existingModule)`。
- [x] 未注册读取 attribute：`Attribute.GetCustomAttributes(moduleType, typeof(ModuleDependencyAttribute), false)`。
- [x] 循环检测：`resolvingTypes.Contains(moduleType)` 抛 `GameException` 并输出链。
- [x] 依赖先启动：遍历 `dependencyTypes` 递归 `ResolveModule(...)` 后才创建目标模块。
- [x] 失败回滚：`createdTypes` 记录本次新建模块，异常时 `RollbackCreatedModules(createdTypes)`。

## 2. 行为与决策核对

**需求摘要逐项验证**

- [x] App 从固定默认预加载改为同步按需 resolver：`StartupInternal()` 不再包含任何 `RegisterDefault` 或固定模块列表。
- [x] `App.Event` / `App.Resource` / `App.Timer` 未预加载可访问：`AppModuleResolverTests` 覆盖 Event/Timer 和 Resource 依赖链。
- [x] `TryGetValue<T>()` 不再创建裸模块：`TryGetValue_WhenModuleIsNotRegistered_DoesNotCreateModule` 覆盖。
- [x] 循环依赖与启动失败有明确异常：循环依赖、依赖失败、目标失败回滚均有测试。

**明确不做逐项核对**

- [x] 未新增 async `GetModuleAsync<T>()`。
- [x] 未做程序集扫描或自动发现所有模块；resolver 只处理被访问模块声明的 `[ModuleDependency]`。
- [x] 未做 optional dependency / 优先级 / 分组 / 延迟 ready。
- [x] 未删除 `Startup.cs`。
- [x] 未改 ResourceModule 的完整异步初始化职责。

**关键决策落地**

- [x] resolver 放在 `App` 内部：所有解析逻辑在 `Assets/GameDeveloperKit/Runtime/App.cs`。
- [x] `GetModule<TModule>()` 作为唯一同步按需入口：同步属性和 `Register<T>()` 均走该入口。
- [x] App 启动状态机保留：`Startup()` / `Shutdown()` 仍有 lifecycle guard 和 completion source。
- [x] `TryGetValue<T>()` 已收口为已注册查询。

**挂载点核对**

- [x] 同步属性访问器：`App.Event` / `App.Resource` / `App.Timer` 等均在清单范围内。
- [x] resolver 逻辑：`GetModule<TModule>()`、`ResolveModuleWithRollback`、`ResolveModule`、`GetModuleDependencyTypes`。
- [x] 固定默认注册列表：`RegisterDefault<T>()` 已删除，grep `RegisterDefault` 无 runtime 命中。
- [x] `TryGetValue<T>()` 裸创建：grep `TryGetValue` 确认 App 内不再 `new T()`。
- [x] 测试挂载点：新增 `AppModuleResolverTests` 覆盖 resolver 最小闭环和失败路径。

## 3. 验收场景核对

- [x] **N1**：`App.GetModule<EventModule>()` 自动先启动 `TimerModule` 再启动 `EventModule`。证据：`GetModule_WhenModuleHasDependency_StartsDependencyBeforeTarget`。
- [x] **N2**：`App.Event`、`App.Resource`、`App.Timer` 在未预加载时可访问。证据：`GetModule_WhenModuleHasDependency_StartsDependencyBeforeTarget`、`GetModule_WhenResourceHasDependencyChain_StartsDeclaredDependencies`、`GetModule_WhenCalledTwice_ReturnsSameInstance`。
- [x] **N3**：`TryGetValue<T>()` 未注册时不创建裸模块。证据：`TryGetValue_WhenModuleIsNotRegistered_DoesNotCreateModule`。
- [x] **N4**：`App.Startup()` 后不自动注册默认模块。证据：`Startup_WhenCalled_DoesNotPreloadDefaultModules`。
- [x] **N5**：循环依赖抛 `GameException` 且包含依赖链。证据：`GetModule_WhenCircularDependencyExists_ThrowsWithDependencyChain`。
- [x] **N6**：启动失败时本次新建模块回滚，请求前已存在依赖保留。证据：`GetModule_WhenTargetStartupFails_RollsBackNewDependencies`、`GetModule_WhenDependencyStartupFails_PreservesExistingDependencies`。
- [x] **B1**：grep `RegisterDefault` 无 runtime 命中；`StartupInternal()` 不再有默认模块列表。
- [x] **B2**：`App.Event` / `App.Resource` / `App.Timer` 改为 `GetModule<T>()`。
- [x] **B3**：`dotnet build GameDeveloperKit.Runtime.csproj --no-restore` 与 `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore` 均通过。

反向核对：

- [x] 代码中不保留固定默认模块列表。
- [x] 代码中不保留 `TryGetValue<T>()` 裸 new 行为。
- [x] 代码中没有 attribute 自动扫描。

## 4. 术语一致性

- resolver：代码中使用 `ResolveModule...` / `GetModuleDependencyTypes`，与方案术语一致。
- 按需获取：公开 API 使用 `GetModule<TModule>()`，同步属性委托到该入口。
- 启动顺序：仍使用 `_moduleOrder` / `TrackModuleOrder`，无新增冲突术语。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md`：新增 `App Module Resolver（模块按需解析）`，记录 `GetModule<T>()`、`App.X` 属性、`[ModuleDependency]` 递归、循环检测、失败回滚、反序关闭。
- [x] `.codestable/architecture/ARCHITECTURE.md`：更新 Event / Procedure / Resource / Timer / Debug / Combat 里关于默认启动计划的旧描述。
- [x] `.codestable/architecture/ARCHITECTURE.md`：已知约束增加 `App 模块按需解析`，变更日志追加 2026-06-17 记录。

## 6. requirement 回写

- [x] design frontmatter `requirement` 为空；本 feature 属于 runtime 架构迁移，不新增单独用户愿景文档。本次跳过 requirement 回写。

## 7. roadmap 回写

- [x] `.codestable/roadmap/module-dependency-loading/module-dependency-loading-items.yaml`：`app-sync-module-resolver` 已从 `in-progress` 改为 `done`。
- [x] `.codestable/roadmap/module-dependency-loading/module-dependency-loading-roadmap.md`：子 feature 清单中 `app-sync-module-resolver` 状态已同步为 `done`。
- [x] YAML 校验通过。

## 8. attention.md 候选盘点

- [x] 无新增候选。既有 `dotnet build GameDeveloperKit.Runtime.csproj --no-restore` 已在 attention.md。

## 9. 遗留

- `module-dependency-annotations` 在当前代码中已随 `core-dependency-attributes` 提前落地首批标注；roadmap 仍保留 planned，需要后续 roadmap update 或轻量验收决定是否标记完成 / drop / 合并说明。
- `Startup.cs` 仍存在；本 feature 明确不删除。后续仍由 `remove-default-preload-startup` 或 roadmap update 决定删除时机。
- `ResourceModule.InitializeAsync()` / Procedure bootstrap 仍未落地，继续由后续 roadmap item 承接。
