---
doc_type: roadmap
slug: module-dependency-loading
status: completed
created: 2026-06-16
last_reviewed: 2026-06-17
tags: [runtime, module, dependency, startup, lifecycle, procedure]
related_requirements: [framework-startup, procedure-module, config-module, resource-build-publish]
related_architecture: [ARCHITECTURE]
---

# Module Dependency Loading

## 1. 背景

旧模型中 `App.Startup()` 通过 `StartupInternal()` 预加载一串默认模块，模块依赖靠 App 内部硬编码顺序维护。这个方向解决了“一键启动”，但也带来新的问题：模块依赖不在模块自身声明，Combat 这类按需模块要不要进默认启动计划会反复摇摆，`Startup.cs` 也变成了为了预加载默认模块而存在的场景脚本。

新方向把框架生命周期拆成两层：模块生命周期同步、轻量、可按需直接访问；业务或资源准备异步、显式、由 Procedure 或业务启动流程编排。模块通过 `ModuleDependencyAttribute` 声明同步启动依赖，`App.Event` 这类属性第一次访问时会递归拉起依赖并返回可用模块；`ResourceModule` 的 manifest / package 等异步准备从 `Startup()` 移到显式 `InitializeAsync()`，由启动 Procedure 调用。

## 2. 范围与明确不做

### 本 roadmap 覆盖

- 把 `IGameModule.Startup()` / `Shutdown()` 从 `UniTask` 改为同步生命周期。
- 在 Core 下新增依赖元数据：`DependencyAttribute` 与 `ModuleDependencyAttribute`。
- 让 `App.GetModule<TModule>()` / `App.Event` 等同步访问器按依赖递归创建并启动模块。
- 移除 App 默认模块预加载列表和 Runtime `Startup` 场景脚本。
- 把 Resource 的异步资源系统准备迁出模块 `Startup()`，改为显式 `InitializeAsync()`。
- 用 Procedure 承担资源、配置、数据等异步启动流程的编排。
- 给现有模块补首批依赖标记，并收口 `TryGetValue<T>()` 创建未启动裸模块的问题。

### 明确不做

- 不做程序集扫描、attribute 自动发现并启动全部模块；模块仍按访问路径按需加载。
- 不把业务自定义模块、第三方插件或场景管理流程纳入框架默认计划。
- 不在同步 `App.X` 属性里阻塞等待异步初始化；异步准备必须显式调用。
- 不让 `ResourceModule.Startup()` 下载 manifest、初始化 package 或读取远端版本指针。
- 不在本 roadmap 内处理 Combat `World.Step()` 热路径分配、Procedure 注册同步等待异步、Config pending 异常清理等审查加固项。
- 不替代 `runtime-scheduling-diagnostics` roadmap 中 Timer update handle、Debug refresh、Procedure/Combat driver 收敛路线。

## 3. 模块拆分（概设）

```text
Module Dependency Loading
├── Sync Module Lifecycle：IGameModule / GameModuleBase 同步 Startup / Shutdown 契约
├── Core Dependency Metadata：DependencyAttribute / ModuleDependencyAttribute
├── App Module Resolver：按需递归解析依赖、循环检测、失败回滚、关闭顺序
├── Module Dependency Annotations：现有模块依赖标记迁移
├── Resource Explicit Initialization：ResourceModule 同步外壳 + 显式异步 InitializeAsync
├── Procedure Bootstrap Flow：Procedure 编排 Resource / Config / Data 等异步准备
└── Startup Removal：移除 App 默认预加载和 Runtime Startup MonoBehaviour
```

### Sync Module Lifecycle

- **职责**：定义模块同步生命周期边界。`Startup()` 只做轻量结构初始化，`Shutdown()` 做同步释放和停止；任何需要 await 的工作都不能放在模块生命周期里。
- **承载的子 feature**：`sync-module-lifecycle-contract`
- **触碰的现有代码 / 模块**：`Assets/GameDeveloperKit/Runtime/Core/IGameModule.cs`、所有实现 `GameModuleBase` 的模块。

