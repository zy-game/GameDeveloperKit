# Black Rain 架构总入口

> 状态：骨架（待填充）
> 创建日期：2026-05-17
> 最近审阅：2026-06-09

## 1. 项目简介

Black Rain — Unity/C# GameDeveloperKit 框架项目

## 2. 核心概念 / 术语表

## 3. 子系统 / 模块索引

### FileSystem（VFS 虚拟文件系统）

入口：`FileModule`（`Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs`），实现 `IGameModule`，通过 `Super.FileSystem` 访问。

核心类型：
- `VfsFileEntry` — 文件元数据（路径、CRC32、版本号、时间戳、存储方式）
- `VfsManifest` — JSON 清单索引，持久化到 `_manifest.json`

存储策略：
- 小文件（< 阈值 4096 字节）：合并写入单一 Bundle 文件（`.vfsb` 自定义二进制格式）
- 大文件（≥ 阈值）：独立文件存储在 VFS 根目录下

关键行为：写入自动 CRC32 校验、版本号调用方指定（string 类型，支持 `"1.0.1"` 语义版本）、软删除标记

### Download（下载模块）

入口：`DownloadModule`（`Assets/GameDeveloperKit/Runtime/Download/DownloadModule.cs`），实现 `IGameModule`，通过 `Super.Download` 访问。

核心类型：
- `DownloadHandler` — 单文件下载控制柄，暴露状态、进度、临时路径、失败类型、暂停/恢复/取消、完成等待和进度/终态回调
- `DownloadListHandler` — 批量下载控制柄，聚合多个 `DownloadHandler`，按顺序执行，单项失败不阻断后续项
- `DownloadStatus` / `DownloadFailureKind` — 下载状态和失败分类
- `DownloadChunk` — 大文件分片下载的内部 Range 分片元数据

存储策略：
- 下载期间只写 `Application.temporaryCachePath + "/downloads"` 下的临时文件
- 单流下载使用 `{文件名}.download` 作为 temp 文件，完成后由业务自行决定是否导入 `FileModule`
- 大文件达到模块阈值且服务端支持 Range 时，使用 `.part` 分片文件下载，全部完成后合并为一个 temp 文件

关键行为：支持单文件下载、批量下载、断点续传、暂停、恢复、取消、失败后恢复和大文件分片下载；同 URL 重复下载复用同一个 handler；取消会清理 temp / `.part`，暂停和失败会保留 temp / `.part` 以便恢复。

### Event（事件模块）

入口：`EventModule`（`Assets/GameDeveloperKit/Runtime/Event/EventModule.cs`），实现 `IGameModule`，通过 `App.Event` 访问。默认启动计划中 `TimerModule` 先于 `EventModule` 注册，Event 在 `Startup()` 中把队列派发注册到 Timer 的 Update tick。

核心类型：
- `IEventArgs` — 事件数据公共契约，提供 `Use()` / `HasUse()` 用于消费事件并停止后续派发。
- `IEventHandle` / `IEventHandle<TEvent>` — 事件处理器契约，统一接收 `sender` 和事件数据。
- `Subscription` — 一次订阅返回的取消句柄，支持 `Cancel()` / `Release()` 取消订阅。
- `BindingAttribute` — source generator 使用的事件绑定标记，用于自动订阅 handle 和生成事件扩展方法。

派发策略：
- `Subscribe<THandle>(handle)` 根据 handle 实现的 `IEventHandle<TEvent>` 识别事件类型并订阅。
- `Subscribe<TEvent>(Action<TEvent>)` 提供轻量委托订阅。
- `Fire<TEvent>(eventData, sender)` 只把事件参数和 sender 保存到 FIFO 队列；队列由 `TimerModule.OnUpdate()` 注册的 `EventModule.Dispatch` 回调在后续 Update 中派发。若当前没有可注册的 `TimerModule`，`Fire()` 会抛 `GameException`，调用方可改用 `FireNow()`。
- `FireNow<TEvent>(eventData, sender)` 是不安全的立即同步派发入口，用于调用方确认不会触发嵌套即时派发的少数场景。
- 任一 handle 调用 `eventData.Use()` 后，后续 handle 不再收到本轮事件。

关键行为：支持强类型订阅、委托订阅、取消订阅、重复订阅去重、Update 队列派发、事件消费中断和 source generator 自动绑定；单次 Update 只处理进入本轮前已经排队的事件，listener 中再次 `Fire()` 的事件留到下一次 Update，避免嵌套派发改写当前快照；不提供异步事件完成等待、优先级、限流、跨进程/网络事件或编辑器可视化面板。

