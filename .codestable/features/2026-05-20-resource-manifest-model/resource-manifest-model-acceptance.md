# resource-manifest-model 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-05-20
> 关联方案 doc：`.codestable/features/2026-05-20-resource-manifest-model/resource-manifest-model-design.md`

## 1. 接口契约核对

**接口示例逐项核对**：
- [x] `ResourceManifest` 查询示例：`TryGetAsset("ui/login")`、`GetAssetsByLabel("ui")`、`GetAssetsByType<GameObject>()` → `Assets/GameDeveloperKit/Runtime/Resource/ResourceManifest.cs` 已落地对应 API。
- [x] `ResourceBundleInfo`：`Name` / `Hash` / `Size` / `Uri` / `Dependencies` → `ResourceBundleInfo.cs` 已落地。
- [x] `ResourceAssetInfo`：`Location` / `BundleName` / `AssetPath` / `TypeName` / `Labels` / `IsScene` / `SceneName` → `ResourceAssetInfo.cs` 已落地。

**名词层"现状 → 变化"逐项核对**：
- [x] 新增 `Assets/GameDeveloperKit/Runtime/Resource/` 目录，承载清单模型。
- [x] 未复用 `VfsManifest` / `VFSMeta`，Resource 清单与 VFS 清单保持职责隔离。
- [x] `ResourceModule` / PlayMode / Provider 仅作为后续接口视图存在，本 feature 未实现其代码。

**流程图核对**：
- [x] 构造输入 → 验证 → bundle index → asset / label / type / scene index → 查询 API，均对应 `ResourceManifest` 构造和私有索引方法。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] 表达 package 下 bundle / asset 归属：`ResourceManifest.Bundles`、`ResourceManifest.Assets` 与 `ResourceAssetInfo.BundleName` 已落地。
- [x] 唯一 location 查询：`HasAsset` / `TryGetAsset` 已落地。
- [x] label / type 批量查询：`GetAssetsByLabel` / `GetAssetsByType<T>` / `GetAssetsByType(Type)` 已落地。
- [x] scene name 查询：`TryGetSceneAsset` 已落地。
- [x] 无效清单构造期拒绝：重复 bundle、重复 location、缺失 bundle、非法 label、非法 scene 均在构造索引时抛出。

**明确不做逐项核对**：
- [x] 不实现清单文件磁盘加载 / 下载 / 持久化：代码无文件 I/O。
- [x] 不实现实际资源加载：grep 未发现 `AssetBundle.LoadFromFileAsync`、`Resources.LoadAsync`、`AssetDatabase.LoadAssetAtPath`。
- [x] 不实现 `ResourceModule`、PlayMode、Provider、Handle：代码未新增这些类型。
- [x] 不实现 Editor 打包工具或资源扫描器：未新增 Editor 入口。
- [x] 不实现清单 diff、版本更新、hash 校验下载策略。

**关键决策落地**：
- [x] 清单只做内存模型和索引：无 I/O、无 Unity 加载 API。
- [x] `location` 是唯一键：重复 location 抛 `GameException`。
- [x] `BundleName` 引用 bundle：非空且不存在时抛 `GameException`。
- [x] type 查询使用字符串：`TypeName` 建索引，查询时匹配 `AssemblyQualifiedName` / `FullName`。
- [x] scene 是 asset 分类：`IsScene` + `SceneName` 建 scene index。

**流程级约束核对**：
- [x] 查询参数 null / 空白抛 `ArgumentNullException` / `ArgumentException`。
- [x] 查无结果：Try 返回 `false`，批量查询返回空只读列表。
- [x] PlayMode / Provider 约束未越界实现：当前只提供清单字段，Provider 不接收 manifest 的决策已同步到 roadmap。

**挂载点反向核对**：
- [x] `Assets/GameDeveloperKit/Runtime/Resource/`：实际新增清单模型目录。
- [x] `ResourceManifest`：作为后续 PlayMode / Provider 统一查询入口。
- [x] roadmap item：已回写 `resource-management` 的 `resource-manifest-model`。
- [x] 反向 grep：本 feature 代码引用集中在 `Runtime/Resource` 和 `Tests/Runtime/ResourceManifestTests.cs`。
- [x] 拔除沙盘：删除 `Runtime/Resource/` 与测试文件后，资源清单能力消失；后续 roadmap 条目会失去前置输入。

