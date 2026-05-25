# bundlemode 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-05-24
> 关联方案 doc：`.codestable/features/2026-05-24-bundlemode/bundlemode-design.md`

## 1. 接口契约核对

对照方案第 2.1 节名词层逐一核查：

**名词层“设计时现状 → 变化”逐项核对**：
- [x] `ResourceMode.Online`：仍由 `ResourceModule.CreateModeByType(ResourceMode.Online)` 创建 `BundleMode`，未新增 OnlineMode。
- [x] `BundleMode`：继续持有 `_providers` 并按 provider 分发；`InitializePackageAsync` / `UninitializePackageAsync` 已把 `Manifest` 传入 package operation。
- [x] `BundleProvider`：继续作为实际 bundle provider；初始化时拿到 `BundleHandle`，asset/raw/scene 加载通过对应 loading operation。
- [x] `ManifestInfo.GetBundle()` / `GetDependencies()`：已按 `Packages[*].Bundles` 查询 bundle 和依赖。
- [x] `InitializePackageOperationHandle`：已按 package 解析 bundle 与递归依赖，创建并初始化 `BundleProvider`，失败时回滚本次新增 provider。
- [x] `UninitializePackageOperationHandle`：已释放并移除目标 package 及其递归依赖对应 provider。
- [x] `InitializeBundleOperationHandle`：已按 `BundleInfo.Name` 作为本地路径或 URI 加载 AssetBundle，并返回 `BundleHandle` 或可观察失败。
- [x] `LoadingAssetOperationHandle` / `LoadingRawAssetOperationHandle` / `LoadingSceneAssetOperationHandle`：已从 `BundleHandle.Asset` 产出对应 handle 或错误。
- [x] `OperationModule`：已补最小 `Execute<T>()` / `WaitCompletionAsync<T>()` 语义，可创建 operation、调用 `Execute(args)`、等待 completion。

**流程图核对**：
- [x] `ResourceModule / InitializePackageAsync → BundleMode → InitializePackageOperationHandle → ManifestInfo → BundleProvider → InitializeBundleOperationHandle → register provider` 均有代码落点。
- [x] `BundleMode.LoadAssetAsync → provider.HasAsset → BundleProvider → Loading*OperationHandle → AssetHandle / RawAssetHandle / SceneAssetHandle` 均有代码落点。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] `BundleMode.InitializePackageAsync(package)` 能通过 package operation 创建并注册 `BundleProvider`；失败 provider 不进入 `_providers`，本次新增 provider 会回滚。
- [x] `BundleProvider.InitializeProviderAsync()` 能通过 `InitializeBundleOperationHandle` 得到 `BundleHandle`，失败时返回 failed operation。
- [x] `BundleProvider.LoadAssetAsync` / raw / scene 通过 loading operation 返回成功 handle 或 failed handle。
- [x] package、bundle、asset 加载失败时错误进入 operation 或 handle，不静默返回成功。

**明确不做逐项核对**：
- [x] 未新增 `BundleResourceProvider`、`BundleOperationHandle`、`ResourceManifest`、`AssetInfo.BundleName`、`AssetInfo.AssetPath` 等旧方案类型 / 字段。
- [x] 未实现 `StreamingAssetMode`、`WebGLMode`、`EditorSimulatorMode` 的真实加载逻辑。
- [x] 未引入 Addressables，未改变 Unity SBP 打包流程。
- [x] 未把 `DownloadModule` 改成 BundleMode 下载编排器。
- [x] 未修改 FileSystem `.vfsb` / `Vfs*` 语义。

**关键决策落地**：
- [x] 决策 1：`BundleMode` 只负责 provider 列表和请求分发；直接 AssetBundle API 只出现在 `InitializeBundleOperationHandle`。
- [x] 决策 2：package 初始化由 `InitializePackageOperationHandle` 承担；`BundleMode` 只校验参数并调 operation。
- [x] 决策 3：provider 只拿 `BundleInfo`，不拿 `ManifestInfo`；跨 package / bundle 查询在 package operation 中完成。
- [x] 决策 4：asset 路由不依赖 `BundleName`；provider 通过 `Location` / `TypeName` / `Labels` 命中。
- [x] 决策 5：本次只补 operation 闭环；未做结构重组或新增远端缓存抽象。

**流程级约束核对**：
- [x] `BundleMode.InitializePackageAsync(package)` 对 null/空白 package 抛参数异常；`BUILTIN` 直接返回失败 operation。
- [x] `BundleMode.UninitializePackageAsync(package)` 对 null/空白 package 抛参数异常；未命中 package 返回失败 operation。
- [x] `BundleProvider.InitializeProviderAsync()` 在 `Info == null` 或 bundle operation 失败时返回失败 operation。
- [x] `BundleProvider.LoadAssetAsync(location)` 在参数为空、`Info` 为空、bundle 未初始化、asset 不存在或 loading operation 失败时返回 failed `AssetHandle`。
- [x] `BundleProvider.UnloadAsset(handle)` 对 null handle 抛 `ArgumentNullException`，命中 `_assets` 后转入 `_pendingUnloadAssets` 并 `Release()`。
- [x] label 批量已改为 `provider.LoadAssetsByLabelAsync(label)`；raw label 批量已改为 `provider.LoadRawAssetsByLabelAsync(label)`。
- [x] `BundleMode.HasPackage(package)` 已按 manifest 的 package bundles 判断，避免把 package 名和 bundle 名直接等同。