### Procedure（流程状态机模块）

入口：`ProcedureModule`（`Assets/GameDeveloperKit/Runtime/Procedure/ProcedureModule.cs`），实现 `IGameModule`，通过 `App.Procedure` 访问。默认启动计划中位于 Resource/Config/Data/Sound/UI 之后，用于维护全局顶层流程状态。

核心类型：
- `ProcedureModule` — 保存已注册流程字典、当前流程、切换状态和运行时 driver，提供注册、查询、切换和 shutdown 释放。
- `ProcedureBase` — 流程基类，提供 `OnInitializeAsync()`、`OnEnterAsync(previous, userData)`、`OnLeaveAsync(next, userData)`、`OnUpdate(deltaTime, unscaledDeltaTime)` 和 `Release()` 生命周期。
- `ProcedureRuntimeDriver` — 内部 `MonoBehaviour`，每帧把 Unity `Time.deltaTime` / `Time.unscaledDeltaTime` 传给当前流程。

关键行为：`Startup()` 创建常驻 `GameDeveloperKit.ProcedureRoot`；`RegisterProcedure()` 初始化并登记实例，缺失流程可由 `ChangeAsync(type, userData)` 通过私有/公开无参构造懒创建；切换期间 `IsChanging == true`，重入切换会抛 `GameException`，当前流程的 `OnUpdate()` 会被跳过。切换顺序为 previous `OnLeaveAsync(next, userData)`、清空 `Current`、next `OnEnterAsync(previous, userData)`、设置 `Current`；切换完成不再通过 `EventModule` 发送 `ProcedureChangedEventArgs`。

### Resource（资源模块）

当前资源模块已落地为 `ManifestInfo` 清单、资源句柄、`ResourceModule` 门面、`ModeBase` 运行模式、`ProviderBase` bundle provider、play mode package operation 和 provider bundle/loading operation 的组合。业务侧通过 `Super.Resource` 访问已注册的 `ResourceModule`；`ResourceModule` 根据 `ResourceSettings.Mode` 创建 mode，并把资源加载 / package 生命周期请求分发给 `ModeBase` 实例。

核心类型：
- `ManifestInfo` — 资源总清单，包含 `Version`、`BuildTime` 和 `Packages`，并实现 `GetBundle()` / `GetDependencies()` 按 `Packages[*].Bundles` 查询 bundle 与依赖。
- `PackageInfo` — 资源组信息，包含 package 的 `Name`、`Version`、`Hash` 和 `Bundles`。
- `BundleInfo` — bundle 元数据，包含 `Name`、`Hash`、`Size`、`Crc`、`Version`、`Assets` 和 `Dependencies`。
- `AssetInfo` — 资源条目，包含 `Location`、`TypeName` 和 `Labels`。
- `ResourceHandle<T>` / `ResourceHandle` — 资源句柄基类，保存 `Info`、`Error`，通过 `Error == null` 判断 `IsValid`。
- `AssetHandle` — Unity `Object` 资源句柄，保存 `Asset` 并提供 `GetAsset<T>()`。
- `RawAssetHandle` — 原始资源句柄，保存 `byte[] Data` 并提供 UTF-8 `GetString()`。
- `SceneAssetHandle` — 场景资源句柄，保存 `Scene Asset`，`SceneName` 来自 `Info.Location`，提供 `Active()`。
- `BundleHandle` — bundle 句柄，保存 `AssetBundle Asset`，释放时会空值保护并卸载 AssetBundle。
- `OperationModule` — operation 的执行 / 等待 / 按 key 终态回写入口，当前可创建 `OperationHandle`、登记运行中 operation、调用 `Execute(args)`、等待完成、通过 `SetResult(key, value)` / `SetException(key, ex)` / `SetCanceled(key)` 完成运行中 operation，并在 `Shutdown()` 时取消清理未完成 operation。
- `ResourceSettings` — ScriptableObject 配置，包含 `Mode`、`DefaultPackages`、`Url` / 旧版 `url`、`ManifestName` 和 `CachePath`，并通过 `ServerUrl` / `ManifestLocation` 提供运行时读取入口。
- `ResourceModule` — 资源模块门面，持有 `_manifest`、`_setting` 和 `List<ModeBase>`，通过 `Super.Resource` 暴露 API。
- `ModeBase` — 运行模式抽象，持有 `ManifestInfo`，声明 package 生命周期、asset/raw/scene 加载、卸载和释放 API。
- `BuiltinMode` / `StreamingAssetMode` / `BundleMode` / `WebGLMode` / `EditorSimulatorMode` — 当前五种 mode 实现；除 `BuiltinMode` 为单 provider 外，其余 mode 都持有 provider 列表，并各自承载自己的 package lifecycle operation。
- `ProviderBase` — bundle provider 抽象，持有 `BundleInfo Info`，统一执行 asset 查询、缓存命中、批量加载遍历、句柄登记和 pending unload 管理；具体 provider 只实现 asset/raw/scene 的实际加载 operation。
- `BuiltinAssetProvider` / `BundleAssetProvider` / `EditorAssetProvider` / `WebAssetProvider` — 当前 provider 实现，均承载自己的 bundle lifecycle 与 loading nested operation；`EditorAssetProvider` 位于 Runtime Resource 目录，通过 `#if UNITY_EDITOR` 包裹 `UnityEditor.AssetDatabase` 调用；`WebAssetProvider` 由 `WebGLMode` 创建并通过 `UnityWebRequestAssetBundle` 加载远端 bundle。
- `ResourceModule.ManifestOperationHandle`、`*Mode.InitializePackageOperationHandle`、`*Mode.UninitializePackageOperationHandle`、`*Provider.InitializeBundleOperationHandle`、`*Provider.UninitializeBundleOperationHandle`、`*Provider.LoadingAssetOperationHandle` / `LoadingRawAssetOperationHandle` / `LoadingSceneAssetOperationHandle` — 资源模块异步编排入口；BundleMode + BundleAssetProvider、WebGLMode + WebAssetProvider、Builtin / Editor provider loading operation 已存在。

