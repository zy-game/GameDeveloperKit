# resource-playmode-provider-contract 验收报告

> 阶段：阶段 3（验收闭环）  
> 验收日期：2026-05-21  
> 关联方案 doc：`.codestable/features/2026-05-21-resource-playmode-provider-contract/resource-playmode-provider-contract-design.md`

## 1. 接口契约核对

对照方案第 2.1 节名词层逐一核查：

**接口示例逐项核对**：
- [x] `PackageOperationStatus`（`Assets/GameDeveloperKit/Runtime/Resource/PackageOperationStatus.cs`）：提供 `None` / `Running` / `Succeeded` / `Failed` / `Released` → 代码实际行为：一致。
- [x] `PackageOperationHandle`（`Assets/GameDeveloperKit/Runtime/Resource/PackageOperationHandle.cs`）：提供 `Package`、`Status`、`Error`、`IsDone`、`IsValid`、`IsReleased`、`Success`、`Failed`、`Release`、`Dispose` → 代码实际行为：一致。
- [x] `IResourcePlayMode`（`Assets/GameDeveloperKit/Runtime/Resource/IResourcePlayMode.cs`）：提供 `Manifest`、`Providers`、查询、加载、卸载、package lifecycle API → 代码实际行为：一致。
- [x] `IResourceProvider`（`Assets/GameDeveloperKit/Runtime/Resource/IResourceProvider.cs`）：提供 `BundleName`、`Bundle`，加载方法接收 `ResourceAssetInfo`，卸载接收 `AssetHandle`，package lifecycle 接收 package → 代码实际行为：一致。
- [x] PlayMode 示例：fake playmode 可声明 `HasAsset` / `LoadAssetAsync` 并返回 handle → `ResourceContracts_CanBeImplementedByFakeProviderAndPlayMode` 编译验证。
- [x] Provider 层示例：fake provider 可按 `ResourceAssetInfo` 返回 `ResourceHandle` → `FakeResourceProvider` 编译验证。

**名词层"现状 → 变化"逐项核对**：
- [x] Resource 目录已有 manifest 和 handle 类型 → 本次新增契约类型，不改既有类型。
- [x] roadmap PlayMode / Provider 草案落地为接口 → `IResourcePlayMode` / `IResourceProvider` 已实现。
- [x] 当前代码此前无 `PackageOperationHandle` → 已新增。

**流程图核对**：
- [x] `Future ResourceModule → IResourcePlayMode`：`IResourcePlayMode` 已作为未来 module 依赖面落地。
- [x] `IResourcePlayMode → ResourceManifest / IResourceProvider`：接口暴露 `Manifest` 和 `Providers`。
- [x] `IResourceProvider → ResourceHandle / RawAssetHandle / SceneAssetHandle`：provider 接口返回三类 handle。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] `IResourcePlayMode` 定义查询、按 location / label / type / scene 加载、卸载、package 初始化 / 反初始化契约：接口已落地。
- [x] `IResourceProvider` 是 bundle 级 provider：暴露 `BundleName` / `Bundle`，加载方法接收 `ResourceAssetInfo`。
- [x] `PackageOperationHandle` 表达成功、失败、释放和错误语义：单测覆盖。
- [x] 契约只依赖已落地 manifest 与 handle 类型：runtime build 通过，fake implementation 编译通过。

**明确不做逐项核对**：
- [x] 不实现 `ResourceModule` / `Super.Resource`：grep 未命中。
- [x] 不实现具体 PlayMode / Provider：仅测试内 fake 类型，Runtime 未新增具体实现类。
- [x] 不实现真实 Unity 资源加载、AssetBundle、Resources、SceneManager、下载对接：grep 未命中相关 API。
- [x] 不实现 provider 注册表、package manifest 读取、缓存、引用计数或卸载策略：grep / review 确认未实现。
- [x] 不改变 `ResourceManifest` / `ResourceHandle` 既有行为：未编辑这两个源码文件。

**关键决策落地**：
- [x] Provider 是 bundle 级，不持有 manifest → `IResourceProvider` 无 `ResourceManifest` 成员或参数。
- [x] PlayMode 持有 manifest 和多个 provider → `IResourcePlayMode.Manifest` / `Providers`。
- [x] 契约返回已落地 handle 类型 → asset/raw/scene 分别返回 `ResourceHandle` / `RawAssetHandle` / `SceneAssetHandle`。
- [x] `PackageOperationHandle` 独立于下载和资源加载状态 → 使用 `PackageOperationStatus`，未复用 `DownloadStatus` / `ResourceHandleStatus`。

**编排层"现状 → 变化"逐项核对**：
- [x] 后续 `ResourceModule` 可只依赖 `IResourcePlayMode`：接口完整覆盖 roadmap module API 所需方法。
- [x] PlayMode 可持有 manifest 和 providers：接口属性已落地。
- [x] Provider 只接收 PlayMode 已定位好的 `ResourceAssetInfo`：接口签名已落地。
- [x] package 初始化 / 反初始化统一返回 `PackageOperationHandle`：playmode/provider 接口均已落地。

**流程级约束核对**：
- [x] package 为空时 operation handle 拒绝：单测覆盖 null / empty / whitespace。
- [x] failed error 为空时拒绝：单测覆盖。
- [x] PlayMode 可返回 failed handle 表示失败：fake playmode 使用 failed handle 编译验证。
- [x] Provider 不接收 `ResourceManifest`：grep `IResourceProvider.cs` 无命中。
- [x] 本 feature 只定义接口，不规定 provider 集合存储结构：未新增注册表 / 字典 / 管理器。

