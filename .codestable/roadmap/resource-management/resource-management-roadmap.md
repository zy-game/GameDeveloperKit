---
doc_type: roadmap
slug: resource-management
status: active
created: 2026-05-20
last_reviewed: 2026-05-24
tags: [resource, assetbundle, manifest, unity]
related_requirements: []
related_architecture: [ARCHITECTURE]
---

# Resource Management

## 1. 背景

当前资源模块已经按源码实现调整为一套运行时资源框架：业务侧通过 `Super.Resource` 访问 `ResourceModule`，`ResourceModule` 根据 `ResourceSettings.Mode` 创建并持有多个 `ModeBase`，具体模式再通过 `ProviderBase` 执行 bundle/asset 加载。资源索引采用 `ManifestInfo -> PackageInfo -> BundleInfo -> AssetInfo`，加载结果由 `ResourceHandle<T>` 及其派生句柄承载，异步编排统一挂到 `OperationHandle` / `OperationModule`。

本 roadmap 以当前代码为准，替换旧方案中的 `ResourceManifest`、`IResourcePlayMode`、`IResourceProvider`、`ResourcesPlayMode`、`HostingPlayMode` 等未落地口径。

## 2. 范围与明确不做

### 本 roadmap 覆盖

- 资源清单数据模型：`ManifestInfo`、`PackageInfo`、`BundleInfo`、`AssetInfo`。
- 资源句柄：`ResourceHandle<T>`、`ResourceHandle`、`AssetHandle`、`RawAssetHandle`、`SceneAssetHandle`、`BundleHandle`。
- `ResourceModule` 门面、`Super.Resource` 入口、`ResourceSettings` 驱动的 mode 创建和 default package 初始化。
- `ModeBase` 抽象与当前五种 mode：
  - `BuiltinMode`：内置 package，默认 package 名为 `BUILTIN`。
  - `StreamingAssetMode`：`ResourceMode.Offline` 对应的 StreamingAssets/bundle 模式。
  - `BundleMode`：`ResourceMode.Online` 对应的在线 bundle 模式。
  - `WebGLMode`：`ResourceMode.Web` 对应的 WebGL 资源模式。
  - `EditorSimulatorMode`：`ResourceMode.EditorSimulator` 对应的编辑器模拟模式。
- `ProviderBase` 抽象与当前三种 provider：`BuiltinProvider`、`BundleProvider`、`EditorProvider`。
- 资源与 package 相关 `OperationHandle` 类型：manifest、package、bundle、asset/raw/scene loading。

### 明确不做

- 不再把未落地的 `IResourcePlayMode` / `IResourceProvider` 接口作为硬约束；当前代码权威抽象是 `ModeBase` / `ProviderBase`。
- 不再把 `ResourceManifest` / `ResourceBundleInfo` / `ResourceAssetInfo` 作为实际类型名；当前类型名是 `ManifestInfo` / `BundleInfo` / `AssetInfo`，并引入 `PackageInfo`。
- 不再声明 `HostingPlayMode` 或远端缓存 provider 已存在；当前 `ResourceMode.Online` 由 `BundleMode` 承载。
- 不把 Unity Editor-only API 接入 Runtime。当前 `EditorProvider` 的 editor loading operation 仍未实现，方案只记录现状。
- 不把 FileSystem 的 `.vfsb` 自定义 bundle 与 Unity `AssetBundle` 混用。
- 不在本 roadmap 内实现打包 Editor 工具、资源引用计数可视化窗口或 Addressables 兼容层。

## 3. 模块拆分（当前实现）

```text
Resource Management
├── Manifest Model：ManifestInfo / PackageInfo / BundleInfo / AssetInfo
├── Handles：ResourceHandle<T> / AssetHandle / RawAssetHandle / SceneAssetHandle / BundleHandle
├── ResourceModule：Super.Resource 入口、settings、mode 列表和 API 分发
├── Modes：ModeBase + BuiltinMode / StreamingAssetMode / BundleMode / WebGLMode / EditorSimulatorMode
├── Providers：ProviderBase + BuiltinProvider / BundleProvider / EditorProvider
└── Operations：Manifest/package/bundle/asset/raw/scene OperationHandle
```