### Core Dependency Metadata

- **职责**：在 Core 下提供通用依赖 attribute 基类和模块依赖派生类，作为 App resolver 的唯一声明来源。
- **承载的子 feature**：`core-dependency-attributes`
- **触碰的现有代码 / 模块**：`Assets/GameDeveloperKit/Runtime/Core/Dependency/`

### App Module Resolver

- **职责**：替代默认预加载列表。同步属性和 `GetModule<T>()` 通过模块类型读取依赖，先启动依赖，再启动目标模块；记录启动顺序供 `Shutdown()` 反序关闭。
- **承载的子 feature**：`app-sync-module-resolver`
- **触碰的现有代码 / 模块**：`Assets/GameDeveloperKit/Runtime/App.cs`

### Module Dependency Annotations

- **职责**：把隐式依赖从 App 默认顺序和模块内部 `TryGetRegistered` 分散逻辑中提炼成 `[ModuleDependency(...)]`。
- **承载的子 feature**：`module-dependency-annotations`
- **触碰的现有代码 / 模块**：Event、Download、Resource、Config、Sound、UI 等运行时模块。

### Resource Explicit Initialization

- **职责**：让 `ResourceModule` 的同步 `Startup()` 只创建状态容器和 mode/provider registry；manifest、publish pointer、默认 package 初始化转为 `InitializeAsync()`。
- **承载的子 feature**：`resource-explicit-initialize`
- **触碰的现有代码 / 模块**：`Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs` 及相关 operation handle。

### Procedure Bootstrap Flow

- **职责**：把资源、配置、数据等异步准备放进启动 Procedure。框架不再通过场景 `Startup.cs` 预加载默认模块，业务通过 Procedure 明确表达“进入游戏前要准备什么”。
- **承载的子 feature**：`procedure-bootstrap-flow`
- **触碰的现有代码 / 模块**：Procedure 使用方式、示例 / 测试用 bootstrap procedure；必要时补轻量约定类型。

### Startup Removal

- **职责**：删除默认预加载入口和 Unity `Startup` 脚本，避免新旧启动模型并存。
- **承载的子 feature**：`remove-default-preload-startup`
- **触碰的现有代码 / 模块**：`Assets/GameDeveloperKit/Runtime/App.cs`、`Assets/GameDeveloperKit/Runtime/Startup.cs`

## 4. 模块间接口契约 / 共享协议（架构层详设）

### 4.1 同步模块生命周期契约

**方向**：所有 Runtime 模块 -> Core

**形式**：接口 / 基类

**契约**：

```csharp
namespace GameDeveloperKit
{
    public interface IGameModule : IReference
    {
        void Startup();
        void Shutdown();
    }

    public abstract class GameModuleBase : IGameModule
    {
        public abstract void Startup();
        public abstract void Shutdown();

        void IReference.Release()
        {
            Shutdown();
        }
    }
}
```

**约束**：

- `Startup()` 不允许调用需要 await 的文件、网络、资源、配置加载流程。
- `Startup()` 可以创建字典、队列、driver GameObject、默认设置对象、订阅同步 callback。
- `Shutdown()` 不返回 `UniTask`，只做同步清理；仍需尽量幂等。
- 原本必须异步完成的准备工作要改为模块自己的显式方法，例如 `InitializeAsync()` / `LoadAsync()` / `OpenAsync()`。
- 若某模块无法在同步 `Startup()` 中完成可用外壳初始化，应拆出“同步外壳 + 异步 ready”两段，不允许在 `Startup()` 中阻塞等待 `UniTask`。

### 4.2 依赖 Attribute 契约

**方向**：模块类型 -> App resolver

**形式**：attribute 元数据

**契约**：