**挂载点反向核对（可卸载性）**：
- [x] 挂载点 `Assets/GameDeveloperKit/Runtime/Resource/`：新增 4 个契约类型均落在该目录。
- [x] 挂载点 `IResourcePlayMode`：未来 ResourceModule 的运行模式契约类型已落地。
- [x] 挂载点 `IResourceProvider`：未来 PlayMode 面向 bundle 操作的契约类型已落地。
- [x] 挂载点 `PackageOperationHandle`：package lifecycle 统一结果类型已落地。
- [x] 挂载点 roadmap item：已回写 `done`。
- [x] 反向核查 grep：本 feature 生产代码命中集中在新增 4 个 Resource 契约文件；测试命中在 `ResourceHandleTests.cs`。
- [x] 拔除沙盘推演：删除新增 4 个契约类型与对应测试后，既有 manifest / handle 仍可独立存在，但后续 module/playmode/provider feature 会失去契约入口，符合设计。

## 3. 验收场景核对

- [x] **N1**：`PackageOperationHandle.Success("default")` → package、status、error、done、valid 符合预期。
  - 证据来源：单测 `PackageOperationHandle_WhenSuccess_ReturnsCompletedHandle`。
  - 结果：通过。
- [x] **N2**：`PackageOperationHandle.Failed("default", exception)` → failed status、error、done 符合预期。
  - 证据来源：单测 `PackageOperationHandle_WhenFailed_StoresError`。
  - 结果：通过。
- [x] **N3**：重复 `Release()` 幂等。
  - 证据来源：单测 `PackageOperationHandle_WhenReleasedRepeatedly_IsIdempotent`。
  - 结果：通过。
- [x] **N4**：fake provider 实现 bundle 级契约，不接收 manifest。
  - 证据来源：`FakeResourceProvider` 编译验证 + grep `IResourceProvider.cs` 无 `ResourceManifest`。
  - 结果：通过。
- [x] **N5**：fake playmode 暴露 manifest / providers 并声明 roadmap API。
  - 证据来源：`FakeResourcePlayMode` 编译验证。
  - 结果：通过。
- [x] **N6**：fake playmode/provider 返回 handle 类型对齐。
  - 证据来源：`FakeResourceProvider` / `FakeResourcePlayMode` 编译验证。
  - 结果：通过。
- [x] **E1**：package 为空抛 `ArgumentException`。
  - 证据来源：单测 `PackageOperationHandle_WhenPackageInvalid_Throws`。
  - 结果：通过。
- [x] **E2**：failed error 为空抛 `ArgumentNullException`。
  - 证据来源：单测 `PackageOperationHandle_WhenFailedErrorNull_Throws`。
  - 结果：通过。
- [x] **E3**：`IResourceProvider` 不存在 `ResourceManifest` 参数或属性。
  - 证据来源：grep `IResourceProvider.cs`。
  - 结果：通过。

反向核对项均通过：未新增 ResourceModule / Super.Resource；未新增具体 PlayMode / Provider；未出现真实资源加载 / 下载 API；未改变 manifest / handle 既有行为；未实现注册表、缓存、引用计数或实际卸载策略。

## 4. 术语一致性

- `IResourcePlayMode`：代码命中均指资源运行模式接口或 fake 测试类型 ✓
- `IResourceProvider`：代码命中均指 bundle provider 接口或 fake 测试类型 ✓
- `PackageOperationHandle`：代码命中均指 package lifecycle 结果句柄 ✓
- `PackageOperationStatus`：代码命中均指 package operation 生命周期状态 ✓
- `ResourcePlayMode` / `ResourceProvider`：未新增具体基类或实现，符合设计 ✓
- 防冲突：未复用 `DownloadStatus` / `ResourceHandleStatus`；未引入 `PackageOperationHandler` ✓

## 5. 架构归并

- [x] 架构 doc `.codestable/architecture/ARCHITECTURE.md`：已补入 `IResourcePlayMode`、`IResourceProvider`、`PackageOperationHandle`。
- [x] 名词归并：新增契约核心类型已写入 Resource 核心类型列表。
- [x] 动词骨架归并：`ResourceModule → IResourcePlayMode → ResourceManifest / IResourceProvider` 的关系已写入契约关系。
- [x] 流程级约束归并：Provider 不接收 Manifest、PackageOperationHandle 不承载下载/资源加载进度已写入已知约束。

## 6. requirement 回写

- [x] `requirement` 为空；本 feature 是 Resource Management roadmap 内的技术契约层，尚未新增用户可直接调用的完整能力入口。无 requirement 回写。

## 7. roadmap 回写

- [x] design frontmatter 包含 `roadmap: resource-management` 与 `roadmap_item: resource-playmode-provider-contract`。
- [x] `.codestable/roadmap/resource-management/resource-management-items.yaml` 中 `resource-playmode-provider-contract` 已由 `in-progress` 改为 `done`。
- [x] `.codestable/roadmap/resource-management/resource-management-roadmap.md` 第 5 节子 feature 清单已同步状态 `done` 和对应 feature。
- [x] roadmap YAML 已通过 `validate-yaml.py` 校验。

## 8. attention.md 候选盘点

- [x] 本 feature 未暴露需要补入 attention.md 的新内容；Runtime test project 的 UniTask 引用问题属于当前 Unity 生成 csproj 验证细节，已通过项目文件纳入验证。

## 9. 遗留

- 后续优化点：无。
- 已知限制：本 feature 只定义契约，不实现具体 PlayMode / Provider / ResourceModule；package operation 不提供 progress、事件或 WaitCompletion API。
- 实现阶段"顺手发现"列表：无。