清单关系：
- `ManifestInfo.Packages` 包含多个 `PackageInfo`。
- `PackageInfo.Bundles` 包含多个 `BundleInfo`。
- `BundleInfo.Assets` 包含多个 `AssetInfo`。
- `AssetInfo.Location` 是业务加载地址，也是 provider 查询资源的主键。
- `AssetInfo.TypeName` 和 `AssetInfo.Labels` 也会被 provider 的 `HasAsset(key)` 用于 type / label 查询。

句柄关系：
- `ResourceHandle : ResourceHandle<AssetInfo>`。
- `AssetHandle : ResourceHandle`。
- `RawAssetHandle : ResourceHandle`。
- `SceneAssetHandle : ResourceHandle`。
- `BundleHandle : ResourceHandle<BundleInfo>`。

契约关系：
- `Super.Resource` 返回已注册的 `ResourceModule`。
- `ResourceModule.Startup()` 直接通过 Unity `Resources.Load<ResourceSettings>("ResourceSettings")` 读取配置，不依赖资源模块自身加载 API；随后按 `ResourceSettings.ManifestLocation` 下载或读取本地 `ManifestInfo`，创建 `StreamingAssetMode`、`BuiltinMode` 和配置指定 mode，再初始化 `BUILTIN` 与默认 package。
- `ResourceModule.InitializePackageAsync(package)` 通过 `ResourceSettings.Mode` 找到当前配置 mode 并委托。
- `BundleMode.InitializePackageAsync(package)` 通过 `BundleMode.InitializePackageOperationHandle(package, providers, Manifest)` 解析目标 package 的 bundle 和递归依赖，创建并初始化 `BundleAssetProvider`，成功后注册到 provider 列表，失败时回滚本次已注册 provider。
- `BundleMode.UninitializePackageAsync(package)` 通过 `BundleMode.UninitializePackageOperationHandle(package, providers, Manifest)` 释放并移除目标 package 及其依赖对应 provider。
- `ResourceMode.EditorSimulator` 通过 `EditorSimulatorMode` 创建 `EditorAssetProvider`；Editor 专用 API 留在 Runtime 目录是当前允许的边界，但必须由 `#if UNITY_EDITOR` 保护，非编辑器路径需要明确失败。
- `ResourceMode.Web` 通过 `WebGLMode` 进入 Web 模式；`WebGLMode.InitializePackageOperationHandle` 创建 `WebAssetProvider`。
- 单资源加载通过 `modes.FirstOrDefault(x => x.HasAsset(location))` 找 mode，再由 mode 找 provider。
- `ModeBase` 持有 `ManifestInfo`；除 `BuiltinMode` 外，mode 通过 `List<ProviderBase>` 管理 provider。
- `ProviderBase` 只持有自己的 `BundleInfo`，不持有 `ManifestInfo`。

