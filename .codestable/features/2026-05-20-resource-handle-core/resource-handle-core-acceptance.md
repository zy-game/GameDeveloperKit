# resource-handle-core 验收报告

> 阶段：阶段 3（验收闭环）  
> 验收日期：2026-05-21  
> 关联方案 doc：`.codestable/features/2026-05-20-resource-handle-core/resource-handle-core-design.md`

## 1. 接口契约核对

对照方案第 2.1 节名词层逐一核查：

**接口示例逐项核对**：
- [x] `ResourceHandleStatus`（`Assets/GameDeveloperKit/Runtime/Resource/ResourceHandleStatus.cs`）：提供 `None` / `Loading` / `Succeeded` / `Failed` / `Released` → 代码实际行为：一致。
- [x] `ResourceHandle`（`Assets/GameDeveloperKit/Runtime/Resource/ResourceHandle.cs`）：提供 `Location`、`Status`、`Error`、`IsDone`、`IsValid`、`IsReleased`、`Release()`、`Dispose()` → 代码实际行为：一致。
- [x] `AssetHandle`（`Assets/GameDeveloperKit/Runtime/Resource/AssetHandle.cs`）：继承 `ResourceHandle`，提供 `Asset` 与 `GetAsset<T>()` → 代码实际行为：一致。
- [x] `RawAssetHandle`（`Assets/GameDeveloperKit/Runtime/Resource/RawAssetHandle.cs`）：继承 `AssetHandle` → 代码实际行为：一致。
- [x] `SceneAssetHandle`（`Assets/GameDeveloperKit/Runtime/Resource/SceneAssetHandle.cs`）：继承 `AssetHandle` 并提供 `SceneName` → 代码实际行为：一致。
- [x] 示例 `AssetHandle.Success("ui/login", prefab)` + `GetAsset<GameObject>()` + 重复 `Release()` → `ResourceHandleTests.AssetHandle_WhenSuccess_ReturnsAsset` 与 `Release_WhenCalledRepeatedly_IsIdempotent` 覆盖，行为一致。

**名词层"现状 → 变化"逐项核对**：
- [x] Resource 目录从清单模型扩展到句柄核心层 → 新增 5 个 Runtime Resource 类型，一致。
- [x] `DownloadHandler` 未复用为资源结果句柄 → grep 未发现资源句柄依赖 Download 类型，一致。
- [x] 未接入 `ReferencePool` / `IReference` → grep 与代码 review 确认，一致。

**流程图核对**：
- [x] `Provider loads Unity Object → Create AssetHandle / RawAssetHandle / SceneAssetHandle`：当前 feature 落地句柄创建入口，Provider 后续接入；符合本 feature 范围。
- [x] `Caller reads Asset / GetAsset<T>`：`AssetHandle.Asset` 与 `GetAsset<T>()` 已落地。
- [x] `Release → Status = Released and local reference cleared`：`AssetHandle.Release()` 清空 `Asset` 并调用基类标记 `Released`。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] 四个句柄类型存在，继承关系符合 roadmap：`ResourceHandle`、`AssetHandle`、`RawAssetHandle`、`SceneAssetHandle` 已实现，测试覆盖继承关系。
- [x] 成功句柄能读取资源对象：`AssetHandle_WhenSuccess_ReturnsAsset` 覆盖。
- [x] 失败句柄能暴露异常：`AssetHandle_WhenFailed_StoresError` 覆盖。
- [x] 释放后句柄状态稳定为 `Released`，重复释放不抛异常：`Release_WhenCalledRepeatedly_IsIdempotent` 覆盖。
- [x] `GetAsset<T>()` 类型不匹配返回 `null`：`GetAsset_WhenTypeMismatched_ReturnsNull` 覆盖。