**挂载点反向核对**：
- [x] `ResourceModule.CreateModeByType()` / `GetModeByType()`：Online 仍挂到 `BundleMode`。
- [x] `BundleMode.InitializePackageAsync` / `UninitializePackageAsync`：Online package 生命周期入口已传入 `Manifest`。
- [x] `InitializePackageOperationHandle` / `UninitializePackageOperationHandle`：package 到 provider 列表的创建、初始化、释放和移除编排点。
- [x] `OperationModule` / `OperationHandle`：最小执行、等待和错误传播入口。
- [x] `BundleProvider`：bundle 级 AssetBundle provider。
- [x] `InitializeBundleOperationHandle` / `UninitializeBundleOperationHandle` / `Loading*OperationHandle`：真实 bundle 和 asset 加载 / 释放节点。
- [x] 反向 grep：本 feature 的运行时代码引用都落在挂载点清单内；额外命中 `EditorProvider` / `BuiltinProvider` 为既有同名 operation 契约，不属于本次实现。
- [x] 拔除沙盘推演：回退上述挂载点和 checklist / architecture / roadmap / acceptance 产物后，本 feature 可整体移除；其他 mode 的既有空实现独立存在。

## 3. 验收场景核对

对照方案第 3 节关键场景清单：

- [x] **N1**：manifest 含 package、bundle、asset 时，`BundleMode.InitializePackageAsync(package)` 成功注册 provider。
  - 证据来源：代码审查 + runtime 编译；package operation 按 package bundles 创建 `BundleProvider`，初始化成功才加入 `_providers`。
  - 结果：通过。
- [x] **N2**：provider 已初始化后 `LoadAssetAsync(location)` 返回有效 `AssetHandle`。
  - 证据来源：代码审查 + runtime 编译；`LoadingAssetOperationHandle` 调 `bundle.Asset.LoadAsset(location)` 并返回 `AssetHandle.Success`。
  - 结果：通过。仓库无现成 AssetBundle 测试资产，未做 Unity 运行时集成测试。
- [x] **N3**：label/type 批量加载返回所有命中 handle。
  - 证据来源：代码审查 + runtime 编译；BundleMode label 转发到 provider 批量 API，type 转发到 `LoadAssetsByTypeAsync<T>()`。
  - 结果：通过。
- [x] **N4**：`LoadRawAssetAsync(location)` 返回 `RawAssetHandle`，失败时错误可读。
  - 证据来源：代码审查 + runtime 编译；raw operation 加载 `TextAsset` 并用 `textAsset.bytes` 创建 handle，失败写 `GameException`。
  - 结果：通过。未做真实 AssetBundle 集成测试。
- [x] **N5**：`LoadSceneAssetAsync(name)` 返回 `SceneAssetHandle`，失败时错误可读。
  - 证据来源：代码审查 + runtime 编译；scene operation additive 加载场景并返回 `SceneAssetHandle.Success`，失败写 `GameException`。
  - 结果：通过。未做真实 AssetBundle 集成测试。
- [x] **N6**：重复初始化同一 package 不重复创建 provider。
  - 证据来源：代码审查；package operation 在创建前检查 `providers.Any(x => x.Info.Name == bundle.Name)`。
  - 结果：通过。
- [x] **N7**：反初始化 package 后 provider 从列表移除。
  - 证据来源：代码审查 + runtime 编译；反初始化 operation 释放并 `providers.Remove(provider)`，并覆盖递归依赖。
  - 结果：通过。
- [x] **B1**：依赖 bundle 未初始化时先处理依赖；依赖失败时主 bundle 不注册。
  - 证据来源：代码审查；`AddBundleWithDependencies` 先递归依赖再加入主 bundle，依赖缺失抛 `GameException`，初始化失败会回滚。
  - 结果：通过。
- [x] **B2**：bundle 或 asset 不存在时不返回成功 handle。
  - 证据来源：代码审查；bundle 文件不存在、AssetBundle 加载失败、asset/raw/scene 加载失败都会 `SetException` 或返回 failed handle。
  - 结果：通过。
- [x] **E1**：package 为 null 或空白时抛 `ArgumentNullException` / `ArgumentException`。
  - 证据来源：代码审查；BundleMode 与 package operation 均有参数校验。
  - 结果：通过。