### Manifest Model

- **职责**：表达 package、bundle、asset 三层资源索引。`ManifestInfo.Packages` 是根集合；`PackageInfo.Bundles` 包含 bundle；`BundleInfo.Assets` 包含可加载资源。
- **当前代码**：`Assets/GameDeveloperKit/Runtime/Resource/Manifest/`
- **当前行为**：`ManifestInfo.GetBundle()` 和 `GetDependencies()` 已按 `Packages[*].Bundles` 补齐，可供 package operation 查询 bundle 与依赖。

### Handles

- **职责**：承载加载结果、错误和释放语义。
- **当前代码**：`Assets/GameDeveloperKit/Runtime/Resource/Handle/`
- **当前关系**：
  - `ResourceHandle<T>` 保存 `Info` 与 `Error`，`IsValid` 由 `Error == null` 得出。
  - `ResourceHandle : ResourceHandle<AssetInfo>`。
  - `AssetHandle : ResourceHandle`，保存 `UnityEngine.Object Asset`。
  - `RawAssetHandle : ResourceHandle`，保存 `byte[] Data`，提供 `GetString()`。
  - `SceneAssetHandle : ResourceHandle`，保存 `UnityEngine.SceneManagement.Scene Asset`，提供 `Active()`。
  - `BundleHandle : ResourceHandle<BundleInfo>`，保存 `AssetBundle Asset`。

### ResourceModule

- **职责**：实现 `GameModuleBase`，通过 `Super.Resource` 暴露资源 API，维护 `_manifest`、`_setting` 和 `List<ModeBase>`。
- **当前代码**：`Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs`、`Assets/GameDeveloperKit/Runtime/Super.cs`
- **当前限制**：`Startup()` 目前先调用 `LoadAssetAsync("Resources/ResourceSettings")`，但此时 `modes` 尚未初始化；随后又把 `_setting` 传给 `ResourceModule.ManifestOperationHandle`，存在启动顺序未闭环风险。

### Modes

- **职责**：`ModeBase` 统一定义 package 生命周期、资源查询、asset/raw/scene 加载、卸载和释放。具体 mode 持有 provider 列表并按 provider 查询资源。
- **当前代码**：`Assets/GameDeveloperKit/Runtime/Resource/PlayMode/`
- **当前模式**：
  - `BuiltinMode` 单 provider，初始化 `BUILTIN` package。
  - `StreamingAssetMode`、`BundleMode`、`WebGLMode`、`EditorSimulatorMode` 当前结构基本一致：持有 `List<ProviderBase>`，package 初始化/反初始化委托 operation，加载时遍历 provider。

### Providers

- **职责**：`ProviderBase` 是 bundle 级资源操作抽象，持有 `BundleInfo Info`，按 `Info.Assets` 内的 `Location` / `TypeName` / `Labels` 查询资源，并委托 loading operation 产出 handle。
- **当前代码**：`Assets/GameDeveloperKit/Runtime/Resource/Provider/`
- **当前 provider**：
  - `BuiltinProvider`：不加载 AssetBundle，直接通过 builtin loading operation 生成 asset/raw/scene handle。
  - `BundleProvider`：初始化 `BundleHandle` 后通过普通 loading operation 从 bundle 中加载 asset/raw/scene，并缓存已加载 handle。
  - `EditorProvider`：结构接近 `BundleProvider`，但 editor loading operation 仍是 `NotImplementedException`。

### Operations

- **职责**：资源模块把 manifest 下载/解析、package 初始化、bundle 初始化、asset/raw/scene 加载都挂到 `OperationHandle`。
- **当前代码**：`Assets/GameDeveloperKit/Runtime/Resource/`、`Assets/GameDeveloperKit/Runtime/Resource/PlayMode/`、`Assets/GameDeveloperKit/Runtime/Resource/Provider/`、`Assets/GameDeveloperKit/Runtime/OperationModule/`
- **当前行为**：`OperationModule.Execute()` / `WaitCompletionAsync()` 已具备最小创建、执行、等待和错误传播语义。Play mode 承载 package lifecycle operation，例如 `BundleMode.InitializePackageOperationHandle` / `BundleMode.UninitializePackageOperationHandle`；provider 承载 bundle lifecycle 与 loading operation，例如 `BundleProvider.InitializeBundleOperationHandle` / `BundleProvider.UninitializeBundleOperationHandle`、`BundleProvider.LoadingAssetOperationHandle` / raw / scene。Builtin loading 仍是 no-op，EditorProvider 的 editor loading 仍未实现。