关键行为：`ResourceModule` 对 `location` / `label` / `name` / `package` 做 null/空白校验；`UnloadAsset(null)` 抛 `ArgumentNullException`，失败或已释放句柄因 `Info == null` 幂等返回；未创建 mode 时资源 API 抛 `GameException("No resource play mode is available.")`。Provider 通过 `AssetInfo.Location`、`TypeName`、`Labels` 查询资源，已加载资源保存在 `_assets`，卸载时转移到 pending unload 列表；`ProviderBase.UnloadUnusedAssetAsync()` 只释放 pending handle，`ResourceModule.UnloadUnusedAssetAsync()` 在所有 mode/provider 释放后统一调用一次 `UnityEngine.Resources.UnloadUnusedAssets()`。`OperationModule` 以 `(operation key, operation type)` 登记运行中 operation；同一 key 的不同 operation type 可并存，同一 key + type 不允许同时运行；只有 key 的外部 `Set*` 回写遇到同 key 多个运行项时抛明确异常，避免随机完成某个 operation。`ResourceMode.Online` 对应的 `BundleMode` + `BundleAssetProvider` 已具备 package -> provider -> bundle -> asset/raw/scene 的 operation 链路：`BundleAssetProvider.InitializeBundleOperationHandle` 以 `BundleInfo.Name` 作为本地路径加载 AssetBundle；`ResourceMode.Web` 对应的 `WebGLMode` + `WebAssetProvider` 以 `ResourceSettings.ServerUrl + BundleInfo.Name` 通过 `UnityWebRequestAssetBundle` 加载远端 AssetBundle。资源模块仍不是全模式端到端闭环：当前仓库 `Assets/StreamingAssets/manifest.json` 仍是旧 JSON 形态，需由资源构建链路同步生成 `ManifestInfo` / `PackageInfo` / `BundleInfo` / `AssetInfo` 当前清单格式；`EditorAssetProvider` 的 Editor API 保护式 Runtime 放置是当前接受的实现边界。

### Combat（战斗 ECS 模块）

入口：`CombatModule`（`Assets/GameDeveloperKit/Runtime/Combat/CombatModule.cs`），实现 `IGameModule`，通过 `App.Combat` 访问。CombatModule 首版按需注册，不在框架默认启动计划中；启动时创建默认 `GameDeveloperKit.Combat.World` 和常驻 `GameDeveloperKit.CombatRoot`，由内部 `CombatRuntimeDriver` 每帧把 Unity `Time.deltaTime` 传给默认 world。

核心类型：
- `World` — 战斗世界门面，内部持有 `Massive.MassiveWorld`、`EntityManager` 和 `SystemManager`，暴露 `Create` / `Destroy`、组件增删查、系统加载卸载、`ForEach(Queryable)`、固定步 `Update` / `Step`、`SaveFrame` / `Rollback`、`Clear` / `Dispose`。
- `EntityManager` — 管理 combat `Entity` wrapper 与底层 massive entity 的映射，所有组件 mutation 都经过它触发系统匹配生命周期。
- `SystemManager` — 管理 `SystemBase` 注册、include/exclude 过滤条件、实体进入/离开匹配集合的 `OnCreate` / `OnDestroy`，以及固定步 `OnUpdate`。
- `Entity` — 受管引用句柄，包含 `Id`、`Version`、`IsAlive` 和组件快捷方法；同一个 alive id/version 复用同一个 wrapper。
- `ComponentBase` — Combat 组件基类；公开组件 API 与 `Queryable` 条件只接受其派生类型。
- `Queryable` — include/exclude 查询描述，保存 `Type[]`，空 include 表示无必需组件，空 exclude 表示无排除组件；重复类型去重，include/exclude 冲突抛 `GameException`。
- `SystemBase` — Combat 系统基类，提供 `Query`、`Initialize(World)`、`OnCreate(Entity)`、`OnDestroy(Entity)`、`OnUpdate(Entity)`。

匹配与生命周期：
- `World.Create()` 创建实体后会通知当前匹配 `Queryable.All` 或相应条件的系统进入集合。
- `AddComponent` / `RemoveComponent` 在变更前捕获受影响系统的匹配状态，变更后只对 include/exclude 涉及该组件类型的系统做差异通知，避免组件 churn 时全系统扫描。
- `OnDestroy` 表示实体离开系统匹配集合，不只表示实体销毁；组件移除、加入 exclude 组件、rollback 后不再匹配、实体销毁和系统卸载都会触发对应回调。
- `World.Step()` 按系统注册顺序，通过 Massive `Query` 现查当前匹配实体并调用 `OnUpdate`；`World.Update(deltaTime)` 按 `FrameRate` / `FixedDeltaTime` 累积真实时间并推进 0 到 N 个固定步。
- 系统回调期间允许加载/卸载系统；`SystemManager` 使用注册表快照和 active 标记避免回调中修改系统列表破坏当前枚举，新系统从后续 step/通知开始参与。
- `World.Rollback(frames)` 在回滚前捕获当前匹配集合，回滚后重建 entity wrapper 缓存并比较回滚前后差异触发生命周期回调。