```csharp
namespace GameDeveloperKit
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public abstract class DependencyAttribute : Attribute
    {
        public Type DependencyType { get; }

        protected DependencyAttribute(Type dependencyType);
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class ModuleDependencyAttribute : DependencyAttribute
    {
        public ModuleDependencyAttribute(Type moduleType);
    }
}
```

**约束**：

- `dependencyType == null` 抛 `ArgumentNullException`。
- `ModuleDependencyAttribute` 的类型必须实现 `IGameModule`，否则 resolver 抛 `GameException`。
- 同一模块重复声明同一个依赖时，resolver 去重，不重复启动。
- 依赖只表达“启动目标模块前必须先启动的模块”，不表达异步 ready 状态。
- 不做 optional dependency 首版语义；可选集成继续用 `App.TryGetRegistered<T>()`。

示例：

```csharp
[ModuleDependency(typeof(TimerModule))]
public sealed class EventModule : GameModuleBase
{
}
```

### 4.3 App 同步按需加载契约

**方向**：业务 / 模块 -> App

**形式**：同步 API + 同步属性

**契约**：

```csharp
public static class App
{
    public static EventModule Event => GetModule<EventModule>();
    public static ResourceModule Resource => GetModule<ResourceModule>();
    public static TimerModule Timer => GetModule<TimerModule>();
    public static CombatModule Combat => GetModule<CombatModule>();

    public static TModule GetModule<TModule>()
        where TModule : class, IGameModule, new();

    public static bool TryGetRegistered<TModule>(out TModule module)
        where TModule : class, IGameModule;

    public static void Unregister<TModule>()
        where TModule : class, IGameModule;

    public static void Shutdown();
}
```

**约束**：

- `GetModule<TModule>()` 若模块已注册，直接返回现有实例。
- 若模块未注册，resolver 读取 `[ModuleDependency]`，递归先启动依赖，再启动目标模块。
- 启动顺序写入 `_moduleOrder`，`Shutdown()` 按反序关闭。
- resolver 必须检测循环依赖，抛 `GameException`，错误消息包含依赖链，例如 `A -> B -> A`。
- 本次请求中新启动的模块如果在目标启动失败时需要回滚；请求前已存在模块不能被回滚关闭。
- `TryGetValue<T>()` 不再创建未启动裸模块；应删除或改为 `TryGetRegistered<T>()` 等价语义。
- 同步属性不能阻塞等待异步初始化；如果模块外壳已启动但未 ready，应由模块 API 自己抛明确错误。

### 4.4 Resource 显式初始化契约

**方向**：Procedure / 业务启动流程 -> ResourceModule

**形式**：异步初始化 API

**契约**：

```csharp
public sealed class ResourceModule : GameModuleBase
{
    public bool IsInitialized { get; }
    public ResourceInitializeState InitializeState { get; }
    public ResourceSettings Settings { get; }
    public ManifestInfo Manifest { get; }

    public override void Startup();
    public override void Shutdown();

    public UniTask InitializeAsync(ResourceInitializeOptions options = null);
    public UniTask UninitializeAsync();
}

public enum ResourceInitializeState
{
    NotInitialized = 0,
    Initializing = 1,
    Initialized = 2,
    Failed = 3
}
```

**约束**：

- `Startup()` 只初始化内部集合、状态和必要默认值，不读取 manifest，不初始化 package。
- `InitializeAsync()` 负责读取 `ResourceSettings`、远端 publish pointer、manifest、mode/provider 和默认 package。
- 重复 `InitializeAsync()`：已完成时直接返回；初始化中时等待同一次初始化；失败后是否允许重试由 feature-design 明确。
- 资源加载 API 在未初始化时抛 `GameException("ResourceModule is not initialized. Call InitializeAsync first.")`。
- `UninitializeAsync()` 释放 package/provider 等异步资源状态；同步 `Shutdown()` 只能做不需要 await 的最后清理和状态复位。
- Resource 的异步初始化不再由 App 自动调用。

### 4.5 Procedure Bootstrap 契约

