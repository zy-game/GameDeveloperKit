---
doc_type: audit-index
audit: 2026-05-27-resource-module
scope: Assets/GameDeveloperKit/Runtime/Resource 资源模块
created: 2026-05-27
status: superseded
superseded-by: 2026-06-26-resource-commercial-readiness
total_findings: 6
---

# resource-module 审计报告

## 范围

审计范围为 `Assets/GameDeveloperKit/Runtime/Resource/`，覆盖资源门面、清单模型、资源句柄、运行模式、Provider 与资源 operation。架构偏离项对照了 `.codestable/architecture/ARCHITECTURE.md` 的 Resource 现状记录。验证命令：`dotnet build GameDeveloperKit.Runtime.csproj --no-restore`，结果为 0 警告、0 错误。

## 总评

本轮共发现 6 条有效问题：1 条 P0、3 条 P1、2 条 P2，均已完成代码修复并通过 `dotnet build GameDeveloperKit.Runtime.csproj --no-restore` 验证。原 Finding 05 经项目约定确认后撤销：`#if UNITY_EDITOR` 包裹的 Editor API 留在 Runtime 目录是当前可接受设计。剩余运行数据风险：当前仓库 `Assets/StreamingAssets/manifest.json` 仍是旧 JSON 形态，需要资源构建链路后续生成当前 `ManifestInfo` 格式清单。

## 发现清单

| # | 性质 | 严重度 | 置信度 | 标题 | 文件 |
|---|---|---|---|---|---|
| 1 | bug | P0 | high | 已修复：`Startup()` 在 mode/settings 初始化前调用资源 API，资源模块无法自举 | [finding-01.md](finding-01.md) |
| 2 | bug | P1 | high | 已修复：`UnloadAsset()` 对失败或已释放句柄直接解引用 `Info` | [finding-02.md](finding-02.md) |
| 3 | bug | P1 | medium | 已修复：批量加载路径未保护 `BundleInfo.Assets == null` | [finding-03.md](finding-03.md) |
| 4 | arch-drift | P1 | high | 已修复：`ResourceMode.Web` 没有接入 `WebAssetProvider`，实际仍走本地 bundle 读取 | [finding-04.md](finding-04.md) |
| 5 | arch-drift | P1 | medium | 已撤销：`EditorAssetProvider` 使用 Editor API 留在 Runtime 是项目约定允许的设计 | [finding-05.md](finding-05.md) |
| 6 | performance | P2 | medium | 已修复：`UnloadUnusedAssetAsync()` 会按 provider 多次触发全局卸载扫描 | [finding-06.md](finding-06.md) |
| 7 | maintainability | P2 | medium | 已修复：Provider 加载编排复制成多份，新增模式时容易继续分叉 | [finding-07.md](finding-07.md) |

## 按维度分布

| 性质 | P0 | P1 | P2 | 合计 |
|---|---|---|---|---|
| bug | 1 | 2 | 0 | 3 |
| security | 0 | 0 | 0 | 0 |
| performance | 0 | 0 | 1 | 1 |
| maintainability | 0 | 0 | 1 | 1 |
| arch-drift | 0 | 1 | 0 | 1 |
| **合计** | **1** | **3** | **2** | **6** |

## 修复结果

- `ResourceModule.Startup()` 已改为直接读取 `Resources/ResourceSettings`，再按 `ManifestLocation` 加载清单，不再依赖资源模块自身加载 API。
- `UnloadAsset()` 对 `Info == null` 的失败 / 已释放句柄幂等返回。
- `BundleInfo.Assets` / `Dependencies` 默认初始化为空列表，Provider 批量遍历统一走 `ProviderBase` 的安全枚举。
- `WebGLMode` 已创建 `WebAssetProvider`，Web bundle 初始化走 `UnityWebRequestAssetBundle`。
- `ProviderBase.UnloadUnusedAssetAsync()` 只释放 pending handle，Unity 全局 `Resources.UnloadUnusedAssets()` 由 `ResourceModule` 统一调用一次。
- Provider 加载编排已上移到 `ProviderBase`，各 provider 只保留具体 loading operation 差异。