关键行为：`World.Dispose()` 后旧 world 引用不再允许继续访问公开 API，旧 entity 的 `IsAlive` 返回 false；继续调用 world/entity mutation 会抛 `GameException`。组件读取缺失组件时由 Combat 层抛 `GameException`，组件类型不继承 `ComponentBase` 时抛 `ArgumentException`。Combat 不实现具体战斗规则、伤害、技能、Buff、AI、寻路、动画、特效、网络同步、GameObject / Transform 自动绑定、程序集扫描式系统自动注册、多 world 全局调度、多线程或 Burst / Job System 安全承诺；底层存储、查询和 rollback 仍由 massive 承担，combat 层只做项目级 API 与生命周期封装。

### Timer（运行时时钟与调度）

入口：`TimerModule`（`Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs`），实现 `IGameModule`，通过 `App.Timer` 访问。默认启动计划中 `TimerModule` 位于 `OperationModule` 之后、`EventModule` 之前，给 Event 队列派发、Debug 日志 tick、Timer tab、业务延时/倒计时/循环调度，以及 runtime update/late-update/fixed-update 回调提供统一时间口径。

核心类型：
- `TimerModule` — 持有唯一全局 `Tick` / `Time` / `UnscaledTime` / `DeltaTime` / `UnscaledDeltaTime`，统一登记、推进、取消和快照所有 timer handles。
- `TimerModule.Timer` — 内部 `MonoBehaviour` driver，分别通过 Unity `Update()`、`LateUpdate()`、`FixedUpdate()` 调用 `TimerModule.Update(...)`；不改写 Unity 全局 `Time.fixedDeltaTime`。
- `TimerHandle` — timer 句柄基类，只保存 module、owner/tag、取消/完成/暂停状态和 `Advance()` 模板；delay/interval/countdown 业务字段不在基类定义。
- `TimerDelayHandle` / `TimerCountdownHandle` / `TimerIntervalHandle` — 分别承载延时执行一次、倒计时状态回调和按 interval 循环调度；三者当前默认走 Update tick。
- `TimerUpdateHandle` — update 型 timer handle 基类，保存 `Enabled`、`LastTick`、`LastException` 和 `HasError`，并隔离单个回调异常；Late/Fixed 派生 handle 可配置 handle 级 `fps` 门控。
- `UpdateTimerHandle` / `LateUpdateTimerHandle` / `FixedUpdateTimerHandle` — 显式选择 Unity `Update`、`LateUpdate` 或 `FixedUpdate` phase 的运行时回调句柄，可通过 `Register(new XxxTimerHandle(...))` 或 `OnUpdate` / `OnLateUpdate` / `OnFixedUpdate` 注册。
- `TimerUpdateContext` — update handle 回调上下文，公开 tick、time、unscaled time、delta 和 unscaled delta；内部携带 tick kind 用于调度和测试。
- `TimerSnapshot` — Debug 和外部诊断读取的只读快照，包含 tick、time、unscaled time、delta、active delays/countdowns/intervals/updates。

关键行为：Timer driver 的 Unity `Update()` 推进唯一全局 clock；`LateUpdate()` / `FixedUpdate()` 只作为 phase trigger 检查匹配的 update handles，不额外推进 `TimerModule.Tick` / `Time` / `UnscaledTime`。`Delay()` 到点执行一次并自动完成；`Countdown()` 每个 Update tick 更新 remaining/progress，到 0 触发 complete；`Interval()` 按 interval 循环执行，单帧跨多个 interval 时补触发并保留余量。`OnUpdate()` / `OnLateUpdate()` / `OnFixedUpdate()` 注册的 update handle 只在匹配 phase 被调用，context 使用当前全局 clock；Late/Fixed handle 传入 `fps` 时按 phase 的 unscaled delta 做 handle 级门控，未传时每个匹配 phase 都调用。单个 update handle 抛异常会记录到该 handle，不阻断后续 handle。`useUnscaledTime` 的 timer 使用 unscaled clock，不受 `Time.timeScale` 暂停影响；默认 timer 使用 scaled clock。callback 中允许取消自己或其他 timer，遍历期间取消不会修改当前 dispatch buffer，清理在本轮推进后统一发生。`Startup()` 重复调用为 no-op，`Shutdown()` 取消全部 timer 并销毁 driver，重复 shutdown 不抛异常。