## 4. 模块间接口契约 / 共享协议

### 4.1 ResourceModule API

业务代码通过 `Super.Resource` 调用以下实际 API：

```csharp
public sealed class ResourceModule : GameModuleBase
{
    public UniTask<OperationHandle> InitializePackageAsync(string package);
    public UniTask<OperationHandle> UninitializePackageAsync(string package);

    public UniTask<AssetHandle> LoadAssetAsync(string location);
    public UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label);
    public UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByTypeAsync<T>() where T : UnityEngine.Object;

    public UniTask<RawAssetHandle> LoadRawAssetAsync(string location);
    public UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label);

    public UniTask<SceneAssetHandle> LoadSceneAssetAsync(string name);

    public UniTask UnloadUnusedAssetAsync();
    public UniTask UnloadAsset(AssetHandle handle);
}
```

约束：

- `location` / `label` / `name` / `package` 为 null 抛 `ArgumentNullException`，为空白抛 `ArgumentException`。
- `UnloadAsset(null)` 抛 `ArgumentNullException`。
- `modes.Count == 0` 时资源 API 抛 `GameException("No resource play mode is available.")`。
- 单资源加载通过 `modes.FirstOrDefault(x => x.HasAsset(key))` 选择 mode。
- package 初始化通过 `ResourceSettings.Mode` 选择当前配置 mode，不对所有 mode 广播。

### 4.2 ModeBase Contract

```csharp
public abstract class ModeBase : IReference
{
    public ManifestInfo Manifest { get; }

    public abstract bool HasAsset(string location);
    public abstract bool HasPackage(string package);
    public abstract UniTask<OperationHandle> InitializePackageAsync(string package);
    public abstract UniTask<OperationHandle> UninitializePackageAsync(string package);
    public abstract UniTask<AssetHandle> LoadAssetAsync(string location);
    public abstract UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label);
    public abstract UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByTypeAsync<T>() where T : UnityEngine.Object;
    public abstract UniTask<RawAssetHandle> LoadRawAssetAsync(string location);
    public abstract UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label);
    public abstract UniTask<SceneAssetHandle> LoadSceneAssetAsync(string name);
    public abstract UniTask UnloadUnusedAssetAsync();
    public abstract UniTask UnloadAsset(AssetHandle handle);
    public abstract void Release();
}
```

约束：

- Mode 持有 manifest，但当前大部分查询仍直接委托 provider 的 `HasAsset()`。
- Mode 的 provider 列表是运行期 package 初始化结果；provider 初始化失败时不应进入列表。
- `HasPackage(package)` 当前按 `provider.Info.Name == package` 判断，这要求 package 与 bundle name 的语义在后续实现中继续收紧。

### 4.3 ProviderBase Contract

```csharp
public abstract class ProviderBase
{
    public BundleInfo Info { get; }

    public abstract UniTask<OperationHandle<BundleHandle>> InitializeProviderAsync();
    public abstract UniTask<OperationHandle> UninitializeProviderAsync();
    public abstract bool HasAsset(string location);
    public abstract UniTask<AssetHandle> LoadAssetAsync(string location);
    public abstract UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label);
    public abstract UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByTypeAsync<T>() where T : UnityEngine.Object;
    public abstract UniTask<RawAssetHandle> LoadRawAssetAsync(string location);
    public abstract UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label);
    public abstract UniTask<SceneAssetHandle> LoadSceneAssetAsync(string name);
    public abstract UniTask UnloadUnusedAssetAsync();
    public abstract UniTask UnloadAsset(AssetHandle handle);
    public virtual void Release();
}
```

约束：

- Provider 不接收 `ManifestInfo`；它只操作构造时传入的 `BundleInfo`。
- `HasAsset(key)` 当前同时匹配 `AssetInfo.Location`、`AssetInfo.TypeName` 和 `AssetInfo.Labels`。
- Provider 内部维护已加载 `_assets` 与待卸载列表，重复加载同一个 `AssetInfo` 时优先复用 handle。

