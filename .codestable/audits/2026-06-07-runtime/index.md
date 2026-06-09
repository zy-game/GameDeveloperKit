---
doc_type: audit-index
audit: 2026-06-07-runtime
scope: Assets/GameDeveloperKit/Runtime 全量 C# runtime 代码
created: 2026-06-07
status: fixed
total_findings: 9
---

# runtime 审计报告

## 范围

本次扫描 `Assets/GameDeveloperKit/Runtime/**/*.cs`，共 172 个 C# 文件、约 16709 行。覆盖 bug、安全、性能、可维护性、架构偏离 5 个维度；对照 `.codestable/architecture/ARCHITECTURE.md`，并参考既有 `filesystem` / `resource-module` 审计记录，避免重复记录已归档的问题。

验证基线：`dotnet build GameDeveloperKit.Runtime.csproj --no-restore` 成功，最终复查为 0 警告、0 错误。

## 总评

共发现 9 条有效问题：P1 5 条、P2 4 条，均已完成代码修复并通过 `dotnet build GameDeveloperKit.Runtime.csproj --no-restore` 验证，结果为 0 警告、0 错误。修复覆盖 VFS 存储与 CRC 契约、资源包卸载判定、场景句柄激活、Raw/Scene 句柄卸载、下载临时文件命名、Debug 外发脱敏、Combat wrapper 缓存清理，以及 raw label 多模式聚合。

## 发现清单

| # | 性质 | 严重度 | 置信度 | 标题 | 文件 |
|---|---|---|---|---|---|
| 1 | arch-drift | P1 | high | 已修复：VFS 存储策略与 CRC32 完整性契约没有落到代码 | [finding-01.md](finding-01.md) |
| 2 | bug | P2 | high | 已修复：`ReadAllStringAsync` 公开 API 永远返回空字符串 | [finding-02.md](finding-02.md) |
| 3 | bug | P1 | high | 已修复：非 Bundle 资源模式用 bundle 名判断 package，导致包卸载不可达 | [finding-03.md](finding-03.md) |
| 4 | bug | P1 | high | 已修复：`SceneAssetHandle.Active()` 在场景已加载时直接返回 | [finding-04.md](finding-04.md) |
| 5 | performance | P1 | medium | 已修复：Raw/Scene 资源句柄会被缓存但没有公开卸载路径 | [finding-05.md](finding-05.md) |
| 6 | bug | P1 | high | 已修复：不同 URL 的同名文件会写入同一个下载临时文件 | [finding-06.md](finding-06.md) |
| 7 | security | P2 | medium | 已修复：Debug 日志脱敏没有覆盖 exception/context/tags 及外发 transport | [finding-07.md](finding-07.md) |
| 8 | performance | P2 | medium | 已修复：Combat 销毁实体后 wrapper 长期留在缓存中 | [finding-08.md](finding-08.md) |
| 9 | bug | P2 | high | 已修复：`LoadRawAssetsByLabelAsync` 只读取第一个命中的资源模式 | [finding-09.md](finding-09.md) |

## 按维度分布

| 性质 | P0 | P1 | P2 | 合计 |
|---|---|---|---|---|
| bug | 0 | 3 | 2 | 5 |
| security | 0 | 0 | 1 | 1 |
| performance | 0 | 1 | 1 | 2 |
| maintainability | 0 | 0 | 0 | 0 |
| arch-drift | 0 | 1 | 0 | 1 |
| **合计** | **0** | **5** | **4** | **9** |

## 修复结果

- VFS 小文件按 packed slot 写入，大文件按独立文件写入；读取时校验 CRC32；`ReadAllStringAsync` 返回实际 UTF-8 文本。
- 非 Bundle 资源模式按 `Manifest.Packages` 中 package 直接 bundle 判定初始化状态，不再把 provider 的 bundle 名当 package 名。
- `SceneAssetHandle.Active()` 仅在场景已加载时激活，并检查 `SceneManager.SetActiveScene` 返回值。
- Resource 模块公开 Raw/Scene 句柄卸载入口，Provider 侧进入已有 pending-unload 流程。
- 下载临时文件名加入完整 URL 的稳定 CRC32 前缀，避免同名 URL 碰撞。
- Debug 日志 record 在写入 buffer/sink/transport 前统一脱敏 message/category/exception/context/tags。
- Combat 实体销毁成功后移除 wrapper 缓存。
- `LoadRawAssetsByLabelAsync` 聚合所有命中资源模式的结果。