### Debug（运行时调试中枢）

入口：`DebugModule`（`Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs`），实现 `IGameModule`，通过 `App.Debug` 访问；当前源码命名空间仍为 `GameDeveloperKit.Logger`，但目录和默认入口已经归入 Runtime Debug 子系统。默认启动计划中 Debug 位于 Timer 之后，日志记录可附带当前 Timer tick。

核心类型：
- `DebugModule` — 唯一调试模块入口和 profile lifecycle 门面，负责注册内建/外部 `ProfileHandle`、维护 Unity log capture、Console/Command 薄入口，并把日志/metrics 兼容 API 委托给内建 handles。
- `DebugProfileHandle` — Debug 日志入口，负责接收日志、脱敏、过滤 category/minimum level，并持有 `DebugLogBuffer`；不再作为 Profiles tab 默认内建 profile 注册。
- `MemoryProfileHandle` — Debug 内建内存/metrics profile，startup 时注册到 Profiles tab；负责采样 `DebugMetricSnapshot`，持有固定容量 memory sample buffer，并自行绘制 memory 柱状图和摘要。
- `DeviceInfoProfileHandle` — Debug 内建设备信息 profile，startup 时注册到 Profiles tab；自行绘制平台、Unity 版本、设备型号/类型、CPU、内存、显卡和图形 API 等非敏感设备信息。
- `DebugSettings` — 控制日志容量、metric 采样间隔、Console、Command、Unity log capture、redaction 和 metrics 开关；Console/Command/Unity capture 默认跟随 `UnityEngine.Debug.isDebugBuild`。
- `DebugLogBuffer` / `DebugLogRecord` — O(1) ring buffer 与结构化日志记录，记录 timestamp、sequence、Unity frame、Timer tick、level、category、message、exception、context 和 tags。
- `ProfileHandle` / `DebugProfileRegistry` — Profiles tab 扩展点；`ProfileHandle` 公开契约只保留 `Name`，绘制由派生类通过内部 draw hook 自行完成。registry 只维护 handle 注册顺序，安全读取 `Name`，并在 GUI 绘制时隔离单个 profile 的 draw 异常。
- `DebugConsole` / `DebugGuiDriver` — 运行时 IMGUI Console 状态和 Unity 生命周期桥接；GUI 绘制细节在 driver 内，Console tabs 包含 Logs、Profiles、Timers、Tools、Settings，并提供 Close 按钮；Console 初始关闭，关闭态只在右上角绘制带 FPS 的重开按钮，IMGUI 按 1920x1080 参考分辨率缩放以适配高分屏。

关键行为：日志写入路径由 `DebugProfileHandle` 校验 level、过滤 enabled/category/minimum level，然后对 category/message/exception/context/tags 做 redaction，再进入内存 buffer。exception/context 在 redaction 前使用安全字符串化，`ToString()` 抛异常时写入 fallback 文本而不让日志 API 反向抛异常；redaction 关闭时保留原 exception/context 对象。Unity 原生日志可接入同一 pipeline，但 Debug 不再提供独立 sink、analytics sink 或 transport 扩展点；后续如需网络实时日志，由 Network 模块读取已脱敏 `DebugLogRecord` 并自行定义 bridge/adapter。`MemoryProfileHandle` 按 `DebugSettings.MetricSampleInterval` 采样并更新 `DebugMetricSnapshot`，memory 柱状图只保留固定容量 ring buffer。Profiles tab 遍历 `DebugProfileRegistry` handles，显示 fallback-safe `Name` 并调用派生类自绘；单个 profile 绘制失败只显示该 profile 错误，不影响其他 profile。Timers tab 通过 `App.TryGetRegistered<TimerModule>()` 读取 `TimerModule.Snapshot()`，展示 tick、time、delta、delay/countdown/interval/update handle 数量和句柄状态。Tools tab 的命令执行仍走已注册的 `CommandModule`，Debug 不自建独立 GM registry。Debug metrics sampling 接入 Timer update handle 仍属后续 roadmap item。

## 4. 关键架构决定

## 5. 已知约束 / 硬边界