**明确不做逐项核对**：
- [x] 不实现实际资源加载、卸载 AssetBundle、引用计数或缓存：grep 未发现 `AssetBundle.Unload`、`Resources.UnloadAsset`、`SceneManager.UnloadSceneAsync`、引用计数字段或缓存实现。
- [x] 不实现 `ResourceModule`、PlayMode、Provider：本次新增仅 Resource 句柄类型和测试 asmdef / tests。
- [x] 不实现 scene 激活 / 卸载流程：未调用 SceneManager。
- [x] 不实现 raw asset 的 byte[] / string 转换策略：`RawAssetHandle` 仅继承 `AssetHandle`，无转换 API。

**关键决策落地**：
- [x] 句柄是加载结果对象，不负责实际卸载资源 → `Release()` 只更新状态 / 清空引用。
- [x] 状态枚举显式表达生命周期 → `ResourceHandleStatus` 已实现五个状态。
- [x] `AssetHandle` 统一承载 Unity `Object` → `Asset` 类型为 `UnityEngine.Object`。
- [x] `RawAssetHandle` 不定义转换策略 → 仅提供 success / failed 构造入口。
- [x] `SceneAssetHandle` 记录 `SceneName` → 属性已实现且校验空白输入。

**编排层"现状 → 变化"逐项核对**：
- [x] 后续 Provider 可创建对应句柄：三个句柄均提供 `Success` / `Failed` 静态入口。
- [x] 业务侧可读取 `Asset` 或 `GetAsset<T>()`：`AssetHandle` 已实现。
- [x] 卸载入口后续可调用 `Release()`：基类与 Asset 子类均实现。
- [x] 失败路径通过 `Status=Failed` 与 `Error` 表达，不返回 null 句柄：`Failed(...)` 返回句柄实例并要求非 null error。

**流程级约束核对**：
- [x] `location` 为空时拒绝构造：`ResourceHandle` 构造校验，测试覆盖 null / empty / whitespace。
- [x] 成功的 `AssetHandle` 必须有非 null `Asset`：构造校验，测试覆盖。
- [x] 失败句柄必须有非 null `Error`：构造校验，测试覆盖。
- [x] `Release()` 幂等；释放后 `Asset=null`，`IsReleased=true`：测试覆盖。
- [x] 不做线程安全承诺：未引入锁或并发抽象，符合设计。

**挂载点反向核对（可卸载性）**：
- [x] 挂载点 `Assets/GameDeveloperKit/Runtime/Resource/`：5 个句柄类型均落在该目录。
- [x] 挂载点 `ResourceHandle` 类型层级：继承链按设计落地。
- [x] 挂载点 `resource-management` roadmap item：已回写 `done`。
- [x] 反向核查 grep：`ResourceHandle` / `AssetHandle` / `RawAssetHandle` / `SceneAssetHandle` / `ResourceHandleStatus` 命中均在 Runtime Resource 与 Runtime Tests；未发现清单外生产代码引用。
- [x] 拔除沙盘推演：删除新增句柄类型与 tests 后，剩余 Resource 清单模型不依赖本 feature；后续 roadmap 的 playmode/provider/module 契约会失去返回类型，符合设计。

## 3. 验收场景核对

- [x] **N1**：成功 `AssetHandle` → `Status=Succeeded`，`Asset` 非 null，`GetAsset<GameObject>()` 返回对象。
  - 证据来源：单测 `ResourceHandleTests.AssetHandle_WhenSuccess_ReturnsAsset`。
  - 结果：通过。
- [x] **N2**：类型不匹配 `GetAsset<Texture>()` 返回 null。
  - 证据来源：单测 `ResourceHandleTests.GetAsset_WhenTypeMismatched_ReturnsNull`。
  - 结果：通过。
- [x] **N3**：失败句柄暴露 error 且 `IsDone=true`。
  - 证据来源：单测 `ResourceHandleTests.AssetHandle_WhenFailed_StoresError`。
  - 结果：通过。
- [x] **N4**：重复释放幂等，`Status=Released`，`Asset=null`。
  - 证据来源：单测 `ResourceHandleTests.Release_WhenCalledRepeatedly_IsIdempotent`。
  - 结果：通过。