**方向**：业务启动流程 -> ProcedureModule / ResourceModule / ConfigModule

**形式**：Procedure 编排约定

**契约示例**：

```csharp
public sealed class BootstrapProcedure : ProcedureBase
{
    public override async UniTask OnEnterAsync(ProcedureBase previous, object userData)
    {
        App.Procedure.RegisterProcedure(new LoginProcedure());

        await App.Resource.InitializeAsync();

        _ = App.Config.Tags;
        App.Procedure.RequestChange<LoginProcedure>();
    }
}
```

**约束**：

- Procedure 是异步启动编排的推荐承载点，但框架不强制固定某个内建 `BootstrapProcedure`。
- App 不再负责“启动全部默认模块并准备资源”；业务选择进入哪个 Procedure，就显式准备哪些能力。
- Procedure 可以直接访问 `App.Resource` / `App.Config`，这些属性会同步创建模块外壳；真正 ready 由显式 async API 完成。
- ConfigModule 的 TagCatalog 当前由同步 `Startup()` 通过 `Resources.Load` 读取，不新增 `LoadTagCatalogAsync()`；若需要异步加载配置表，启动 Procedure 应显式调用对应 `LoadTableAsync<TRow>()`。
- 启动失败应停在当前 Procedure 或切到错误 Procedure，不由 App 吞掉异常。

### 4.6 首批模块依赖标记

**方向**：现有模块 -> App resolver

**形式**：attribute 标注

**建议首批标注**：

```csharp
[ModuleDependency(typeof(TimerModule))]
public sealed class EventModule : GameModuleBase { }

[ModuleDependency(typeof(OperationModule))]
public sealed class DownloadModule : GameModuleBase { }

[ModuleDependency(typeof(OperationModule))]
[ModuleDependency(typeof(DownloadModule))]
[ModuleDependency(typeof(FileModule))]
public sealed class ResourceModule : GameModuleBase { }

[ModuleDependency(typeof(ResourceModule))]
[ModuleDependency(typeof(DownloadModule))]
public sealed class ConfigModule : GameModuleBase { }

[ModuleDependency(typeof(ResourceModule))]
public sealed class SoundModule : GameModuleBase { }

[ModuleDependency(typeof(ResourceModule))]
public sealed class UIModule : GameModuleBase { }
```

**约束**：

- Debug 对 Timer / Command 的读取当前是可选能力，首版不强制标注；继续使用 `TryGetRegistered`。
- Data 对 File 的使用当前是可选持久化路径，首版不强制标注；若后续 Data 必须依赖 File，再补 dependency。
- Combat 当前仍可按需同步启动；等 `runtime-scheduling-diagnostics` 的 `combat-timer-consumer` 落地后，再给 Combat 标记 Timer 依赖。

## 5. 子 feature 清单

1. **sync-module-lifecycle-contract** — 把 `IGameModule` / `GameModuleBase` 生命周期改为同步 `void Startup()` / `void Shutdown()`，并迁移所有模块实现到可编译状态。
   - 所属模块：Sync Module Lifecycle
   - 依赖：无
   - 状态：done
   - 对应 feature：2026-06-16-sync-module-lifecycle-contract
   - 备注：迁移时只做生命周期签名和轻量同步逻辑，不顺手重写异步业务初始化。

2. **core-dependency-attributes** — 在 Core 下新增 `DependencyAttribute` 与 `ModuleDependencyAttribute`，固定依赖元数据契约。
   - 所属模块：Core Dependency Metadata
   - 依赖：无
   - 状态：done
   - 对应 feature：2026-06-16-core-dependency-attributes
   - 备注：先不做 optional dependency 或模块扫描；本 feature 同时落地首批静态模块依赖标注，供 resolver 最小闭环读取。

