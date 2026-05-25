# resource-module-api 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-05-21
> 关联方案 doc：`.codestable/features/2026-05-21-resource-module-api/resource-module-api-design.md`

## 1. 接口契约核对

对照方案第 2.1 节名词层逐一核查：

**接口示例逐项核对**：
- [x] `ResourceModule`（`Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs`）：提供 `CurrentPlayMode`、`Startup`、`Shutdown`、`Release`、`SetPlayMode`、asset/raw/scene/unload/package API → 代码实际行为：一致。
- [x] `Super.Resource`（`Assets/GameDeveloperKit/Runtime/Super.cs`）：返回已注册 `ResourceModule` → `Super_WhenResourceModuleRegistered_ReturnsResource` 覆盖：一致。
- [x] 示例调用 `Super.Register<ResourceModule>()` → `Super.Resource.SetPlayMode(playMode)` → `Super.Resource.LoadAssetAsync("ui/login")`：注册入口、挂载入口和加载转发均有实际落点：一致。

**名词层“现状 → 变化”逐项核对**：
- [x] `ResourceModule`：从不存在变为资源模块门面 → 新增 `ResourceModule.cs`：一致。
- [x] `Super.Resource`：从旧注释入口变为有效静态入口 → `Super.cs` 新增 `public static ResourceModule Resource => Get<ResourceModule>();`：一致。
- [x] `CurrentPlayMode` / `SetPlayMode`：作为显式挂载点 → `CurrentPlayMode` 私有 setter，`SetPlayMode(null)` 拒绝：一致。

**流程图核对**（第 2.2 节开头 mermaid 图）：
- [x] `Caller → Super.Resource → ResourceModule → Validate input and CurrentPlayMode → IResourcePlayMode → Handle` 均有实际落点；grep 确认 `Super.Resource`、`ResourceModule`、`CurrentPlayMode`、`SetPlayMode` 命中，ResourceModule 方法转发到 `GetPlayMode()`。

## 2. 行为与决策核对

对照方案第 1 节 + 第 2.2 节：

**需求摘要逐项验证**：
- [x] 新增 `ResourceModule` 并实现 `IGameModule`：`ResourceModule : IGameModule`，通过编译验证。
- [x] 新增 `Super.Resource` 入口：`Super.cs` 已新增属性，测试覆盖。
- [x] 未设置 PlayMode 时调用资源 API 有明确错误：`GetPlayMode()` 抛 `GameException`，测试覆盖。
- [x] 设置 PlayMode 后所有 Resource API 转发：fake playmode 记录 asset/raw/scene/unload/package 调用，测试覆盖。
- [x] `Startup` / `Shutdown` / `Release` 生命周期稳定：`Startup` 返回 completed，`Shutdown` / `Release` 清空 `CurrentPlayMode`，测试覆盖。

**明确不做逐项核对**：
- [x] 不实现具体 PlayMode 或 Provider：本次未新增具体 PlayMode / Provider 类型。
- [x] 不实现真实 Unity 加载、AssetBundle、Resources、SceneManager 或下载对接：`ResourceModule.cs` grep 无 `Resources.Load` / `AssetBundle` / `SceneManager` / `UnityWebRequest` 命中。
- [x] 不实现 package manifest 读取、provider 注册表、资源缓存、引用计数或卸载策略：`ResourceModule.cs` 未访问 manifest/provider/cache/refCount。
- [x] 不改 `Super` 通用注册机制语义：只新增 `Resource` 属性，`Register` / `Unregister` / `GetOrCreate` 未改语义。
- [x] 不改 `IResourcePlayMode` / `IResourceProvider` / handle / manifest 已有契约：本次只消费既有契约。

**关键决策落地**：
- [x] ResourceModule 是薄门面 → 只校验输入并委托 `CurrentPlayMode`。
- [x] PlayMode 显式挂载 → `SetPlayMode(IResourcePlayMode)` + `CurrentPlayMode` 只读暴露。
- [x] 未设置 PlayMode 是模块配置错误 → `GameException("Resource play mode is not set.")`。
- [x] 生命周期只管理挂载引用 → `Shutdown` / `Release` 只清空 `CurrentPlayMode`。

**编排层“现状 → 变化”逐项核对**：
- [x] 业务侧通过 `Super.Resource` 获取 `ResourceModule`：已落地。
- [x] 调用方通过 `SetPlayMode` 挂载运行模式：已落地。
- [x] ResourceModule 对公开 API 做基础参数校验和 PlayMode 存在性校验：已落地。
- [x] ResourceModule 将请求转发给 `CurrentPlayMode` 并返回结果：已落地。
- [x] `Shutdown` / `Release` 清空 `CurrentPlayMode`：已落地。

**流程级约束核对**：
- [x] `SetPlayMode(null)` 抛 `ArgumentNullException`。
- [x] `location` / `label` / `name` / `package` 为 null 或空白抛对应参数异常。
- [x] `UnloadAsset(null)` 抛 `ArgumentNullException`。
- [x] 未设置 `CurrentPlayMode` 时调用资源 API 抛 `GameException`。
- [x] ResourceModule 不捕获 PlayMode 异常、不替换返回 handle：转发方法直接返回 PlayMode 调用结果。
- [x] ResourceModule 不直接访问 `ResourceManifest` / `IResourceProvider`：grep 无命中。

