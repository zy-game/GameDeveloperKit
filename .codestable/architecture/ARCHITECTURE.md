# Black Rain 架构总入口

> 状态：骨架（待填充）
> 创建日期：2026-05-17
> 最近审阅：2026-05-27

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

入口：`EventModule`（`Assets/GameDeveloperKit/Runtime/Event/EventModule.cs`），实现 `IGameModule`，通过 `Super.Event` 访问。

核心类型：
- `IEventArgs` — 事件数据公共契约，提供 `Use()` / `HasUse()` 用于消费事件并停止后续派发。
- `IEventHandle` / `IEventHandle<TEvent>` — 事件处理器契约，统一接收 `sender` 和事件数据。
- `Subscription` — 一次订阅返回的取消句柄，支持 `Cancel()` / `Release()` 取消订阅。
- `BindingAttribute` — source generator 使用的事件绑定标记，用于自动订阅 handle 和生成事件扩展方法。

派发策略：
- `Subscribe<THandle>(handle)` 根据 handle 实现的 `IEventHandle<TEvent>` 识别事件类型并订阅。
- `Subscribe<TEvent>(Action<TEvent>)` 提供轻量委托订阅。
- `Fire<TEvent>(eventData, sender)` 立即同步派发，不入队、不延迟；派发时使用 listener 快照，订阅/取消不会破坏当前枚举。
- 任一 handle 调用 `eventData.Use()` 后，后续 handle 不再收到本轮事件。

关键行为：支持强类型订阅、委托订阅、取消订阅、重复订阅去重、同步派发、事件消费中断和 source generator 自动绑定；不提供异步事件、事件队列、跨进程/网络事件或编辑器可视化面板。

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

## 4. 关键架构决定

## 5. 已知约束 / 硬边界

- **CRC32 数据完整性**：FileModule.ReadFileAsync 读取后强制 CRC32 校验，不匹配抛 `GameException`
- **幂等写入**：同路径多次 WriteFileAsync 为覆盖更新，Manifest 仅保留最新条目
- **根目录固定**：`Application.persistentDataPath + "/vfs"`，不可配置
- **首版不做并发控制**：所有公开 API 假定在主线程调用
- **下载临时落盘**：DownloadModule 只写 `Application.temporaryCachePath + "/downloads"`，不主动写入 FileModule
- **下载恢复语义**：暂停和失败保留 temp / `.part`；取消和 CancelAll 删除 temp / `.part`
- **批量下载失败继续**：DownloadListHandler 中单项 Failed 不阻断后续下载
- **事件模块同步派发**：EventModule 公开 API 假定主线程调用；`Fire<TEvent>` 使用 listener 快照同步派发，直到事件被 `Use()` 消费或 listener 列表结束
- **事件模块无异步/队列语义**：EventModule 不提供异步事件、事件队列、优先级、限流或跨线程并发安全承诺
- **OperationModule 不提供调度语义**：OperationModule 只管理运行中 operation 的登记、等待、终态回写和关闭清理，不提供队列、优先级、重试、调度线程或线程安全承诺；公开 API 假定 Unity 主线程调用
- **资源清单当前根类型**：资源模块当前代码使用 `ManifestInfo` / `PackageInfo` / `BundleInfo` / `AssetInfo`，不要在新方案里继续引用未落地的 `ResourceManifest` / `ResourceBundleInfo` / `ResourceAssetInfo`
- **资源 Mode / Provider 当前抽象**：资源模块当前代码使用 `ModeBase` / `ProviderBase`，不要在实现计划里继续把未落地的 `IResourcePlayMode` / `IResourceProvider` 当作现状
- **资源 Provider 不持有 Manifest**：Provider 只负责自己 `BundleInfo` 内的资源操作；跨 package / bundle 查询属于 Mode 或 Manifest 层
- **资源句柄释放语义**：`ResourceHandle<T>.Release()` 清空 `Info` / `Error`；`AssetHandle.Release()` 额外清空 `Asset`；`BundleHandle.Release()` 会 `AssetBundle.Unload(true)`
- **BundleMode / WebGLMode provider operation 已最小闭环**：`OperationModule`、`ManifestInfo.GetBundle()` / `GetDependencies()`、BundleMode package operation 与 BundleAssetProvider bundle/loading operation、WebGLMode 与 WebAssetProvider 接线已补齐；未覆盖 Builtin / StreamingAssets / EditorSimulator 的全部真实加载差异
- **BundleInfo.Name 当前兼任加载定位**：BundleMode 以 `BundleInfo.Name` 作为 AssetBundle 本地路径或 URI；后续如需下载缓存、CRC、远端 URL 策略，应新增显式字段或解析服务
- **EditorSimulator 允许在 Runtime 目录**：`EditorSimulatorMode` / `EditorAssetProvider` 当前位于 Runtime Resource 目录；`UnityEditor` API 调用必须由 `#if UNITY_EDITOR` 包裹，Player 路径不得直接引用 `UnityEditor`，非编辑器路径需要明确抛错；不要求仅因使用受保护的 Editor API 移到 Editor-only asmdef
- **在线资源模式当前命名**：当前没有独立 `HostingPlayMode`；`ResourceMode.Online` 对应 `BundleMode`

## 6. 变更日志

- 2026-05-27：同步 Resource 模块现状，更新 provider 命名、Web 模式 provider 接线状态，以及 Editor API 在 Runtime 中由 `#if UNITY_EDITOR` 保护且可接受的边界。
- 2026-05-27：同步 Resource 审计修复后的现状，记录 settings/manifest 自举、WebAssetProvider 接线、ProviderBase 加载编排上移、卸载调度集中到 ResourceModule，以及当前 StreamingAssets 清单仍需生成侧同步为 `ManifestInfo` 格式。