- **CRC32 数据完整性**：FileModule.ReadFileAsync 读取后强制 CRC32 校验，不匹配抛 `GameException`
- **幂等写入**：同路径多次 WriteFileAsync 为覆盖更新，Manifest 仅保留最新条目
- **根目录固定**：`Application.persistentDataPath + "/vfs"`，不可配置
- **首版不做并发控制**：所有公开 API 假定在主线程调用
- **下载临时落盘**：DownloadModule 只写 `Application.temporaryCachePath + "/downloads"`，不主动写入 FileModule
- **下载恢复语义**：暂停和失败保留 temp / `.part`；取消和 CancelAll 删除 temp / `.part`
- **批量下载失败继续**：DownloadListHandler 中单项 Failed 不阻断后续下载
- **事件模块 Update 队列派发**：EventModule 公开 API 假定主线程调用；`Fire<TEvent>` 依赖已注册的 TimerModule，只入队并由 Timer Update 回调派发，直到事件被 `Use()` 消费或 listener 列表结束；listener 中再次 `Fire()` 的事件进入下一次 Update
- **事件模块即时派发边界**：`FireNow<TEvent>` 是不安全的同步派发入口，调用方需自行保证不会产生破坏当前派发快照的嵌套即时派发；EventModule 不提供异步完成等待、优先级、限流或跨线程并发安全承诺
- **Procedure 切换无全局事件**：ProcedureModule 切换完成只更新 `Current`，不再通过 EventModule 发送 `ProcedureChangedEventArgs`；需要切换通知时由调用侧显式扩展
- **OperationModule 不提供调度语义**：OperationModule 只管理运行中 operation 的登记、等待、终态回写和关闭清理，不提供队列、优先级、重试、调度线程或线程安全承诺；公开 API 假定 Unity 主线程调用
- **资源清单当前根类型**：资源模块当前代码使用 `ManifestInfo` / `PackageInfo` / `BundleInfo` / `AssetInfo`，不要在新方案里继续引用未落地的 `ResourceManifest` / `ResourceBundleInfo` / `ResourceAssetInfo`
- **资源 Mode / Provider 当前抽象**：资源模块当前代码使用 `ModeBase` / `ProviderBase`，不要在实现计划里继续把未落地的 `IResourcePlayMode` / `IResourceProvider` 当作现状
- **资源 Provider 不持有 Manifest**：Provider 只负责自己 `BundleInfo` 内的资源操作；跨 package / bundle 查询属于 Mode 或 Manifest 层
- **资源句柄释放语义**：`ResourceHandle<T>.Release()` 清空 `Info` / `Error`；`AssetHandle.Release()` 额外清空 `Asset`；`BundleHandle.Release()` 会 `AssetBundle.Unload(true)`
- **BundleMode / WebGLMode provider operation 已最小闭环**：`OperationModule`、`ManifestInfo.GetBundle()` / `GetDependencies()`、BundleMode package operation 与 BundleAssetProvider bundle/loading operation、WebGLMode 与 WebAssetProvider 接线已补齐；未覆盖 Builtin / StreamingAssets / EditorSimulator 的全部真实加载差异
- **BundleInfo.Name 当前兼任加载定位**：BundleMode 以 `BundleInfo.Name` 作为 AssetBundle 本地路径或 URI；后续如需下载缓存、CRC、远端 URL 策略，应新增显式字段或解析服务
- **EditorSimulator 允许在 Runtime 目录**：`EditorSimulatorMode` / `EditorAssetProvider` 当前位于 Runtime Resource 目录；`UnityEditor` API 调用必须由 `#if UNITY_EDITOR` 包裹，Player 路径不得直接引用 `UnityEditor`，非编辑器路径需要明确抛错；不要求仅因使用受保护的 Editor API 移到 Editor-only asmdef
- **在线资源模式当前命名**：当前没有独立 `HostingPlayMode`；`ResourceMode.Online` 对应 `BundleMode`
- **Combat 首版边界**：CombatModule 按需注册，不进默认启动计划；不做具体战斗规则、表现同步、自动系统扫描、多 world 全局调度、多线程安全或网络同步；公开 API 假定 Unity 主线程调用
- **Combat lifecycle 封口**：`World.Dispose()` 后旧 world 公开 API 抛 `GameException`，旧 entity `IsAlive == false`
- **Combat 系统重入语义**：系统回调内可加载/卸载系统；当前 step/通知使用注册表快照和 active 标记，新系统不插入当前枚举，已卸载系统不继续更新
- **Combat 组件变更匹配索引**：组件增删只通知 include/exclude 涉及该组件类型的系统；全量匹配只用于 create/destroy/rollback/system add/remove 等需要全局判断的路径
- **Timer Unity tick 口径**：TimerModule 只有一份全局 `Tick` / `Time` / `UnscaledTime` / `DeltaTime` / `UnscaledDeltaTime`，仅 Unity `Update` 推进全局 clock；`LateUpdate` / `FixedUpdate` 只触发对应 update handles，可用 handle 级 `fps` 门控；不改写 Unity 全局 `Time.fixedDeltaTime`
- **Timer 主线程语义**：Timer 公开 API 假定 Unity 主线程调用，不提供后台线程、任务队列、优先级、重试、job system、服务器时间、网络锁步或回滚确定性承诺
- **Debug 运行时边界**：Debug Console、command、Unity log capture 默认随 debug build 开启；DebugModule 不直接持有 sink/transport/analytics 列表，日志由 `DebugProfileHandle` 接收，内存/metrics 由 `MemoryProfileHandle` 采样并自绘柱状图，设备信息由 `DeviceInfoProfileHandle` 自绘；日志只进入内存 buffer，不提供本地 rolling file、DebugBundle、离线上传包或 Debug 自有网络 transport