**挂载点反向核对（可卸载性）**：
- [x] 挂载点 `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs`：新增门面文件。
- [x] 挂载点 `Assets/GameDeveloperKit/Runtime/Super.cs`：新增 `Super.Resource`。
- [x] 挂载点 `CurrentPlayMode` / `SetPlayMode`：后续 PlayMode 接入点已落地。
- [x] 挂载点 roadmap item：`resource-management-items.yaml` 已回写 `done`。
- [x] 反向核查：grep `ResourceModule|Super.Resource|CurrentPlayMode|SetPlayMode`，运行时代码命中均落在清单内。
- [x] 拔除沙盘推演：删除 `ResourceModule.cs`、移除 `Super.Resource`、回退 roadmap item 后，本 feature 无额外运行时残留；测试文件中的 ResourceModule 覆盖随 feature 一并删除即可。

## 3. 验收场景核对

对照方案第 3 节关键场景清单，逐条可观察证据验证：

- [x] **N1**：`module.SetPlayMode(fake)` → `CurrentPlayMode` 为 fake。
  - 证据来源：单测 `ResourceModule_WhenPlayModeSet_StoresCurrentPlayMode`
  - 结果：通过
- [x] **N2**：未挂载 PlayMode 调用 API → 抛 `GameException`。
  - 证据来源：单测 `ResourceModule_WhenPlayModeMissing_Throws`
  - 结果：通过
- [x] **N3**：asset API 转发。
  - 证据来源：单测 `ResourceModule_ForwardsAssetApisToCurrentPlayMode`
  - 结果：通过
- [x] **N4**：raw / scene API 转发。
  - 证据来源：单测 `ResourceModule_ForwardsRawAndSceneApisToCurrentPlayMode`
  - 结果：通过
- [x] **N5**：unload / package API 转发。
  - 证据来源：单测 `ResourceModule_ForwardsUnloadAndPackageApisToCurrentPlayMode`
  - 结果：通过
- [x] **N6**：`Super.Register<ResourceModule>()` 后访问 `Super.Resource`。
  - 证据来源：单测 `Super_WhenResourceModuleRegistered_ReturnsResource`
  - 结果：通过
- [x] **N7**：`Shutdown()` / `Release()` 清空 PlayMode。
  - 证据来源：单测 `ResourceModule_WhenShutdownOrReleased_ClearsPlayMode`
  - 结果：通过
- [x] **E1**：参数为空。
  - 证据来源：单测 `ResourceModule_WhenArgumentsInvalid_Throws`
  - 结果：通过
- [x] **E2**：`UnloadAsset(null)`。
  - 证据来源：单测 `ResourceModule_WhenArgumentsInvalid_Throws`
  - 结果：通过
- [x] **E3**：范围反向核对。
  - 证据来源：grep `ResourceModule.cs`
  - 结果：未出现 `Resources.Load` / `AssetBundle` / `SceneManager` / `UnityWebRequest` / Provider 注册表。

## 4. 术语一致性

对照方案第 0 节 + 第 2.1 节命名 grep 代码：

- `ResourceModule`：运行时代码和测试命名一致。
- `Super.Resource`：入口命名一致。
- `CurrentPlayMode`：属性命名一致。
- `SetPlayMode`：方法命名一致。
- `Resource API`：API 名称对齐 `IResourcePlayMode` 与 roadmap。
- 防冲突：`ResourceModule.cs` 未引入禁用的真实加载 / 注册表 / 缓存术语。

## 5. 架构归并

对照方案第 4 节：

- [x] 架构 doc `.codestable/architecture/ARCHITECTURE.md`：已写入 `ResourceModule` 门面、`Super.Resource` 入口、`ResourceModule → IResourcePlayMode` 委托关系、`CurrentPlayMode` / `SetPlayMode` 挂载点。
- [x] 架构 doc `.codestable/architecture/ARCHITECTURE.md`：已写入薄门面约束：不直接访问 Unity 加载 API，不管理 provider 注册 / 缓存 / 引用计数。
- [x] `.codestable/attention.md`：无需更新，未暴露会影响每个 feature 的新环境 / 工作流规则。

## 6. requirement 回写

- [x] `requirement` 为空，且本 feature 来源为 roadmap 的内部模块能力切片；不新增独立 requirement 文档，跳过 requirement 回写。

## 7. roadmap 回写

- [x] `roadmap: resource-management` / `roadmap_item: resource-module-api` 均有值。
- [x] `.codestable/roadmap/resource-management/resource-management-items.yaml` 中 `resource-module-api` 已从 `in-progress` 改为 `done`，feature 保持 `2026-05-21-resource-module-api`。
- [x] `.codestable/roadmap/resource-management/resource-management-roadmap.md` 第 5 节子 feature 清单已同步为 `状态：done`、`对应 feature：2026-05-21-resource-module-api`。
- [x] YAML 校验通过。

## 8. attention.md 候选盘点

- [x] 本 feature 未暴露需要补入 attention.md 的内容。

## 9. 遗留

- 后续优化点：无。
- 已知限制：尚未实现具体 PlayMode；下一 roadmap item 为 `resources-playmode`。
- 实现阶段“顺手发现”列表：无。