- [x] **E2**：bundle 路径无效时 `InitializeBundleOperationHandle` 失败且 provider 不注册。
  - 证据来源：代码审查；本地路径不存在时 `SetException`，package operation 看到失败后不加入 provider 并回滚。
  - 结果：通过。
- [x] **E3**：loading operation 失败时 `BundleProvider` 返回 failed handle。
  - 证据来源：代码审查；asset/raw/scene operation 失败后 provider 使用 `AssetHandle.Failure` / `RawAssetHandle.Failure` / `SceneAssetHandle.Failure`。
  - 结果：通过。

验证命令：
- [x] `dotnet build GameDeveloperKit.Runtime.csproj --no-restore`：通过，0 errors，2 warnings。warning 来自既有 `EditorProvider._assets` / `_pendingUnloadAssets` 未赋值，属于 EditorSimulator 范围。
- [x] `python .codestable/tools/validate-yaml.py --file .codestable/roadmap/resource-management/resource-management-items.yaml --yaml-only`：通过。
- [x] `python .codestable/tools/validate-yaml.py --file .codestable/features/2026-05-24-bundlemode/bundlemode-design.md`：通过。

## 4. 术语一致性

对照方案第 0 节 + 第 2.1 节命名 grep 代码：

- `BundleMode`：Online 模式命名一致，未新增 OnlineMode / HostingPlayMode。
- `BundleProvider`：实际 provider 命名一致，未新增 `BundleResourceProvider`。
- `ManifestInfo`：实际清单根类型一致，未新增 `ResourceManifest`。
- `AssetInfo.Location` / `TypeName` / `Labels`：provider 查询仍使用这些字段，未新增 `BundleName` / `AssetPath` / `IsScene` / `SceneName`。
- `OperationHandle`：继续使用现有 operation 体系，未新增 `BundleOperationHandle`。
- 防冲突：禁用词在本 feature design/checklist 中作为反向核对文本出现；运行时代码未新增旧方案类型或字段。

## 5. 架构归并

对照方案第 4 节：

- [x] 架构 doc `.codestable/architecture/ARCHITECTURE.md`：已写入 `ManifestInfo.GetBundle()` / `GetDependencies()` 当前行为。
- [x] 架构 doc `.codestable/architecture/ARCHITECTURE.md`：已写入 `OperationModule` 的最小执行 / 等待语义。
- [x] 架构 doc `.codestable/architecture/ARCHITECTURE.md`：已写入 BundleMode package -> provider -> bundle -> asset/raw/scene operation 链路。
- [x] 架构 doc `.codestable/architecture/ARCHITECTURE.md`：已写入剩余边界：`ResourceModule.Startup()` 顺序、Builtin loading operation、EditorSimulator editor loading operation、`BundleInfo.Name` 兼任路径 / URI。

## 6. requirement 回写

- [x] `requirement` 为空，且本 feature 来源为 `resource-management` roadmap 的内部技术能力切片；不新增独立 requirement 文档，跳过 requirement 回写。

## 7. roadmap 回写

- [x] `roadmap: resource-management` / `roadmap_item: bundlemode` 均有值。
- [x] `.codestable/roadmap/resource-management/resource-management-items.yaml` 中 `bundlemode` 已从 `in-progress` 改为 `done`，feature 保持 `2026-05-24-bundlemode`。
- [x] `.codestable/roadmap/resource-management/resource-management-roadmap.md` 第 5 节子 feature 清单已同步为 `状态：done`。
- [x] `.codestable/roadmap/resource-management/resource-management-roadmap.md` 已同步当前限制和后续排期，不再把 BundleMode 已补齐的内容列为未实现。
- [x] YAML 校验通过。

## 8. attention.md 候选盘点

- [x] 本 feature 未暴露需要补入 attention.md 的新内容。已有 `dotnet build GameDeveloperKit.Runtime.csproj --no-restore` 可继续作为 Runtime 快速编译验证命令。

## 9. 遗留

- 后续优化点：修正 `ResourceModule.Startup()` 的 settings/manifest 加载顺序。
- 后续优化点：补 Builtin loading operation，让 BuiltinMode 可真实返回 asset/raw/scene handle。
- 后续优化点：对齐 StreamingAssetMode / WebGLMode / EditorSimulatorMode 的 package operation 参数与 label 批量加载语义。
- 已知限制：仓库没有现成 AssetBundle 集成测试资产，本次未做 Unity 运行时真实 bundle 加载验证。
- 已知限制：BundleMode 当前以 `BundleInfo.Name` 作为 AssetBundle 本地路径或 URI，尚无显式远端 URL、缓存路径、CRC 或下载编排字段。
- 已知限制：递归依赖 provider 会随 package 反初始化一起释放；当前未做跨 package 共享依赖引用计数。
- 已知限制：EditorSimulator 的 editor loading operation 仍是 `NotImplementedException`，且 `EditorProvider` 两个字段仍有未赋值 warning，属于本 feature 明确不做范围。