## 6. 变更日志

- 2026-06-09：同步 Timer 全局 clock 修正现状：TimerModule 不再维护 Update/LateUpdate/FixedUpdate 三套 clock，Late/Fixed 只做 phase trigger，并支持 handle 级 fps 门控。
- 2026-06-09：同步 Event 与 Procedure audit 修复现状：`Fire<TEvent>` 改为 Timer Update 队列派发，新增不安全即时 `FireNow<TEvent>`，Procedure 切换不再发送全局 `ProcedureChangedEventArgs`，默认启动顺序改为 Timer 先于 Event。
- 2026-06-09：同步 MemoryProfileHandle 绘制现状：memory 采样仍为固定容量 ring buffer，Profiles tab 中改为柱状图，不再使用旋转矩阵绘制曲线。
- 2026-06-09：同步 Debug Console 关闭态交互和高分屏 IMGUI 适配现状：Console startup 后默认关闭，关闭态显示右上角 FPS 重开按钮，driver 使用参考分辨率矩阵缩放并缓存基础 GUIStyle。
- 2026-06-09：同步 Debug ProfileHandle table-first 设计修正后的现状，记录 `ProfileHandle` 只公开 `Name`、profile 派生类自绘、默认 Profiles tab 收敛为 Memory 曲线和 Device Info。
- 2026-06-09：同步 Debug 旧输出扩展点移除后的现状，记录 DebugProfileHandle 只负责日志接收、脱敏、过滤和内存 buffer，Network 实时日志由未来 Network 模块自行桥接。
- 2026-06-08：同步 Debug profile-centric 现状，记录 DebugModule 只管理 profile/lifecycle/薄门面，DebugProfileHandle 接收日志，MemoryProfileHandle 承载 metrics snapshot，GUI 绘制迁入 DebugGuiDriver。
- 2026-06-08：同步 Debug ProfileHandle 硬化现状，记录 profile metadata/snapshot 异常隔离、redaction 安全字符串化和内建 Runtime/Debug 状态 profile。
- 2026-06-08：同步 Timer 显式 update handle 现状，记录 Update/LateUpdate/FixedUpdate 三类 tick、TimerHandle 基类瘦身、UpdateTimerHandle/LateUpdateTimerHandle/FixedUpdateTimerHandle 和 TimerSnapshot.Updates。
- 2026-06-07：补充 Timer 与 Debug 子系统现状，记录 Timer 自有 fixed tick、Delay/Countdown/Interval、scaled/unscaled 语义、Debug log/profile/console/timers tab 和默认启动顺序。
- 2026-06-07：补充 Combat 子系统现状，记录默认 world、Entity/Component/System/Queryable 生命周期、固定步、rollback、dispose 封口、系统回调重入和组件变更匹配索引。
- 2026-05-27：同步 Resource 模块现状，更新 provider 命名、Web 模式 provider 接线状态，以及 Editor API 在 Runtime 中由 `#if UNITY_EDITOR` 保护且可接受的边界。
- 2026-05-27：同步 Resource 审计修复后的现状，记录 settings/manifest 自举、WebAssetProvider 接线、ProviderBase 加载编排上移、卸载调度集中到 ResourceModule，以及当前 StreamingAssets 清单仍需生成侧同步为 `ManifestInfo` 格式。