### 4.4 Manifest Model

```csharp
public sealed class ManifestInfo
{
    public string Version;
    public long BuildTime;
    public List<PackageInfo> Packages = new List<PackageInfo>();

    public BundleInfo GetBundle(string bundleName);
    public IReadOnlyList<BundleInfo> GetDependencies(string bundleName);
}

public sealed class PackageInfo
{
    public string Name;
    public string Version;
    public string Hash;
    public List<BundleInfo> Bundles = new List<BundleInfo>();
}

public sealed class BundleInfo
{
    public string Name;
    public string Hash;
    public long Size;
    public uint Crc;
    public string Version;
    public List<AssetInfo> Assets;
    public List<string> Dependencies;
}

public sealed class AssetInfo
{
    public string Location;
    public string TypeName;
    public List<string> Labels;
}
```

字段语义：

- `PackageInfo.Name` 是业务 package 名。
- `BundleInfo.Name` 是 bundle/provider 身份。
- `AssetInfo.Location` 是业务加载地址，也是 provider 查找资源的主键。
- `AssetInfo.TypeName` 和 `Labels` 也会被 `HasAsset()` 当作批量查询入口使用。

## 5. 子 feature 清单

1. **resource-manifest-model** — 代码落地为 `ManifestInfo` / `PackageInfo` / `BundleInfo` / `AssetInfo`，表达 package -> bundle -> asset 清单。
   - 所属模块：Manifest Model
   - 依赖：无
   - 状态：done
   - 对应 feature：2026-05-20-resource-manifest-model

2. **resource-handle-core** — 代码落地为资源、asset、raw、scene、bundle 句柄，当前通过 `Error` 判断有效性。
   - 所属模块：Handles
   - 依赖：无
   - 状态：done
   - 对应 feature：2026-05-20-resource-handle-core

3. **resource-playmode-provider-contract** — 代码落地为 `ModeBase` / `ProviderBase` 与 package/bundle operation handle，而不是接口契约。
   - 所属模块：Modes / Providers / Operations
   - 依赖：resource-manifest-model, resource-handle-core
   - 状态：done
   - 对应 feature：2026-05-21-resource-playmode-provider-contract

4. **resource-module-api** — 代码落地为 `ResourceModule`、`Super.Resource`、`ResourceSettings` 驱动的 mode 创建与 API 分发。
   - 所属模块：ResourceModule
   - 依赖：resource-playmode-provider-contract
   - 状态：done
   - 对应 feature：2026-05-21-resource-module-api

5. **resources-playmode** — 旧 roadmap 名称；当前代码落地为固定 `BuiltinMode` / `BuiltinProvider`，使用 `BUILTIN` package 承载内置资源。
   - 所属模块：Modes / Providers
   - 依赖：resource-module-api
   - 状态：done
   - 对应 feature：2026-05-22-resource-playmodes

6. **offline-playmode** — 代码落地为 `StreamingAssetMode`，通过 provider 列表承接 StreamingAssets/bundle 路径。
   - 所属模块：Modes / Providers
   - 依赖：resource-module-api
   - 状态：done
   - 对应 feature：2026-05-22-resource-playmodes

7. **editor-simulator-playmode** — 代码落地为 `EditorSimulatorMode` / `EditorProvider`，但 editor loading operation 仍未实现。
   - 所属模块：Modes / Providers / Operations
   - 依赖：resource-module-api
   - 状态：done
   - 对应 feature：2026-05-22-resource-playmodes

8. **hosting-playmode** — 旧 roadmap 名称；当前代码没有独立 Hosting mode，在线模式由 `BundleMode` 承载。
   - 所属模块：Modes / Providers
   - 依赖：offline-playmode
   - 状态：done
   - 对应 feature：2026-05-22-resource-playmodes

9. **web-playmode** — 代码落地为 `WebGLMode`，结构与其他 provider-list mode 一致。
   - 所属模块：Modes / Providers
   - 依赖：resource-module-api
   - 状态：done
   - 对应 feature：2026-05-22-resource-playmodes