## 3. 验收场景核对

- [x] **N1 location 查询**：`ResourceManifestTests.TryGetAsset_WhenLocationExists_ReturnsAsset` 覆盖。
- [x] **N2 HasAsset**：`HasAsset_WhenLocationMissing_ReturnsFalse` 覆盖。
- [x] **N3 label 查询**：`GetAssetsByLabel_WhenLabelExists_ReturnsMatchingAssetsInInputOrder` 覆盖。
- [x] **N4 type 查询**：`GetAssetsByType_WhenTypeExists_ReturnsMatchingAssets` 覆盖。
- [x] **N5 scene 查询**：`TryGetSceneAsset_WhenSceneExists_ReturnsSceneAsset` 覆盖。
- [x] **N6 Module API 覆盖**：design 与 checklist 已核对每个 Module 加载入口均有 manifest 查询映射。
- [x] **N7 PlayMode / Provider 输入覆盖**：roadmap 与 architecture 已同步为 PlayMode 按 `BundleName` 路由到 bundle provider，provider 不接收 manifest。
- [x] **B1 查无结果**：`Queries_WhenMissing_ReturnEmptyOrFalse` 覆盖。
- [x] **E1 重复 location**：`Constructor_WhenLocationDuplicated_Throws` 覆盖。
- [x] **E2 缺失 bundle**：`Constructor_WhenBundleMissing_Throws` 覆盖。
- [x] **E3 非法 label**：`Constructor_WhenLabelInvalid_Throws` 覆盖。
- [x] **E4 非法查询参数**：`Query_WhenArgumentInvalid_Throws` 覆盖。

验证说明：`dotnet build GameDeveloperKit.Runtime.csproj --no-restore` 通过；Unity EditMode batchmode 因当前项目已有 Unity 实例打开无法运行。

## 4. 术语一致性

- `ResourceManifest`：代码命中集中在 Runtime 类型与测试，命名一致。
- `ResourceBundleInfo`：代码命中集中在 Runtime 类型与测试，命名一致。
- `ResourceAssetInfo`：代码命中集中在 Runtime 类型与测试，命名一致。
- `location` / `label` / `type` / `package`：公开 API 与设计术语一致。
- 防冲突：未新增 `ResourceManager`，未复用 `VfsManifest`。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md`：已新增 Resource 子系统条目。
- [x] 名词归并：写入 `ResourceManifest`、`ResourceBundleInfo`、`ResourceAssetInfo`。
- [x] 动词骨架归并：写入 manifest 构造期建立 location / label / type / scene 索引。
- [x] 流程级约束归并：写入 location 唯一、Provider 不持有 Manifest。

## 6. requirement 回写

- [x] `requirement: null`。本条是 `resource-management` roadmap 下的内部前置 feature，只落资源清单模型，不形成完整用户可感资源模块能力；本次不 backfill requirement。

## 7. roadmap 回写

- [x] `roadmap: resource-management` / `roadmap_item: resource-manifest-model`。
- [x] `.codestable/roadmap/resource-management/resource-management-items.yaml` 已将 `resource-manifest-model` 改为 `done`，`feature` 为 `2026-05-20-resource-manifest-model`。
- [x] `.codestable/roadmap/resource-management/resource-management-roadmap.md` 已同步子 feature 状态，并修正 Provider 契约为 bundle 级 provider。
- [x] `resource-management-items.yaml` 已通过 YAML 校验。

## 8. attention.md 候选盘点

- [x] 候选 1：Unity batchmode 测试在项目已被 Unity Editor 打开时会失败，报 "another Unity instance is running with this project open"。

## 9. 遗留

- 后续优化点：无。
- 已知限制：当前 `GameDeveloperKit.Runtime.csproj` 是 Unity 生成文件，未自动包含新增 `Runtime/Resource` 文件；Unity 刷新后应由项目生成器同步。
- 实现阶段顺手发现：无。