- [x] **N5**：`RawAssetHandle : AssetHandle`。
  - 证据来源：单测 `ResourceHandleTests.RawAssetHandle_InheritsAssetHandle`。
  - 结果：通过。
- [x] **N6**：`SceneAssetHandle` 保持 scene name 且继承 `AssetHandle`。
  - 证据来源：单测 `ResourceHandleTests.SceneAssetHandle_WhenSuccess_StoresSceneName`。
  - 结果：通过。
- [x] **E1**：非法 location 抛 `ArgumentException`。
  - 证据来源：单测 `ResourceHandleTests.Constructor_WhenLocationInvalid_Throws`。
  - 结果：通过。
- [x] **E2**：成功句柄 asset 为 null 抛 `ArgumentNullException`。
  - 证据来源：单测 `ResourceHandleTests.AssetHandle_WhenSuccessAssetNull_Throws`。
  - 结果：通过。
- [x] **E3**：失败句柄 error 为 null 抛 `ArgumentNullException`。
  - 证据来源：单测 `ResourceHandleTests.AssetHandle_WhenFailedErrorNull_Throws`。
  - 结果：通过。

反向核对项均通过：未出现实际卸载 API、未新增 ResourceModule/PlayMode/Provider、未实现 raw 转换、未实现引用计数/缓存/异步进度。

## 4. 术语一致性

- `ResourceHandle`：代码命中均指资源句柄基类或测试 ✓
- `AssetHandle`：代码命中均指 Unity Object 资源句柄或测试 ✓
- `RawAssetHandle`：代码命中均指原始资源句柄或测试 ✓
- `SceneAssetHandle`：代码命中均指场景资源句柄或测试 ✓
- `ResourceHandleStatus`：代码命中均指句柄生命周期状态 ✓
- `Release`：实现语义与设计一致，未混入实际资源卸载 ✓
- 防冲突：未复用 `DownloadStatus` / `DownloadHandler`，未引入方案外新概念 ✓

## 5. 架构归并

- [x] 架构 doc `.codestable/architecture/ARCHITECTURE.md`：已在 Resource 子系统补入句柄核心类型、继承关系、状态与释放约束。
- [x] 名词归并：`ResourceHandle`、`AssetHandle`、`RawAssetHandle`、`SceneAssetHandle`、`ResourceHandleStatus` 已写入 Resource 核心类型 / 关键行为。
- [x] 动词骨架归并：句柄作为后续 ResourceModule / PlayMode / Provider 返回结果承载对象已写入 Resource 描述。
- [x] 流程级约束归并：`Release()` 幂等、清空本地引用、不直接调用卸载 API 已写入已知约束。

## 6. requirement 回写

- [x] `requirement` 为空；本 feature 是 Resource Management roadmap 内的技术契约层，尚未新增用户可直接调用的完整能力入口。无 requirement 回写。

## 7. roadmap 回写

- [x] design frontmatter 包含 `roadmap: resource-management` 与 `roadmap_item: resource-handle-core`。
- [x] `.codestable/roadmap/resource-management/resource-management-items.yaml` 中 `resource-handle-core` 已由 `in-progress` 改为 `done`，`feature` 保持 `2026-05-20-resource-handle-core`。
- [x] `.codestable/roadmap/resource-management/resource-management-roadmap.md` 第 5 节子 feature 清单已同步状态 `done` 和对应 feature。
- [x] roadmap YAML 已通过 `validate-yaml.py` 校验。

## 8. attention.md 候选盘点

- [x] 候选 1：Runtime 测试验证应优先使用 `dotnet build GameDeveloperKit.Runtime.Tests.csproj`；普通 `Assembly-CSharp.csproj` 不适合承载 NUnit runtime tests，测试目录需要 asmdef 隔离。

## 9. 遗留

- 后续优化点：无。
- 已知限制：句柄不负责真实资源卸载；raw asset 不提供 byte[] / string 转换；scene handle 不承诺 scene 加载 / 激活状态，均按设计留给后续 feature。
- 实现阶段"顺手发现"列表：无。