10. **bundlemode** — 补齐 `ResourceMode.Online` 对应的 `BundleMode` / `BundleProvider` / bundle operation 编排。
    - 所属模块：Modes / Providers / Operations
    - 依赖：resource-module-api
    - 状态：done
    - 对应 feature：2026-05-24-bundlemode

## 6. 当前主流程

```mermaid
flowchart TD
    Register["Super.Register<ResourceModule>()"]
    Startup["ResourceModule.Startup()"]
    LoadSettings["LoadAssetAsync(\"Resources/ResourceSettings\")"]
    ManifestOp["ResourceModule.ManifestOperationHandle downloads/parses ManifestInfo"]
    Modes["Add StreamingAssetMode + BuiltinMode + configured mode"]
    Builtin["Initialize BUILTIN package"]
    Defaults["Initialize ResourceSettings.DefaultPackages"]
    Load["LoadAssetAsync(location)"]
    SelectMode["Find first mode where HasAsset(location)"]
    Provider["Mode finds provider where HasAsset(location)"]
    Operation["Provider starts loading OperationHandle"]
    Handle["Return AssetHandle / RawAssetHandle / SceneAssetHandle"]

    Register --> Startup --> LoadSettings --> ManifestOp --> Modes --> Builtin --> Defaults
    Load --> SelectMode --> Provider --> Operation --> Handle
```

注意：这是当前代码意图和结构。BundleMode 的 operation 链路已补齐，但 `ResourceModule.Startup()` 的启动顺序和 Builtin / EditorSimulator 等其他模式的真实 loading operation 仍未完全实现，因此资源模块整体不应被文档表述为全模式端到端可运行。

## 7. 已知实现差距

- `ResourceModule.Startup()` 在 mode 创建前调用 `LoadAssetAsync("Resources/ResourceSettings")`，而 `LoadAssetAsync` 会因 `modes.Count == 0` 抛错。
- `ResourceModule.ManifestOperationHandle` 期望第一个参数是 URL string，但 `ResourceModule.Startup()` 当前传入 `_setting`，且 `_setting` 此时尚未赋值。
- Builtin loading operation 的 `Execute()` 仍为空，BuiltinMode 还不能通过 operation 真实加载 asset/raw/scene。
- StreamingAssetMode / WebGLMode / EditorSimulatorMode 的 package operation 已传入 `ManifestInfo`；bundle lifecycle 与 loading operation 当前归属具体 provider，三者的真实 loading 语义仍与 BundleMode 不同。
- StreamingAssetMode / WebGLMode / EditorSimulatorMode 的 `LoadAssetsByLabelAsync(label)` 仍调用单资源 API，批量语义仍需修正。
- `EditorProvider` 的 `_assets` / `_pendingUnloadAssets` 已在构造函数初始化，但 editor loading operation 仍尚未实现。
- BundleMode 当前以 `BundleInfo.Name` 作为 AssetBundle 本地路径或 URI，尚无显式远端 URL、缓存路径或 CRC 下载策略字段。

## 8. 排期思路

后续优先顺序应以打通最窄运行闭环为准：

1. 修正 `ResourceModule.Startup()` 的 settings/manifest 加载顺序。
2. 补 Builtin loading operation，让 `BuiltinMode` 返回有效 asset/raw/scene handle。
3. 把 StreamingAssets/WebGL/EditorSimulator 的 package operation 参数和批量 label 语义对齐 BundleMode。
4. 为 BundleMode 引入显式远端 URL、缓存路径、CRC 或下载编排策略。
5. 再处理 EditorSimulator 的 Editor-only 隔离和平台差异。

## 9. 观察项

- 如果后续希望恢复接口式契约，可以另起 refactor 把 `ModeBase` / `ProviderBase` 抽象为接口；当前 roadmap 不再把旧接口作为要求。
- 当前 `RawAssetHandle` 和 `SceneAssetHandle` 不继承 `AssetHandle`，这与旧 design 不一致，但符合当前代码；若希望统一卸载类型，需要另起行为兼容评估。
- 当前 `ResourceStatus` 枚举存在但未被 handle 或 operation 使用，后续可评估删除、接入或改名。