3. **app-sync-module-resolver** — 实现 `App.GetModule<T>()`、同步属性按需加载、递归依赖解析、循环检测、失败回滚和反序 shutdown。
   - 所属模块：App Module Resolver
   - 依赖：sync-module-lifecycle-contract, core-dependency-attributes
   - 状态：done
   - 对应 feature：2026-06-17-app-sync-module-resolver
   - 备注：最小闭环；完成后 `App.Event` 第一次访问会自动先启动 `TimerModule`。

4. **module-dependency-annotations** — 给 Event、Download、Resource、Config、Sound、UI 等模块补首批 `[ModuleDependency]` 标记。
   - 所属模块：Module Dependency Annotations
   - 依赖：app-sync-module-resolver
   - 状态：done
   - 对应 feature：2026-06-16-core-dependency-attributes
   - 备注：首批标注已随 core-dependency-attributes 提前落地；可选依赖保持 `TryGetRegistered`，不强行标注。

5. **resource-explicit-initialize** — 把 `ResourceModule.Startup()` 中的异步资源准备迁到 `InitializeAsync()` / `UninitializeAsync()`，让 Startup 只做同步外壳初始化。
   - 所属模块：Resource Explicit Initialization
   - 依赖：app-sync-module-resolver
   - 状态：done
   - 对应 feature：2026-06-17-resource-explicit-initialize
   - 备注：资源加载 API 未初始化时必须抛明确错误。

6. **procedure-bootstrap-flow** — 建立 Procedure 承担异步启动流程的约定和示例，覆盖 Resource 初始化、Config / Tag 准备和进入下一个业务 Procedure。
   - 所属模块：Procedure Bootstrap Flow
   - 依赖：resource-explicit-initialize
   - 状态：done
   - 对应 feature：2026-06-17-procedure-bootstrap-flow
   - 备注：不强制内建固定 BootstrapProcedure；可以先用测试/示例验证。

7. **remove-default-preload-startup** — 删除 App 默认预加载列表与 Runtime `Startup.cs` 脚本，收口 `TryGetValue<T>()` 裸创建行为。
   - 所属模块：Startup Removal
   - 依赖：module-dependency-annotations, procedure-bootstrap-flow
   - 状态：done
   - 对应 feature：2026-06-17-remove-default-preload-startup
   - 备注：已删除 Runtime `Startup.cs`，默认预加载和 `TryGetValue<T>()` 裸创建已由前置 feature 收口。

**最小闭环**：第 3 条 `app-sync-module-resolver` 做完后，`App.Event` 可在没有任何预加载的情况下直接访问：resolver 读取 `EventModule` 的 `ModuleDependency(typeof(TimerModule))`，同步启动 Timer，再同步启动 Event，返回可用 EventModule。

## 6. 排期思路

先改生命周期签名和依赖 attribute，因为这是后续 resolver 的硬前置。随后实现 App 同步 resolver，拿 `App.Event -> TimerModule -> EventModule` 做最小闭环验证。闭环稳定后再给现有模块补 dependency 标记，并把 Resource 的异步初始化迁出 `Startup()`。最后用 Procedure 明确承接启动链路，再删除默认预加载和 `Startup.cs`，避免新旧模型长期并存。

## 7. 观察项

- 本 roadmap 会取代 `2026-06-04-framework-startup` 中“Unity Startup 脚本 + 默认模块计划”的方向；当前不修改 requirement / architecture，等实现验收后再回写现状。
- `runtime-scheduling-diagnostics` 第 2 节写了“不删除 Startup bootstrap”，这是旧启动模型下的边界；本 roadmap 落地后需要 update 那份 roadmap，说明 Startup 脚本已由按需模块 resolver + Procedure bootstrap 替代。
- Resource 初始化从模块 `Startup()` 移走后，所有调用 `App.Resource.Load*` 的模块和业务需要明确自己处于资源 ready 之后；否则会收到未初始化错误。
- `App.X` 同步属性的语义将从“取已注册模块”升级为“按需同步创建模块外壳”。这会让访问更方便，但要求模块 `Startup()` 严格保持同步轻量。
